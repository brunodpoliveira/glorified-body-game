#!/usr/bin/env python3
"""Devlog assembly: timing.json (from narrate.js) + the visuals -> one MP4, a contact sheet, REVIEW.md.

Each section lasts as long as its narration plus a short pad. Its visuals share that time evenly. A still gets a
slow push-in, a clip is cut (or held on its last frame), a card is drawn here. kind: long = 1920x1080,
short = 1080x1920 with the title on top and the section text as a caption.
Run: python3 -I assemble.py --timing <dir>/timing.json [--renders ~/glorified-body-renders] [--label v1]
Out: <dir>/final.mp4, <dir>/contact_sheet.png, <dir>/REVIEW.md, <dir>/seg/ (the pieces)
"""
import argparse
import json
import os
import shutil
import subprocess
import sys
import textwrap

from PIL import Image, ImageDraw, ImageFont

FPS = 30
PAD_S = 0.6          # silence after each section
CARD_ONLY_S = 3.0    # a section with no narration
MIN_VISUAL_S = 1.5
BG = (12, 12, 14)
FONT_BOLD = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"


def run(*cmd):
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode != 0:
        sys.stderr.write(" ".join(cmd) + "\n" + r.stderr[-2000:] + "\n")
        raise SystemExit(f"command failed: {cmd[0]}")
    return r.stdout


def probe_seconds(path):
    out = run("ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", path).strip()
    try:
        return float(out)
    except ValueError:
        return 0.0


def seq_input(folder):
    """A frame sequence folder -> (pattern, frames)."""
    files = sorted(f for f in os.listdir(folder) if f.endswith(".png") and os.path.splitext(f)[0].isdigit())
    if not files:
        raise SystemExit(f"no frames in {folder}")
    width = len(os.path.splitext(files[0])[0])
    return os.path.join(folder, f"%0{width}d.png"), len(files)


class Frame:
    def __init__(self, kind):
        self.kind = kind
        self.w, self.h = (1920, 1080) if kind == "long" else (1080, 1920)
        # The picture band for the short: 16:9 inside the tall frame, a little above the centre.
        self.band_w, self.band_h = (self.w, self.h) if kind == "long" else (1080, 608)
        self.band_y = 0 if kind == "long" else 560


def card_png(lines, w, h, out):
    """A card at w x h: the first line big, the others small, centred. Fonts scale with the width."""
    img = Image.new("RGB", (w, h), BG)
    dr = ImageDraw.Draw(img)
    # The short's band is 1080 wide on a phone: larger type there.
    big_px, small_px = (w // 26, w // 48) if w >= 1600 else (w // 18, w // 34)
    big = ImageFont.truetype(FONT_BOLD, big_px)
    small = ImageFont.truetype(FONT, small_px)
    wrapped = []
    for i, ln in enumerate(lines):
        width = 32 if i == 0 else 60
        for part in textwrap.wrap(ln, width) or [""]:
            wrapped.append((part, big if i == 0 else small))
    heights = [int((big_px if f is big else small_px) * 1.3) for _, f in wrapped]
    y = (h - sum(heights)) // 2
    for (txt, f), hgt in zip(wrapped, heights):
        x = (w - dr.textlength(txt, font=f)) // 2
        dr.text((x, y), txt, font=f, fill=(240, 240, 236) if f is big else (190, 190, 186))
        y += hgt
    img.save(out)


def text_overlay_png(frame, title, caption, out):
    """The short's title (top) and caption (under the band) as one RGBA layer, drawn with PIL."""
    img = Image.new("RGBA", (frame.w, frame.h), (0, 0, 0, 0))
    dr = ImageDraw.Draw(img)
    tf = ImageFont.truetype(FONT_BOLD, 60)
    cf = ImageFont.truetype(FONT_BOLD, 48)
    y = 280
    for ln in textwrap.wrap(title, 24):
        x = (frame.w - dr.textlength(ln, font=tf)) // 2
        dr.text((x, y), ln, font=tf, fill=(255, 255, 255, 255))
        y += 72
    if caption:
        lines = textwrap.wrap(caption, 30)
        line_h = 60
        box_h = line_h * len(lines) + 36
        top = frame.band_y + frame.band_h + 90
        dr.rectangle((70, top, frame.w - 70, top + box_h), fill=(0, 0, 0, 150))
        y = top + 18
        for ln in lines:
            x = (frame.w - dr.textlength(ln, font=cf)) // 2
            dr.text((x, y), ln, font=cf, fill=(255, 255, 255, 255))
            y += line_h
    img.save(out)


def fit_filter(w, h):
    """Scale to fit inside w x h and letterbox on the dark background."""
    return (f"scale={w}:{h}:force_original_aspect_ratio=decrease:flags=lanczos,"
            f"pad={w}:{h}:(ow-iw)/2:(oh-ih)/2:color=0x0c0c0e,setsar=1")


def encode_args(frames):
    return ["-r", str(FPS), "-frames:v", str(frames), "-c:v", "libx264", "-crf", "18", "-preset", "veryfast",
            "-pix_fmt", "yuv420p", "-an"]


def render_visual(v, frames, frame, renders, out, tmp):
    """One visual -> a silent MP4 of exactly `frames` frames at the picture-band size."""
    bw, bh = frame.band_w, frame.band_h
    seconds = frames / FPS
    if v["kind"] == "card":
        png = out + ".card.png"
        card_png(v.get("lines") or [""], bw, bh, png)
        run("ffmpeg", "-loglevel", "error", "-y", "-loop", "1", "-framerate", str(FPS), "-i", png,
            "-vf", fit_filter(bw, bh), *encode_args(frames), out)
        return
    src = os.path.join(renders, v["id"])
    if not os.path.exists(src):
        # A missing file never stops the night build: a card names it, the review shows it.
        print(f"  MISSING {v['kind']} {v['id']}: a card stands in")
        return render_visual({"kind": "card", "lines": ["MISSING", v["id"]]}, frames, frame, renders, out, tmp)
    if v["kind"] == "still":
        # Letterbox to the band, then a slow push-in (zoompan makes `frames` frames from one picture).
        zoom = f"zoompan=z='min(1+0.00045*on,1.10)':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d={frames}:s={bw}x{bh}:fps={FPS}"
        run("ffmpeg", "-loglevel", "error", "-y", "-i", src,
            "-vf", f"{fit_filter(bw * 2, bh * 2)},{zoom}", *encode_args(frames), out)
        return
    if v["kind"] in ("clip", "seq"):
        if v["kind"] == "seq":
            pattern, n = seq_input(src)
            inp = ["-framerate", str(FPS), "-i", pattern]
            avail = n / FPS
        else:
            avail = probe_seconds(src)
            inp = []
            if "start" in v:
                inp += ["-ss", f"{v['start']:.3f}"]
                avail = max(0.0, avail - v["start"])
            if "end" in v and "start" in v:
                avail = min(avail, v["end"] - v["start"])
                inp += ["-t", f"{avail:.3f}"]
            inp += ["-i", src]
        hold = max(0.0, seconds - avail + 0.1)
        vf = f"fps={FPS},{fit_filter(bw, bh)}"
        if hold > 0.01:
            vf += f",tpad=stop_mode=clone:stop_duration={hold:.3f}"
        run("ffmpeg", "-loglevel", "error", "-y", *inp, "-vf", vf, *encode_args(frames), out)
        return
    raise SystemExit(f"unknown visual {v}")


def short_compose(band_mp4, frames, frame, title, caption, out, tmp):
    """The short: the band over a tall dark frame, the title above, the caption under it (a PIL layer)."""
    layer = tmp + ".text.png"
    text_overlay_png(frame, title, caption, layer)
    run("ffmpeg", "-loglevel", "error", "-y",
        "-f", "lavfi", "-i", f"color=c=0x0c0c0e:s={frame.w}x{frame.h}:r={FPS}",
        "-i", band_mp4, "-i", layer,
        "-filter_complex", f"[0:v][1:v]overlay=0:{frame.band_y}:shortest=1[b];[b][2:v]overlay=0:0",
        *encode_args(frames), out)


def contact_sheet(final, sections, starts, out):
    thumbs = []
    for s, t0 in zip(sections, starts):
        mid = t0 + s["seconds_total"] / 2
        png = out + f".{s['index']:02d}.png"
        run("ffmpeg", "-loglevel", "error", "-y", "-ss", f"{mid:.2f}", "-i", final, "-frames:v", "1",
            "-vf", "scale=480:-2", png)
        thumbs.append((s, png))
    cols = 4
    tw, th = 480, max(Image.open(p).height for _, p in thumbs) if thumbs else 270
    rows = (len(thumbs) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * (tw + 10) + 10, rows * (th + 40) + 10), BG)
    dr = ImageDraw.Draw(sheet)
    font = ImageFont.truetype(FONT, 18)
    for i, (s, p) in enumerate(thumbs):
        x = 10 + (i % cols) * (tw + 10)
        y = 10 + (i // cols) * (th + 40)
        sheet.paste(Image.open(p), (x, y))
        dr.text((x, y + th + 6), f"{s['index']:02d} {s['name'][:40]}  {s['seconds_total']:.1f}s", font=font, fill=(220, 220, 216))
        os.remove(p)
    sheet.save(out)


def review_md(timing, sections, starts, out_dir, label, total):
    meta = timing.get("meta", {})
    lines = [f"# Review: episode {meta.get('episode', '?')}, {meta.get('kind', 'long')}, {label}", "",
             f"Title: {meta.get('title', '')}", f"Voice: {timing.get('voice')}",
             f"Length: {total:.0f} s ({total / 60:.1f} min). Narration tokens this build: {timing.get('tokens', 0)}.", "",
             "1. Watch `final.mp4` full screen. Open `contact_sheet.png` for the one-page view.",
             "2. Write your notes in `NOTES.md`, one line per note, start with the section number.",
             "3. The next night run reads the notes, writes the next script version and renders it.", "",
             "| # | At | Section | s | Visuals | Narration |", "|---|---|---|---|---|---|"]
    for s, t0 in zip(sections, starts):
        vis = "; ".join(v.get("id") or " / ".join(v.get("lines", [])) for v in s["visuals"]) or "(none)"
        lines.append(f"| {s['index']:02d} | {int(t0) // 60}:{int(t0) % 60:02d} | {s['name']} | {s['seconds_total']:.1f} | {vis} | {s['text']} |")
    lines += ["", "## Upload checklist (T13)", "",
              "- Paid promotion: ON. Altered or synthetic content: YES (the voice is a clone). Audience: not for kids.",
              "- The description starts with the sponsor line and the link. The open card says and shows it.",
              "- Title and thumbnail: calm, no shock words."]
    with open(os.path.join(out_dir, "REVIEW.md"), "w") as f:
        f.write("\n".join(lines) + "\n")
    notes = os.path.join(out_dir, "NOTES.md")
    if not os.path.exists(notes):
        with open(notes, "w") as f:
            f.write("# Bruno's notes\n\nOne line per note. Start with the section number, for example `03: cut the second sentence`.\n\n")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--timing", required=True)
    ap.add_argument("--renders", default=os.path.expanduser("~/glorified-body-renders"))
    ap.add_argument("--label", default="")
    a = ap.parse_args()
    timing = json.load(open(a.timing))
    out_dir = os.path.dirname(os.path.abspath(a.timing))
    frame = Frame(timing.get("meta", {}).get("kind", "long"))
    title = timing.get("meta", {}).get("title", "")
    seg = os.path.join(out_dir, "seg")
    shutil.rmtree(seg, ignore_errors=True)
    os.makedirs(seg)
    sections = timing["sections"]

    video_parts, audio_parts, starts = [], [], []
    t = 0.0
    for s in sections:
        visuals = s["visuals"] or [{"kind": "card", "lines": [s["name"]]}]
        total_s = (s["seconds"] + PAD_S) if s["seconds"] > 0 else CARD_ONLY_S
        total_frames = max(int(round(total_s * FPS)), int(MIN_VISUAL_S * FPS) * len(visuals))
        s["seconds_total"] = total_frames / FPS
        starts.append(t)
        t += s["seconds_total"]
        share = [total_frames // len(visuals)] * len(visuals)
        share[-1] += total_frames - sum(share)
        for k, (v, n) in enumerate(zip(visuals, share)):
            band = os.path.join(seg, f"{s['index']:02d}_{k}.band.mp4")
            render_visual(v, n, frame, a.renders, band, band)
            if frame.kind == "short":
                part = os.path.join(seg, f"{s['index']:02d}_{k}.mp4")
                short_compose(band, n, frame, title, s["text"], part, band)
            else:
                part = band
            video_parts.append(part)
        # Audio: the narration padded to the section length, or silence.
        apart = os.path.join(seg, f"{s['index']:02d}.wav")
        if s["seconds"] > 0 and os.path.exists(s["file"]):
            run("ffmpeg", "-loglevel", "error", "-y", "-i", s["file"], "-af", f"apad=whole_dur={s['seconds_total']:.3f}",
                "-t", f"{s['seconds_total']:.3f}", "-ar", "48000", "-ac", "2", apart)
        else:
            run("ffmpeg", "-loglevel", "error", "-y", "-f", "lavfi", "-i", "anullsrc=r=48000:cl=stereo",
                "-t", f"{s['seconds_total']:.3f}", apart)
        audio_parts.append(apart)
        print(f"{s['index']:02d} {s['name']}: {s['seconds_total']:.1f} s, {len(visuals)} visual(s)")

    with open(os.path.join(seg, "video.txt"), "w") as f:
        f.writelines(f"file '{p}'\n" for p in video_parts)
    with open(os.path.join(seg, "audio.txt"), "w") as f:
        f.writelines(f"file '{p}'\n" for p in audio_parts)
    video = os.path.join(seg, "video.mp4")
    audio = os.path.join(seg, "narration.wav")
    run("ffmpeg", "-loglevel", "error", "-y", "-f", "concat", "-safe", "0", "-i", os.path.join(seg, "video.txt"), "-c", "copy", video)
    run("ffmpeg", "-loglevel", "error", "-y", "-f", "concat", "-safe", "0", "-i", os.path.join(seg, "audio.txt"),
        "-af", "loudnorm=I=-16:TP=-1.5:LRA=11", "-ar", "48000", audio)
    final = os.path.join(out_dir, "final.mp4")
    run("ffmpeg", "-loglevel", "error", "-y", "-i", video, "-i", audio, "-map", "0:v", "-map", "1:a",
        "-c:v", "copy", "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart", "-shortest", final)
    total = probe_seconds(final)
    contact_sheet(final, sections, starts, os.path.join(out_dir, "contact_sheet.png"))
    review_md(timing, sections, starts, out_dir, a.label, total)
    print(f"ASSEMBLED {final} ({total:.1f} s, {frame.w}x{frame.h})")


if __name__ == "__main__":
    main()
