# architecture.md — Unity Architecture & Code Contracts

> **Role of this file**
> This document defines *how the game is built* at a technical level.
> Claude Code should use this to stay consistent with the project's architecture.

---

## 1. Architectural Goals

- High gameplay iteration speed
- Predictable data flow (STATE structs as single source of truth)
- Debuggable systems (deterministic simulation via PurrDiction)
- Minimal hidden magic
- **Networking-first: all gameplay runs through PurrDiction prediction**

---

## 2. Unity Programming Model

### Primary Paradigm
- [x] Hybrid

**Explanation:**
PurrDiction's `PredictedIdentity<INPUT, STATE>` for all predicted gameplay (movement, combat, health, stamina) + MonoBehaviours for non-predicted systems (UI, menus, scene management) + ScriptableObjects for configuration data. This combines the deterministic prediction model required by PurrDiction with Unity's standard component patterns for everything else.

---

## 3. Data vs Behavior

### ScriptableObjects Are Used For:
- Weapon stats (damage values, draw speed, arrow speed)
- Stamina config (drain rates, regen speed, max stamina)
- Arrow config (max carry, retrieval settings)
- Spawn settings (positions, timers)
- Game mode rules (respawn timers, win conditions)

### MonoBehaviours Are Used For:
- UI controllers (HUD, menus, death screen)
- Camera controller (3rd person + aim zoom)
- Menu systems
- Non-predicted scene logic

### PredictedIdentity<INPUT, STATE> Are Used For:
- Player movement (walk, sprint, crouch)
- Bow/arrow simulation (draw, fire, flight)
- Health and stamina tracking
- Melee kick
- Pickup interaction (herbs, arrows)

---

## 4. Gameplay System Breakdown

### Networking
- **Transport:** PurrNet for transport, RPCs, spawning
- **Prediction:** PurrDiction for client-side prediction + server reconciliation
- **Topology:** Dedicated server OR player-hosted (listen server)
- **Arrow hits:** Client predicts locally -> server validates within tolerance -> reconciles
- **Spawning:** PredictedHierarchy for arrow spawning/destruction

### Input System
- Unity New Input System (requires package install)
- Input gathered in PredictedIdentity's `GetFinalInput()` / `UpdateInput()`
- **Actions:** Move, Sprint, Crouch, Draw/Fire Bow, Melee Kick, Interact (pickup)

### Player Controller
- `PredictedIdentity<PlayerInput, PlayerState>`
- **PlayerState:** position, rotation, velocity, stamina, health, arrowCount, drawStrength, isCrouching, isSprinting
- **PlayerInput:** moveDir, lookDir, sprint, crouch, drawBow, releaseBow, melee, interact
- **Simulate():** deterministic movement + combat logic
- **Camera:** Hybrid 3rd person — default classic, zoom to over-shoulder when aiming

### Combat / Arrows
- Arrow spawned via `PredictedHierarchy.Create()` on bow release
- Arrow is `PredictedIdentity<ArrowState>` (no input — state-only, physics sim)
- **ArrowState:** position, velocity, rotation, isStuck, damage
- **Simulate():** apply gravity, move along velocity, raycast for collision
- **Hit detection:** SphereCast or raycast each tick, check collider zones (head/torso/limb)
- **Damage:** base zone damage * draw strength multiplier

### Health & Damage
- Health tracked in PlayerState (predicted)
- Damage applied in Simulate() when arrow collision confirmed
- Death triggers via server RPC (respawn timer, spectator switch)

### Stamina
- Tracked in PlayerState, drained by: sprinting, holding bow at full draw, melee kick
- Passive regen when not draining
- Config values in ScriptableObject

### AI / Enemies
- N/A — multiplayer PvP only, no AI enemies in prototype

---

## 5. Event & Communication Patterns

- **PurrNet RPCs** for non-predicted events (death notification, game mode transitions, chat)
- **C# events** for local-only UI updates (health changed, ammo changed, stamina changed)
- **STATE structs** own all predicted game state — no external state mutation during Simulate()

---

## 6. Folder & Namespace Rules

```
Assets/
  _Project/
    Scripts/
      Core/           (NetworkManager setup, game mode logic, shared utilities)
      Player/          (PredictedPlayer, camera, input)
      Combat/          (Arrow, damage, hitbox zones)
      Items/           (Healing herbs, arrow pickups)
      UI/              (HUD, menus, death screen)
    Prefabs/
      Player/
      Projectiles/
      Items/
      UI/
    ScriptableObjects/
      Config/          (WeaponStats, StaminaConfig, GameModeRules)
    Scenes/
    Materials/
    Settings/
```

**Namespace Convention:**
`VerdantHunt.Core`, `VerdantHunt.Player`, `VerdantHunt.Combat`, `VerdantHunt.Items`, `VerdantHunt.UI`

---

## 7. Coding Standards (Enforced)

### General
- One class per file
- No logic in constructors
- No Find() calls at runtime
- All simulation logic must be deterministic
- STATE structs are the single source of truth during Simulate()
- INPUT and STATE must be structs implementing `IPredictedData`

### Performance
- No LINQ in Update / FixedUpdate / Simulate
- Cache component references
- Object pooling for arrows (via PredictedHierarchy pooling)

### Conventions
- `SerializeField` only for constants (speed, prefab refs) — never for simulation variables
- Store `PredictedObjectID` not `GameObject` references (survive rollbacks)

---

## 8. Save / Load Strategy

- No save/load for prototype (multiplayer session-based)
- Game mode settings stored in ScriptableObjects
- Session state lives entirely in PurrDiction STATE structs during gameplay

---

## 9. Testing & Debugging

- **Play Mode testing:** Test with 2 Unity editor instances (ParrelSync or build + editor)
- **Network diagnostics:** PurrNet's built-in network diagnostics
- **Debug visualizations:** Arrow trajectories (Gizmos), hitbox zones, stamina/health overlays
- **Logging rules:** Use Unity `Debug.Log` with `[Server]`/`[Client]` prefixes

---

## 10. PurrDiction Patterns

Key patterns from PurrDiction docs:

- `PredictedIdentity<INPUT, STATE>` for player-controlled entities
- `PredictedIdentity<STATE>` for autonomous predicted objects (arrows, pickups)
- STATE must implement `IPredictedData<STATE>` (extends `IMath` + `IPackedAuto`)
- **Override methods:** `GetInitialState`, `GetUnityState`, `SetUnityState`, `Simulate`, `UpdateView`
- **Input methods:** `GetFinalInput`, `SanitizeInput`, `UpdateInput`
- **Spawning:** `PredictedHierarchy.Create()` / `Delete()` with `PredictedObjectID`
- Store `PredictedObjectID` not `GameObject` references (survive rollbacks)
- **Scene requirements:** NetworkManager + PredictionManager + PredictedPrefabs asset

---

## 11. Change Log

- 2026-03-02: Populated all sections with initial architecture decisions from design interview
