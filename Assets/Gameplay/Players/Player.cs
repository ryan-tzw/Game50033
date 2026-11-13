namespace Game50033
{
    using UnityEngine;
    using UnityEngine.InputSystem;

    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInput), typeof(PlayerMovement), typeof(PlayerHealth))]
    public sealed class Player : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer avatarRenderer;
        [SerializeField, Min(0.01f)] private float autoAimRange = 14f;
        [Tooltip("Non-player IHittable targets and boss prototypes on these layers can be selected automatically.")]
        [SerializeField] private LayerMask enemyTargetMask = ~0;

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
        private InputAction _dodgeAction;
        private InputAction _primaryAction;
        private InputAction _secondaryAction;
        private InputAction _defensiveAction;
        private InputAction _specialAction;
        private bool _initialized;

        public PlayerMovement Movement { get; private set; }
        public PlayerHealth Health { get; private set; }
        public PlayerAbilities Abilities { get; private set; }
        public GameSession Session { get; internal set; }
        public int PlayerIndex { get; private set; } = -1;
        public Color PlayerColor { get; private set; } = Color.white;
        public CharacterDefinition Character { get; private set; }
        public Vector3 AimDirection { get; private set; } = Vector3.forward;
        public bool HasActiveInput =>
            _playerInput != null && _playerInput.isActiveAndEnabled && _playerInput.inputIsActive;
        public bool SpecialInputEnabled => HasActiveInput && _specialAction != null && _specialAction.enabled;
        public bool SpecialPressedThisFrame => SpecialInputEnabled && _specialAction.WasPressedThisFrame();
        public bool SpecialHeld => SpecialInputEnabled && _specialAction.IsPressed();

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
            if (avatarRenderer == null) avatarRenderer = GetComponentInChildren<SpriteRenderer>(true);
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
                AimDirection = GetAttackDirection(
                    transform.position + Vector3.up * 0.25f,
                    autoAimRange,
                    Physics.AllLayers);

                // dodge is press-only; ability activation is retried while held.
                if (_dodgeAction.WasPressedThisFrame()) Movement.TryDodge(CanDodge);

                // each activation may change the restrictions for subsequent actions this frame.
                if (CanUseAbilities && _primaryAction.IsPressed()) Abilities.TryUsePrimary();
                if (CanUseAbilities && _secondaryAction.IsPressed()) Abilities.TryUseSecondary();
                if (CanUseAbilities && _defensiveAction.IsPressed()) Abilities.TryUseDefensive();

                // Keep the press edge so a quick Special tap can still start charging.
                if (CanUseAbilities && (_specialAction.IsPressed() || _specialAction.WasPressedThisFrame()))
                {
                    Abilities.TryUseSpecial();
                }
            }

            // continue grounding and charge recovery while action input is blocked.
            Movement.Tick(Time.deltaTime, CanMove);
        }

        public Vector3 GetAttackDirection(
            Vector3 origin,
            float range,
            int collisionMask = Physics.AllLayers,
            Collider attackVolume = null,
            ResonatingAttack resonatingAttack = null)
        {
            var target = AttackTargeting.FindTarget(
                this,
                origin,
                Mathf.Min(range, autoAimRange),
                enemyTargetMask,
                collisionMask,
                attackVolume,
                resonatingAttack);

            if (target != null)
            {
                var offset = AttackTargeting.GetAimPoint(target, origin) - origin;
                if (offset.sqrMagnitude > 0.0001f)
                {
                    return offset.normalized;
                }
            }

            return Movement != null ? Movement.FacingDirection : Vector3.forward;
        }

        private void OnDisable()
        {
            Movement.CancelDodge();
            Movement.CancelDisplacement();
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
            Movement.CancelDisplacement();
            Movement.ReadInput(Vector2.zero);
            if (Abilities != null) Abilities.Cancel();
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
