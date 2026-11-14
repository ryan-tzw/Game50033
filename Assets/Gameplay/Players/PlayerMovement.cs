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
        private PlayerHitstop _hitstop;
        private bool _initialized;
        private Vector2 _moveInput;
        private Vector3 _dodgeDirection;
        private float _dodgeElapsed;
        private float _nextChargeAt;
        private float _verticalSpeed;
        private Vector3 _displacementDirection;
        private float _displacementDistance;
        private float _displacementDuration;
        private float _displacementElapsed;
        private float _displacementStartsAt;
        private AnimationCurve _displacementCurve;
        private float _curveStart;
        private float _curveSpan;

        public Vector3 MoveDirection { get; private set; }
        public Vector3 FacingDirection { get; private set; } = Vector3.forward;
        public bool IsDodging { get; private set; }
        public bool IsDisplacing { get; private set; }
        public bool IsInvulnerable => IsDodging && isActiveAndEnabled && _controller != null && _controller.enabled;
        public bool CanDodge => isActiveAndEnabled && _controller != null && _controller.enabled
            && !IsDodging && !IsDisplacing && DodgeCharges > 0 && (_hitstop == null || !_hitstop.IsStopped);
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
            _hitstop = GetComponent<PlayerHitstop>();
            DodgeCharges = maxDodgeCharges;
            _initialized = true;
        }

        private void OnDisable()
        {
            CancelDodge();
            CancelDisplacement();
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

        public bool TryStartDisplacement(Vector3 direction, float distance, float duration, AnimationCurve easing, float delay = 0f)
        {
            direction.y = 0f;
            if (!isActiveAndEnabled || _controller == null || !_controller.enabled
                || _hitstop != null && _hitstop.IsStopped
                || IsDodging || IsDisplacing || direction.sqrMagnitude < 0.0001f || duration <= 0f) return false;

            _displacementDirection = direction.normalized;
            _displacementDistance = distance;
            _displacementDuration = duration;
            _displacementElapsed = 0f;
            _displacementStartsAt = (_hitstop != null ? _hitstop.ActionTime : Time.time) + Mathf.Max(0f, delay);
            _displacementCurve = easing;
            _curveStart = easing != null ? easing.Evaluate(0f) : 0f;
            _curveSpan = easing != null ? easing.Evaluate(1f) - _curveStart : 0f;
            IsDisplacing = true;
            return true;
        }

        public void CancelDisplacement()
        {
            IsDisplacing = false;
        }

        internal void Tick(float deltaTime, bool canMove, float movementSpeedMultiplier = 1f)
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
                CancelDisplacement();
                return;
            }
            // recharge uses world time above, but a frozen player must not move or depenetrate.
            if (_hitstop != null && _hitstop.IsStopped) return;

            var displacement = Vector3.zero;
            var dodging = IsDodging;
            if (dodging)
            {
                var previousProgress = Mathf.Clamp01(_dodgeElapsed / dodgeDuration);
                _dodgeElapsed += deltaTime;
                var progress = Mathf.Clamp01(_dodgeElapsed / dodgeDuration);
                var distance = dodgeDistance * (Mathf.SmoothStep(0f, 1f, progress)
                    - Mathf.SmoothStep(0f, 1f, previousProgress));
                displacement = _dodgeDirection * distance;
                if (progress >= 1f) IsDodging = false;
            }
            else if (IsDisplacing)
            {
                var previousProgress = Mathf.Clamp01(_displacementElapsed / _displacementDuration);
                _displacementElapsed = (_hitstop != null ? _hitstop.ActionTime : Time.time) - _displacementStartsAt;
                var progress = Mathf.Clamp01(_displacementElapsed / _displacementDuration);

                // normalize the endpoints to preserve total distance while allowing curve overshoot.
                var previousDistance = Mathf.SmoothStep(0f, 1f, previousProgress);
                var currentDistance = Mathf.SmoothStep(0f, 1f, progress);
                if (_displacementCurve != null && Mathf.Abs(_curveSpan) > 0.0001f)
                {
                    previousDistance = (_displacementCurve.Evaluate(previousProgress) - _curveStart) / _curveSpan;
                    currentDistance = (_displacementCurve.Evaluate(progress) - _curveStart) / _curveSpan;
                }

                displacement = _displacementDirection * (_displacementDistance * (currentDistance - previousDistance));
                if (progress >= 1f) IsDisplacing = false;
            }
            // walking can be slowed independently of a skill's fixed displacement, even during its delay.
            // use the dodge state from this tick's start so its final frame cannot also add walking.
            if (canMove && !dodging)
            {
                displacement += MoveDirection * (moveSpeed * Mathf.Max(0f, movementSpeedMultiplier) * deltaTime);
            }

            // retain grounding while horizontal actions are blocked, including while downed.
            _verticalSpeed = _controller.isGrounded ? -2f : Mathf.Max(-30f, _verticalSpeed + Physics.gravity.y * deltaTime);
            displacement.y = _verticalSpeed * deltaTime;
            _controller.Move(displacement);
        }
    }
}
