# Grab & Go dialogue backend

A tiny Node.js (18+) server that turns verified game events into one short AJ radio line.
It exists so the LLM provider's API key stays **on the server** and never ships in the Windows build.

## Run locally
```
cd Backend
copy .env.example .env      (macOS/Linux: cp .env.example .env)
npm start                   (no npm install needed: zero dependencies)
npm test                    (self-test in mock mode)
```
`PROVIDER=mock` returns canned lines so you can test the whole pipeline without a key.
Set `PROVIDER=anthropic` + `ANTHROPIC_API_KEY` (or `openai` + an OpenAI-compatible endpoint) for real lines.
Check the provider's current documentation for model names before choosing one.

## Connect the game
Edit `Assets/StreamingAssets/dialogue_config.json`:
```json
{ "enabled": true, "endpoint": "http://127.0.0.1:8787/dialogue", "clientToken": "", "timeoutSeconds": 4 }
```
If the backend is off, slow, rate-limited or returns anything invalid, the game silently uses prewritten lines.

## API
`POST /dialogue` with the compact context (event, gameState, timeRemaining, claireHealth, lootValue, coins,
goldStacks, policePressure, recentDialogue[<=4], claireReply, persona) -> `200 {"line": "..."}`.
Errors: 400 invalid request, 401 bad token, 413 body > 4 KB, 429 rate/daily cap, 502/504 provider problem.
`GET /health` -> `{ ok, provider }`.

## Safety and cost
- Request size limit, field validation and clamping; unknown events rejected.
- Per-IP rate limit (`RATE_LIMIT_PER_MIN`) and a daily cap (`MAX_REQUESTS_PER_DAY`) bound cost.
- Provider timeout (`PROVIDER_TIMEOUT_MS`); replies are validated (single line, <=120 chars, no markup/links/code).
- Logs only event name, status and latency - no request bodies or personal data.
- The model only writes dialogue. Unity owns every gameplay value and never executes model output.
- Binds to 127.0.0.1 by default. If you host it publicly, put it behind HTTPS and keep the limits on.
