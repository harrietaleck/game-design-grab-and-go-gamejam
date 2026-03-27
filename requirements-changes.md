# Requirements Changes & Adaptations

## Document Purpose
This document tracks changes made to the original requirements during development, including rationale for deviations from the initial design document.

---

## Change Log

### Change 1: Lane Count (March 24, 2026)
**Original Requirement:** 3-lane system as described in design document  
**Changed To:** 4-lane system  
**Rationale:** Visual analysis of road asset revealed 4 distinct traffic lanes with painted lane markings. Adapting to 4 lanes improved visual alignment with the environment and provided better spacing for obstacle placement.  
**Impact:** Updated all lane-dependent systems (PlayerLaneMovement, AIThiefLaneDodge, spawners)  
**Status:** Implemented and validated

---

### Change 2: Lane Spacing (March 24, 2026)
**Original Value:** 2.5 units between lanes  
**Changed To:** 3.0 units between lanes  
**Rationale:** User feedback indicated player character was positioned too close to lane markings, making visual alignment unclear. Increasing spacing improved readability and reduced visual confusion.  
**Impact:** Updated laneDistance parameter in all components and scene configuration  
**Status:** Implemented and validated

---

### Change 3: Thief Starting Distance (March 24, 2026)
**Original Value:** -1.06 Z offset from player  
**Changed To:** -9.06 Z offset from player  
**Rationale:** Thief was following too closely, making it difficult to see loot drops and creating visual clutter. Increased distance improved visibility and gave player more reaction time.  
**Impact:** Modified MainScene.unity serialized position for AI thief GameObject  
**Status:** Implemented and validated

---

### Change 4: Collectible Parenting Strategy (March 25, 2026)
**Original Approach:** Manual distance-based cleanup for collectibles  
**Changed To:** Parent collectibles to road tiles for automatic cleanup  
**Rationale:** Parenting approach simplified cleanup logic, ensured collectibles despawn with tiles, and eliminated edge cases where collectibles might persist after tiles were destroyed.  
**Impact:** Added FindTileParent() raycasting in ThiefCollectibleDropper, improved performance  
**Status:** Implemented and validated

---

### Change 5: Obstacle Hit Behavior (March 26, 2026)
**Original Requirement:** Player stops moving when hitting obstacles  
**Changed To:** Player takes damage but continues running  
**Rationale:** Stopping the player breaks endless runner flow and creates frustrating gameplay. Taking damage while maintaining momentum is standard for the genre and feels more appropriate for survival-focused gameplay.  
**Impact:** Changed LaneObstacle.disableHandlerMovementOnHit default from true to false  
**Status:** Implemented and validated

---

### Change 6: Collectible Collision Detection (March 25-26, 2026)
**Original Approach:** Rely on prefab collider configuration  
**Changed To:** Programmatically override all colliders with controlled BoxCollider  
**Rationale:** Imported coin prefabs had oversized colliders (triggering from 19+ units away), causing phantom pickups. Multiple debugging iterations revealed defensive programming was necessary for third-party assets.  
**Impact:** 
- EnsureSetup() now disables all existing colliders
- Adds fresh 0.015-unit BoxCollider for precise detection
- Prevents asset configuration issues from breaking gameplay  
**Status:** Implemented after extensive debugging

---

### Change 7: Collectible Spawn Grace Period (March 25, 2026)
**Original Approach:** Immediate collection on trigger  
**Changed To:** 0.25-0.4 second grace period after spawn  
**Rationale:** Collectibles spawning near or overlapping player would vanish instantly, creating confusion. Grace period ensures visible spawn and collection moment.  
**Impact:** Added pickupDelaySeconds to CollectiblePickup, improved player clarity  
**Status:** Implemented and validated

---

### Change 8: Coin Rotation Axis (March 25, 2026)
**Original Implementation:** Y-axis rotation (horizontal spin)  
**Changed To:** Z-axis rotation (vertical orientation spin)  
**Rationale:** User feedback indicated coins were lying flat instead of standing upright. Z-axis rotation maintains vertical orientation while providing visual motion.  
**Impact:** Changed CollectibleSpin.axis default to Vector3.forward  
**Status:** Implemented and validated

---

### Change 9: Ambient Audio Playback (March 26, 2026)
**Original Design:** Random intervals for both ambient and gameplay sounds  
**Changed To:** Continuous ambient playback, random intervals only for gameplay  
**Rationale:** User feedback noted "long delay before ambient sound plays." Continuous ambient soundscape creates better atmosphere and eliminates awkward silence.  
**Impact:** Modified AudioDirector to start ambient immediately and loop continuously  
**Status:** Implemented and validated

---

### Change 10: Alert Duration (March 26, 2026)
**Original Value:** 1.5 seconds  
**Changed To:** 0.5 seconds  
**Rationale:** User requested shorter display time for pickup alerts to reduce visual clutter and improve pacing in fast gameplay.  
**Impact:** Updated HUDController.alertDuration default value  
**Status:** Implemented and validated

---

### Change 11: Loot Counter Display Format (March 26, 2026)
**Original Format:** "Loot: 57"  
**Changed To:** "57" (no prefix)  
**Rationale:** User preferred clean numeric display without label prefix for minimal HUD aesthetic.  
**Impact:** Removed lootPrefix from display logic in HUDController.RefreshLoot()  
**Status:** Implemented and validated

---

### Change 12: Timer Implementation (March 26, 2026)
**Original Approach:** Use Time.deltaTime for countdown  
**Changed To:** Use Time.unscaledDeltaTime for countdown  
**Rationale:** Timer would freeze during pause/unpause transitions (Time.timeScale = 0). Unscaled time ensures timer continues accurately regardless of timescale manipulation.  
**Impact:** Updated HUDController.Update() and related time calculations  
**Status:** Implemented and validated

---

### Change 13: Proximity Pickup Tuning (March 25-26, 2026)
**Evolution:**
1. Initial: No proximity, trigger only
2. Iteration 1: Added proximity with 0.7 radius
3. Iteration 2: Reduced to 0.35 radius (too small)
4. Iteration 3: Removed proximity entirely (still buggy)
5. Final: Restored proximity with 0.7 radius, fixed root cause (oversized colliders)

**Rationale:** Multiple iterations revealed the actual problem wasn't proximity logic but massive prefab colliders. Once colliders were fixed (0.015 size), proximity became unnecessary but was kept as optional fallback.  
**Impact:** useProximityPickup configurable, defaults to true with proper radius  
**Status:** Implemented with toggle option

---

## Deferred Requirements

### DR1: Tear Gas Timer Extension
**Original Priority:** Core mechanic (Phase 1)  
**Current Status:** Deferred to post-jam  
**Reason:** Time constraints - focused on functional core loop first. System is architected to support this (HUDController can add time, ThiefCollectibleDropper supports new drop types).  
**Estimated Effort:** 2-3 hours

---

### DR2: Game Over Screen
**Original Priority:** Core mechanic (Phase 1)  
**Current Status:** Deferred to post-jam  
**Reason:** Timer/health reaching 0 currently has no end state. Focused on gameplay feel over end screens within time limit.  
**Estimated Effort:** 3-4 hours

---

### DR3: Victory Condition
**Original Priority:** Core mechanic (Phase 1)  
**Current Status:** Undefined  
**Reason:** Design document doesn't clearly specify win condition. Needs design clarification: survive X seconds? Collect X loot? Reach destination?  
**Estimated Effort:** 1-2 hours (once defined)

---

### DR4: Progressive Difficulty
**Original Priority:** Phase 2  
**Current Status:** Deferred to post-jam  
**Reason:** Constant spawn rate provides consistent challenge for initial release. Scaling difficulty requires balancing and tuning time.  
**Estimated Effort:** 2-3 hours

---

### DR5: K9 Units & Officer Obstacles
**Original Design:** Multiple police unit types mentioned in document  
**Current Status:** Only vehicles implemented  
**Reason:** Vehicle obstacles sufficient for core loop validation. Additional types would require new prefabs and behavior variations.  
**Estimated Effort:** 4-5 hours per new obstacle type

---

## Technical Adaptations

### TA1: Collider Management Strategy
**Original Assumption:** Use prefab colliders as-is  
**Reality:** Prefab colliders were misconfigured, oversized, or incompatible with triggers  
**Adaptation:** Implemented defensive EnsureSetup() pattern that disables existing colliders and adds controlled replacements  
**Lesson:** Always validate third-party asset configurations programmatically

---

### TA2: Reward Timing
**Original Approach:** Apply reward in CollectNow() method  
**Reality:** Caused issues with tile cleanup and non-collection despawns  
**Adaptation:** Moved reward logic to OnDestroy() with _collected flag check  
**Lesson:** Unity lifecycle methods have specific timing - leverage OnDestroy for "truly removed" state

---

### TA3: Collision Ignore Strategy
**Original Approach:** Use layers or tags for collision filtering  
**Reality:** Physics.IgnoreCollision more reliable for dynamic spawned objects  
**Adaptation:** Explicit collision ignoring between thief and each spawned drop  
**Lesson:** Dynamic collision management more flexible than static layer matrix

---

### TA4: Scene Build Configuration
**Original Assumption:** Unity includes all scenes in build automatically  
**Reality:** Build Settings require manual scene addition  
**Discovery:** User built project, got blank screen (SampleScene was listed, not MainScene)  
**Resolution:** Documented proper Build Settings configuration in README  
**Lesson:** Unity build process not intuitive for first-time developers

---

## Requirements Met vs Deferred

### ✅ Fully Implemented (10/15 Core Requirements)
- FR1: Player Control System
- FR2: AI Thief Behavior
- FR3: Collectible System
- FR4: Obstacle System
- FR5: Timer System
- FR6: Health System
- FR7: Audio System
- FR8: UI/HUD System
- FR9: Game Flow Control
- FR10: Scene Management

### ⏳ Partially Implemented (0/15)
None - all implemented requirements are fully functional

### ❌ Deferred (5 Design Goals)
- Tear Gas Resource System
- Game Over Screen & Logic
- Victory/Defeat Conditions
- Progressive Difficulty Curve
- Multiple Police Unit Types

**Completion Rate:** 67% (10/15 original design goals)  
**Core Loop Status:** 100% functional  
**Polish Status:** 40% complete

---

## Impact Assessment

### High Impact Changes (User-Facing)
- 4-lane system: Improved visual alignment and gameplay spacing
- Collision fixes: Transformed unusable pickup system into responsive mechanic
- Continuous ambience: Better atmosphere and audio presence
- Timer countdown: Creates intended survival pressure

### Medium Impact Changes (Feel & Balance)
- Increased lane spacing: Better visual clarity
- Thief distance increase: Improved visibility and planning time
- Shorter alert duration: Reduced UI clutter
- No-freeze on hit: Maintained momentum for endless runner feel

### Low Impact Changes (Technical)
- Unscaled time for timer: Fixed edge case with pause
- Convex mesh colliders: Eliminated console warnings
- Parent-to-tile cleanup: Performance optimization

---

## Lessons for Future Requirements

1. **Validate Assets Early**: Test third-party prefabs in isolation before integration
2. **Prototype First**: Build minimal version before full system implementation
3. **Flexible Over Rigid**: Configurable toggles better than hardcoded behavior
4. **User Feedback Critical**: Designer/developer assumptions often incorrect
5. **Document Assumptions**: Explicit requirements prevent implementation misalignment
6. **Iterative Refinement**: First implementation rarely optimal - budget time for iteration
7. **AI Assistance Validation**: Generated code requires testing and often needs correction

---

## Sign-Off

**Requirements Author:** Harriet Maleck  
**Last Updated:** March 26, 2026  
**Status:** Core requirements met, polish deferred to post-jam development  
**Approval:** Ready for submission with documented limitations
