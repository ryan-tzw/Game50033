namespace Game50033
{
    using System;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Player))]
    public sealed class FluteFlow : MonoBehaviour
    {
        [SerializeField] private FluteTuning tuning;

        private Player _player;
        private Vector3 _previousPosition;
        private Vector3 _previousDirection;
        private bool _hasDirection;
        private bool _skipMovementSample;

        public float Current { get; private set; }
        public float Maximum => tuning.flow.maximum;
        public float Normalized => Mathf.Clamp01(Current / Mathf.Max(0.01f, tuning.flow.maximum));
        public bool IsMoving { get; private set; }
        public bool IsGainingFromMovement { get; private set; }
        public bool TurnedAbruptly { get; private set; }
        public event Action<float, float> Changed;

        private void Awake()
        {
            _player = GetComponent<Player>();
            if (tuning == null)
            {
                Debug.LogError("FluteFlow requires Flute tuning.", this);
                enabled = false;
                return;
            }
            Current = Mathf.Clamp(tuning.flow.initial, 0f, tuning.flow.maximum);
        }

        private void OnEnable()
        {
            ResetMovementTracking();
            _skipMovementSample = true;
            SceneManager.activeSceneChanged += HandleSceneChanged;
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleSceneChanged;
            ResetMovementTracking();
        }

        private void HandleSceneChanged(Scene previous, Scene current)
        {
            // Ignore the encounter entry teleport, even when it is shorter than the teleport threshold.
            _skipMovementSample = true;
            ResetMovementTracking();
        }

        private void LateUpdate()
        {
            // Expose the existing movement decision without changing resource arithmetic.
            IsGainingFromMovement = false;
            TurnedAbruptly = false;
            var displacement = transform.position - _previousPosition;
            _previousPosition = transform.position;
            displacement.y = 0f;

            if (_skipMovementSample)
            {
                _skipMovementSample = false;
                ResetMovementTracking();
                return;
            }

            // Skip teleports and inactive gameplay so that it wouldn't count as movement/ lack of movement
            if (_player == null || !_player.isActiveAndEnabled || !_player.HasActiveInput
                || _player.Health == null || _player.Health.IsDowned
                || _player.Hitstop.IsStopped
                || Time.deltaTime <= 0f
                || displacement.sqrMagnitude > tuning.flow.teleportDistance * tuning.flow.teleportDistance)
            {
                ResetMovementTracking();
                return;
            }

            IsMoving = displacement.magnitude / Time.deltaTime >= tuning.flow.minimumMovementSpeed;
            if (!IsMoving)
            {
                _hasDirection = false;
                Lose(tuning.flow.stationaryLoss * Time.deltaTime);
                return;
            }

            var direction = displacement.normalized;
            var angle = _hasDirection ? Vector3.Angle(_previousDirection, direction) : 0f;

            // Reward steady movement and penalize sharp turns or repeated spinning.
            if (_hasDirection && angle >= tuning.flow.abruptTurnAngle)
            {
                TurnedAbruptly = true;
                Lose(tuning.flow.abruptTurnLoss);
            }
            else if (_hasDirection && angle / Time.deltaTime > tuning.flow.maximumTurnRate)
            {
                Lose(tuning.flow.turningLoss * Time.deltaTime);
            }
            else
            {
                IsGainingFromMovement = true;
                Gain(tuning.flow.movementGain * Time.deltaTime);
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
            var next = Mathf.Clamp(value, 0f, tuning.flow.maximum);
            if (next == Current)
            {
                return;
            }

            Current = next;
            Changed?.Invoke(Current, tuning.flow.maximum);
        }

        private void ResetMovementTracking()
        {
            _previousPosition = transform.position;
            _hasDirection = false;
            IsMoving = false;
            IsGainingFromMovement = false;
            TurnedAbruptly = false;
        }
    }
}
