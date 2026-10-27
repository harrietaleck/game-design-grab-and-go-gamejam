# Development log - October 2026 roadmap

Branch: `stage1-stability` (from `main`). One commit per task; see `git log main..stage1-stability`.

## What was done
| Stage | Task(s) | Summary |
|---|---|---|
| 0 | Safety | Unity .gitignore; generated files (Library, Logs, UserSettings, .vs, obj, csproj/sln) untracked; stray `My project*` folders ignored |
| 1 | 1.2 | MainScene is build scene 0 (SampleScene disabled); editor script re-applies it |
| 1 | 1.4 + 1.5 | `GameStartFlow` is the single run controller: states, countdown, health, loot, one-time endings, retry/menu, pause |
| 1 | 1.6 | AJ caught: stops movement and loot drops, notifies run controller |
| 1 | 1.3 | One lane config (4 lanes, 3.0 apart) shared by Claire, AJ and spawners |
| 1 | fix | Characters use kinematic Rigidbodies (moved by script) |
| 1 | 1.7 | Space/Enter start (menu only), README controls/version |
| 1 | 1.8 | HUD display-only, health "x/100", CLAIRE label, pause overlay, results screen |
| 2 | 2.1 + 2.2 | Safe start, fair reachable rows, difficulty ramp |
| 2 | 2.3 | AJ: safe crossings, lane hold, near-miss events, recovery after rescue |
| 2 | 2.4 | Police pressure meter (events only), capture at 100 |
| 2 | 2.5 | Distraction Pulse (3 charges, cooldown) |
| 2 | 2.6 | Loot popups, separate coin/stack counts, cheaper pickup checks |
| 3 | 3.1 + 3.2 | Rescue window (get in AJ's lane), clear consequences |
| 3/4 | 3.3, 4.1-4.5 | AJ radio dialogue, local-first, priorities/cooldowns, partnership reactions |
| 4 | 4.6-4.9 | Node backend (keys server-side, limits, validation, mock mode) + Unity client |
| 4 | 4.10 + 4.11 | Claire reply options, per-run memory |
| 6 | 6.1-6.3 | Success rule, results stats, personal bests |
| 7 | 7.1-7.4 | Menu guide, hit shake, AJ tag, first-run tips |
| 7/8 | fix | Canvas scales with resolution; UI re-tuned |
| 8 | 8.1 | This log, regression checklist, README rewrite |

## Decisions made (when no one was available to ask)
- **Rescue design:** reach AJ in his lane (simplest fair option with the existing lane system). Window >= 1.8 s; obstacles in his lane are cleared.
- **Success rule:** escape only when 2:00 runs out with Claire alive and AJ free. Time running out during a rescue = failure.
- **Pressure capture:** maxed police pressure catches AJ, so Claire's crashes matter to AJ.
- **Space** is both "start" (menu only) and "Distraction Pulse" (run only); a frame guard prevents double use.
- **New UI is built at runtime** to avoid hand-editing MainScene YAML while it was open in the editor.
- **Value-5 loot** is a gold *coin stack* model, so the UI says "GOLD", not "gold bar" (the dialogue event keeps the design name GOLD_BAR_COLLECTED).

## Not done / needs Harriet
- **Stage 5 (AJ voice / TTS):** needs a provider choice (cost, latency, licensing). Ambience ducking for dialogue is already in place.
- **Real LLM test:** needs a provider API key in `Backend/.env`. Mock mode passes.
- **Windows build (8.3):** not produced - only ~3.9 GB free on C:. Free space, then build from Build Profiles.
- **Play-test the TODO rows** in `REGRESSION_CHECKLIST.md`, especially endings, rescue and lane edges.
- **Merge:** review the branch, then merge `stage1-stability` into `main` and push (nothing has been pushed).
