# Crescendo (50.033 Game Design and Development)

A music-themed, 2D top-down boss rush for 2–4 players in local co-op. See `50.033 GDD.pdf` for the full design.

- **Unity:** 6000.3.24f1 (URP)
- **Scene flow:** `Lobby` → `Cutscene1` → `Game` (the first three scenes in Build Profiles). Open `Lobby` and press Play to go through the whole flow. The main menu isn't built yet; it will go in front of `Lobby`.
  - `Cutscene1` is a placeholder: it fades the word "Placeholder" in and out, then loads `Game`.
  - Players who joined in the lobby are carried through to `Game` with their characters.

---

## 1. One-time setup on each machine

### Unity Smart Merge for scenes and prefabs

`.gitattributes` already tells git to merge Unity files (`.unity`, `.prefab`, `.asset`, `.meta`, ...) with a merge driver called `unityyamlmerge`. **Git doesn't share the driver itself through the repo**, so everyone has to define it once in their own git config. Without it, git falls back to a plain text merge, which can corrupt scenes and prefabs when they conflict.

Run these three commands once. Change the path if Unity is installed somewhere else.

```bash
git config --global merge.unityyamlmerge.name "Unity SmartMerge (UnityYAMLMerge)"
```

```bash
git config --global merge.unityyamlmerge.driver "'C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -h -p --force %O %B %A %A"
```

```bash
git config --global merge.unityyamlmerge.recursive binary
```

- **Finding your path:** in Unity Hub, go to *Installs* → the gear icon next to 6000.3.24f1 → *Show in Explorer*. The tool is `Editor/Data/Tools/UnityYAMLMerge.exe`. On macOS it's `/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/Tools/UnityYAMLMerge`.
- **Checking it worked:**

  ```bash
  git config --global --get-regexp merge.unityyamlmerge
  ```

### Always commit `.meta` files

Unity creates a `.meta` file next to every asset and folder. It holds the asset's ID (GUID) and its import settings, and scenes and prefabs reference assets by that ID. Commit each `.meta` file together with its asset. If one is missing, Unity makes a new ID on other machines, and every reference to that asset breaks.

---

## 2. Controls

> Current as of the `InputSystem_Actions` asset in `Assets/Settings`. **Update this table whenever the bindings change.**

Gamepad buttons are bound by **position**, not by the letter printed on them. Switch Pro has A/B and X/Y swapped compared with Xbox.

| Action | Keyboard & mouse | Gamepad position | Xbox | PlayStation | Switch Pro | Status |
|---|---|---|---|---|---|---|
| Move | WASD / arrow keys | Left stick | Left stick | Left stick | Left stick | ✅ Working |
| Aim | Automatic | Automatic | — | — | — | Auto-targeting, with movement-facing fallback; Guitar snapshots this direction per strike |
| Primary attack | Left click | Left face button | X | □ | Y | Guitar: three-hit sweeping combo; hold to continue/repeat. Other characters retain their current abilities/placeholders. |
| Secondary attack | Right click | Top face button | Y | △ | X | Guitar: travelling Strum wave; empowers the next Primary. |
| Special attack | E | Bottom face button | A | ✕ | B | Not implemented (used as **join / pick** in the lobby) |
| Defensive | Q | Right face button | B | ○ | A | Not implemented (used as **back** in the lobby) |
| Dash | Space | Right trigger | RT | R2 | ZR | Existing dodge: two charges, invulnerability, timed recharge |

The D-pad is not bound to anything yet.

### Lobby

| Do this | Keyboard & mouse | Gamepad |
|---|---|---|
| Join | E | Bottom face button (A / ✕ / Switch B) |
| Browse characters | A / D or ← / → | Left stick left / right |
| Pick the highlighted character | E | Bottom face button |
| Back out of the lobby (while picking) | Q | Right face button (B / ○ / Switch A) |
| Hit the mic dummy | Left click | Left face button (X / □ / Switch Y) |
| Start the game | Everyone who joined has picked and stands in the yellow zone by the door for 3 seconds | Same |

Each character can only be picked by one player. Characters taken by someone else are greyed out and show **TAKEN**.

---

## 3. Testing local multiplayer

**Each input device is one player** (up to 4).

- **Keyboard and mouse:** count as one player together, so two people can't share a keyboard right now.
- **Joining:** press the join button on each device to add another player.
- **Keyboard focus:** in the editor, click inside the **Game view** first, or keyboard and mouse input won't reach the game. Gamepads work without focus.

**Controllers:**
- **PlayStation 4 / 5:** USB or Bluetooth. To pair over Bluetooth, hold **Share + PS** until the light bar flashes, then add it in Windows Bluetooth settings.
- **Switch Pro Controller:** use **USB**, because Bluetooth detection is unreliable in Unity. Separate Joy-Cons are not supported.
- **Close Steam (or turn off Steam Input)** while testing. Otherwise it can take over PlayStation and Switch controllers.
- **Several controllers at once:** each controller pairs with the PC, and one PC can hold several at the same time. Four Bluetooth controllers on one built-in adapter may lag or drop out, so use USB cables (or a USB hub) for the 3rd and 4th.
- **A controller doesn't respond?** Open **Window → Analysis → Input Debugger** with it connected. If it isn't listed, Unity can't see it (usually Steam or Bluetooth).

---

## 4. Where things are

| Folder | Contents |
|---|---|
| `Assets/Gameplay/Players` | Player prefab, input/aim (`Player`), movement/dodge (`PlayerMovement`), health/revival (`PlayerHealth`), ability contract (`PlayerAbilities`), joining (`PlayerJoinManager`), lobby character select (`PlayerCharacterSelect`) |
| `Assets/Gameplay` | Persistent player registry (`GameSession`) |
| `Assets/Gameplay/Bosses` | Boss movement/targeting prototype (`BaseBoss`) and animated bone projectile (`Bone`); damage/phases unfinished |
| `Assets/Gameplay/Lobby` | Lobby state, select panels, start zone, lobby → cutscene handoff (`StartGameSequence`) |
| `Assets/Gameplay/Characters` | `CharacterDefinition` / `CharacterRoster` and their data assets (`Data/`) |
| `Assets/Gameplay/Combat` | `IHittable` (shared "can be hit" interface), mic training dummy, shared REV/RES mechanics |
| `Assets/Gameplay/Players/Abilities/Guitar` | Guitar Primary combo, Strum/empowerment, overhead Special, weapon/VFX, prefabs, tuning types and art |
| `Assets/Gameplay/Flow` | Scene names, placeholder cutscene (`PlaceholderCutscene`) |
| `Assets/Gameplay/Game` | Game scene entry: puts carried-over players on spawn points and gives them control |
| `Assets/Rendering` | Pixel-art renderer feature and sprite / toon / dissolve shaders |
| `Assets/Settings` | URP assets and the input actions |

## 5. Testing Guitar

Open `Assets/Scenes/TestScenes/GuitarPrimaryTest.unity` and press Play. Join with E or the bottom gamepad face button. The first/third player uses Guitar; the second/fourth uses Flute. Move toward a dummy and hold Primary to cycle through the three sweeps. Primary slows normal movement to 20% throughout windup, active and recovery, while keeping dodge and other abilities locked. Its original forward lunge is added during Active; full walking speed returns after recovery. Moving closer gives normal hits; placing the target toward the outer part of the arc gives sweet-spot damage. Default damage is 2 normally and 3 in the sweet spot; dummies display the latest hit and total damage. The wall on the right can be used to check lunge collision and Strum blocking. Right-click / press the top face button to Strum. Alternate Secondary and Primary to play `S → P1 → S → P2 → S → P3`: Strum refreshes the combo continuation window without changing its step. Holding an ability retries it; holding both still gives Primary the existing input priority.

Press **E / the bottom gamepad face button (A/Cross)** as a joined Guitar player to use **Special: Overhead Smash**. It locks aim when pressed, pulls the guitar backward behind the player and holds it near shoulder height before a fast overhead downstroke. The ground indicator follows your slowed movement until impact, then stays fixed. Default timing is 0.9 s Windup → 0.12 s Active → 0.45 s Recovery; impact occurs 80% through Active. Walking runs at 20% throughout, with no lunge; dodge and other abilities remain locked. The 12 s cooldown starts on activation and continues during hitstop. Special resets the Primary combo.

The impact circle is centred 1 unit ahead, with a 0.75-unit outer radius and 0.25-unit inner sweet spot. Outer overlap deals 8 damage; the authored hurtbox's **ground-footprint centre (world X/Z, ignoring height) must lie inside the inner circle** for 16 damage. The sweet spot uses the ground circle directly; outer overlap retains the forgiving camera-projected hurtbox query. Raising the hurtbox does not shift its sweet-spot position. The collider fallback also uses its bounds centre in X/Z. A large hurtbox overlapping the inner circle without its centre inside receives normal damage. Targets without an authored hurtbox use a world-collider fallback. Each target is hit once; normal/sweet contacts request 0.06 / 0.1 s attacker-only hitstop, also holding the circular impact effect. Secondary empowerment raises these values to 12 / 24. Downing/cancellation stops the windup and indicator without an impact. REV/RES is deferred.

The three test dummies opt into camera-projected hurtboxes for **Guitar Primary and Special**. Enable **Gizmos** in each viewport (or toggle the `CombatHurtbox` entry in its Gizmos menu): the **Scene view shows the yellow 3D capsule**, while the **Game view shows its cyan projected outline over the artwork** when **Show Game View Preview** is checked in the hurtbox tuning asset. That checkbox defaults to off and controls all hurtboxes using the asset; the Game view's master Gizmos toggle must also be enabled. It does not affect the Scene-view preview or damage. Object selection is not required. These Editor previews do not appear in builds or change hit acceptance. A disabled hurtbox has no preview and cannot receive Primary or Special hits.

Tune all three through `Assets/Tuning/Combat/GuitarTestDummyHurtbox.asset`: **Center** is a 3D local-space offset, **Radius** sets the round cross-section, and **Height** is the total capsule height along local Y, including both rounded ends. The optional Anchor supplies position, rotation and scale; otherwise the component uses its own transform. Height scales along local Y, radius uses the larger absolute X/Z scale, and the capsule becomes a sphere when height is less than its diameter. The dummy defaults (radius 0.5, height 2, centre zero) follow their existing scaled capsule geometry. Adjust the asset while checking both previews to match the vulnerable visible body, including camera-facing sprite projection. The same world-space endpoints/radius supply both previews and combat queries; the Game-view preview has no independent shape settings. These are authored combat volumes, not pixel-perfect sprite outlines.

At Primary's initial impact, the attack compares the capsule and the initial crescent in the fixed orthographic camera's view. Apparent overlap can hit even when their world-space shapes are separated in depth. A hurtbox overlapping both the normal and sweet bands takes normal damage; only overlap confined to the outer band gets sweet-spot damage. The initial crescent's inner hole, angular edges and stretched reach still apply. Later smear poses cannot hit. Hurtboxes do not change physical world collision, and disabling one stops projected Primary hits rather than restoring the object's world collider as a damage shape. Targets without `CombatHurtbox` keep existing world-space detection. Secondary, other instruments and REV/RES still use their existing world-space queries; migration is separate. Projected queries do not provide wall occlusion and require a downward-facing orthographic gameplay camera.

Primary requests attacker-only hitstop on a valid melee contact: Each entry under **Attack → Combo** has its own **Hitstop Duration** and **Sweet Spot Hitstop Duration**: defaults are 0.04 / 0.06 s for the first strike, 0.05 / 0.075 s for the second, and 0.06 / 0.09 s for the third. A miss does not pause; an area hit resolves every target once, then uses the longest applicable duration instead of adding pauses. Set both durations on a swing to 0 to disable its feedback. The attacker stops walking/lunging, its attack phase, weapon animation and smear poses pause, and the local action clock resumes without a movement or animation catch-up jump. Health, cooldowns, UI, impact particles, enemies, other players and launched waves continue. Hitstop adds no invulnerability. Secondary/ranged hits do not request it.

Other melee classes can call `Player.Hitstop.Request(seconds)` after resolving contact. Use `Player.Hitstop.ActionTime` for action phase timestamps and `ActionDeltaTime` for local animation progress; keep cooldown/health/world-effect clocks on normal Unity time. `IsStopped` exposes the pause, and `Clear()` ends it without rewinding action time. `Player` ticks the clock before routing input and movement; callers must not tick it again. Overlapping requests keep the longer remaining duration. Ranged attacks must not request attacker hitstop. Cancel your action and clear its pause on cancellation; the player clears it on down/disable. No change to `IHittable`/`HitInfo` is needed.

Player sprites use `Assets/Rendering/PlayerShadows/PlayerShadowReceiver.mat` to receive environment and character shadows while excluding weapon shadows from the main directional light. The world still receives normal weapon shadows. Player shadow sampling follows the original sprite mesh geometry to avoid false self-shadowing while keeping the body’s ground shadow. `PC_Renderer.asset` runs `PlayerShadowRendererFeature`; its settings live in `Assets/Tuning/Rendering/PlayerShadows.asset` (2048 atlas by default). Set an instrument weapon's renderer child to **WeaponVisual (layer 13)** and enable Cast Shadows; keep its gameplay colliders and effects on their existing layers. Guitar is wired, and player receiver materials are assigned on the base Player and standalone Trombone prefab. No scene-light changes are needed. Guitar's resting and attack materials enable **Environment Shadows Only**, selecting a separate weapon receiver atlas that excludes `SpriteVisual` character sprites and `WeaponVisual` casters. The guitar still receives environment shadows, while characters and weapons retain their normal ground shadows. `PlayerShadows.asset` also contains **Weapon Atlas Resolution** (1024 by default) and **Weapon Excluded Caster Layers**. This adds one additional caster cull and atlas render; use the `Weapon Shadows (Environment Only)` profiler marker when assessing cost. Character/enemy sprite renderers must use the excluded visual layer for this filtering to apply. Additional point/spot-light shadows currently retain ordinary URP behaviour. The extra shadow atlas and caster cull add rendering cost; profile the named Player Shadows pass before raising resolution. Disabling the feature restores normal shadow reception.

Player body animation uses the base prefab’s Animator and `Assets/Gameplay/Players/Animation/Player.controller`. Idle (frames 0–1), Walk (8–11) and Run (12–15) loop at **8 FPS** and switch based on actual horizontal movement, including collision blocking. `Assets/Tuning/Players/PlayerAnimationTuning.asset` sets the idle cutoff (0.05 units/s) and run threshold (above 1.5 units/s). The Animator pauses with the player’s hitstop. Attack (4–5), TakeDamage (6–7), Jump (24–31) and Death (36–39) are prepared as non-looping 8 FPS clips and disconnected states; they are not triggered yet. Edit frame sequences and playback timing in Unity’s Animation window. Guitar Primary, Secondary and Special override locomotion with held body poses: **Windup uses literal sheet frame 4, Active frame 2, Recovery frame 0**. These are the AttackWindup/AttackActive/AttackRecovery clips, selected by the controller’s `AttackPhase` parameter using the ability’s existing timings. Completion/cancellation resumes normal locomotion; hitstop holds the current attack pose. Horizontal movement input flips the body sprite, retaining facing when stationary or moving vertically.


For integration, open `Lobby`, select Guitar, and attack the mic dummy. The same selected Guitar player and controller pairing continue through the cutscene into `Game`.

Tune Guitar in **`Assets/Tuning/Instruments/Guitar/GuitarTuning.asset`**. Its five foldout sections contain the shared configuration used by every Guitar player:

- **Attack**: the three windup/active/recovery timings, sweep angles, lunges, Movement Speed Multiplier, per-strike reach multipliers, combo reset/cooldown, base reach/inner radius, stretch axis/depth stretch, hit thickness, sweet-spot radius, damage, per-strike normal/sweet Hitstop Duration, impact delay and easing curves. Impact Delay is measured from the active phase starting; at that instant the full crescent hits once and appears. Its centre stays at that impact position despite the remaining lunge. Movement Speed Multiplier defaults to 0.2: normal directional movement runs at 20% speed throughout Primary's windup, active and recovery, including swings with zero lunge distance. Set it to 0 to lock walking or 1 for full speed. Live input steers only that slowed walking, preserving analog input strength. The captured forward lunge keeps its own distance, timing and easing and is added during Active. Aim remains captured, and the impact area stays at its initial impact position. Secondary has its own independently tuned Movement Speed Multiplier. The third strike defaults to 1.4× reach; its sweet-spot radius scales by the same multiplier. Depth Stretch defaults to 1.5 along world Z (Stretch Axis `(0, 1)`), elongating the hit area, inner hole and sweet-spot band together to compensate for the flat world geometry's foreshortening. Set it to 1 for a circular sector. The axis is fixed in arena space, independent of player aim and the rendering camera.
- **Secondary**: wave prefab, Movement Speed Multiplier, windup/active/recovery, cooldown, damage, next-Primary-or-Special multiplier, travel duration/range, movement/growth/fade curves, spawn offset, curved front size/angle/tip taper, collision thickness/mask, swing angle/easing and backstep distance/easing. Defaults are 0.2 s windup + 0.12 s active + 0.08 s recovery, 0.5 s cooldown, 1 wave damage and a 1.5× empowerment multiplier. Travel Duration defaults to 0.75 s and controls the projectile independently of the short casting action. Travel Curve maps normalized lifetime to normalized travel distance; its default launches quickly and slows down. Growth Curve scales the crescent radius (default 1 → 1.8), keeping line thickness steady; collision expands with the visible front. Fade Curve holds opacity through the first 65% of lifetime, then fades to zero. Range measures the wave centre's travel; Spawn Offset positions its initial front tip relative to the player. The wave inherits Attack's arena-space stretch. Normal movement runs at Secondary's Movement Speed Multiplier (default 0.2) throughout windup, active and recovery, while dodge and other abilities remain locked. Walking input and its strength stay live; the captured-direction backstep is added independently during Active, and zero-backstep casts still allow slowed walking. Finishing or cancelling restores normal walking speed. On wave release, the weapon sweeps like Primary and the player retreats opposite the captured aim. Backstep Distance defaults to 0.15 units; the motion lasts for Secondary Active and uses Backstep Easing through the shared CharacterController displacement API. Walls constrain it, and cancellation/downing stops it. The wave launches at Active's start. Recovery settles the weapon after the swing/backstep; inputs unlock after Recovery while the wave continues travelling.
- **Special**: windup/active/recovery, cooldown and impact fraction; forward distance, outer/inner radii, impact height and legacy collider-query height/mask; damage and sweet-spot multiplier; movement slowdown and hitstop; raise/hold fraction, overhead angles, grip heights, Windup Backward Offset and easing; Windup Shake Angle / Frequency control a stepped guitar-only tremble that strengthens toward release (set angle to 0 to disable); indicator colours/opacity and impact lifetime. Geometry is snapshotted at activation, and the same radii supply damage and the visible circles. The raised weapon holds behind character sprites during Windup, then uses the normal attack material for the downstroke.
- **Weapon**: grip offset, separate Resting Grip Point / Attack Grip Point in sprite-local units, Resting Angle Offset, position/rotation response, sprite angle and scale. The resting body points opposite movement facing with a configurable screen-space angular offset (Resting Angle Offset, default 20 degrees clockwise). Facing left mirrors it counterclockwise; diagonals use their left/right screen-facing half, while straight up/down use clockwise. Up faces the body down-left, right up-left, down up-right, and left up-right. Resting Grip Point starts at `(0, 0.1)` inside the grey neck; Attack Grip Point keeps the original `(0, 0.45)` outside the sprite. Windup blends between grips using the position response; the active swing uses the exact attack grip, and returning to idle blends back. Grip Offset controls the attachment position; the old Shoulder Lift direction bias is removed. Idle depth geometry follows the same smoothed visible axis; attack swing angles are unchanged. The weapon keeps its follow-through between combo strikes, then blends back to the shoulder pose. Strum uses the same swing pose implementation as Primary; its swing controls live under Secondary. While resting, `GuitarRestingWeapon.mat` draws at queue 2449 with ForwardLit depth writes off, before avatar sprites at queue 2450. This puts the entire resting guitar behind overlapping character bodies, including other players, while retaining world depth testing and floor visibility. Primary/Secondary poses and held combo follow-through use `GuitarAttackWeapon.mat` at reserved queue 2500 with depth writes off. The renderer draws these weapons after character sprites against a separate world-only depth buffer, so the entire guitar covers characters while world geometry still occludes it. Special keeps its held Windup behind characters and uses this front pass for Active/Recovery. Character-to-character depth remains unchanged; subsequent transparent passes retain character depth, which does not include front weapons. Both materials are prefab references; neither is modified at runtime. The weapon geometry stays flat on XZ for depth testing. `2DSpriteMat` projects the image toward the camera and rotates it around the configured grip; no camera-facing transform or camera-depth lift is applied.
- **VFX**: Strum body/rim/tail colours, tail length/opacity, breakup scale, flow speed and stepped fade levels, plus Primary's four pose durations (X broad slash, Y follow-through, Z broken crescent, W endpoint streaks), body/sweet-spot colors and impact colors/sizes. The `SlashSmear` child renders a flat XZ mesh with four held shapes and hard color bands. Pose 3 has a flat leading edge and torn trailing edge; the final pose keeps streaks in the latter half of the swing. The initial pose uses the exact impact centre, inner/outer radii, angles and sweet-spot radius supplied by GuitarAbilities; later poses are cosmetic afterimages and cannot hit. The shader undoes the supplied stretch before comparing distance with that same sweet-spot radius, so the color boundary follows the gameplay ellipse. Decorative edge clipping does not change the underlying sector.

Strum grants a single non-stacking empowerment on launch, even if it misses. It has no expiry and is consumed when the next Primary or Special successfully starts, applying to every target hit by that strike and multiplying sweet-spot damage too. A missed Primary or Special still spends it. Downing, disabling or cancelling the player clears it. The wave pierces enemies once each, ignores players, stops at solid world geometry and expires when its travel duration or fade finishes. Swept queries include both travel and expansion between frames. Its bold leading crescent is the hit volume; the darker fragmented tail is cosmetic. The dedicated Strum shader uses flat warm colour bands, a crisp rim, hard-edged breakup and stepped opacity, with no bloom or soft colour gradient. Already-launched waves continue if the caster is downed, and belong to the encounter scene rather than the persistent player root.

The earlier controlled DPS comparison predates the separate Secondary recovery phase. Recheck Primary-only versus alternating Strum/Primary after timing or damage changes; that earlier measurement does not validate the current cadence.

The `GuitarPlayer` prefab assigns the same tuning asset to `GuitarAbilities`, `GuitarWeaponVisual` and `GuitarVFX`. Those components keep their own runtime state; tuning the asset affects all users, while cancelling one player does not affect another. Renderer/transform wiring stays on the prefab. Create additional Guitar tuning assets through **Assets > Create > Game50033 > Tuning > Guitar** when an intentional configuration variant is needed.

The weapon sprite is imported directly from the unpadded 20×39 `Abilities/Guitar/Art/guitar2.aseprite` source at 50 pixels per unit with a centred import pivot. `GuitarWeaponVisual` holds the grey neck at rest and places the attack grip three pixels above the artwork (`Attack Grip Point = (0, 0.45)`). It uses an outward angle of −90° for the upright image. Sprite Scale defaults to 1; 50 PPU matches the test scene’s 360-pixel reference height and ortho size 3.6 before deliberate animation scaling. Defensive still returns false for Guitar. Primary, Secondary and Special do not create or consume Reverberations.
