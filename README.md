# Grab & Go: Loot on the Run

A fast-paced 3D endless runner where you support an AI-controlled thief escaping through a city. Developed for a game jam exploring the theme "You are not the main character."

## Game Concept

You don't control the thief - you ARE the getaway partner running behind them, collecting dropped loot and ensuring their survival. The thief runs automatically using AI, dropping valuable items as they flee from police. Your job is to grab the loot, dodge obstacles, and keep the escape alive.

## Controls

- **A / Left Arrow**: Move left
- **D / Right Arrow**: Move right
- **Space**: Start game (from menu)

## Gameplay Features

### Core Systems
- **4-Lane Movement**: Navigate across four traffic lanes
- **AI Thief Behavior**: Autonomous thief with obstacle dodging
- **Dynamic Collectibles**: Weighted random drops (coins worth 1, gold bars worth 5)
- **Health System**: Take damage from obstacles, monitored via health bar
- **Timer Countdown**: Escape timer with color-coded pressure (cyan → orange → red)
- **Audio Director**: Ambient city sounds and gameplay audio cues

### HUD Elements
- **Escape Timer** (Top Center): Countdown showing police arrival time
- **Thief Status** (Top Left): SAFE or CAUGHT indicator
- **Loot Counter** (Top Right): Total collected value
- **Health Bar** (Bottom Left): Player survivability with numeric display
- **Alert Feed** (Bottom Center): Real-time pickup and damage notifications
- **Controls Hint** (Bottom Right): Input reminder (auto-hides)

## Project Structure

```
game-design-grab/
├── Assets/
│   ├── Scripts/
│   │   ├── LaneMath.cs                    # Lane calculation utility
│   │   ├── PlayerLaneMovement.cs          # Player controller
│   │   ├── AIThiefLaneDodge.cs           # AI thief behavior
│   │   ├── CollectiblePickup.cs           # Pickup detection & rewards
│   │   ├── CollectibleSpin.cs             # Coin rotation animation
│   │   ├── ThiefCollectibleDropper.cs     # Random loot spawning
│   │   ├── AudioDirector.cs               # Sound management
│   │   ├── GameStartFlow.cs               # Menu/gameplay transition
│   │   ├── HUDController.cs               # UI management
│   │   ├── LaneObstacle.cs                # Obstacle collision behavior
│   │   ├── LaneObstacleSpawner.cs         # Obstacle spawning system
│   │   └── RoadSpawner.cs                 # Road tile generation
│   ├── MainScene.unity                    # Main game scene
│   ├── Sounds/                            # Audio assets
│   ├── Space_Exploration_GUI_Kit/         # UI assets
│   └── TextMesh Pro/                      # Text rendering
```

## Technical Highlights

### Lane System
Centralized mathematical model for consistent lane positioning across player, AI, and spawning systems:
```csharp
float x = laneCenterX + (laneIndex - (laneCount - 1) * 0.5f) * laneDistance;
```

### Intelligent Collision Management
- Convex mesh colliders for trigger support on vehicles
- Tiny normalized colliders (0.015 local units) for precise pickup detection
- Physics.IgnoreCollision prevents thief from collecting own drops
- Spawn grace period (0.25s) prevents instant overlap collection

### Parent-to-Tile Cleanup
Collectibles spawn parented to road tiles, automatically despawning when tiles move out of view - no manual distance checks needed.

### Unscaled Time for UI
Timer uses `Time.unscaledDeltaTime` to maintain accurate countdown during pause/unpause transitions.

## Development Stack

- **Engine**: Unity 2022.3+ (Built-in Render Pipeline)
- **Language**: C#
- **UI Framework**: Unity UI with TextMesh Pro
- **Audio**: Unity Audio System with 2D spatial audio
- **Physics**: Unity Physics with trigger-based collision detection

## Setup Instructions

### Prerequisites
- Unity 2022.3 or newer
- TextMesh Pro (imported via Package Manager)

### Running the Game

1. Open project in Unity:
   ```
   Unity Hub → Add → Select "game-design-grab" folder
   ```

2. Open main scene:
   ```
   Assets/MainScene.unity
   ```

3. Configure Inspector (if not already set):
   - **GameStartFlow**: Assign menu panel, HUD panel, play button, HUD controller
   - **HUDController**: Assign all text elements, health slider, thief reference
   - **ThiefCollectibleDropper**: Configure drop table with coin/gold bar prefabs
   - **AudioDirector**: Add ambient and gameplay audio clips

4. Press Play in Unity Editor

### Building the Game

1. Open Build Settings: `File → Build Settings` (Ctrl+Shift+B)
2. Ensure `MainScene` is in "Scenes In Build" list (index 0, checked)
3. Remove or uncheck any demo scenes (SampleScene, etc.)
4. Select target platform (Windows/Mac/Linux)
5. Click `Build` or `Build and Run`

## Design Philosophy

### Theme: "You Are Not The Main Character"
The player operates as support, not the protagonist. The AI thief is the narrative focal point - you ensure their survival indirectly through collecting loot and managing resources.

### Dual-Character System
- **Thief** (AI): Autonomous forward movement, obstacle dodging, loot dropping
- **Handler** (Player): Lane control, collection, survival management

### Survival Loop
- Timer creates constant pressure (police approaching)
- Health depletes from obstacles
- Loot rewards risk-taking behavior
- Balance between greed (collecting loot) and caution (avoiding damage)

## Known Limitations

### Not Yet Implemented
- **Tear Gas System**: Resource that extends timer (core survival mechanic from design doc)
- **Game Over Screen**: Timer/health reaching 0 doesn't show proper end state
- **Victory Condition**: No defined win state when escaping successfully
- **Progressive Difficulty**: Obstacle spawn rate doesn't increase over time
- **Score Multipliers**: No combo or near-miss bonus systems

### Current Behavior
- Timer reaching 0:00 sets status to "TIME UP" but game continues
- Health reaching 0 shows empty bar but no game over
- Obstacles spawn at constant rate throughout

## Future Enhancements

### Priority 1: Complete Core Loop
- Tear gas collectibles that extend timer (+10-15 seconds)
- Game over screen with final stats (loot, time, status)
- Restart button functionality
- Victory screen when timer expires with thief still safe

### Priority 2: Difficulty Curve
- Gradually increase obstacle spawn rate
- Speed up forward movement over time
- More aggressive AI thief behavior under pressure

### Priority 3: Polish & Juice
- Screen shake on collisions
- Particle effects on pickups
- Character trail effects
- Dynamic music intensity based on timer
- Whoosh sound on lane changes
- Visual feedback for near-misses

### Priority 4: Extended Features
- Power-ups (shield, magnet, speed boost)
- Combo multiplier system
- Leaderboard/high score tracking
- Multiple thief characters with different behaviors
- Environmental hazards beyond vehicles

## Credits

**Developer**: Harriet Maleck (ST10252836)  
**Development Period**: March 24-26, 2026 (3-day game jam)  
**Theme**: "You are not the main character"  
**AI Assistance**: Code generation, debugging, system design consultation

### Assets Used
- [Cartoon City Free](https://assetstore.unity.com/packages/3d/environments/urban/cartoon-city-free-low-poly-city-3d-models-pack-328170) - Urban environment
- [Basic Treasure Coins](https://assetstore.unity.com/packages/2d/textures-materials/metals/basic-treasure-coins-26609) - Collectible coins
- [Money Pack](https://assetstore.unity.com/packages/3d/props/money-pack-84433) - Gold bars
- [Space Explorer GUI Kit](https://assetstore.unity.com/packages/2d/gui/icons/sleek-essential-ui-pack-170650) - UI elements
- Mixamo - Character animations

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

## License

Educational project developed for game jam submission. Assets retain their original licenses from Unity Asset Store.

---

**Version**: 1.0  
**Last Updated**: March 26, 2026  
**Build Status**: Core systems functional, ready for expansion
