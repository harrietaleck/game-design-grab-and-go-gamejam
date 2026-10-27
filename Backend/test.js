// Minimal self-test: starts the server in mock mode on a random port and checks the main paths.
'use strict';
process.env.PROVIDER = 'mock';
process.env.PORT = '0';
const assert = require('assert');
const { server, validateLine } = require('./server');

async function post(port, body, headers = {}) {
  const res = await fetch(`http://127.0.0.1:${port}/dialogue`, {
    method: 'POST', headers: { 'content-type': 'application/json', ...headers }, body: typeof body === 'string' ? body : JSON.stringify(body),
  });
  return { status: res.status, json: await res.json() };
}

(async () => {
  await new Promise((r) => server.listen(0, '127.0.0.1', r));
  const port = server.address().port;
  try {
    const ok = await post(port, { event: 'GOLD_BAR_COLLECTED', gameState: 'Playing', timeRemaining: 72, claireHealth: 80, lootValue: 14, policePressure: 'High', recentDialogue: [{ speaker: 'AJ', text: 'Keep up!' }], claireReply: null });
    assert.strictEqual(ok.status, 200); assert.ok(ok.json.line.length <= 120);
    assert.strictEqual((await post(port, { event: 'DROP_TABLE' })).status, 400);
    assert.strictEqual((await post(port, '{not json')).status, 400);
    assert.strictEqual((await post(port, 'x'.repeat(5000))).status, 413);
    assert.strictEqual(validateLine('AJ: "Run, Claire!"'), 'Run, Claire!');
    assert.strictEqual(validateLine('<b>hi</b>'), null);
    assert.strictEqual(validateLine('x'.repeat(121)), null);
    assert.strictEqual(validateLine('see https://example.com'), null);
    const health = await (await fetch(`http://127.0.0.1:${port}/health`)).json();
    assert.strictEqual(health.ok, true);
    console.log('All backend tests passed.');
  } finally {
    server.close();
  }
})().catch((e) => { console.error(e); server.close(); process.exit(1); });
