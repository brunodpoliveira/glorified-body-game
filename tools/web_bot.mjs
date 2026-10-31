#!/usr/bin/env node
// The web build's bot run (2026-10-31, itch.io first). It serves a web build the way itch.io does (a .gz file goes out
// with "Content-Encoding: gzip", a .br file with "br"), starts Chrome headless, opens the page with the bot in the URL
// (?gbBot=<bot>), writes every console line and page error to console.log, takes a screenshot at each bot shot
// ("OK   shot_" lines, the bot holds each one for gbShotWait s) and every 20 s, and stops at "finished" or the time limit.
// No npm package: Node 22's WebSocket talks to Chrome's debug port.
//
// Usage: node tools/web_bot.mjs <build dir> <bot> <out dir> [time limit s] [chrome flags...]
// Out:   <out dir>/console.log, <out dir>/shots/*.png, <out dir>/web_bot.json (pass, load seconds, renderer, errors)

import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { spawn } from 'node:child_process';

const [buildDir, bot = 'demo', outDir = 'web_run', limitArg = '1500', ...extraFlags] = process.argv.slice(2);
if (!buildDir) { console.error('usage: node tools/web_bot.mjs <build dir> <bot> <out dir> [time limit s] [chrome flags...]'); process.exit(2); }
const limitMs = Number(limitArg) * 1000;
fs.mkdirSync(path.join(outDir, 'shots'), { recursive: true });
const logFile = fs.createWriteStream(path.join(outDir, 'console.log'));
const log = (line) => { logFile.write(line + '\n'); };

// ── The server: itch.io's headers ─────────────────────────────────
const types = { '.html': 'text/html', '.js': 'application/javascript', '.wasm': 'application/wasm', '.data': 'application/octet-stream',
  '.css': 'text/css', '.png': 'image/png', '.ico': 'image/x-icon', '.json': 'application/json', '.webm': 'video/webm', '.mp4': 'video/mp4' };
const server = http.createServer((req, res) => {
  const url = decodeURIComponent(req.url.split('?')[0]);
  const file = path.resolve(buildDir, '.' + (url === '/' ? '/index.html' : url));
  if (!file.startsWith(path.resolve(buildDir) + path.sep)) { res.writeHead(403); res.end(); return; }
  fs.stat(file, (err, st) => {
    if (err || !st.isFile()) { res.writeHead(404); res.end(); log(`[server] 404 ${url}`); return; }
    let ext = path.extname(file), encoding = null;
    if (ext === '.gz') { encoding = 'gzip'; ext = path.extname(file.slice(0, -3)); }
    if (ext === '.br') { encoding = 'br'; ext = path.extname(file.slice(0, -3)); }
    const headers = { 'Content-Type': types[ext] || 'application/octet-stream', 'Content-Length': st.size };
    if (encoding) headers['Content-Encoding'] = encoding;
    // Range requests: the video element asks for parts of a clip.
    const range = req.headers.range && /bytes=(\d*)-(\d*)/.exec(req.headers.range);
    if (range && !encoding) {
      const start = range[1] ? Number(range[1]) : 0, end = range[2] ? Number(range[2]) : st.size - 1;
      res.writeHead(206, { ...headers, 'Content-Length': end - start + 1, 'Content-Range': `bytes ${start}-${end}/${st.size}`, 'Accept-Ranges': 'bytes' });
      fs.createReadStream(file, { start, end }).pipe(res);
      return;
    }
    res.writeHead(200, { ...headers, 'Accept-Ranges': 'bytes' });
    fs.createReadStream(file).pipe(res);
  });
});
await new Promise((ok) => server.listen(0, '127.0.0.1', ok));
const port = server.address().port;

// ── Chrome ────────────────────────────────────────────────────────
const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'gb-chrome-'));
const debugPort = 9300 + Math.floor(Math.random() * 600);
const chrome = spawn('google-chrome', ['--headless=new', `--remote-debugging-port=${debugPort}`, `--user-data-dir=${profile}`,
  '--window-size=1280,720', '--autoplay-policy=no-user-gesture-required', '--no-first-run', '--no-default-browser-check',
  '--ignore-gpu-blocklist', '--enable-gpu', ...extraFlags, 'about:blank'], { stdio: 'ignore' });
let target = null;
for (let i = 0; i < 50 && !target; i++) {
  await new Promise((ok) => setTimeout(ok, 200));
  try { target = (await (await fetch(`http://127.0.0.1:${debugPort}/json/list`)).json()).find((t) => t.type === 'page'); } catch { /* not up yet */ }
}
if (!target) { console.error('Chrome did not start'); chrome.kill(); process.exit(3); }
const ws = new WebSocket(target.webSocketDebuggerUrl);
await new Promise((ok) => ws.addEventListener('open', ok));
let nextId = 1;
const pending = new Map();
const send = (method, params = {}) => new Promise((ok) => { const id = nextId++; pending.set(id, ok); ws.send(JSON.stringify({ id, method, params })); });

// ── The run ───────────────────────────────────────────────────────
const t0 = Date.now();
const result = { bot, pass: false, finished: false, loadSeconds: null, renderer: null, errors: [], warnings: 0, shots: 0, seconds: 0 };
let shotCount = 0, done = false;
async function shot(name) {
  const r = await send('Page.captureScreenshot', { format: 'png' });
  if (r && r.data) { fs.writeFileSync(path.join(outDir, 'shots', `${String(shotCount++).padStart(3, '0')}_${name}.png`), Buffer.from(r.data, 'base64')); result.shots++; }
}
function onLine(level, text) {
  const t = ((Date.now() - t0) / 1000).toFixed(1);
  log(`${t} [${level}] ${text}`);
  if ((level === 'error' || level === 'exception') && !/favicon\.ico/.test(text)) result.errors.push(text.slice(0, 300));
  if (level === 'warning') result.warnings++;
  if (result.loadSeconds === null && /\[GameManager\]|MainMenu|\[Gore\]/.test(text)) result.loadSeconds = Number(t);
  const m = /\] OK +(shot_\S+)/.exec(text);
  if (m) shot(m[1]);
  const f = /finished pass=(\w+)/.exec(text);
  if (f) { result.finished = true; result.pass = f[1] === 'True'; done = true; }
}
ws.addEventListener('message', (ev) => {
  const msg = JSON.parse(ev.data);
  if (msg.id && pending.has(msg.id)) { pending.get(msg.id)(msg.result); pending.delete(msg.id); return; }
  if (msg.method === 'Runtime.consoleAPICalled') onLine(msg.params.type === 'warn' ? 'warning' : msg.params.type, msg.params.args.map((a) => a.value ?? a.description ?? '').join(' '));
  if (msg.method === 'Runtime.exceptionThrown') onLine('exception', msg.params.exceptionDetails.exception?.description || msg.params.exceptionDetails.text);
  if (msg.method === 'Log.entryAdded') onLine(msg.params.entry.level, `${msg.params.entry.source}: ${msg.params.entry.text} ${msg.params.entry.url || ''}`);
});
await send('Runtime.enable');
await send('Log.enable');
await send('Page.enable');
await send('Emulation.setDeviceMetricsOverride', { width: 1280, height: 720, deviceScaleFactor: 1, mobile: false });
const probe = await send('Runtime.evaluate', { expression: `(() => { const g = document.createElement('canvas').getContext('webgl2'); if (!g) return 'no webgl2'; const d = g.getExtension('WEBGL_debug_renderer_info'); return d ? g.getParameter(d.UNMASKED_RENDERER_WEBGL) : g.getParameter(g.RENDERER); })()`, returnByValue: true });
result.renderer = probe?.result?.value ?? null;
log(`[driver] renderer: ${result.renderer}`);
const shotWait = bot === 'demo' ? '&gbShotWait=1.5' : '';
await send('Page.navigate', { url: `http://127.0.0.1:${port}/index.html?gbBot=${bot}&gbTimeout=${Math.floor(limitMs / 1000) - 30}${shotWait}` });
const timer = setInterval(() => { if (!done) shot('t' + Math.round((Date.now() - t0) / 1000)); }, 20000);
while (!done && Date.now() - t0 < limitMs) await new Promise((ok) => setTimeout(ok, 500));
clearInterval(timer);
await new Promise((ok) => setTimeout(ok, 1500));
await shot('end');
const mem = await send('Runtime.evaluate', { expression: 'performance.memory ? performance.memory.usedJSHeapSize : 0', returnByValue: true });
result.jsHeapMB = Math.round((mem?.result?.value ?? 0) / 1048576);
result.seconds = Math.round((Date.now() - t0) / 1000);
fs.writeFileSync(path.join(outDir, 'web_bot.json'), JSON.stringify(result, null, 2) + '\n');
console.log(JSON.stringify({ pass: result.pass, finished: result.finished, errors: result.errors.length, shots: result.shots, seconds: result.seconds, renderer: result.renderer }));
ws.close();
const exited = new Promise((ok) => chrome.once('exit', ok));
chrome.kill();
await Promise.race([exited, new Promise((ok) => setTimeout(ok, 5000))]);
server.close();
fs.rmSync(profile, { recursive: true, force: true, maxRetries: 3 });
process.exit(result.pass ? 0 : 1);
