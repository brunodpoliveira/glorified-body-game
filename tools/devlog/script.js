// The devlog script format, shared by narrate.js and assemble.py (through script.json).
// See tools/devlog/README.md. A script is Markdown:
//
//   ---
//   episode: 0
//   kind: long            (long = 16:9, short = 9:16 with captions)
//   title: Why the series became a game
//   voice: me             (a name in voices.json)
//   ---
//   ## 1. Cold open
//   VISUAL: clip T12/edit/T12_slice_1080.mp4 0:10-0:18
//   VISUAL: still game/step3/telegraph.png
//   VISUAL: card "Big line" "small line"
//   NARRATION:
//   The words the voice reads. Several lines are fine.
//
// Usage: node script.js <script.md>   -> prints the parsed JSON (also used by narrate.js).
const fs = require('fs');

function parseTime(s) {
  const m = String(s).match(/^(\d+):(\d\d(?:\.\d+)?)$/);
  if (m) return parseInt(m[1], 10) * 60 + parseFloat(m[2]);
  return parseFloat(s);
}

function parseVisual(line) {
  const rest = line.replace(/^VISUAL:\s*/i, '').trim();
  const kind = (rest.split(/\s+/)[0] || '').toLowerCase();
  if (kind === 'card') {
    const parts = [...rest.matchAll(/"([^"]*)"/g)].map((m) => m[1]);
    return { kind: 'card', lines: parts.length ? parts : [rest.slice(4).trim()] };
  }
  if (kind === 'clip' || kind === 'still' || kind === 'seq') {
    const [, id, range] = rest.match(/^\w+\s+(\S+)(?:\s+(\S+))?/);
    const v = { kind, id };
    if (range && range.includes('-')) {
      const [a, b] = range.split('-');
      v.start = parseTime(a);
      v.end = parseTime(b);
    }
    return v;
  }
  return { kind: 'unknown', raw: rest };
}

function parse(md) {
  const out = { meta: {}, sections: [] };
  let body = md;
  const fm = md.match(/^---\n([\s\S]*?)\n---\n?/);
  if (fm) {
    for (const l of fm[1].split('\n')) {
      const m = l.match(/^(\w+):\s*(.*)$/);
      if (m) out.meta[m[1]] = m[2].trim();
    }
    body = md.slice(fm[0].length);
  }
  let cur = null;
  let inNarration = false;
  for (const raw of body.split('\n')) {
    const line = raw.replace(/\s+$/, '');
    const h = line.match(/^##\s+(?:(\d+)\.\s*)?(.*)$/);
    if (h) {
      cur = { index: out.sections.length + 1, name: h[2].trim(), visuals: [], narration: [] };
      out.sections.push(cur);
      inNarration = false;
      continue;
    }
    if (!cur) continue;
    if (/^VISUAL:/i.test(line)) { cur.visuals.push(parseVisual(line)); inNarration = false; continue; }
    if (/^NARRATION:/i.test(line)) { inNarration = true; const rest = line.replace(/^NARRATION:\s*/i, ''); if (rest) cur.narration.push(rest); continue; }
    if (inNarration && line.trim() && !line.startsWith('<!--')) cur.narration.push(line.trim());
  }
  for (const s of out.sections) {
    s.text = s.narration.join(' ').replace(/\s+/g, ' ').trim();
    delete s.narration;
  }
  return out;
}

module.exports = { parse };

if (require.main === module) {
  const file = process.argv[2];
  if (!file) { console.error('usage: node script.js <script.md>'); process.exit(2); }
  console.log(JSON.stringify(parse(fs.readFileSync(file, 'utf8')), null, 2));
}
