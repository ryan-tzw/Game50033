# Agent instructions — Crescendo / Game50033

This is a Unity 6 (6000.3.24f1), URP, local 2–4-player, music-themed boss-rush game developed by a six-person team. Gameplay takes place in an XZ world plane with a fixed angled camera and stylized/pixel-art presentation.

**Read [`docs/agents/ARCHITECTURE.md`](docs/agents/ARCHITECTURE.md) before modifying gameplay, scenes, prefabs, or shared APIs.** It specifies ownership, dependency boundaries, asset placement, scene lifetime, and planned migrations. The existing [`README.md`](README.md) covers the current playable flow, controls, Unity setup, and device testing. The architecture document is authoritative for design intent; verify the code for what actually exists.

## Non-negotiable working rules

1. **Small, isolated changes.** Work within the feature you are assigned. Do not refactor adjacent systems, reformat entire scenes, or move unrelated Unity assets without an explicit reason.
2. **Maintain Unity references.** Commit `.meta` files with every asset/folder. Move/rename Unity assets with their existing `.meta` files; never regenerate GUIDs, hand-invent GUIDs, or delete metadata casually. Preserve prefab and scene references.
3. **Respect the dependency boundaries.** Instrument abilities implement `PlayerAbilities` and do not duplicate shared movement/input/health. Boss code does not depend on `PlayerJoinManager`. Gameplay decisions do not depend on VFX/UI scripts. Effects do not apply damage by themselves.
4. **Prefer composition, plain C#, and serialized references.** Do not introduce generic service frameworks, singletons, custom event buses, or broad base-class hierarchies to solve one feature. Use events only when another component genuinely needs notifications.
5. **Don't change established mechanics as incidental cleanup.** Holding an ability input may intentionally retry activation; moving can influence aim; dodge invulnerability currently follows the active dodge. Changes to these behaviours require a specific task/design decision.
6. **Only the persistent player/session root survives scene transitions.** Encounter bosses, arenas, hazards and encounter-specific UI belong to the encounter scene. Keep device-to-player pairing and selected character identity intact across transitions.
7. **Every instrument owns a floating weapon visual.** Each class has its own concrete implementation of `FloatingWeaponVisual` with distinct motions. Keep weapon motion in presentation, separate from attack damage/timing.
8. **Avoid shared-asset conflicts.** Prefer instrument-specific prefab variants and per-boss prefabs. Coordinate edits to `Player.prefab`, `PlayerJoinManager.prefab`, input actions, shared shaders, `GameSession`, scene flow, and build-scene configuration.
9. **No speculative rewrites.** A folder in the architecture tree marked *planned* is not permission to create placeholder managers or to migrate all assets. Implement only what the feature needs; update the architecture doc if a stable boundary changes.
10. **Verify and report accurately.** Check compilation where a Unity environment is available; otherwise explicitly say Unity compilation and play-mode behaviour are unverified. Test feature behaviour and at least one multi-player interaction where relevant. Never claim a test passed without running it.
11. **Keep the docs accurate.** When adding a significant component, changing a public combat contract, relocating features, or altering scene lifetimes, update `docs/agents/ARCHITECTURE.md` and, if controls or how-to-run change, `README.md` in the same change.

## How to approach a feature task

- Inspect the latest branch and the relevant existing scripts/prefabs before editing. Do not assume old prototype files or prior instructions have been integrated.
- State which files and public contracts need to change; prefer feature-local files first.
- Reuse existing interfaces such as `IHittable`, `HitInfo`, `PlayerAbilities`, and existing `Reverberation`/`ResonanceResolver` mechanics instead of creating parallel systems.
- Expose only the small public API needed by the next feature. Put tuning values in serialized fields where appropriate.
- When finished, report: files changed, behavioural/API changes, tests actually run, Unity Editor checks remaining, and any shared files that teammates should avoid editing simultaneously.

## Code review focus

Flag cross-feature dependencies on concrete instrument/boss implementations, missing Unity `.meta` files or broken references, gameplay placed inside presentation code, duplicate damage/input logic, unsafe scene-lifetime assumptions, and broad unrequested asset moves. Be especially careful with `PlayerInput` device pairing and persistent objects during lobby-to-encounter transitions.
