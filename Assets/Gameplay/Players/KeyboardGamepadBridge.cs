namespace Game50033
{
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.InputSystem.LowLevel;

    /// <summary>
    /// Helper which maps keyboard inputs to gamepad inputs, so that we can test it out solo without needing any additional controllers (for ease of dev)
    /// </summary>
    [DefaultExecutionOrder(-800)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInputManager))]
    [AddComponentMenu("Game50033/Development/Keyboard Gamepad Bridge")]
    public sealed class KeyboardGamepadBridge : MonoBehaviour
    {
        [Header("Flute Movement")]
        [SerializeField] private Key moveUp = Key.UpArrow;
        [SerializeField] private Key moveDown = Key.DownArrow;
        [SerializeField] private Key moveLeft = Key.LeftArrow;
        [SerializeField] private Key moveRight = Key.RightArrow;

        [Header("Flute Aim")]
        [SerializeField] private Key aimUp = Key.I;
        [SerializeField] private Key aimDown = Key.K;
        [SerializeField] private Key aimLeft = Key.J;
        [SerializeField] private Key aimRight = Key.L;

        [Header("Flute Buttons")]
        [SerializeField] private Key primaryKey = Key.H;
        [SerializeField] private Key secondaryKey = Key.U;
        [SerializeField] private Key specialKey = Key.Enter;
        [SerializeField] private Key defensiveKey = Key.O;
        [SerializeField] private Key dashKey = Key.RightShift;

        private PlayerInputManager _manager;
        private Gamepad _gamepad;
        private PlayerInput _keyboardPlayer;
        private PlayerInput _virtualPlayer;
        private InputActionAsset _originalActions;
        private InputActionAsset _demoActions;
        private bool _focused;
        private bool _paused;
        private bool _waitingForRelease;

        private void OnEnable()
        {
            if (!Application.isEditor && !Debug.isDebugBuild)
            {
                enabled = false;
                return;
            }

            _manager = GetComponent<PlayerInputManager>();
            _focused = Application.isFocused;
            _paused = false;
            _waitingForRelease = true;

            // Give Player 2 its own input device while Player 1 keeps the keyboard and mouse.
            _gamepad = InputSystem.AddDevice<Gamepad>("KeyboardDemoGamepad");
            if (_virtualPlayer != null && _virtualPlayer.isActiveAndEnabled)
            {
                _virtualPlayer.SwitchCurrentControlScheme("Gamepad", _gamepad);
            }

            _manager.onPlayerJoined += HandlePlayerJoined;
            _manager.onPlayerLeft += HandlePlayerLeft;
            InputSystem.onAfterUpdate += UpdateGamepad;
            FindKeyboardPlayer();
        }

        private void OnDisable()
        {
            InputSystem.onAfterUpdate -= UpdateGamepad;
            if (_manager != null)
            {
                _manager.onPlayerJoined -= HandlePlayerJoined;
                _manager.onPlayerLeft -= HandlePlayerLeft;
            }

            CancelVirtualAbilities();
            if (_gamepad != null && _gamepad.added)
            {
                InputState.Change(_gamepad, new GamepadState());
                InputSystem.RemoveDevice(_gamepad);
            }

            _gamepad = null;
            RestoreKeyboardActions();
        }

        private void HandlePlayerJoined(PlayerInput playerInput)
        {
            foreach (var device in playerInput.devices)
            {
                if (device == _gamepad)
                {
                    _virtualPlayer = playerInput;
                }
            }

            if (_keyboardPlayer != null || playerInput.actions == null)
            {
                return;
            }

            foreach (var device in playerInput.devices)
            {
                if (!(device is Keyboard keyboard))
                {
                    continue;
                }

                _keyboardPlayer = playerInput;
                _originalActions = playerInput.actions;

                // Override a runtime copy so the shared Input Action asset stays unchanged.
                _demoActions = Instantiate(_originalActions);
                _demoActions.name = _originalActions.name + " (Keyboard Demo)";
                _demoActions.LoadBindingOverridesFromJson(_originalActions.SaveBindingOverridesAsJson());

                var move = _demoActions.FindAction("Player/Move", true);
                for (var i = 0; i < move.bindings.Count; i++)
                {
                    var path = move.bindings[i].effectivePath;
                    if (Matches(path, keyboard, moveUp) || Matches(path, keyboard, moveDown)
                        || Matches(path, keyboard, moveLeft) || Matches(path, keyboard, moveRight))
                    {
                        move.ApplyBindingOverride(i, string.Empty);
                    }
                }

                AssignActions(playerInput, _demoActions);
                return;
            }
        }

        private void HandlePlayerLeft(PlayerInput playerInput)
        {
            if (playerInput == _virtualPlayer)
            {
                _virtualPlayer = null;
            }

            if (playerInput == _keyboardPlayer)
            {
                RestoreKeyboardActions();
            }
        }

        private void FindKeyboardPlayer()
        {
            foreach (var playerInput in PlayerInput.all)
            {
                HandlePlayerJoined(playerInput);
                if (_keyboardPlayer != null)
                {
                    return;
                }
            }
        }

        private void UpdateGamepad()
        {
            var updateType = InputState.currentUpdateType;
            if (updateType != InputUpdateType.Dynamic && updateType != InputUpdateType.Fixed)
            {
                return;
            }

            if (_gamepad == null || !_gamepad.added)
            {
                return;
            }

            if (_keyboardPlayer == null)
            {
                FindKeyboardPlayer();
            }

            var state = new GamepadState();
            var keyboard = Keyboard.current;
            if (keyboard == null || _keyboardPlayer == null)
            {
                _waitingForRelease = true;
                CancelVirtualAbilities();
            }

            if (_focused && !_paused && keyboard != null && _keyboardPlayer != null)
            {
                // Require a fresh press after joining, refocusing or resuming the demo.
                if (_waitingForRelease && !AnyDemoKeyPressed(keyboard))
                {
                    _waitingForRelease = false;
                }

                if (!_waitingForRelease)
                {
                    state.leftStick = ReadStick(keyboard, moveLeft, moveRight, moveDown, moveUp);
                    state.rightStick = ReadStick(keyboard, aimLeft, aimRight, aimDown, aimUp);
                    state = state.WithButton(GamepadButton.West, ButtonPressed(keyboard, primaryKey))
                        .WithButton(GamepadButton.North, ButtonPressed(keyboard, secondaryKey))
                        .WithButton(GamepadButton.South, ButtonPressed(keyboard, specialKey))
                        .WithButton(GamepadButton.East, ButtonPressed(keyboard, defensiveKey));
                    state.rightTrigger = ButtonPressed(keyboard, dashKey) ? 1f : 0f;
                }
            }

            InputState.Change(_gamepad, state, updateType);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            _focused = hasFocus;
            if (!hasFocus)
            {
                SuspendInput();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            _paused = paused;
            if (paused)
            {
                SuspendInput();
            }
        }

        private void SuspendInput()
        {
            _waitingForRelease = true;
            CancelVirtualAbilities();
            if (_gamepad != null && _gamepad.added)
            {
                InputState.Change(_gamepad, new GamepadState());
            }
        }

        private void CancelVirtualAbilities()
        {
            if (_gamepad == null)
            {
                return;
            }

            foreach (var playerInput in PlayerInput.all)
            {
                foreach (var device in playerInput.devices)
                {
                    if (device == _gamepad && playerInput.TryGetComponent(out PlayerAbilities abilities))
                    {
                        abilities.Cancel();
                    }
                }
            }
        }

        private void RestoreKeyboardActions()
        {
            // Restore the original bindings when the demo bridge is disabled or Player 1 leaves.
            if (_keyboardPlayer != null && _keyboardPlayer.actions == _demoActions)
            {
                AssignActions(_keyboardPlayer, _originalActions);
            }

            if (_demoActions != null)
            {
                Destroy(_demoActions);
            }

            _keyboardPlayer = null;
            _originalActions = null;
            _demoActions = null;
        }

        private static void AssignActions(PlayerInput playerInput, InputActionAsset actions)
        {
            var devices = new InputDevice[playerInput.devices.Count];
            for (var i = 0; i < devices.Length; i++)
            {
                devices[i] = playerInput.devices[i];
            }

            var scheme = playerInput.currentControlScheme;
            var actionMap = playerInput.currentActionMap?.name;
            var wasActive = playerInput.inputIsActive;

            if (playerInput.TryGetComponent(out PlayerAbilities abilities))
            {
                abilities.Cancel();
            }

            playerInput.actions = actions;
            if (!playerInput.isActiveAndEnabled)
            {
                return;
            }

            // Preserve device pairing and the active map when swapping the runtime actions.
            if (!string.IsNullOrEmpty(scheme))
            {
                playerInput.SwitchCurrentControlScheme(scheme, devices);
            }

            if (wasActive && !string.IsNullOrEmpty(actionMap))
            {
                playerInput.SwitchCurrentActionMap(actionMap);
            }

            if (!wasActive)
            {
                playerInput.DeactivateInput();
            }
        }

        private static bool Matches(string path, Keyboard keyboard, Key key)
        {
            return key != Key.None
                && !string.IsNullOrEmpty(path)
                && InputControlPath.Matches(path, keyboard[key]);
        }

        private static bool Pressed(Keyboard keyboard, Key key)
        {
            return key != Key.None && keyboard[key].isPressed;
        }

        private static bool ButtonPressed(Keyboard keyboard, Key key)
        {
            return key != Key.None && (keyboard[key].isPressed || keyboard[key].wasPressedThisFrame);
        }

        private static Vector2 ReadStick(Keyboard keyboard, Key left, Key right, Key down, Key up)
        {
            return Vector2.ClampMagnitude(
                new Vector2(
                    (Pressed(keyboard, right) ? 1f : 0f) - (Pressed(keyboard, left) ? 1f : 0f),
                    (Pressed(keyboard, up) ? 1f : 0f) - (Pressed(keyboard, down) ? 1f : 0f)),
                1f);
        }

        private bool AnyDemoKeyPressed(Keyboard keyboard)
        {
            return Pressed(keyboard, moveUp) || Pressed(keyboard, moveDown)
                || Pressed(keyboard, moveLeft) || Pressed(keyboard, moveRight)
                || Pressed(keyboard, aimUp) || Pressed(keyboard, aimDown)
                || Pressed(keyboard, aimLeft) || Pressed(keyboard, aimRight)
                || Pressed(keyboard, primaryKey) || Pressed(keyboard, secondaryKey)
                || Pressed(keyboard, specialKey) || Pressed(keyboard, defensiveKey)
                || Pressed(keyboard, dashKey);
        }
    }
}
