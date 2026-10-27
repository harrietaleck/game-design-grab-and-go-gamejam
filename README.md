# Grab & Go: Loot on the Run

A 3D endless lane runner made for the game-jam theme **"You are not the main character."**

**AJ** is the thief. He just pulled a heist and is sprinting through the city, dodging traffic on his own (gameplay AI).
**You are Claire**, his getaway partner. You run behind him, grab the loot he drops, keep the police off his back and, when
he gets caught, you are the one who has to save him. The supporting character decides whether the getaway works.

Unity `6000.0.44f1` - URP 17 - Windows PC - C#

## How to play

| Input | Action | When |
|---|---|---|
| **A / D** or **Left / Right** | Change lane (4 lanes) | During a run |
| **Space** (or Shift) | Distraction Pulse - 3 per run | During a run |
| **1 / 2 / 3** | Answer AJ on the radio (optional) | When options appear |
| **Esc / P** | Pause / resume | During a run |
| **Space / Enter**, or click **PLAY** | Start | Main menu |
| **R / Enter** - **Esc / M** | Retry - Main menu | Results screen |

**Goal:** survive the **2:00** countdown with Claire alive and AJ free.

- **Loot:** AJ drops gold coins (+1) and gold coin stacks (+5) behind him. Run over them to collect.
- **Traffic:** every crash costs Claire 10 of her 100 health and raises **police pressure**.
- **Police pressure (0-100):** rises when Claire crashes (+18), AJ has a near miss (+6) or a gold stack is grabbed (+3);
  cools down after 3 calm seconds. At 100 the police grab AJ.
- **Distraction Pulse:** -35 pressure and no new pressure for 3 s (5 s cooldown). It never steers AJ.
- **Rescue:** if AJ is caught (no escape lane, or pressure maxed) he stops in his lane. Get into **his lane** before you
  reach him. Same lane = he is free and sprints back ahead; wrong lane = the run ends.

**Endings:** escaped (timer reaches 0, Claire alive, AJ free) - Claire down (0 health) - busted (missed rescue) -
time up while AJ was caught. The results screen shows the outcome, reason, loot, coins, gold stacks, time survived,
health, hits, rescues and personal bests.

## Features
- Single authoritative run controller (`GameStartFlow`): MainMenu / Playing / Paused / AJCaught / Results, one-time endings, retry and menu.
- Fair, ramping obstacle rows: clean start, never all lanes blocked, every row reachable from the previous one, density grows over the run.
- AJ's gameplay AI: look-ahead and urgent dodges, safe lane crossing, no lane flip-flopping, emergency dodge, caught state, post-rescue recovery.
- AJ radio dialogue: prewritten lines that react to real events (offline), optional LLM lines through a secure backend, Claire reply options.
- HUD: timer, AJ status, loot, Claire's health, police-pressure meter, distraction charges, rescue banner, alerts, flashes, loot popups, AJ name tag.
- Main-menu how-to-play, first-run tips, personal bests (PlayerPrefs), resolution-independent UI.

## Project layout
```
Assets/
  MainScene.unity              gameplay scene (build scene 0)
  Scripts/                     core gameplay (GameStartFlow, HUDController, PlayerLaneMovement, AIThiefLaneDodge,
                               ThiefCollectibleDropper, CollectiblePickup, LaneObstacle, LaneMath, AudioDirector, ...)
  Scripts/Systems/             PolicePressure, DistractionPulse, RescueController, ProgressionStore, ProceduralSfx
  Scripts/Dialogue/            AJDialogueController, DialogueFallbackLibrary, RadioDialogueUI, ClaireReplyController,
                               DialogueAPIClient, AJPersonalityProfile
  Scripts/UI/                  runtime-built UI (results, pause, HUD extras, menu guide, onboarding, popups)
  Editor/BuildSceneSetup.cs    keeps MainScene as build scene 0 (menu: Grab & Go > Fix Build Scenes)
  StreamingAssets/dialogue_config.json   LLM dialogue switch (disabled by default)
  ithappy/.../RoadSpawner.cs   road tiles + fair obstacle rows (modified asset-pack script)
Backend/                       optional Node.js dialogue server (keeps LLM API keys out of the game)
Docs/                          regression checklist and development log
```
New systems attach themselves to the `GamePlayCanvas` at runtime (`GameStartFlow.RegisterBootstrap`), so MainScene
needed no manual wiring. Existing Inspector references and field names were kept.

## Open, play, build
1. Unity Hub -> **Add -> Add project from disk** -> select the `game-design-grab` folder (do not use *New project*).
   Open it with Unity **6000.0.44f1**.
2. Open `Assets/MainScene.unity` and press Play.
3. Build: **File -> Build Profiles -> Windows**, confirm `MainScene` is scene 0 (ticked), build to a folder **outside** the
   project. The first build needs a few GB of free disk space.

## Optional: LLM dialogue
The game is fully playable offline with prewritten lines. To try generated lines, see [`Backend/README.md`](Backend/README.md):
run `npm start` in `Backend/` (use `PROVIDER=mock` to test without a key), then set `"enabled": true` in
`Assets/StreamingAssets/dialogue_config.json`. API keys live only in `Backend/.env` (git-ignored) - never in Unity or a build.
If the service is off, slow or fails, AJ falls back to prewritten lines instantly.

## Known limitations
- AJ's voice (text-to-speech) is not implemented yet; dialogue is subtitles + a radio blip. Needs a TTS provider decision.
- There are no police vehicles or pursuit; police pressure is a HUD meter with audio cues.
- The LLM backend has been tested in mock mode only (no provider key was used).
- See `Docs/REGRESSION_CHECKLIST.md` for what has and has not been play-tested.

## Credits
**Design, direction and development:** Harriet Maleck (ST10252836) - game jam March 24-26, 2026, extended October 2026.

Third-party assets (original licenses apply):
- Cartoon City Free (ithappy) - city, road tiles, traffic obstacles
- Devtoid Gold Coins (in "LiquidFire Package 4 - BSH games") - gold coin and gold coin stack pickups
- Space Exploration GUI Kit - HUD elements
- Mixamo - character running animations (Claire, AJ)
- Mixkit - city ambience, train, siren and coin sounds; one ElevenLabs-generated voice clip
- Also imported: Money Pack, Medieval Gold, LowPoly Fantasy Bags, jewellery shop, Explosives, LiquidFire effects

## AI Ethics Statement

### AI Usage Disclosure

This project was developed with significant AI assistance (Cursor AI with Claude Sonnet 4.5) as part of an exploration into AI-assisted game development. Transparency about this collaboration is essential for academic integrity and honest evaluation.

**AI Contribution Scope:**
- **Code Generation**: ~60-70% of initial code structures were AI-generated
- **Debugging**: AI provided systematic debugging strategies and logging approaches
- **Documentation**: README, implementation summary, and requirements documents were AI-assisted
- **Design Consultation**: AI suggested technical patterns and architectural approaches

**Human Contribution Scope:**
- **Creative Direction**: All game concept, narrative, theme interpretation, and design decisions
- **Code Validation**: All AI-generated code was tested, modified, and debugged through iteration
- **Decision Making**: Final approval on all implementations, features, and scope decisions
- **Problem Definition**: Identification of bugs, articulation of requirements, gameplay tuning

### Attribution Practices

**Transparency Measures:**
1. All git commits include "Co-authored-by: Cursor" acknowledging AI contribution
2. This README explicitly discloses AI assistance in credits section
3. REFLECTION.txt provides comprehensive analysis of AI collaboration
4. No AI-generated visual or audio assets used (only code assistance)

**What AI Did NOT Create:**
- Game concept and theme interpretation
- Narrative and character roles
- Visual style decisions
- Gameplay feel and balance tuning
- Asset selection and scene composition
- Testing and quality validation

### Ethical Considerations

**Knowledge Dependency:**
During development, I became aware of depending on AI for syntax and patterns I should learn independently. This creates a knowledge gap between implementation speed and deep understanding. Future projects will prioritize learning fundamentals before AI acceleration.

**Authenticity Concerns:**
While I directed all technical decisions and validated correctness, I did not personally type most code. This collaboration model is analogous to using advanced IDE tools or frameworks but raises questions about individual technical competency assessment in academic contexts.

**Documentation Voice:**
The professional quality of documentation reflects AI enhancement beyond my typical writing level. This disclosure ensures readers can fairly evaluate my natural capabilities versus AI-augmented output.

### Commitment to Responsible AI Use

**Future Practices:**
1. **Understanding Over Speed**: Mandatory independent research before requesting AI assistance
2. **Incremental Requests**: Build simple versions first, add complexity only if needed
3. **Testing Validation**: Systematic verification of AI solutions rather than blind acceptance
4. **Honest Attribution**: Granular disclosure of AI contribution per component
5. **Skill Development**: Use AI as learning accelerator, not learning replacement

**Educational Integrity:**
This project represents a genuine exploration of AI-assisted development workflows while maintaining honest disclosure. All creative decisions, design choices, and final implementations were human-directed and validated. AI served as a powerful tool but not a replacement for developer judgment.

### References
- Cursor AI: https://cursor.com/
- Claude Sonnet 4.5 by Anthropic
- AI Assistance acknowledged per academic honesty requirements

### October 2026 update
The Stage 1-8 roadmap work on the `stage1-stability` branch (run-state system, rescue mechanic,
police pressure, Distraction Pulse, dialogue system and backend, UI and docs) was implemented with
Claude (Anthropic) under Harriet's direction and design brief. Each task is a separate, attributed commit.


## License
Educational project developed for game jam submission. Assets retain their original licenses from their sources.
