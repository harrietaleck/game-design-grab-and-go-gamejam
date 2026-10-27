// Grab & Go dialogue backend (Task 4.6)
// - Keeps the LLM provider key on the server (never in the Unity build).
// - Validates a small request, calls the provider with a short system prompt, validates the reply,
//   and returns { "line": "..." }. Any failure returns an error status so the game uses a prewritten line.
// - No dependencies: Node 18+ (built-in fetch).
'use strict';

const http = require('http');
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

// ---------- tiny .env loader (no dependency) ----------
(function loadEnv() {
  const file = path.join(__dirname, '.env');
  if (!fs.existsSync(file)) return;
  for (const raw of fs.readFileSync(file, 'utf8').split(/\r?\n/)) {
    const line = raw.trim();
    if (!line || line.startsWith('#')) continue;
    const eq = line.indexOf('=');
    if (eq < 1) continue;
    const key = line.slice(0, eq).trim();
    let value = line.slice(eq + 1).trim();
    if ((value.startsWith('"') && value.endsWith('"')) || (value.startsWith("'") && value.endsWith("'"))) value = value.slice(1, -1);
    if (!(key in process.env)) process.env[key] = value;
  }
})();

const CONFIG = {
  host: process.env.HOST || '127.0.0.1',
  port: parseInt(process.env.PORT || '8787', 10),
  provider: (process.env.PROVIDER || 'mock').toLowerCase(), // mock | anthropic | openai
  clientToken: process.env.CLIENT_TOKEN || '',
  maxBodyBytes: 4096,
  providerTimeoutMs: parseInt(process.env.PROVIDER_TIMEOUT_MS || '6000', 10),
  ratePerMinute: parseInt(process.env.RATE_LIMIT_PER_MIN || '20', 10),
  maxPerDay: parseInt(process.env.MAX_REQUESTS_PER_DAY || '500', 10),
  maxLineChars: 120,
  anthropic: {
    key: process.env.ANTHROPIC_API_KEY || '',
    model: process.env.ANTHROPIC_MODEL || 'claude-haiku-5-5',
    url: 'https://api.anthropic.com/v1/messages',
    version: '2023-06-01',
  },
  openai: { // any OpenAI-compatible chat completions endpoint
    key: process.env.OPENAI_API_KEY || '',
    model: process.env.OPENAI_MODEL || '',
    baseUrl: (process.env.OPENAI_BASE_URL || 'https://api.openai.com/v1').replace(/\/+$/, ''),
  },
};

const ALLOWED_EVENTS = new Set([
  'RUN_STARTED', 'COIN_COLLECTED', 'GOLD_BAR_COLLECTED', 'MULTIPLE_LOOT_COLLECTED', 'CLAIRE_COLLISION',
  'AJ_NEAR_MISS', 'POLICE_PRESSURE_HIGH', 'DISTRACTION_USED', 'AJ_CAUGHT', 'AJ_RESCUED',
  'CLAIRE_LOW_HEALTH', 'RUN_WON', 'RUN_FAILED', 'TIME_UP',
]);
const ALLOWED_STATES = new Set(['MainMenu', 'Playing', 'Paused', 'AJCaught', 'Results']);
const PRESSURE = new Set(['Low', 'Medium', 'High']);

// ---------- validation ----------
const str = (v, max) => (typeof v === 'string' ? v.replace(/[\u0000-\u001f<>{}`]/g, ' ').trim().slice(0, max) : '');
const int = (v, lo, hi) => (Number.isFinite(v) ? Math.min(hi, Math.max(lo, Math.round(v))) : 0);

function validateRequest(body) {
  if (!body || typeof body !== 'object') return { error: 'body must be a JSON object' };
  if (!ALLOWED_EVENTS.has(body.event)) return { error: 'unknown event' };
  const state = ALLOWED_STATES.has(body.gameState) ? body.gameState : 'Playing';
  const turns = Array.isArray(body.recentDialogue) ? body.recentDialogue.slice(-4) : [];
  return {
    value: {
      event: body.event,
      gameState: state,
      timeRemaining: int(body.timeRemaining, 0, 600),
      claireHealth: int(body.claireHealth, 0, 1000),
      lootValue: int(body.lootValue, 0, 100000),
      coins: int(body.coins, 0, 100000),
      goldStacks: int(body.goldStacks, 0, 100000),
      policePressure: PRESSURE.has(body.policePressure) ? body.policePressure : 'Low',
      detail: str(body.detail, 40),
      claireReply: str(body.claireReply, 140) || null,
      persona: str(body.persona, 600),
      trust: int(body.trust, -10, 10),
      tension: int(body.tension, -10, 10),
      recentDialogue: turns
        .filter((t) => t && typeof t === 'object')
        .map((t) => ({ speaker: str(t.speaker, 12) || 'AJ', text: str(t.text, 200) }))
        .filter((t) => t.text),
    },
  };
}

function validateLine(raw) {
  if (typeof raw !== 'string') return null;
  let s = raw.replace(/[\r\n\t]+/g, ' ').replace(/\s+/g, ' ').trim();
  s = s.replace(/^AJ\s*:\s*/i, '').replace(/^["'“‘]+|["'”’]+$/g, '').trim();
  if (!s || s.length > CONFIG.maxLineChars) return null;
  if (/[<>{}`]|https?:\/\//i.test(s)) return null; // no markup, code or links
  return s;
}

// ---------- prompt ----------
function systemPrompt(persona) {
  return [
    'You write ONE short radio line spoken by AJ, a thief escaping a heist on foot, to Claire, his getaway partner, in the arcade game "Grab & Go: Loot on the Run".',
    'Character notes: ' + (persona || 'Cocky, funny, street-smart, nervous when danger is real, relies on Claire but rarely admits it.'),
    'Rules:',
    '- Speak only as AJ, in first person, to Claire. Never sound like an assistant. No narration, no stage directions, no emojis, no swearing.',
    '- Max 18 words, one or two sentences, under 120 characters.',
    '- React to the given EVENT using only the supplied facts. Never invent loot, obstacles, police actions, injuries or numbers.',
    '- Never give game instructions as commands to execute, never output code, JSON, links or markup.',
    '- Do not repeat or closely paraphrase lines in recentDialogue. Vary tone: funny when safe, tense when pressure is High or AJ is caught.',
    '- If claireReply is present, AJ may respond to it naturally.',
    'Output only the line text.',
  ].join('\n');
}

function userPrompt(ctx) {
  const { persona, ...facts } = ctx; // persona already in the system prompt
  return 'Game facts (authoritative, do not change them):\n' + JSON.stringify(facts);
}

// ---------- providers (replaceable) ----------
async function withTimeout(fn) {
  const ctrl = new AbortController();
  const timer = setTimeout(() => ctrl.abort(), CONFIG.providerTimeoutMs);
  try { return await fn(ctrl.signal); } finally { clearTimeout(timer); }
}

const MOCK_LINES = {
  GOLD_BAR_COLLECTED: 'Gold stack secured? Claire, you might be the real star here. Might.',
  MULTIPLE_LOOT_COLLECTED: 'You are hoovering that road clean. Remind me why I need you? Oh right.',
  AJ_RESCUED: 'You actually came back for me. Okay, I owe you. Do not make it weird.',
  CLAIRE_LOW_HEALTH: 'Claire, you are running on fumes. Hang on, we are nearly clear.',
  DISTRACTION_USED: 'Look at them chasing shadows. Nice move, partner.',
  RUN_WON: 'We made it! Best getaway ever, and yes, you were... fine. Great. You were great.',
  RUN_FAILED: 'Well, that went sideways. Same plan next time, less crashing.',
};

const providers = {
  async mock(ctx) {
    return MOCK_LINES[ctx.event] || 'Keep moving, Claire. We are almost out.';
  },

  async anthropic(ctx) {
    if (!CONFIG.anthropic.key) throw new Error('ANTHROPIC_API_KEY not set');
    const res = await withTimeout((signal) => fetch(CONFIG.anthropic.url, {
      method: 'POST',
      signal,
      headers: {
        'content-type': 'application/json',
        'x-api-key': CONFIG.anthropic.key,
        'anthropic-version': CONFIG.anthropic.version,
      },
      body: JSON.stringify({
        model: CONFIG.anthropic.model,
        max_tokens: 80,
        system: systemPrompt(ctx.persona),
        messages: [{ role: 'user', content: userPrompt(ctx) }],
      }),
    }));
    if (!res.ok) throw new Error(`provider HTTP ${res.status}`);
    const data = await res.json();
    const block = Array.isArray(data.content) ? data.content.find((b) => b.type === 'text') : null;
    return block ? block.text : null;
  },

  async openai(ctx) {
    if (!CONFIG.openai.key || !CONFIG.openai.model) throw new Error('OPENAI_API_KEY / OPENAI_MODEL not set');
    const res = await withTimeout((signal) => fetch(`${CONFIG.openai.baseUrl}/chat/completions`, {
      method: 'POST',
      signal,
      headers: { 'content-type': 'application/json', authorization: `Bearer ${CONFIG.openai.key}` },
      body: JSON.stringify({
        model: CONFIG.openai.model,
        max_tokens: 80,
        messages: [
          { role: 'system', content: systemPrompt(ctx.persona) },
          { role: 'user', content: userPrompt(ctx) },
        ],
      }),
    }));
    if (!res.ok) throw new Error(`provider HTTP ${res.status}`);
    const data = await res.json();
    return data && data.choices && data.choices[0] && data.choices[0].message ? data.choices[0].message.content : null;
  },
};

// ---------- limits ----------
const perIp = new Map(); // ip -> timestamps (last minute)
let dayKey = new Date().toISOString().slice(0, 10);
let dayCount = 0;

function allow(ip) {
  const now = Date.now();
  const today = new Date().toISOString().slice(0, 10);
  if (today !== dayKey) { dayKey = today; dayCount = 0; }
  if (dayCount >= CONFIG.maxPerDay) return 'daily request cap reached';
  const list = (perIp.get(ip) || []).filter((t) => now - t < 60000);
  if (list.length >= CONFIG.ratePerMinute) { perIp.set(ip, list); return 'rate limited'; }
  list.push(now);
  perIp.set(ip, list);
  dayCount++;
  return null;
}

function tokenOk(req) {
  if (!CONFIG.clientToken) return true;
  const given = Buffer.from(String(req.headers['x-client-token'] || ''));
  const expected = Buffer.from(CONFIG.clientToken);
  return given.length === expected.length && crypto.timingSafeEqual(given, expected);
}

// ---------- http ----------
function send(res, status, obj) {
  const body = JSON.stringify(obj);
  res.writeHead(status, { 'content-type': 'application/json', 'content-length': Buffer.byteLength(body), 'cache-control': 'no-store' });
  res.end(body);
}

function readBody(req) {
  return new Promise((resolve, reject) => {
    let size = 0;
    let tooLarge = false;
    const chunks = [];
    req.on('data', (c) => {
      size += c.length;
      if (size > CONFIG.maxBodyBytes) { tooLarge = true; chunks.length = 0; return; } // keep draining, store nothing
      chunks.push(c);
    });
    req.on('end', () => {
      if (tooLarge) reject(Object.assign(new Error('body too large'), { status: 413 }));
      else resolve(Buffer.concat(chunks).toString('utf8'));
    });
    req.on('error', reject);
  });
}

const server = http.createServer(async (req, res) => {
  const started = Date.now();
  const ip = req.socket.remoteAddress || 'unknown';
  try {
    if (req.method === 'GET' && req.url === '/health') return send(res, 200, { ok: true, provider: CONFIG.provider });
    if (req.method !== 'POST' || req.url !== '/dialogue') return send(res, 404, { error: 'not found' });
    if (!tokenOk(req)) return send(res, 401, { error: 'unauthorized' });
    const limited = allow(ip);
    if (limited) return send(res, 429, { error: limited });

    let body;
    try { body = JSON.parse(await readBody(req)); } catch (e) { return send(res, e.status || 400, { error: e.status ? e.message : 'invalid JSON' }); }
    const { value: ctx, error } = validateRequest(body);
    if (error) return send(res, 400, { error });

    const provider = providers[CONFIG.provider];
    if (!provider) return send(res, 500, { error: 'unknown provider' });

    let raw;
    try { raw = await provider(ctx); } catch (e) {
      console.warn(`[dialogue] ${ctx.event} provider error: ${e.name === 'AbortError' ? 'timeout' : e.message} (${Date.now() - started}ms)`);
      return send(res, e.name === 'AbortError' ? 504 : 502, { error: 'provider unavailable' });
    }
    const line = validateLine(raw);
    if (!line) {
      console.warn(`[dialogue] ${ctx.event} rejected invalid line (${Date.now() - started}ms)`);
      return send(res, 502, { error: 'invalid line' });
    }
    console.log(`[dialogue] ${ctx.event} ok (${Date.now() - started}ms, day ${dayCount}/${CONFIG.maxPerDay})`);
    return send(res, 200, { line });
  } catch (e) {
    console.error('[dialogue] unexpected error:', e.message);
    return send(res, 500, { error: 'server error' });
  }
});

if (require.main === module) {
  server.listen(CONFIG.port, CONFIG.host, () => {
    console.log(`Grab & Go dialogue backend on http://${CONFIG.host}:${CONFIG.port} (provider: ${CONFIG.provider})`);
    if (CONFIG.provider !== 'mock' && !CONFIG[CONFIG.provider]?.key) console.warn('WARNING: provider key missing - requests will fail and the game will use prewritten lines.');
  });
}

module.exports = { server, validateRequest, validateLine, systemPrompt, CONFIG };
