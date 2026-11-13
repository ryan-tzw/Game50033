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
| `Assets/Gameplay/Players/Abilities/Guitar` | Guitar Primary combo, Strum/empowerment, weapon/VFX, prefabs, tuning types and art |
| `Assets/Gameplay/Flow` | Scene names, placeholder cutscene (`PlaceholderCutscene`) |
| `Assets/Gameplay/Game` | Game scene entry: puts carried-over players on spawn points and gives them control |
| `Assets/Rendering` | Pixel-art renderer feature and sprite / toon / dissolve shaders |
| `Assets/Settings` | URP assets and the input actions |

## 5. Testing Guitar Primary and Secondary

Open `Assets/Scenes/TestScenes/GuitarPrimaryTest.unity` and press Play. Join with E or the bottom gamepad face button. The first/third player uses Guitar; the second/fourth uses Flute. Move toward a dummy and hold Primary to cycle through the three sweeps. Primary slows normal movement to 20% throughout windup, active and recovery, while keeping dodge and other abilities locked. Its original forward lunge is added during Active; full walking speed returns after recovery. Moving closer gives normal hits; placing the target toward the outer part of the arc gives sweet-spot damage. Default damage is 2 normally and 3 in the sweet spot; dummies display the latest hit and total damage. The wall on the right can be used to check lunge collision and Strum blocking. Right-click / press the top face button to Strum. Alternate Secondary and Primary to play `S → P1 → S → P2 → S → P3`: Strum refreshes the combo continuation window without changing its step. Holding an ability retries it; holding both still gives Primary the existing input priority.

The three test dummies opt into camera-projected hurtboxes for **Guitar Primary**. Enable **Gizmos** in each viewport (or toggle the `CombatHurtbox` entry in its Gizmos menu): the **Scene view shows the yellow 3D capsule**, while the **Game view shows its cyan projected outline over the artwork** when **Show Game View Preview** is checked in the hurtbox tuning asset. That checkbox defaults to off and controls all hurtboxes using the asset; the Game view's master Gizmos toggle must also be enabled. It does not affect the Scene-view preview or damage. Object selection is not required. These Editor previews do not appear in builds or change hit acceptance. A disabled hurtbox has no preview and cannot receive Primary hits.

Tune all three through `Assets/Tuning/Combat/GuitarTestDummyHurtbox.asset`: **Center** is a 3D local-space offset, **Radius** sets the round cross-section, and **Height** is the total capsule height along local Y, including both rounded ends. The optional Anchor supplies position, rotation and scale; otherwise the component uses its own transform. Height scales along local Y, radius uses the larger absolute X/Z scale, and the capsule becomes a sphere when height is less than its diameter. The dummy defaults (radius 0.5, height 2, centre zero) follow their existing scaled capsule geometry. Adjust the asset while checking both previews to match the vulnerable visible body, including camera-facing sprite projection. The same world-space endpoints/radius supply both previews and combat queries; the Game-view preview has no independent shape settings. These are authored combat volumes, not pixel-perfect sprite outlines.

At Primary's initial impact, the attack compares the capsule and the initial crescent in the fixed orthographic camera's view. Apparent overlap can hit even when their world-space shapes are separated in depth. A hurtbox overlapping both the normal and sweet bands takes normal damage; only overlap confined to the outer band gets sweet-spot damage. The initial crescent's inner hole, angular edges and stretched reach still apply. Later smear poses cannot hit. Hurtboxes do not change physical world collision, and disabling one stops projected Primary hits rather than restoring the object's world collider as a damage shape. Targets without `CombatHurtbox` keep existing world-space detection. Secondary, other instruments and REV/RES still use their existing world-space queries; migration is separate. Projected queries do not provide wall occlusion and require a downward-facing orthographic gameplay camera.

For integration, open `Lobby`, select Guitar, and attack the mic dummy. The same selected Guitar player and controller pairing continue through the cutscene into `Game`.

Tune Guitar in **`Assets/Tuning/Instruments/Guitar/GuitarTuning.asset`**. Its four foldout sections contain the shared configuration used by every Guitar player:

- **Attack**: the three windup/active/recovery timings, sweep angles, lunges, Movement Speed Multiplier, per-strike reach multipliers, combo reset/cooldown, base reach/inner radius, stretch axis/depth stretch, hit thickness, sweet-spot radius, damage, impact delay and easing curves. Impact Delay is measured from the active phase starting; at that instant the full crescent hits once and appears. Its centre stays at that impact position despite the remaining lunge. Movement Speed Multiplier defaults to 0.2: normal directional movement runs at 20% speed throughout Primary's windup, active and recovery, including swings with zero lunge distance. Set it to 0 to lock walking or 1 for full speed. Live input steers only that slowed walking, preserving analog input strength. The captured forward lunge keeps its own distance, timing and easing and is added during Active. Aim remains captured, and the impact area stays at its initial impact position. Secondary has its own independently tuned Movement Speed Multiplier. The third strike defaults to 1.4× reach; its sweet-spot radius scales by the same multiplier. Depth Stretch defaults to 1.5 along world Z (Stretch Axis `(0, 1)`), elongating the hit area, inner hole and sweet-spot band together to compensate for the flat world geometry's foreshortening. Set it to 1 for a circular sector. The axis is fixed in arena space, independent of player aim and the rendering camera.
- **Secondary**: wave prefab, Movement Speed Multiplier, windup/active/recovery, cooldown, damage, next-Primary multiplier, travel duration/range, movement/growth/fade curves, spawn offset, curved front size/angle/tip taper, collision thickness/mask, swing angle/easing and backstep distance/easing. Defaults are 0.2 s windup + 0.12 s active + 0.08 s recovery, 0.5 s cooldown, 1 wave damage and a 1.5× Primary multiplier. Travel Duration defaults to 0.75 s and controls the projectile independently of the short casting action. Travel Curve maps normalized lifetime to normalized travel distance; its default launches quickly and slows down. Growth Curve scales the crescent radius (default 1 → 1.8), keeping line thickness steady; collision expands with the visible front. Fade Curve holds opacity through the first 65% of lifetime, then fades to zero. Range measures the wave centre's travel; Spawn Offset positions its initial front tip relative to the player. The wave inherits Attack's arena-space stretch. Normal movement runs at Secondary's Movement Speed Multiplier (default 0.2) throughout windup, active and recovery, while dodge and other abilities remain locked. Walking input and its strength stay live; the captured-direction backstep is added independently during Active, and zero-backstep casts still allow slowed walking. Finishing or cancelling restores normal walking speed. On wave release, the weapon sweeps like Primary and the player retreats opposite the captured aim. Backstep Distance defaults to 0.15 units; the motion lasts for Secondary Active and uses Backstep Easing through the shared CharacterController displacement API. Walls constrain it, and cancellation/downing stops it. The wave launches at Active's start. Recovery settles the weapon after the swing/backstep; inputs unlock after Recovery while the wave continues travelling.
- **Weapon**: grip offset, sprite-local grip point, shoulder lift, position/rotation response, sprite angle and scale. The resting body points behind movement facing; the grip point can sit outside the sprite. The weapon keeps its follow-through between combo strikes, then blends back to the shoulder pose. Strum uses the same swing pose implementation as Primary; its swing controls live under Secondary. The weapon geometry stays flat on XZ for depth testing. `2DSpriteMat` projects the image toward the camera and rotates it around the configured grip; no camera-facing transform or camera-depth lift is applied.
- **VFX**: Strum body/rim/tail colours, tail length/opacity, breakup scale, flow speed and stepped fade levels, plus Primary's four pose durations (X broad slash, Y follow-through, Z broken crescent, W endpoint streaks), body/sweet-spot colors and impact colors/sizes. The `SlashSmear` child renders a flat XZ mesh with four held shapes and hard color bands. Pose 3 has a flat leading edge and torn trailing edge; the final pose keeps streaks in the latter half of the swing. The initial pose uses the exact impact centre, inner/outer radii, angles and sweet-spot radius supplied by GuitarAbilities; later poses are cosmetic afterimages and cannot hit. The shader undoes the supplied stretch before comparing distance with that same sweet-spot radius, so the color boundary follows the gameplay ellipse. Decorative edge clipping does not change the underlying sector.

Strum grants a single non-stacking empowerment on launch, even if it misses. It has no expiry and is consumed when the next Primary successfully starts, applying to every target hit by that strike and multiplying sweet-spot damage too. A missed Primary still spends it. Downing, disabling or cancelling the player clears it. The wave pierces enemies once each, ignores players, stops at solid world geometry and expires when its travel duration or fade finishes. Swept queries include both travel and expansion between frames. Its bold leading crescent is the hit volume; the darker fragmented tail is cosmetic. The dedicated Strum shader uses flat warm colour bands, a crisp rim, hard-edged breakup and stepped opacity, with no bloom or soft colour gradient. Already-launched waves continue if the caster is downed, and belong to the encounter scene rather than the persistent player root.

The earlier controlled DPS comparison predates the separate Secondary recovery phase. Recheck Primary-only versus alternating Strum/Primary after timing or damage changes; that earlier measurement does not validate the current cadence.

The `GuitarPlayer` prefab assigns the same tuning asset to `GuitarAbilities`, `GuitarWeaponVisual` and `GuitarVFX`. Those components keep their own runtime state; tuning the asset affects all users, while cancelling one player does not affect another. Renderer/transform wiring stays on the prefab. Create additional Guitar tuning assets through **Assets > Create > Game50033 > Tuning > Guitar** when an intentional configuration variant is needed.

The weapon sprite is imported directly from the unpadded 20×39 `Abilities/Guitar/Art/guitar2.aseprite` source at 50 pixels per unit with a centred import pivot. `GuitarWeaponVisual` places its external grip three pixels above the artwork (`Sprite Grip Point = (0, 0.45)`) and uses an outward angle of −90° for the upright image. Sprite Scale defaults to 1; 50 PPU matches the test scene’s 360-pixel reference height and ortho size 3.6 before deliberate animation scaling. Defensive and Special still return false for Guitar. Neither Primary nor Secondary creates or consumes Reverberations.
