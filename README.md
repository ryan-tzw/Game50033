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
| Aim | Mouse position | Right stick | Right stick | Right stick | Right stick | Not implemented |
| Primary attack | Left click | Left face button | X | □ | Y | Placeholder attack (lobby dummy) |
| Secondary attack | Right click | Top face button | Y | △ | X | Not implemented |
| Special attack | E | Bottom face button | A | ✕ | B | Not implemented (used as **join / pick** in the lobby) |
| Defensive | Q | Right face button | B | ○ | A | Not implemented (used as **back** in the lobby) |
| Dash | Space | Right trigger | RT | R2 | ZR | Not implemented |

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
| `Assets/Gameplay/Players` | Player prefab, movement (`PlayerController`), joining (`PlayerJoinManager`), lobby character select (`PlayerCharacterSelect`) |
| `Assets/Gameplay/Lobby` | Lobby state, select panels, start zone, lobby → cutscene handoff (`StartGameSequence`) |
| `Assets/Gameplay/Characters` | `CharacterDefinition` / `CharacterRoster` and their data assets (`Data/`) |
| `Assets/Gameplay/Combat` | `IHittable` (shared "can be hit" interface), mic training dummy, placeholder `PlayerCombat` |
| `Assets/Gameplay/Flow` | Scene names, placeholder cutscene (`PlaceholderCutscene`) |
| `Assets/Gameplay/Game` | Game scene entry: puts carried-over players on spawn points and gives them control |
| `Assets/Rendering` | Pixel-art renderer feature and sprite / toon / dissolve shaders |
| `Assets/Settings` | URP assets and the input actions |
