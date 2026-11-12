namespace Game50033
{
    using UnityEngine;

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float moveSpeed = 5f;
        [SerializeField, Min(0f)] private float dodgeDistance = 1f;
        [SerializeField, Min(0.01f)] private float dodgeDuration = 0.2f;
        [SerializeField, Min(1)] private int maxDodgeCharges = 2;
        [SerializeField, Min(0.01f)] private float dodgeRechargeTime = 7f;

        private CharacterController _controller;
        private bool _initialized;
        private Vector2 _moveInput;
        private Vector3 _dodgeDirection;
        private float _dodgeElapsed;
        private float _nextChargeAt;
        private float _verticalSpeed;

        public Vector3 MoveDirection { get; private set; }
        public Vector3 FacingDirection { get; private set; } = Vector3.forward;
        public bool IsDodging { get; private set; }
        public bool IsInvulnerable => IsDodging && isActiveAndEnabled && _controller != null && _controller.enabled;
        public bool CanDodge => isActiveAndEnabled && _controller != null && _controller.enabled
            && !IsDodging && DodgeCharges > 0;
        public int DodgeCharges { get; private set; }
        public int MaxDodgeCharges => maxDodgeCharges;
        public float DodgeRechargeRemaining => DodgeCharges < maxDodgeCharges
            ? Mathf.Max(0f, _nextChargeAt - Time.time) : 0f;

        private void Awake()
        {
            Initialize();
        }

        internal void Initialize()
        {
            if (_initialized) return;

            _controller = GetComponent<CharacterController>();
            DodgeCharges = maxDodgeCharges;
            _initialized = true;
        }

        private void OnDisable()
        {
            CancelDodge();
            ReadInput(Vector2.zero);
        }

        internal void ReadInput(Vector2 input)
        {
            _moveInput = Vector2.ClampMagnitude(input, 1f);
            MoveDirection = new Vector3(_moveInput.x, 0f, _moveInput.y);
            if (MoveDirection.sqrMagnitude > 0.0001f) FacingDirection = MoveDirection.normalized;
        }

        internal bool TryDodge(bool allowed)
        {
            if (!allowed || !CanDodge) return false;

            if (DodgeCharges == maxDodgeCharges) _nextChargeAt = Time.time + dodgeRechargeTime;
            DodgeCharges--;
            _dodgeDirection = FacingDirection;
            _dodgeElapsed = 0f;
            IsDodging = true;
            return true;
        }

        internal void CancelDodge()
        {
            IsDodging = false;
        }

        internal void Tick(float deltaTime, bool canMove)
        {
            if (!isActiveAndEnabled) return;

            // spending the second charge does not restart the first charge's recovery.
            while (DodgeCharges < maxDodgeCharges && Time.time >= _nextChargeAt)
            {
                DodgeCharges++;
                _nextChargeAt += dodgeRechargeTime;
            }

            if (!_controller.enabled)
            {
                CancelDodge();
                return;
            }

            var displacement = Vector3.zero;
            if (IsDodging)
            {
                var previousProgress = Mathf.Clamp01(_dodgeElapsed / dodgeDuration);
                _dodgeElapsed += deltaTime;
                var progress = Mathf.Clamp01(_dodgeElapsed / dodgeDuration);
                var distance = dodgeDistance * (Mathf.SmoothStep(0f, 1f, progress)
                    - Mathf.SmoothStep(0f, 1f, previousProgress));
                displacement = _dodgeDirection * distance;
                if (progress >= 1f) IsDodging = false;
            }
            else if (canMove)
            {
                displacement = MoveDirection * (moveSpeed * deltaTime);
            }

            // retain grounding while horizontal actions are blocked, including while downed.
            _verticalSpeed = _controller.isGrounded ? -2f : Mathf.Max(-30f, _verticalSpeed + Physics.gravity.y * deltaTime);
            displacement.y = _verticalSpeed * deltaTime;
            _controller.Move(displacement);
        }
    }
}
