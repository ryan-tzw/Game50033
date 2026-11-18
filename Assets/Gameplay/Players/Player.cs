namespace Game50033
{
    using System;
    using UnityEngine;
    using UnityEngine.InputSystem;

    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInput), typeof(PlayerMovement), typeof(PlayerHealth))]
    [RequireComponent(typeof(PlayerHitstop))]
    public sealed class Player : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer avatarRenderer;
        [SerializeField] private PlayerAimTuning aimTuning;

        private static readonly int PlayerTintId = Shader.PropertyToID("_PlayerTint");

        private static readonly Color[] PlayerColors =
        {
            Color.HSVToRGB(0.6f, 0.6f, 0.9f),
            Color.HSVToRGB(0.0f, 0.6f, 0.9f),
            Color.HSVToRGB(0.1f, 0.6f, 0.9f),
            Color.HSVToRGB(0.3f, 0.6f, 0.9f),
        };

        private PlayerInput _playerInput;
        private InputActionAsset _boundActions;
        private InputActionAsset _ownedActions;
        private InputAction _moveAction;
        private InputAction _dodgeAction;
        private InputAction _primaryAction;
        private InputAction _secondaryAction;
        private InputAction _defensiveAction;
        private InputAction _specialAction;
        private InputAction _toggleAimModeAction;
        private bool _manualMouseAim;
        private Behaviour _autoFacingTarget;
        private float _autoFacingRefreshAt;
        private bool _ownsCursor;
        private Texture2D _cursorTexture;
        private Vector2 _cursorHotspot;
        private bool _initialized;

        public PlayerMovement Movement { get; private set; }
        public PlayerHealth Health { get; private set; }
        public PlayerHurtbox Hurtbox { get; private set; }
        public PlayerAbilities Abilities { get; private set; }
        public PlayerHitstop Hitstop { get; private set; }
        public PlayerHitReaction HitReaction { get; private set; }
        public bool IsHitStunned => HitReaction != null && HitReaction.IsHitStunned;
        public GameSession Session { get; internal set; }
        public int PlayerIndex { get; private set; } = -1;
        public Color PlayerColor { get; private set; } = Color.white;
        public CharacterDefinition Character { get; private set; }
        public Vector3 AimDirection { get; private set; } = Vector3.forward;
        public Behaviour AutoTarget { get; private set; }
        public event Action<Player, Behaviour> AutoTargetChanged;
        public bool HasActiveInput =>
            _playerInput != null && _playerInput.isActiveAndEnabled && _playerInput.inputIsActive;
        public bool SpecialInputEnabled => HasActiveInput && _specialAction != null && _specialAction.enabled;
        public bool SpecialPressedThisFrame => SpecialInputEnabled && _specialAction.WasPressedThisFrame();
        public bool SpecialHeld => SpecialInputEnabled && _specialAction.IsPressed();

        public bool CanMove => isActiveAndEnabled && Movement.isActiveAndEnabled && !Health.IsDowned && !IsHitStunned && !Hitstop.IsStopped
            && (Abilities == null || !Abilities.isActiveAndEnabled || !Abilities.BlocksMovement);
        public bool CanDodge => isActiveAndEnabled && _playerInput.inputIsActive && !Health.IsDowned && !IsHitStunned && !Hitstop.IsStopped
            && Movement.CanDodge && (Abilities == null || !Abilities.isActiveAndEnabled || !Abilities.BlocksDodge);
        public bool CanUseAbilities => isActiveAndEnabled && _playerInput.inputIsActive && !Health.IsDowned && !IsHitStunned && !Hitstop.IsStopped
            && !Movement.IsDodging && Abilities != null && Abilities.isActiveAndEnabled && !Abilities.BlocksAbilities;

        public bool CanUseDefensive => isActiveAndEnabled && _playerInput.inputIsActive && !Health.IsDowned && !IsHitStunned && !Hitstop.IsStopped
            && !Movement.IsDodging && Abilities != null && Abilities.isActiveAndEnabled && !Abilities.BlocksDefensive;

        private void Awake()
        {
            Initialize();
        }

        internal void Initialize()
        {
            if (_initialized) return;

            _playerInput = GetComponent<PlayerInput>();
            // the first PlayerInput otherwise pairs the project-wide asset itself, filtering global UI to that device.
            var projectActions = InputSystem.actions;
            if (projectActions != null && _playerInput.actions == projectActions)
            {
                var devices = new InputDevice[_playerInput.devices.Count];
                for (var i = 0; i < devices.Length; i++) devices[i] = _playerInput.devices[i];
                var scheme = _playerInput.currentControlScheme;
                var actionMap = _playerInput.currentActionMap?.name;
                var wasActive = _playerInput.inputIsActive;

                _ownedActions = Instantiate(projectActions);
                _playerInput.actions = _ownedActions;
                // replacing actions clears device pairing; restore the existing player instead of letting E join again.
                if (_playerInput.isActiveAndEnabled)
                {
                    if (!string.IsNullOrEmpty(scheme) && devices.Length > 0)
                        _playerInput.SwitchCurrentControlScheme(scheme, devices);
                    if (wasActive && !string.IsNullOrEmpty(actionMap)) _playerInput.SwitchCurrentActionMap(actionMap);
                    if (!wasActive) _playerInput.DeactivateInput();
                }
                projectActions.devices = null;
                projectActions.bindingMask = null;
                projectActions.FindActionMap("UI", false)?.Enable();
            }
            Movement = GetComponent<PlayerMovement>();
            Health = GetComponent<PlayerHealth>();
            Hurtbox = GetComponent<PlayerHurtbox>();
            Abilities = GetComponent<PlayerAbilities>();
            Hitstop = GetComponent<PlayerHitstop>();
            HitReaction = GetComponent<PlayerHitReaction>();
            if (avatarRenderer == null) avatarRenderer = GetComponentInChildren<SpriteRenderer>(true);
            Movement.Initialize();
            Health.Initialize();
            Health.Downed += HandleDowned;
            _initialized = true;
        }

        private void Update()
        {
            Hitstop.Tick(Time.deltaTime);
            // bind after player input has enabled and created its device-filtered action copy.
            if (_boundActions != _playerInput.actions)
            {
                _boundActions = _playerInput.actions;
                if (_boundActions == null) { PublishAutoTarget(null); return; }

                _moveAction = _boundActions.FindAction("Player/Move", true);
                _dodgeAction = _boundActions.FindAction("Player/Dodge", true);
                _primaryAction = _boundActions.FindAction("Player/Primary", true);
                _secondaryAction = _boundActions.FindAction("Player/Secondary", true);
                _defensiveAction = _boundActions.FindAction("Player/Defensive", true);
                _specialAction = _boundActions.FindAction("Player/Special", true);
                _toggleAimModeAction = _boundActions.FindAction("Player/ToggleAimMode", true);
            }

            if (_boundActions == null) { PublishAutoTarget(null); return; }

            // update movement facing before abilities acquire aim or dodge reads its direction.
            var acceptsInput = _playerInput.isActiveAndEnabled && _playerInput.inputIsActive && !Health.IsDowned;
            var moveInput = acceptsInput ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
            Movement.ReadInput(moveInput);

            if (acceptsInput)
            {
                if (_playerInput.currentControlScheme == "Keyboard&Mouse" && _toggleAimModeAction.WasPressedThisFrame())
                    _manualMouseAim = !_manualMouseAim;

                // dodge is press-only; ability activation is retried while held.
                if (_dodgeAction.WasPressedThisFrame()) Movement.TryDodge(CanDodge);

                var blockMouseCombat = Session != null && Session.Debug.BlocksMouseCombat;

                // defensive gets priority over held attacks, including instrument-defined cancellation windows.
                if (CanUseDefensive && _defensiveAction.IsPressed()) Abilities.TryUseDefensive();

                // each activation may change the restrictions for subsequent actions this frame.
                if (CanUseAbilities && _primaryAction.IsPressed()
                    && (!blockMouseCombat || !(_primaryAction.activeControl?.device is Mouse))) Abilities.TryUsePrimary();
                if (CanUseAbilities && _secondaryAction.IsPressed()
                    && (!blockMouseCombat || !(_secondaryAction.activeControl?.device is Mouse))) Abilities.TryUseSecondary();

                // Keep the press edge so a quick Special tap can still start charging.
                if (CanUseAbilities && (_specialAction.IsPressed() || _specialAction.WasPressedThisFrame()))
                {
                    Abilities.TryUseSpecial();
                }
            }

            // continue grounding and charge recovery while action input is blocked.
            var movementSpeedMultiplier = Abilities != null && Abilities.isActiveAndEnabled
                ? Abilities.MovementSpeedMultiplier : 1f;
            Movement.Tick(Hitstop.ActionDeltaTime, CanMove, movementSpeedMultiplier);

            Behaviour autoTarget = null;
            var aimOrigin = transform.position + Vector3.up * 0.25f;
            AimDirection = Movement.FacingDirection;
            if (TryGetManualAimDirection(aimOrigin, out var manualDirection))
            {
                AimDirection = manualDirection;
            }
            else if (HasActiveInput && !Health.IsDowned && aimTuning != null)
            {
                // follow the cached target every frame, but only search periodically or when it becomes invalid.
                // a failed search keeps the deadline too, so an empty arena cannot trigger a scan every frame.
                var validTarget = AttackTargeting.IsValidTarget(_autoFacingTarget);
                if (Time.time >= _autoFacingRefreshAt || _autoFacingTarget != null && !validTarget)
                {
                    _autoFacingTarget = AttackTargeting.FindNearestTarget(aimOrigin);
                    _autoFacingRefreshAt = Time.time + Mathf.Max(0.02f, aimTuning.autoFacingRefreshInterval);
                    validTarget = AttackTargeting.IsValidTarget(_autoFacingTarget);
                }
                if (validTarget)
                {
                    autoTarget = _autoFacingTarget;
                    var direction = AttackTargeting.GetAimPoint(_autoFacingTarget, aimOrigin) - aimOrigin;
                    direction.y = 0f;
                    if (direction.sqrMagnitude > 0.0001f) AimDirection = direction.normalized;
                }
            }

            PublishAutoTarget(autoTarget);

            // only the paired keyboard/mouse player owns the one system cursor; gamepads never overwrite it.
            if (Application.isFocused && HasActiveInput && _playerInput.currentControlScheme == "Keyboard&Mouse" && aimTuning != null)
            {
                var texture = _manualMouseAim ? aimTuning.manualCursor : aimTuning.autoCursor;
                var hotspot = texture == null ? Vector2.zero
                    : _manualMouseAim ? aimTuning.manualHotspot : aimTuning.autoHotspot;
                if (texture != null)
                {
                    hotspot.x = Mathf.Clamp(hotspot.x, 0f, texture.width - 1f);
                    hotspot.y = Mathf.Clamp(hotspot.y, 0f, texture.height - 1f);
                }
                if (!_ownsCursor || texture != _cursorTexture || hotspot != _cursorHotspot)
                {
                    Cursor.SetCursor(texture, hotspot, CursorMode.Auto);
                    _cursorTexture = texture;
                    _cursorHotspot = hotspot;
                    _ownsCursor = true;
                }
            }
            else if (_ownsCursor)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                _ownsCursor = false;
            }
        }

        // abilities request the shared defaults after their activation checks; idle facing never queries physics.
        public Vector3 GetAttackDirection() => GetAttackDirection(
            transform.position + Vector3.up * 0.25f, float.PositiveInfinity);

        public Vector3 GetAttackDirection(
            Vector3 origin,
            float range,
            int collisionMask = Physics.AllLayers,
            Collider attackVolume = null,
            ResonatingAttack resonatingAttack = null)
        {
            if (TryGetManualAimDirection(origin, out var manualDirection)) return manualDirection;

            // legacy range/collision/REV arguments remain source-compatible; they no longer limit auto-aim.
            // projectile travel and collision still belong to the individual attacks.
            var target = AttackTargeting.FindNearestTarget(origin);
            if (target != null)
            {
                var offset = AttackTargeting.GetAimPoint(target, origin) - origin;
                if (offset.sqrMagnitude > 0.0001f) return offset.normalized;
            }
            return Movement != null ? Movement.FacingDirection : Vector3.forward;
        }

        private bool TryGetManualAimDirection(Vector3 origin, out Vector3 direction)
        {
            direction = Movement != null ? Movement.FacingDirection : Vector3.forward;
            if (!HasActiveInput) return false;

            // use the current paired action copy, including after device or character reconfiguration.
            var aimAction = _playerInput.actions?.FindAction("Player/Aim", false);
            if (_playerInput.currentControlScheme == "Gamepad" && aimAction != null && aimAction.enabled)
            {
                var stick = aimAction.ReadValue<Vector2>();
                // the stick control already applies its deadzone; neutral returns to Auto, never the old aim.
                if (stick.sqrMagnitude <= 0f) return false;
                direction = new Vector3(stick.x, 0f, stick.y).normalized;
                return true;
            }
            if (_playerInput.currentControlScheme != "Keyboard&Mouse" || !_manualMouseAim) return false;

            var camera = Camera.main;
            if (camera != null && aimAction != null && aimAction.enabled)
            {
                var ray = camera.ScreenPointToRay(aimAction.ReadValue<Vector2>());
                var plane = new Plane(Vector3.up, transform.position);
                if (plane.Raycast(ray, out var distance))
                {
                    var offset = ray.GetPoint(distance) - origin;
                    offset.y = 0f;
                    if (offset.sqrMagnitude > 0.0001f) direction = offset.normalized;
                }
            }
            // an invalid manual projection still counts as Manual; never silently snap to an automatic target.
            return true;
        }

        private void PublishAutoTarget(Behaviour target)
        {
            // reference equality also notices a destroyed Unity target changing to a real null.
            if (ReferenceEquals(AutoTarget, target)) return;
            AutoTarget = target;
            AutoTargetChanged?.Invoke(this, target);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused || !_ownsCursor) return;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            _ownsCursor = false;
        }

        private void OnDisable()
        {
            PublishAutoTarget(null);
            _autoFacingTarget = null;
            _autoFacingRefreshAt = 0f;
            if (_ownsCursor) Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            _ownsCursor = false;
            if (Hitstop != null) Hitstop.Clear();
            HitReaction?.Clear();
            Health?.ClearPostHitInvulnerability();
            Movement.CancelDodge();
            Movement.CancelDisplacement();
            Movement.ReadInput(Vector2.zero);
            if (Abilities != null) Abilities.Cancel();
        }

        private void OnDestroy()
        {
            if (_ownedActions != null) Destroy(_ownedActions);
            if (Health != null) Health.Downed -= HandleDowned;
            if (Session != null) Session.UnregisterPlayer(this);
        }

        private void HandleDowned()
        {
            PublishAutoTarget(null);
            Hitstop.Clear();
            Movement.CancelDodge();
            Movement.CancelDisplacement();
            Movement.ReadInput(Vector2.zero);
            if (Abilities != null) Abilities.Cancel();
        }

        // called by the scene's GameManager at an encounter boundary (fight start, return to lobby).
        public void ResetForEncounter()
        {
            Initialize();
            PublishAutoTarget(null);
            Hitstop.Clear();
            Movement.ResetForEncounter();
            HitReaction?.Clear();
            Health.ResetForEncounter();
            if (Abilities != null) Abilities.ResetForEncounter();
        }

        public void ConfigurePlayer(int playerIndex)
        {
            gameObject.name = $"Player {playerIndex + 1}";
            PlayerIndex = playerIndex;
            PlayerColor = PlayerColors[playerIndex % PlayerColors.Length];
            SetTint(Character != null ? Character.Tint : PlayerColor);
        }

        public void ApplyCharacter(CharacterDefinition character)
        {
            Character = character;
            if (character == null) return;

            if (avatarRenderer != null && character.Sprite != null) avatarRenderer.sprite = character.Sprite;

            SetTint(character.Tint);
        }

        private void SetTint(Color color)
        {
            if (avatarRenderer == null) return;

            var materialProperties = new MaterialPropertyBlock();
            avatarRenderer.GetPropertyBlock(materialProperties);
            materialProperties.SetColor(PlayerTintId, color);
            avatarRenderer.SetPropertyBlock(materialProperties);
        }
    }
}
