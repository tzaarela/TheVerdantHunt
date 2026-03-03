# PurrDiction Reference Guide

> **Purpose:** Quick-reference for building predicted gameplay systems in The Verdant Hunt.
> Synthesized from PurrDiction docs, community guides, and the blog series by Zayed Charef.

---

## 1. Overview & Mental Model

**PurrDiction** is a client-side prediction (CSP) framework built on **PurrNet** (networking transport/RPCs). It makes multiplayer feel responsive despite network latency.

**The core loop:**

```
1. Player presses a key        → character moves IMMEDIATELY on screen (prediction)
2. Input is sent to the server → server simulates authoritatively
3. Server sends back true state → client compares, corrects if needed (reconciliation)
```

The server is the **unquestionable source of truth**. Clients only ever send **inputs** (movement direction, button presses) — never state claims like position or health. If the client's prediction was wrong, the server's answer wins, and the client smoothly corrects.

**Why this matters for a 3rd-person shooter:** With 100ms latency, a naive server-authoritative model means 0.1s delay before your character moves. CSP eliminates that perceived delay entirely — you move instantly, and corrections are invisible if your simulation is deterministic.

---

## 2. Core Components

### PredictionManager
One per scene. The "director" that orchestrates ticks, rollbacks, reconciliation, and view updates.

### PredictedIdentity Variants

| Type | Use Case | Example |
|------|----------|---------|
| `PredictedIdentity<INPUT, STATE>` | Player-controlled entities | Player character |
| `PredictedIdentity<STATE>` | Autonomous predicted objects (no player input) | Arrows, AI, platforms |
| `StatelessPredictedIdentity` | Pure logic/orchestrators with no custom state | PlayerController "brain" |
| `PredictedModule<STATE>` | Encapsulated reusable logic attached to an identity | Health, timers, stamina |
| `DeterministicIdentity<STATE>` | Strict bit-stable simulation (`sfloat`) | Not needed for this project |

---

## 3. INPUT & STATE Struct Design

Both must be **structs** implementing the appropriate interfaces:

```csharp
// INPUT — what the player wants to do this tick
public struct PlayerInput : IPredictedData
{
    public Vector2 moveDir;       // WASD
    public float lookYaw;         // mouse horizontal (accumulated)
    public bool sprint;           // held
    public bool crouch;           // held
    public bool drawBow;          // held
    public bool releaseBow;       // edge-triggered (use |= in UpdateInput)
    public bool melee;            // edge-triggered
    public bool interact;         // edge-triggered

    public void Dispose() { }
}

// STATE — the simulation truth
public struct PlayerState : IPredictedData<PlayerState>
{
    public Vector3 position;
    public float yaw;             // character facing (horizontal rotation)
    public Vector3 velocity;
    public float stamina;
    public float health;
    public int arrowCount;
    public float drawStrength;    // 0 = not drawing, 0→1 = charging
    public bool isCrouching;
    public bool isSprinting;

    public void Dispose() { }
}
```

### What Goes IN State
- Simulation-critical data: positions, velocities, timers, flags
- Anything that affects future ticks
- `PredictedRandom` if you need randomness
- `PredictedObjectID` for references to other predicted objects

### What Stays OUT of State
- **Static config** — use ScriptableObjects (speed, damage values)
- **Derived data** — anything calculable from other state fields
- **Unity references** — Transform, Rigidbody, etc. (can't serialize)
- **Debug/UI data** — not gameplay-relevant

### The Time Travel Test
Ask three questions before adding a field to STATE:
1. Will simulation differ without it? → **Include**
2. Can it be recalculated from other state? → **Exclude**
3. Does it change during simulation? → **Exclude if NO** (put in SerializeField)

### Performance: Use `ref`
```csharp
// FAST — direct modification, no copy
ref var state = ref currentState;
state.health -= damage;

// SLOW — copies entire struct
var state = currentState;
state.health -= damage;
currentState = state;
```

---

## 4. Tick Lifecycle

### Per-Tick Pipeline (runs on both server and client)

```
Pre-Tick
  └→ Prepare Inputs (GetFinalInput, SanitizeInput)
  └→ Save Pre-Sim State
  └→ Simulate(input, ref state, delta)
  └→ LateSimulate(input, ref state, delta)
  └→ Physics Pass (Unity physics step)
  └→ Save Post-Sim State
  └→ Networking (server sends frames to clients)
  └→ PostSimulate()
  └→ Advance Tick
```

### Reconciliation (client, when server frame arrives)

```
Verified Frame arrives
  └→ Compare against predicted state at that tick
  └→ Rollback to verified tick
  └→ Re-simulate forward to current tick (using saved inputs)
  └→ Update Interpolation (smooth visual correction)
  └→ Sync Transforms
```

This entire process happens in a **single frame** — the player never sees the rollback.

### View Update (every visual frame, client only)

```
Update or LateUpdate
  └→ UpdateView(viewState, verified?) per identity
```

---

## 5. Override Methods Cheat Sheet

### Input (on `PredictedIdentity<INPUT, STATE>`)

| Method | When | Purpose |
|--------|------|---------|
| `UpdateInput(ref INPUT)` | Every Unity frame | Cache edge-triggered inputs with `\|=`. Runs before tick. |
| `GetFinalInput(ref INPUT)` | Once per tick | Set continuous inputs (movement axes, held buttons). |
| `SanitizeInput(ref INPUT)` | Before simulation | Clamp/normalize values. Runs on server for security. |
| `ModifyExtrapolatedInput(ref INPUT)` | Missing remote input | Degrade gracefully (e.g., reduce movement by 60%). |

### Simulation

| Method | When | Purpose |
|--------|------|---------|
| `GetInitialState()` | First spawn | Return default STATE values. |
| `Simulate(INPUT, ref STATE, float delta)` | Every tick | Core deterministic logic. Mutate only STATE. |
| `LateSimulate(INPUT, ref STATE, float delta)` | After Simulate each tick | Optional second pass (derived values). |
| `PostSimulate()` | After tick finishes | Finalization per tick. |
| `SimulationStart()` | Before first Simulate | One-time setup (caching). |

### Unity Bridging (for external components like CharacterController)

| Method | When | Purpose |
|--------|------|---------|
| `GetUnityState(ref STATE)` | Manager needs Unity data | Read Unity components → STATE. |
| `SetUnityState(STATE)` | Rollback | Apply STATE → Unity components. |

### View & Interpolation

| Method | When | Purpose |
|--------|------|---------|
| `UpdateView(STATE viewState, STATE? verified)` | Every visual frame | Render visuals. **Never** runs during rollback. |
| `ResetInterpolation()` | Teleports, respawns | Clear accumulated smoothing error. |
| `OnViewOwnerChanged(old, new)` | Ownership change | View-only callback. |

### Spawning & Pooling

| Method | Purpose |
|--------|---------|
| `LateAwake()` | Once on fresh spawn. View-only setup. |
| `ResetState()` | Clear IDs and interpolation for pooled reuse. |
| `Destroyed()` | Cleanup on despawn. |

---

## 6. Determinism Rules

> "For the same set of inputs, your code must produce the exact same outcome, every time, on any machine."

### The Three Killers

**1. Randomness**
- Never use `UnityEngine.Random` — it seeds from the system clock
- Use `PredictedRandom` stored in your STATE struct
- Initialize once with a deterministic seed (e.g., network object ID)

```csharp
public struct MyState : IPredictedData<MyState>
{
    public PredictedRandom random;
}
// In Simulate: float val = state.random.NextFloat();
```

**2. Time**
- Never use `Time.deltaTime` — varies by framerate
- Always use the `delta` parameter provided to `Simulate()`
- PurrDiction enforces a fixed tick rate (e.g., 1/30s per tick)

**3. Collection Ordering**
- `FindObjectsByType()` has no guaranteed order
- Sort deterministically before iterating:

```csharp
var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None)
    .OrderBy(p => p.id.GetHashCode())
    .ToArray();
```

### Hard Rules
- Never put visual effects, sound, or animations in `Simulate()`
- `SerializeField` only for constants — never simulation variables
- No LINQ in Simulate (GC pressure)
- Cache component references in `Awake()` / `LateAwake()`
- Use `DisposableList<T>` instead of `new List<T>()` in simulation

---

## 7. Built-in Components

### PredictedTransform
Predicts and reconciles position/rotation with smooth visual correction.

**Key settings:**
- **Graphics:** Optional child Transform for visuals only (prevents jitter)
- **Unparent Graphics:** Avoid disable/enable artifacts on reconcile
- **Character Controller Patch:** Temporarily disable CC when applying rollback state
- **Interpolation Settings:** Assign a `TransformInterpolationSettings` asset
  - `correctionRateMinMax` — base correction speed range
  - `correctionBlendMinMax` — scales correction by error magnitude
  - `teleportThresholdMinMax` — lower = ignore tiny error, upper = snap immediately
- **Float Accuracy:** `Purrfect` (full), `Medium` (compressed), `Low` (half)

**Works with:** bare Transform, Rigidbody, Rigidbody2D, CharacterController.

### PredictedProjectile3D
Simple projectile physics using raycasting during flight. No collider needed — uses built-in radius value. Has its own physics events (similar to Unity's OnCollision/OnTrigger). Can use physics materials and act as a trigger.

### PredictedIdentitySpawner
Mirrors `NetworkIdentity` objects between server and clients based on prediction state. Handles early spawn, observer assignment, ownership, and finalization.

---

## 8. PredictedHierarchy (Dynamic Spawning)

For creating/destroying predicted objects at runtime (e.g., arrows).

### Creation
```csharp
// Returns a stable ID that survives rollbacks
PredictedObjectID? id = predictionManager.hierarchy.Create(arrowPrefab, spawnPos, spawnRot);

// Safer version
if (predictionManager.hierarchy.TryCreate(arrowPrefab, out PredictedObjectID id))
{
    state.lastArrowId = id; // store in STATE, not as a field
}
```

### Deletion
```csharp
predictionManager.hierarchy.Delete(state.arrowId);
```

### Retrieval
```csharp
// Get component from a PredictedObjectID
var arrow = predictionManager.hierarchy.GetComponent<ArrowController>(state.arrowId);

// Get GameObject
GameObject go = predictionManager.hierarchy.GetGameObject(state.arrowId);
```

### Rules
- Always store `PredictedObjectID` in STATE, **never** `GameObject` references (they don't survive rollbacks)
- Register prefabs in the `PredictedPrefabs` asset (referenced by PredictionManager)
- Only create/destroy from within simulation code paths (deterministic)
- Prefer `TryCreate` and check the boolean

---

## 9. Multi-Identity Interactions

When one predicted identity needs to affect another (e.g., arrow applies damage to player):

### Reading/Writing Another Identity's State
```csharp
// Inside Simulate() — resolve via hierarchy
var target = predictionManager.hierarchy.GetComponent<PlayerController>(state.targetId);
if (target != null)
{
    ref var ts = ref target.currentState;  // by ref for direct mutation
    ts.health -= damage;                   // deterministic
}
```

### Reading Another Identity's Input
```csharp
var player = predictionManager.hierarchy.GetComponent<PlayerController>(state.playerId);
if (player != null)
{
    var input = player.currentInput; // read-only view
}
```

### Cross-Identity Events
Use `PredictedEvent` for view-safe events that won't double-fire during replay:

```csharp
private PredictedEvent onHit;

protected override void LateAwake()
{
    onHit = new PredictedEvent(predictionManager, this);
}

void SimulateHit()
{
    onHit.Invoke(); // only fires in valid contexts (not during replay)
}
```

### Ordering
- Identities simulate in consistent order (by object ID, then component ID)
- If strict ordering matters, consolidate shared logic into a single orchestrator identity

---

## 10. Views, Interpolation & Visual Correction

### Key Concepts
- **`viewState`** — smoothed/interpolated state for rendering this frame
- **`verified`** — last server-confirmed state (null if none yet)
- Interpolation buffer absorbs jitter (~tickRate/10 ticks, minimum 2)

### Default Interpolation
Linear blend using `IMath<T>` operations (`Add`, `Scale`, `Negate`) on STATE.

### Custom Interpolation
Override for non-linear data (e.g., quaternion slerp):

```csharp
protected override MyState Interpolate(MyState from, MyState to, float t)
{
    return new MyState
    {
        position = Vector3.Lerp(from.position, to.position, t),
        rotation = Quaternion.Slerp(from.rotation, to.rotation, t),
        // ...
    };
}
```

### Teleports & Respawns
Call `ResetInterpolation()` to clear accumulated error — prevents the camera/character from "dragging back" to the old position.

### TransformInterpolationSettings
- Small errors → ignored (below lower teleport threshold)
- Medium errors → smoothly corrected over multiple frames
- Large errors → snap immediately (above upper teleport threshold)

---

## 11. Ownership & Animation Patterns

### Context Flags

| Flag | Meaning |
|------|---------|
| `isOwner` | You control this object (your character = true, others = false) |
| `isServer` | Running on server/host (always true on server, false on clients) |
| `isVerified` | Current tick is based on server-confirmed data |
| `isVerifiedAndReplaying` | Client is re-simulating from a server correction |

### Animation Strategy

| What | Where | Why |
|------|-------|-----|
| Own character animations | `UpdateView()` | Maximum responsiveness to your input |
| Other players' animations | `Simulate()` with `isVerified` guard | Correctness — wait for server confirmation |
| Feedback VFX (hit flash) | Allow prediction | Game feel |
| Critical state changes (death) | Wait for `verified` | Must be accurate |

```csharp
protected override void UpdateView(MyState viewState, MyState? verified)
{
    if (isOwner)
    {
        // Animate own character — responsive
        animator.SetFloat("Speed", viewState.velocity.magnitude);
    }
}

protected override void Simulate(MyInput input, ref MyState state, float delta)
{
    if (!isOwner && (isServer || isVerifiedAndReplaying))
    {
        // Animate other players — correct
        UpdateRemoteAnimation(state);
    }
}
```

---

## 12. Security & Input Validation

### Trust Model
- Server authoritative: **only inputs** accepted from clients
- Server re-simulates using received inputs → produces authoritative state
- Clients reconcile to server's truth
- Ownership enforced: server only accepts inputs from an identity's owner

### SanitizeInput
```csharp
protected override void SanitizeInput(ref PlayerInput input)
{
    // Clamp movement vector magnitude
    input.moveDir = Vector2.ClampMagnitude(input.moveDir, 1f);

    // Clamp look yaw to reasonable range per tick
    input.lookYaw = Mathf.Clamp(input.lookYaw, -180f, 180f);
}
```

### Checklist
- Clamp analog ranges (normalize movement vectors)
- Gate one-shot actions with game-side cooldowns (not just input booleans)
- Ignore inputs that don't make sense for current state (e.g., jump while airborne if disallowed)

---

## 13. 3rd-Person Shooter Patterns (Project-Specific)

### Camera: Cinemachine 3rd Person Follow

The camera is **local-only** — it is NOT predicted. It runs as a standard MonoBehaviour.

```
[Cinemachine Camera]
  └→ Body: 3rd Person Follow
  └→ Follow: Player Graphics transform
  └→ Look At: Player aim target (or none, driven by script)
```

- Mouse movement rotates the camera orbit
- Character yaw follows camera yaw (player faces where camera looks)
- Pitch is camera-only (up/down look angle) — not sent over the network
- When aiming (drawing bow), camera transitions to over-shoulder position

### Mouse Look Flow

```
UpdateInput() — every Unity frame:
  └→ Read Mouse.current.delta.ReadValue()
  └→ Accumulate into input.lookYaw (horizontal mouse delta)

GetFinalInput() — once per tick:
  └→ lookYaw is already accumulated from UpdateInput

Simulate() — deterministic:
  └→ state.yaw += input.lookYaw * sensitivity
  └→ state.yaw = NormalizeAngle(state.yaw)

UpdateView() — every visual frame:
  └→ Apply viewState.yaw to character rotation
  └→ Cinemachine follows the rotated character naturally
  └→ Apply local pitch to camera rig (not predicted)
```

**Key insight:** Only the **yaw** (horizontal look) is predicted and sent as input. The **pitch** (vertical look) is purely local/visual — it only affects the camera and aim direction, which is resolved at fire time.

### Movement Flow

```
UpdateInput():
  └→ input.moveDir = new Vector2(horizontalAxis, verticalAxis)
  └→ input.sprint |= sprintKey held
  └→ input.crouch |= crouchKey held

Simulate():
  └→ Vector3 forward = YawToForward(state.yaw)
  └→ Vector3 right = YawToRight(state.yaw)
  └→ Vector3 worldMove = (forward * input.moveDir.y + right * input.moveDir.x)
  └→ worldMove = Vector3.ClampMagnitude(worldMove, 1f)
  └→ float speed = input.sprint ? sprintSpeed : walkSpeed
  └→ if (state.isCrouching) speed *= crouchMultiplier
  └→ controller.Move(worldMove * speed * delta)
```

### Character Rotation
- Yaw is stored in STATE as a float angle
- In `Simulate()`: `state.yaw += input.lookYaw * sensitivity`
- In `UpdateView()`: apply rotation `Quaternion.Euler(0, viewState.yaw, 0)` to the character
- The character always faces where the camera is pointing horizontally

### PredictedTransform Setup
- Add `PredictedTransform` to the player prefab
- Create a **Graphics** child (holds mesh/capsule) and assign it to `PredictedTransform.Graphics`
- Enable **Character Controller Patch** (disables CC during rollback state application)
- Enable **Unparent Graphics** to avoid visual artifacts during reconcile

---

## 14. Scene Setup Checklist

1. **NetworkManager** — Empty GameObject with `NetworkManager` component
2. **PredictionManager** — Empty GameObject with `PredictionManager` component
   - Click "New" to create a `PredictedPrefabs` asset
   - Add `PredictedPlayerSpawner` component, assign Player prefab
3. **Player Prefab** —
   - Your `PredictedIdentity<INPUT, STATE>` script
   - `PredictedTransform` component (assign Graphics child, enable CC Patch)
   - `CharacterController` component
   - **Graphics** child GameObject (capsule/mesh, remove its collider)
4. **Camera** — Cinemachine camera with 3rd Person Follow body, pointed at player
5. **Register** the Player prefab in the `PredictedPrefabs` asset

---

## 15. Complete Example Skeleton

```csharp
using PurrNet.Prediction;
using UnityEngine;

namespace VerdantHunt.Player
{
    // --- INPUT ---
    public struct PlayerInput : IPredictedData
    {
        public Vector2 moveDir;        // WASD (continuous)
        public float lookYaw;          // mouse X delta (accumulated per tick)
        public bool sprint;            // held
        public bool crouch;            // held
        public bool drawBow;           // held
        public bool releaseBow;        // edge-triggered
        public bool melee;             // edge-triggered
        public bool interact;          // edge-triggered

        public void Dispose() { }
    }

    // --- STATE ---
    public struct PlayerState : IPredictedData<PlayerState>
    {
        public Vector3 position;
        public float yaw;
        public Vector3 velocity;
        public float stamina;
        public float health;
        public int arrowCount;
        public float drawStrength;     // 0→1 charge
        public bool isCrouching;
        public bool isSprinting;

        public void Dispose() { }
    }

    // --- PREDICTED PLAYER ---
    [RequireComponent(typeof(CharacterController))]
    public class PredictedPlayer : PredictedIdentity<PlayerInput, PlayerState>
    {
        [SerializeField] float walkSpeed = 4f;
        [SerializeField] float sprintSpeed = 7f;
        [SerializeField] float crouchSpeedMult = 0.5f;
        [SerializeField] float gravity = -15f;
        [SerializeField] float mouseSensitivity = 0.15f;

        CharacterController _cc;

        protected void Awake()
        {
            _cc = GetComponent<CharacterController>();
        }

        // --- Initial State ---
        protected override PlayerState GetInitialState() => new PlayerState
        {
            position = transform.position,
            yaw = transform.eulerAngles.y,
            health = 100f,
            stamina = 100f,
            arrowCount = 10,
        };

        // --- Unity Bridging ---
        protected override void GetUnityState(ref PlayerState state)
        {
            state.position = transform.position;
        }

        protected override void SetUnityState(PlayerState state)
        {
            // CharacterController needs to be disabled to set position directly
            // PredictedTransform handles this if "Character Controller Patch" is on
            transform.position = state.position;
            transform.rotation = Quaternion.Euler(0, state.yaw, 0);
        }

        // --- Input (every Unity frame) ---
        protected override void UpdateInput(ref PlayerInput input)
        {
            // Edge-triggered inputs — use |= to not miss between ticks
            input.releaseBow |= /* bow release action triggered */  false;
            input.melee      |= /* melee action triggered */        false;
            input.interact   |= /* interact action triggered */     false;
        }

        // --- Input (once per tick) ---
        protected override void GetFinalInput(ref PlayerInput input)
        {
            // Continuous inputs — set directly
            input.moveDir = new Vector2(
                /* horizontal axis */ 0f,
                /* vertical axis */   0f
            );
            input.lookYaw   = /* accumulated mouse X delta */ 0f;
            input.sprint    = /* sprint key held */ false;
            input.crouch    = /* crouch key held */ false;
            input.drawBow   = /* draw key held */  false;
        }

        // --- Input Validation ---
        protected override void SanitizeInput(ref PlayerInput input)
        {
            input.moveDir = Vector2.ClampMagnitude(input.moveDir, 1f);
            input.lookYaw = Mathf.Clamp(input.lookYaw, -180f, 180f);
        }

        // --- Extrapolation (missing remote input) ---
        protected override void ModifyExtrapolatedInput(ref PlayerInput input)
        {
            input.moveDir *= 0.4f;       // reduce movement prediction
            input.releaseBow = false;     // don't predict one-shot actions
            input.melee = false;
            input.interact = false;
        }

        // --- Core Simulation (deterministic, every tick) ---
        protected override void Simulate(PlayerInput input, ref PlayerState state, float delta)
        {
            // Rotation
            state.yaw += input.lookYaw * mouseSensitivity;

            // Movement direction relative to facing
            Vector3 forward = new Vector3(Mathf.Sin(state.yaw * Mathf.Deg2Rad), 0,
                                          Mathf.Cos(state.yaw * Mathf.Deg2Rad));
            Vector3 right = new Vector3(forward.z, 0, -forward.x);
            Vector3 moveWorld = (forward * input.moveDir.y + right * input.moveDir.x);
            moveWorld = Vector3.ClampMagnitude(moveWorld, 1f);

            // Speed
            state.isSprinting = input.sprint && state.stamina > 0f;
            state.isCrouching = input.crouch;
            float speed = state.isSprinting ? sprintSpeed : walkSpeed;
            if (state.isCrouching) speed *= crouchSpeedMult;

            // Gravity
            if (IsGrounded() && state.velocity.y < 0f)
                state.velocity.y = -0.5f; // small downward force to stay grounded
            state.velocity.y += gravity * delta;

            // Final movement
            Vector3 finalMove = (moveWorld * speed) + (Vector3.up * state.velocity.y);
            _cc.Move(finalMove * delta);

            // Update position from CC (CC modifies transform directly)
            state.position = transform.position;

            // Stamina (placeholder)
            if (state.isSprinting)
                state.stamina -= 15f * delta;
            else
                state.stamina = Mathf.Min(100f, state.stamina + 10f * delta);
        }

        // --- View (every visual frame, NOT during rollback) ---
        protected override void UpdateView(PlayerState viewState, PlayerState? verified)
        {
            // PredictedTransform handles position/rotation smoothing
            // Drive animations, camera, UI from viewState here

            transform.rotation = Quaternion.Euler(0, viewState.yaw, 0);

            // Example: update Cinemachine target, animation params, HUD
        }

        // --- Helpers ---
        bool IsGrounded()
        {
            return Physics.SphereCast(
                transform.position + Vector3.up * 0.1f,
                _cc.radius - 0.05f,
                Vector3.down,
                out _,
                0.2f
            );
        }
    }
}
```

### Camera Setup (Separate MonoBehaviour — NOT Predicted)

```csharp
using UnityEngine;
using Unity.Cinemachine;

namespace VerdantHunt.Player
{
    /// <summary>
    /// Local-only camera controller. Manages pitch (vertical look).
    /// Yaw is driven by the predicted character rotation.
    /// Attach to the player prefab. Only active for isOwner.
    /// </summary>
    public class PlayerCameraController : MonoBehaviour
    {
        [SerializeField] float pitchSpeed = 0.15f;
        [SerializeField] float minPitch = -60f;
        [SerializeField] float maxPitch = 60f;

        float _pitch;

        // Cinemachine reads from a follow target —
        // create a child "CameraTarget" transform that combines
        // character yaw (from prediction) + local pitch.

        [SerializeField] Transform cameraTarget;

        void Update()
        {
            // Only run for the local player (check isOwner on PredictedPlayer)
            float mouseY = Mouse.current?.delta.ReadValue().y ?? 0f;
            _pitch -= mouseY * pitchSpeed;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

            // Apply pitch to camera target (yaw comes from PredictedPlayer)
            cameraTarget.localRotation = Quaternion.Euler(_pitch, 0, 0);
        }
    }
}
```

**Cinemachine setup:**
- CinemachineCamera → Body: **3rd Person Follow**
- **Follow:** Player's CameraTarget transform
- **Look At:** (none — camera direction is driven by CameraTarget rotation)
- Shoulder offset, damping, and distance tuned for TPS feel

---

## 16. PredictedModules (Optional Modularity)

Break complex logic into self-contained modules instead of one monolithic script:

```csharp
public struct StaminaState : IPredictedData<StaminaState>
{
    public float current;
    public float max;
    public void Dispose() { }
}

public class StaminaModule : PredictedModule<StaminaState>
{
    public StaminaModule(PredictedIdentity identity, float max) : base(identity)
    {
        currentState.max = max;
        currentState.current = max;
        ResetInterpolation();
    }

    protected override void Simulate(ref StaminaState state, float delta)
    {
        // Passive regen when not draining (drain controlled by parent)
        state.current = Mathf.Min(state.max, state.current + 10f * delta);
    }

    public void Drain(float amount) => currentState.current -= amount;
    public bool HasStamina(float amount) => currentState.current >= amount;
}
```

**Benefits:** Own delta compression per module (bandwidth efficient), automatic history/rollback, reusable across identities.

**Trade-off:** Each module is an additional simulation + state to track. Lighter than separate PredictedIdentity components, but heavier than fields in a single STATE struct.

---

## 17. Development Shortcuts

PurrDiction's reconciliation can paper over non-deterministic code during prototyping:

| Shortcut | Effect | Mitigation |
|----------|--------|------------|
| Server-only state change | Client snaps to corrected state | Tune interpolation settings |
| Server-only spawn/despawn | Objects pop in late | Use PredictedIdentitySpawner |
| Server-only teleport | Hard correction | Call `ResetInterpolation()` |
| Skip SanitizeInput on client | Minor feel differences | Server still enforces |

**Rule:** Use shortcuts for prototyping, then graduate to deterministic implementations for core gameplay (movement, combat).

---

## Quick Reference: File Locations

| What | Where |
|------|-------|
| PurrDiction docs | `PurrDocs/client-side-prediction/` |
| Community guides | `PurrDocs/community-guides/` |
| CharacterController example | `PurrDocs/community-guides/character-controller-client-side-prediction.md` |
| Knockback example | `PurrDocs/community-guides/character-controller-knockback-client-side-prediction.md` |
| Our scripts | `Assets/_Project/Scripts/Player/` |
| Our namespace | `VerdantHunt.Player` |
