namespace Game50033
{
    using System;
    using UnityEngine;

    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Player))]
    public sealed class FluteFlow : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float maximumFlow = 100f;
        [SerializeField, Min(0f)] private float initialFlow;
        [SerializeField, Min(0f)] private float movementGainPerSecond = 8f;
        [SerializeField, Min(0f)] private float stationaryLossPerSecond = 12f;
        [SerializeField, Min(0f)] private float turningLossPerSecond = 12f;
        [SerializeField, Range(1f, 180f)] private float abruptTurnAngle = 70f;
        [SerializeField, Min(0f)] private float abruptTurnLoss = 10f;
        [SerializeField, Min(0f)] private float maximumSmoothTurnRate = 120f;
        [SerializeField, Min(0.01f)] private float minimumMovementSpeed = 0.1f;
        [SerializeField, Min(0.1f)] private float teleportDistance = 3f;

        private Player _player;
        private Vector3 _previousPosition;
        private Vector3 _previousDirection;
        private bool _hasDirection;

        public float Current { get; private set; }
        public float Maximum => maximumFlow;
        public float Normalized => Mathf.Clamp01(Current / Mathf.Max(0.01f, maximumFlow));
        public bool IsMoving { get; private set; }
        public event Action<float, float> Changed;

        private void Awake()
        {
            _player = GetComponent<Player>();
            Current = Mathf.Clamp(initialFlow, 0f, maximumFlow);
        }

        private void OnEnable()
        {
            ResetMovementTracking();
        }

        private void OnDisable()
        {
            ResetMovementTracking();
        }

        private void LateUpdate()
        {
            var displacement = transform.position - _previousPosition;
            _previousPosition = transform.position;
            displacement.y = 0f;

            // Skip teleports and inactive gameplay so that it wouldn't count as movement/ lack of movement
            if (_player == null || !_player.isActiveAndEnabled || !_player.HasActiveInput
                || _player.Health == null || _player.Health.IsDowned
                || Time.deltaTime <= 0f
                || displacement.sqrMagnitude > teleportDistance * teleportDistance)
            {
                ResetMovementTracking();
                return;
            }

            IsMoving = displacement.magnitude / Time.deltaTime >= minimumMovementSpeed;
            if (!IsMoving)
            {
                _hasDirection = false;
                Lose(stationaryLossPerSecond * Time.deltaTime);
                return;
            }

            var direction = displacement.normalized;
            var angle = _hasDirection ? Vector3.Angle(_previousDirection, direction) : 0f;

            // Reward steady movement and penalize sharp turns or repeated spinning.
            if (_hasDirection && angle >= abruptTurnAngle)
            {
                Lose(abruptTurnLoss);
            }
            else if (_hasDirection && angle / Time.deltaTime > maximumSmoothTurnRate)
            {
                Lose(turningLossPerSecond * Time.deltaTime);
            }
            else
            {
                Gain(movementGainPerSecond * Time.deltaTime);
            }

            _previousDirection = direction;
            _hasDirection = true;
        }

        public void Gain(float amount)
        {
            if (amount > 0f && !float.IsInfinity(amount))
            {
                SetValue(Current + amount);
            }
        }

        public void Lose(float amount)
        {
            if (amount > 0f && !float.IsInfinity(amount))
            {
                SetValue(Current - amount);
            }
        }

        public bool TryConsume(float amount)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0f || Current < amount)
            {
                return false;
            }

            SetValue(Current - amount);
            return true;
        }

        private void SetValue(float value)
        {
            var next = Mathf.Clamp(value, 0f, maximumFlow);
            if (next == Current)
            {
                return;
            }

            Current = next;
            Changed?.Invoke(Current, maximumFlow);
        }

        private void ResetMovementTracking()
        {
            _previousPosition = transform.position;
            _hasDirection = false;
            IsMoving = false;
        }
    }
}
