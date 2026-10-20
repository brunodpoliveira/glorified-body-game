#!/usr/bin/env python3
"""Triage Unity logs in a run folder. Writes summary.json and prints a short report.

Usage: log-triage.py RUN_DIR [--quiet]
Reads every *.log (editor/player logs), runtime.jsonl (RunLogSink) and *.xml (test results)
in RUN_DIR. Lines that match a regex in tools/log-noise.txt are ignored.
Exit code: 0 = clean, 1 = errors found, 2 = bad usage.
"""
import json
import pathlib
import re
import sys
import xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parent
NOISE_FILE = ROOT / "log-noise.txt"

RE_CS_ERROR = re.compile(r"error CS\d{4}")
RE_CS_WARN = re.compile(r"warning CS\d{4}")
RE_EXCEPTION = re.compile(r"^([A-Za-z_][\w.]*Exception)(: .*)?$")
RE_ASSERT = re.compile(r"Assertion failed")
RE_MISSING_SCRIPT = re.compile(r"referenced script.*is missing|Missing Prefab|missing script", re.I)
RE_STACK = re.compile(r"^\s*(at |[\w.<>`]+[:.][\w<>`]+ ?\(.*\)( \(at .*\))?$|\(Filename:)|^Parameter name:|^Your script should")
RE_LOGERROR_FRAME = re.compile(r"UnityEngine\.Debug:LogError|UnityEngine\.Debug:LogException|UnityEngine\.Logger:LogError")
RE_LOGWARN_FRAME = re.compile(r"UnityEngine\.Debug:LogWarning")
RE_PROJECT_FRAME = re.compile(r"(?:\(at | in /.*?/)(Assets/[^)\s]+)")


def load_noise():
    if not NOISE_FILE.exists():
        return []
    pats = []
    for line in NOISE_FILE.read_text().splitlines():
        line = line.strip()
        if line and not line.startswith("#"):
            pats.append(re.compile(line))
    return pats


def is_noise(text, noise):
    return any(p.search(text) for p in noise)


def first_project_frame(stack):
    for s in stack:
        m = RE_PROJECT_FRAME.search(s)
        if m:
            return m.group(1)
    frames = [x.strip() for x in stack if not x.startswith(("Parameter name:", "Your script should"))]
    return frames[0] if frames else ""


def parse_log(path, noise, out):
    lines = path.read_text(errors="replace").splitlines()
    i = 0
    n = len(lines)
    while i < n:
        line = lines[i]
        # Collect a message block: message line + following stack lines.
        stack = []
        j = i + 1
        while j < n and RE_STACK.match(lines[j]):
            stack.append(lines[j])
            j += 1
        block = "\n".join([line] + stack)
        if is_noise(block, noise):
            i = j
            continue
        src = f"{path.name}:{i + 1}"
        if RE_CS_ERROR.search(line):
            out["compile_errors"].append({"msg": line.strip(), "src": src})
        elif RE_CS_WARN.search(line):
            out["counts"]["compile_warnings"] += 1
        elif RE_EXCEPTION.match(line.strip()):
            out["exceptions"].append({"msg": line.strip()[:300], "at": first_project_frame(stack), "src": src})
        elif RE_ASSERT.search(line):
            out["asserts"].append({"msg": line.strip()[:300], "at": first_project_frame(stack), "src": src})
        elif RE_MISSING_SCRIPT.search(line):
            out["missing_scripts"].append({"msg": line.strip()[:300], "src": src})
        elif any(RE_LOGERROR_FRAME.search(s) for s in stack):
            out["errors"].append({"msg": line.strip()[:300], "at": first_project_frame(stack), "src": src})
        elif any(RE_LOGWARN_FRAME.search(s) for s in stack):
            out["counts"]["warnings"] += 1
            if len(out["warning_samples"]) < 15:
                out["warning_samples"].append({"msg": line.strip()[:200], "at": first_project_frame(stack)})
        i = j if stack else i + 1


def parse_jsonl(path, noise, out):
    for k, raw in enumerate(path.read_text(encoding="utf-8-sig", errors="replace").splitlines(), 1):
        try:
            rec = json.loads(raw)
        except json.JSONDecodeError:
            continue
        kind = rec.get("type", "")
        msg = str(rec.get("message", ""))
        stack = str(rec.get("stack", "")).splitlines()
        if kind == "event":
            out["events"].append(rec)
            continue
        if is_noise(msg + "\n" + "\n".join(stack), noise):
            continue
        item = {"msg": msg[:300], "at": first_project_frame(stack), "src": f"{path.name}:{k}", "frame": rec.get("frame")}
        if kind == "Exception":
            out["exceptions"].append(item)
        elif kind == "Assert":
            out["asserts"].append(item)
        elif kind == "Error":
            out["errors"].append(item)
        elif kind == "Warning":
            out["counts"]["warnings"] += 1


def parse_tests(path, out):
    try:
        root = ET.parse(path).getroot()
    except ET.ParseError:
        return
    run = root if root.tag == "test-run" else root.find(".//test-run")
    if run is None:
        return
    t = out["tests"]
    for key in ("total", "passed", "failed", "skipped"):
        t[key] += int(run.get(key, 0))
    for case in root.iter("test-case"):
        if case.get("result") == "Failed":
            msg = case.findtext("failure/message") or ""
            t["failures"].append({"name": case.get("fullname"), "msg": msg.strip()[:300]})


def dedupe(items):
    seen = {}
    for it in items:
        key = (it["msg"], it.get("at", ""))
        if key in seen:
            seen[key]["count"] += 1
        else:
            seen[key] = dict(it, count=1)
    return list(seen.values())


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    quiet = "--quiet" in sys.argv
    if len(args) != 1:
        print(__doc__)
        return 2
    run = pathlib.Path(args[0])
    noise = load_noise()
    out = {
        "run": str(run),
        "compile_errors": [], "exceptions": [], "asserts": [], "errors": [], "missing_scripts": [],
        "warning_samples": [], "events": [],
        "counts": {"warnings": 0, "compile_warnings": 0},
        "tests": {"total": 0, "passed": 0, "failed": 0, "skipped": 0, "failures": []},
    }
    for p in sorted(run.glob("*.log")):
        parse_log(p, noise, out)
    for p in sorted(run.glob("*.jsonl")):
        parse_jsonl(p, noise, out)
    for p in sorted(run.glob("*.xml")):
        parse_tests(p, out)
    for key in ("compile_errors", "exceptions", "asserts", "errors", "missing_scripts"):
        out[key] = dedupe(out[key])
    bad = {k: len(out[k]) for k in ("compile_errors", "exceptions", "asserts", "errors", "missing_scripts")}
    bad["test_failures"] = out["tests"]["failed"]
    for lp in run.glob("*.log"):
        if "Caught fatal signal" in lp.read_text(errors="replace"):
            bad["native_crash"] = 1  # the editor or player crashed in native code
    meta = run / "meta.txt"
    if meta.exists() and meta.read_text().startswith("mode=play") and not (run / "playthrough.json").exists():
        bad["no_playthrough"] = 1  # a play run that never wrote its result
    if not any(run.glob("*.log")) and not any(run.glob("*.jsonl")) and not any(run.glob("*.xml")):
        bad["no_logs"] = 1  # Unity never wrote a log: the run failed before start.
    out["clean"] = not any(bad.values())
    out["bad_counts"] = bad
    (run / "summary.json").write_text(json.dumps(out, indent=2))
    if not quiet:
        status = "CLEAN" if out["clean"] else "DIRTY"
        print(f"[triage] {status} {run}")
        print("[triage] " + " ".join(f"{k}={v}" for k, v in bad.items())
              + f" warnings={out['counts']['warnings']} events={len(out['events'])}"
              + (f" tests={out['tests']['passed']}/{out['tests']['total']}" if out["tests"]["total"] else ""))
        for key in ("compile_errors", "exceptions", "asserts", "errors", "missing_scripts"):
            for it in out[key][:8]:
                print(f"  {key[:-1] if key.endswith('s') else key}: {it['msg'][:180]} @ {it.get('at') or it['src']} x{it['count']}")
        for f in out["tests"]["failures"][:8]:
            print(f"  test_failure: {f['name']}: {f['msg'][:150]}")
    return 0 if out["clean"] else 1


if __name__ == "__main__":
    sys.exit(main())
