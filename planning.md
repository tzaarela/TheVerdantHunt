# planning.md — Project Planning & AI Context

> **Role of this file**
> This document defines *what* we are building and *how the AI should help right now*.
> It is intentionally lightweight on low-level architecture details (see `architecture.md`).
>
> Claude Code should treat this as the **primary source of truth**.

---

## 1. Project Snapshot

**Project Name:** The Verdant Hunt

**One-Sentence Elevator Pitch:**
A slow, tactical 3rd-person multiplayer archery game where 2-4 players stalk each other through dark fantasy woodlands — headshots are instant kills, every arrow counts.

**Target Platform(s):** PC (Windows)

**Unity Version / Render Pipeline:** 6.3 LTS / URP

**Packages:**
-PurrNet
-PurrDiction
-New Input System
-Cinemachine


---

## 2. Design Pillars (Gameplay-Driven)

> These are non-negotiable. If a suggestion violates one, it should be rejected.

1. **Gameplay Feel First** – responsive bow mechanics, tight movement. Moment-to-moment responsiveness matters more than systems elegance.
2. **Readable State** – player always knows HP, arrows, stamina. The player should always understand what is happening.
3. **Iteration Speed** – tunable via ScriptableObjects. Features must be fast to tweak and rebalance.

---

## 3. Core Gameplay Loop

> Described from the player's perspective.

1. Player spawns in a forested arena with limited arrows
2. Player moves tactically — crouching, sprinting (stamina-limited), using cover
3. Player spots an enemy — decides to engage or reposition
4. Player draws bow (stamina drains while held at full draw), aims, releases
5. Arrow travels with physics trajectory; hit zones determine damage (head=instant kill, torso=50, limbs=35)
6. Missed arrows stick in the world and can be retrieved; healing herbs spawn on the map
7. Last standing or first to kill target wins

---

## 4. Player Abilities & Systems (High Level)

### Player Capabilities
- **Movement:** Walk, sprint (stamina cost), crouch. Slow & tactical feel. No climbing/vaulting.
- **Combat:** Hold-to-draw bow with stamina drain at full draw. Draw strength affects arrow speed + damage. Crosshair aiming, no trajectory preview. Simple melee kick/shove (low damage, stagger, stamina cost).
- **Equipment:** Single bow, single arrow type. Limited arrows (retrievable from world). Healing herbs as world pickups.

### Core Systems
- **Health:** 100 HP. Head=instant kill. Torso=50 dmg. Limbs=35 dmg. Draw strength scales damage. No passive health regen.
- **Stamina:** Shared pool for sprint, bow draw hold, and melee kick. Regenerates passively when not draining.
- **Arrows:** Limited carry (e.g. 10). Retrievable from ground (missed shots). No passive regen. Out of arrows = melee only.
- **Healing:** World pickup herbs at map locations. No passive health regen.
- **Game Modes:** Free-for-all deathmatch (timed respawn 3-5s) + Last Man Standing (no respawn, spectator on death).
- **Death:** Simple death screen (killer name). Spectator mode in Last Man Standing.

---

## 5. Scope Control

### In Scope (Phase 1 — Prototype)
- PurrNet + PurrDiction networking (dedicated server + player-hosted)
- Client-side predicted player movement (walk, sprint, crouch)
- Bow draw + fire with physics arrows (predicted, server-validated, reconciled)
- Zoned hitbox damage (head/torso/limbs)
- Stamina system
- Limited arrows + retrieval
- Simple melee kick
- Healing herb pickups
- FFA deathmatch + Last Man Standing modes
- Direct connect (IP/room code)
- Basic HUD (HP, arrows, stamina, crosshair)
- Placeholder capsule characters

### Explicitly Out of Scope
- Multiple arrow/bow types
- Team modes
- Matchmaking / lobby browser / Steam integration
- Audio / sound design
- Character models / animations (beyond placeholders)
- Kill cam / arrow replay
- Maps beyond a single test arena
- Progression / unlocks / cosmetics

### Nice-to-Have (Do Not Build Yet)
- Trajectory preview dots
- Arrow types (fire, poison)
- Spectator camera polish
- Environmental hazards

---

## 6. Current Development Phase

**Phase:** Prototype

### Active Goals
- [ ] Set up PurrNet + PurrDiction networking foundation
- [ ] Implement predicted player movement (walk/sprint/crouch)
- [ ] Implement bow draw, aim, and fire with physics arrows
- [ ] Implement arrow hit detection with zoned damage

### Risks
- PurrDiction prediction complexity for arrow physics may require iteration
- Hitbox zone accuracy with placeholder capsule characters
- Balancing bow draw time vs stamina drain without real playtesting

---

## 7. AI ASSISTANT INSTRUCTIONS (Claude Code)

> This section is authoritative. Claude must follow it strictly.

### Role
You are a **senior Unity gameplay engineer** assisting an experienced developer.

### Expectations
- Prioritize gameplay clarity over abstraction
- Optimize only when required or requested
- Prefer Unity-idiomatic solutions
- **Networking-first priority:** All gameplay systems must be multiplayer-aware from the start using PurrDiction's PredictedIdentity pattern

### When Requirements Are Unclear
- Ask **one concise clarifying question** before writing code
- Do not invent mechanics or rules

### Output Rules
- Provide complete, compilable C# scripts
- Use clear comments explaining intent
- State assumptions explicitly

---

## 8. Open Questions

> These are unresolved. Do not assume answers.

- Arrow carry count (suggested 10 — needs playtesting)
- Exact stamina drain rates and regen speed
- Melee kick damage and stagger duration
- Healing herb restore amount and spawn frequency
- Respawn timer exact value (3s or 5s)
- Draw time to full power (suggested ~1.5s)
- Map size and layout specifics

---

## 9. Change Log

- 2026-03-02: Populated all sections with initial game design decisions from design interview
