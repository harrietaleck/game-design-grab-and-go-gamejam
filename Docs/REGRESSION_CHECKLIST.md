# Regression checklist (Task 8.1)

Status key: **PASS** = checked in the Unity editor (Play Mode) on 27 Oct 2026 · **CODE** = verified by reading the code only ·
**TODO** = not yet tested. Run the TODO items before a release build.

| # | Test | Steps | Expected | Status |
|---|---|---|---|---|
| 1 | Compile | Open project in 6000.0.44f1 | Console: 0 errors, 0 warnings | PASS |
| 2 | Build scenes | File > Build Profiles | MainScene scene 0 ticked, SampleScene unticked | TODO (file + editor script set it) |
| 3 | Main menu | Play | Title, PLAY, how-to-play panel, bests; no overlap | PASS |
| 4 | Start | Click PLAY / Space / Enter | Menu hides, HUD shows, 02:00 counts down | PASS (click) |
| 5 | Lanes | A/D and arrows to both edges | Claire stops at lanes 1 and 4, smooth moves | TODO |
| 6 | Collision | Run into a car | -10 health, WATCH OUT!, red flash, shake, pressure up | PASS (health/alert) |
| 7 | Loot | Run over coin / gold stack | +1 / +5, popup, sound, counter punch; only when touching | TODO |
| 8 | Fair start | Start a run | No traffic before ~6 s | TODO |
| 9 | Fair rows | Play 2 min | Never all lanes blocked; always a reachable gap | TODO |
| 10 | AJ dodging | Watch AJ | Dodges early, no lane flip-flop, never cuts through cars | TODO |
| 11 | Pause | Esc during run | PAUSED overlay, timer stops; Esc resumes | PASS |
| 12 | Police pressure | Crash several times | Bar fills green->red, alarm at 70% | TODO |
| 13 | Distraction | Space during run | -35 pressure, POLICE DISTRACTED, cyan flash, charge used, 5 s cooldown | PASS |
| 14 | Distraction limits | Space x4, Space in menu/results | 4th says NO CHARGES; no effect outside a run | TODO |
| 15 | AJ caught (pressure) | Let pressure hit 100 | AJ stops, AJ! tag, rescue banner + arrows | TODO |
| 16 | Rescue success | Get into AJ's lane | AJ RESCUED!, AJ sprints ahead, pressure 45 | TODO |
| 17 | Rescue fail | Stay in wrong lane | Results: GETAWAY FAILED, busted reason | TODO |
| 18 | Zero health | Crash 10 times | Run ends once: Claire down | TODO (CODE) |
| 19 | Time up | Survive 2:00 | GETAWAY SUCCESSFUL, best stats saved | TODO (CODE) |
| 20 | Time up during rescue | Be in rescue at 0:00 | Results: time ran out while AJ was caught | TODO (CODE) |
| 21 | Results actions | R / Enter, Esc / M, buttons | Retry starts immediately; Menu shows menu | TODO |
| 22 | Repeated runs | 5 runs in a row | No duplicate radio lines/events, stats reset, no errors | TODO |
| 23 | Radio dialogue | Play | AJ lines on start, gold, crashes, rescue; one at a time; replies 1/2/3 | PASS (start, distraction, replies shown) |
| 24 | Dialogue offline | dialogue_config disabled | Prewritten lines only, no errors | PASS |
| 25 | Dialogue backend | `npm test` in Backend | All backend tests passed | PASS (mock) |
| 26 | Dialogue online | Backend running, config enabled | Some lines generated; fallback when slow | TODO |
| 27 | Network fail during game over | Stop backend mid-run | No errors, fallback lines, results normal | TODO |
| 28 | Resolutions | Game view 1280x720, 1920x1080, 2560x1440 | HUD readable, nothing overlapping | TODO |
| 29 | Windows build | Build + run .exe outside editor | Opens on menu, full run, retry works | TODO |
| 30 | Console clean | After all of the above | No errors or warnings | TODO |
