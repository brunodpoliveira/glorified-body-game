// Narration for a devlog script: one WAV for each section, read by the voice named in the script's front
// matter (voices.json maps names to RFab voice ids). The audio cache is keyed by the voice and the text, so
// an unchanged section is never rendered again, in any version, at any position, in the long or the short.
//
// Run: node narrate.js --script <script.md> --out <dir> [--cache <dir>] [--voices <voices.json>] [--dry]
// Out: <cache>/<hash>.wav (+ .txt), <dir>/timing.json (sections, seconds, visuals, text, the file of each)
const crypto = require('crypto');
const fs = require('fs');
const path = require('path');
const { parse } = require('./script');

const args = {};
for (let i = 2; i < process.argv.length; i++) {
  const a = process.argv[i];
  if (a.startsWith('--')) { const k = a.slice(2); const v = process.argv[i + 1] && !process.argv[i + 1].startsWith('--') ? process.argv[++i] : true; args[k] = v; }
}
if (!args.script || !args.out) { console.error('usage: node narrate.js --script <script.md> --out <dir> [--cache <dir>] [--voices <json>] [--dry]'); process.exit(2); }

const VOICES = JSON.parse(fs.readFileSync(args.voices || path.join(process.env.HOME, 'MEGA/glorified-body-renders/devlog/voices.json'), 'utf8'));
const script = parse(fs.readFileSync(args.script, 'utf8'));
const voiceName = script.meta.voice || 'me';
const voice = VOICES[voiceName];
if (!voice) { console.error(`no voice named "${voiceName}" in voices.json`); process.exit(2); }
const CACHE = args.cache || path.join(args.out, 'audio');
fs.mkdirSync(CACHE, { recursive: true });
fs.mkdirSync(args.out, { recursive: true });

function wavSeconds(file) {
  // RIFF header: byte rate at byte 28. Good enough for our own PCM files.
  const b = fs.readFileSync(file);
  const byteRate = b.readUInt32LE(28);
  return byteRate ? (b.length - 44) / byteRate : 0;
}

(async () => {
  const c = args.dry ? null : require('./rfab_client');
  const before = c ? await c.balance() : 0;
  const timing = { meta: script.meta, voice: voiceName, cache: CACHE, sections: [] };
  let rendered = 0;
  for (const s of script.sections) {
    const nn = String(s.index).padStart(2, '0');
    const entry = { index: s.index, name: s.name, text: s.text, visuals: s.visuals, file: null, seconds: 0 };
    if (!s.text) { timing.sections.push(entry); continue; }
    const key = crypto.createHash('sha1').update(`${voice}\n${s.text}`).digest('hex').slice(0, 16);
    const wav = path.join(CACHE, `${key}.wav`);
    entry.file = wav;
    if (!fs.existsSync(wav) && c) {
      const { status, json } = await c.api('POST', '/api/audio/dialogue', {
        lines: [{ speaker: 'narrator', voice, text: s.text }], gapMs: 0, format: 'wav',
      });
      if (!json.url) { console.error(`section ${nn} "${s.name}": ${status} ${JSON.stringify(json).slice(0, 200)}`); process.exit(1); }
      await c.download(json.url, wav);
      fs.writeFileSync(path.join(CACHE, `${key}.txt`), `${voice}\n${s.text}\n`);
      rendered++;
      console.log(`${nn} ${s.name}: ${json.durationSec} s (rendered)`);
    } else {
      console.log(`${nn} ${s.name}: ${fs.existsSync(wav) ? 'cached' : 'dry'}`);
    }
    if (fs.existsSync(wav)) entry.seconds = Math.round(wavSeconds(wav) * 100) / 100;
    timing.sections.push(entry);
  }
  timing.totalSeconds = Math.round(timing.sections.reduce((a, s) => a + s.seconds, 0) * 10) / 10;
  timing.tokens = c ? before - (await c.balance()) : 0;
  fs.writeFileSync(path.join(args.out, 'timing.json'), JSON.stringify(timing, null, 2));
  console.log(`${script.sections.length} sections, ${rendered} rendered, ${timing.totalSeconds} s of narration, ${timing.tokens} tokens -> ${path.join(args.out, 'timing.json')}`);
})().catch((e) => { console.error(e.message); process.exit(1); });
