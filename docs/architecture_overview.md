# Crescendo — Architecture Overview

The architecture is straightforward: **Unity components for gameplay, a few coordinators for shared state and scene flow, and common interfaces where features interact.** The point is to let us work in parallel without duplicating logic or constantly editing the same files.

## 1. Components and responsibilities

Every instrument shares the same player foundation:

```text
Base Player prefab
├── Player                 Input, aim, shared action restrictions
├── PlayerMovement         Movement, dodge, skill displacement
└── PlayerHealth           Health, downing, automatic revival

Instrument prefab variant (e.g. Guitar)
├── <Instrument>Abilities  Class-specific skills and cooldowns
└── <Instrument>WeaponVisual : FloatingWeaponVisual
                            Floating weapon positioning and animation
```

Every instrument has a **floating weapon** and its own prefab variant, abilities and effects. `FloatingWeaponVisual` is an abstract shared contract; each instrument supplies a concrete subclass with different motion. The shared `Player` does not implement weapon animation.

Bosses follow the same **composition principle**, but are independent of the player implementation:

```text
Boss prefab (target design)
├── BossController           Targeting, movement, attack/phase decisions
├── BossHealth               HP, damage acceptance, defeat
├── <Boss>Attacks            Boss-specific patterns/projectiles
└── <Boss>Presentation       Sprite animation, VFX and SFX
```

These are **responsibilities, not mandatory classes**. Currently `BaseBoss` combines several of them for the checkoff prototype; separate them incrementally when implementing substantive boss features. Bosses get eligible players from `GameSession`, not the join manager. Boss attacks use `PlayerHealth` to apply damage; player attacks use `IHittable` to damage bosses. The encounter controls fight start/end, not individual attack scripts.

| System | Owns |
|---|---|
| `PlayerJoinManager` | Joining, leaving and controller pairing |
| `GameSession` | Persistent roster of players, accessible to bosses and UI |
| Lobby / scene flow | Character selection, transitions and encounter entry |
| `IHittable` / `HitInfo` | Common player-attack damage contract |
| Reverberation / Resonance | Shared cooperative attack interactions |
| Boss-specific components | Boss behaviour, attacks and phases |

**Gameplay decides what happens; visuals show it.** An ability determines attack timing and damage. Weapon movement, VFX, SFX and UI respond to those decisions, rather than implementing their own damage rules.

```text
Player input → Instrument ability → Hit detection → IHittable / Resonance
                                └→ Weapon visuals / VFX / SFX
```

## 2. Scenes and folders

The current flow is `Lobby → Cutscene1 → Game`. Players and their device assignments persist across transitions; bosses, arena objects and hazards belong to the encounter scene. Future bosses can have separate scenes.

Use **feature-oriented folders**. Keep instrument/boss scripts, prefabs and effects together, with shared code in shared folders:

```text
Assets/
├── Gameplay/
│   ├── Players/             Shared player components
│   │   └── Abilities/        Guitar/, Flute/, Trombone/ ...
│   ├── Combat/              Hit contracts and Resonance/
│   ├── Bosses/              Separate folder per boss
│   ├── Lobby/               Lobby logic
│   ├── Game/ and Flow/      Scene/encounter flow
│   └── Characters/          Character data
├── Scenes/                 Playable and test scenes
├── Rendering/              Shared shaders/rendering
└── Settings/               Input and URP settings
```

This is the **target layout**; we can move existing files gradually rather than reorganising everything at once.

## 3. Team conventions

1. **Don't duplicate shared systems.** Instrument abilities use `PlayerMovement`, `PlayerHealth`, `PlayerAbilities` and existing combat contracts.
2. **Keep features independent.** Avoid instrument-specific code in `Player`, and avoid boss code depending on the input/join manager.
3. **Prefer local edits.** Use feature prefabs and test scenes; coordinate before changing the shared player prefab, input actions, combat API or main gameplay scene.
4. **Preserve Unity references.** Commit `.meta` files and move them with their assets.
5. **Don't over-engineer.** Add a manager, base class or event abstraction only when it solves a real shared problem.
6. **Integrate and test.** A feature is not finished until it works with the actual game, including multiplayer when relevant.

For agents and implementation details, see [`ARCHITECTURE.md`](agents/ARCHITECTURE.md) and the repository-root [`AGENTS.md`](../AGENTS.md). See [`README.md`](../README.md) for controls and setup.
