namespace Game50033
{
    using UnityEngine;
    using UnityEngine.InputSystem;

    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInput), typeof(PlayerMovement), typeof(PlayerHealth))]
    public sealed class Player : MonoBehaviour
    {
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
        private InputAction _moveAction;
        private InputAction _aimAction;
        private InputAction _dodgeAction;
        private InputAction _primaryAction;
        private InputAction _secondaryAction;
        private InputAction _defensiveAction;
        private InputAction _specialAction;
        private Camera _aimCamera;
        private bool _initialized;

        public PlayerMovement Movement { get; private set; }
        public PlayerHealth Health { get; private set; }
        public PlayerAbilities Abilities { get; private set; }
        public GameSession Session { get; internal set; }
        public int PlayerIndex { get; private set; } = -1;
        public Color PlayerColor { get; private set; } = Color.white;
        public Vector3 AimDirection { get; private set; } = Vector3.forward;

        public bool CanMove => isActiveAndEnabled && Movement.isActiveAndEnabled && !Health.IsDowned
            && (Abilities == null || !Abilities.isActiveAndEnabled || !Abilities.BlocksMovement);
        public bool CanDodge => isActiveAndEnabled && _playerInput.inputIsActive && !Health.IsDowned
            && Movement.CanDodge && (Abilities == null || !Abilities.isActiveAndEnabled || !Abilities.BlocksDodge);
        public bool CanUseAbilities => isActiveAndEnabled && _playerInput.inputIsActive && !Health.IsDowned
            && !Movement.IsDodging && Abilities != null && Abilities.isActiveAndEnabled && !Abilities.BlocksAbilities;

        private void Awake()
        {
            Initialize();
        }

        internal void Initialize()
        {
            if (_initialized) return;

            _playerInput = GetComponent<PlayerInput>();
            Movement = GetComponent<PlayerMovement>();
            Health = GetComponent<PlayerHealth>();
            Abilities = GetComponent<PlayerAbilities>();
            Movement.Initialize();
            Health.Initialize();
            Health.Downed += HandleDowned;
            _initialized = true;
        }

        private void Update()
        {
            // bind after player input has enabled and created its device-filtered action copy.
            if (_boundActions != _playerInput.actions)
            {
                _boundActions = _playerInput.actions;
                if (_boundActions == null) return;

                _moveAction = _boundActions.FindAction("Player/Move", true);
                _aimAction = _boundActions.FindAction("Player/Aim", true);
                _dodgeAction = _boundActions.FindAction("Player/Dodge", true);
                _primaryAction = _boundActions.FindAction("Player/Primary", true);
                _secondaryAction = _boundActions.FindAction("Player/Secondary", true);
                _defensiveAction = _boundActions.FindAction("Player/Defensive", true);
                _specialAction = _boundActions.FindAction("Player/Special", true);
            }

            if (_boundActions == null) return;

            // update movement facing before resolving aim and dodge direction.
            var acceptsInput = _playerInput.isActiveAndEnabled && _playerInput.inputIsActive && !Health.IsDowned;
            var moveInput = acceptsInput ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
            Movement.ReadInput(moveInput);
            AimDirection = Movement.FacingDirection;

            if (acceptsInput)
            {
                // mouse aim intersects the ground plane; a released aim stick falls back to movement facing.
                var aimInput = _aimAction.ReadValue<Vector2>();
                if (_playerInput.currentControlScheme == "Keyboard&Mouse")
                {
                    if (_aimCamera == null) _aimCamera = Camera.main;
                    if (_aimCamera != null)
                    {
                        var ray = _aimCamera.ScreenPointToRay(aimInput);
                        var ground = new Plane(Vector3.up, Vector3.zero);
                        if (ground.Raycast(ray, out var distance))
                        {
                            var offset = ray.GetPoint(distance) - transform.position;
                            offset.y = 0f;
                            if (offset.sqrMagnitude > 0.0001f) AimDirection = offset.normalized;
                        }
                    }
                }
                else if (aimInput.sqrMagnitude > 0.0001f)
                {
                    AimDirection = new Vector3(aimInput.x, 0f, aimInput.y).normalized;
                }

                // dodge is press-only; ability activation is retried while held.
                if (_dodgeAction.WasPressedThisFrame()) Movement.TryDodge(CanDodge);

                // each activation may change the restrictions for subsequent actions this frame.
                if (CanUseAbilities && _primaryAction.IsPressed()) Abilities.TryUsePrimary();
                if (CanUseAbilities && _secondaryAction.IsPressed()) Abilities.TryUseSecondary();
                if (CanUseAbilities && _defensiveAction.IsPressed()) Abilities.TryUseDefensive();
                if (CanUseAbilities && _specialAction.IsPressed()) Abilities.TryUseSpecial();
            }

            // continue grounding and charge recovery while action input is blocked.
            Movement.Tick(Time.deltaTime, CanMove);
        }

        private void OnDisable()
        {
            Movement.CancelDodge();
            Movement.ReadInput(Vector2.zero);
            if (Abilities != null) Abilities.Cancel();
        }

        private void OnDestroy()
        {
            if (Health != null) Health.Downed -= HandleDowned;
            if (Session != null) Session.UnregisterPlayer(this);
        }

        private void HandleDowned()
        {
            Movement.CancelDodge();
            Movement.ReadInput(Vector2.zero);
            if (Abilities != null) Abilities.Cancel();
        }

        public void ConfigurePlayer(int playerIndex)
        {
            gameObject.name = $"Player {playerIndex + 1}";
            PlayerIndex = playerIndex;
            PlayerColor = PlayerColors[playerIndex % PlayerColors.Length];
            var materialProperties = new MaterialPropertyBlock();

            foreach (var childRenderer in GetComponentsInChildren<Renderer>(true))
            {
                childRenderer.GetPropertyBlock(materialProperties);
                materialProperties.SetColor(PlayerTintId, PlayerColor);
                childRenderer.SetPropertyBlock(materialProperties);
                materialProperties.Clear();
            }
        }
    }
}
