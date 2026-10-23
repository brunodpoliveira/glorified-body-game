// Small client for the RFab backend, for the devlog narration. Prints no key or token.
// BASE: the backend (default http://localhost:3000, production: https://api.rfab.ai).
// RFAB_TOKEN: a login token. Without it, the local backend mints one for EMAIL (local development only).
const path = require('path');
const fs = require('fs');
const { execSync } = require('child_process');

const BE = process.env.RFAB_BACKEND || '/home/pc/MEGA/code/reality-fabricator/rfab-backend';
const BASE = process.env.BASE || 'http://localhost:3000';
// The local account: EMAIL, or the first line of ~/glorified-body-renders/devlog/account.txt (private, not in git).
const ACCOUNT_FILE = path.join(process.env.HOME, 'glorified-body-renders/devlog/account.txt');
const EMAIL = process.env.EMAIL || (fs.existsSync(ACCOUNT_FILE) ? fs.readFileSync(ACCOUNT_FILE, 'utf8').split('\n')[0].trim() : '');

function mintToken() {
  if (process.env.RFAB_TOKEN) return process.env.RFAB_TOKEN;
  const out = execSync(`node scripts/get_user_token.js --email ${EMAIL} --ttl 6h`, { cwd: BE, encoding: 'utf8' });
  return out.match(/ey[A-Za-z0-9._-]+/g).pop();
}

const auth = { Authorization: `Bearer ${mintToken()}` };

async function api(method, route, body) {
  const r = await fetch(`${BASE}${route}`, {
    method,
    headers: body ? { ...auth, 'Content-Type': 'application/json' } : auth,
    body: body ? JSON.stringify(body) : undefined,
    signal: AbortSignal.timeout(900000),
  });
  const j = await r.json().catch(() => ({}));
  return { status: r.status, json: j };
}

async function balance() {
  const { json } = await api('GET', '/api/tokens/balance');
  return json.tokenBalance ?? null;
}

async function download(url, file) {
  const full = url.startsWith('http') ? url : `${BASE}${url}`;
  // The login token goes only to our backend: S3 answers 400 to a request with a Bearer header.
  const r = await fetch(full, { headers: full.startsWith(BASE) ? auth : {} });
  if (!r.ok) throw new Error(`download ${r.status} ${url}`);
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, Buffer.from(await r.arrayBuffer()));
  return file;
}

module.exports = { BASE, api, balance, download };
