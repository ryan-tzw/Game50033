namespace Game50033
{
    using UnityEngine;

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(300)]
    public sealed class PlayerHitReaction : MonoBehaviour
    {
        [SerializeField] private PlayerHitReactionTuning tuning;
        [SerializeField] private SpriteRenderer avatarRenderer;

        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");

        private Player _player;
        private PlayerHealth _health;
        private PlayerDodgeVFX _dodgeVfx;
        private MaterialPropertyBlock _properties;
        private float _stunUntil;
        private float _hitAt;
        private bool _flashing;
        private bool _pulsing;
        private float _appliedFlash;
        private bool _shaking;
        private Vector3 _basePosition;
        private Vector3 _shakenPosition;
        private bool _offsetApplied;

        public bool IsHitStunned => isActiveAndEnabled && Time.time < _stunUntil;

        private void Awake()
        {
            _player = GetComponent<Player>();
            _health = GetComponent<PlayerHealth>();
            _dodgeVfx = GetComponent<PlayerDodgeVFX>();
            _properties = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (tuning == null || avatarRenderer == null || _health == null || _player == null)
            {
                Debug.LogError($"{nameof(PlayerHitReaction)} requires player health, tuning and an avatar renderer.", this);
                enabled = false;
                return;
            }

            _health.Damaged += HandleDamage;
            _health.Downed += Clear;
            _health.Revived += Clear;
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.Damaged -= HandleDamage;
                _health.Downed -= Clear;
                _health.Revived -= Clear;
            }
            Clear();
        }

        private void HandleDamage(int applied)
        {
            if (_health.IsDowned || !_player.isActiveAndEnabled) return;

            Clear();
            _hitAt = Time.time;
            _stunUntil = _hitAt + tuning.hitstunDuration;
            _player.Hitstop.Clear();
            _player.Abilities?.Cancel();
            _player.Movement.CancelDisplacement();

            _flashing = tuning.flashDuration > 0f;
            _pulsing = _health.PostHitInvulnerabilityRemaining > 0f;
            _shaking = tuning.shakeDuration > 0f && tuning.shakeAmplitude > 0f;
            if (_flashing) SetFlash(1f);
        }

        private void Update()
        {
            // release our blend before ability visuals write their own flash in LateUpdate.
            if (_appliedFlash > 0f && _health.AbilityInvulnerabilityRemaining > 0f) ClearFlash();
        }

        private void LateUpdate()
        {
            // remove our last offset only if animation has not since written a new pose.
            RestoreSpritePosition();
            var elapsed = Time.time - _hitAt;

            _flashing &= elapsed < tuning.flashDuration;
            var remaining = _health.PostHitInvulnerabilityRemaining;
            _pulsing &= remaining > 0f;

            if (_health.AbilityInvulnerabilityRemaining <= 0f)
            {
                var flash = 0f;
                if (_player.Movement.IsInvulnerable && _dodgeVfx != null && _dodgeVfx.Tuning != null)
                {
                    var dodge = _dodgeVfx.Tuning;
                    flash = dodge.invulnerabilityFlashIntensity
                        * Mathf.Clamp01(dodge.invulnerabilityFlashDecay.Evaluate(_player.Movement.DodgeProgress));
                }
                else if (_flashing)
                {
                    flash = Mathf.Clamp01(tuning.flashDecay.Evaluate(elapsed / tuning.flashDuration));
                }
                else if (_pulsing && tuning.immunityPulseRate > 0f)
                {
                    var cycles = (elapsed - tuning.flashDuration) * tuning.immunityPulseRate;
                    var pulse = 0.5f - 0.5f * Mathf.Cos(cycles * 2f * Mathf.PI);
                    var fadeOut = Mathf.Clamp01(remaining * tuning.immunityPulseRate * 2f);
                    flash = pulse * fadeOut * tuning.immunityPulseIntensity;
                }

                if (flash > 0f) SetFlash(flash);
                else ClearFlash();
            }

            if (!_shaking) return;
            _shaking = elapsed < tuning.shakeDuration;
            if (!_shaking) return;

            var progress = elapsed / tuning.shakeDuration;
            var wave = Mathf.Sin(progress * tuning.shakeCycles * 2f * Mathf.PI);
            var offset = transform.right * (wave * tuning.shakeAmplitude * Mathf.Clamp01(tuning.shakeDecay.Evaluate(progress)));
            var sprite = avatarRenderer.transform;
            _basePosition = sprite.localPosition;
            _shakenPosition = _basePosition + sprite.parent.InverseTransformVector(offset);
            sprite.localPosition = _shakenPosition;
            _offsetApplied = true;
        }

        internal void Clear()
        {
            _stunUntil = 0f;
            ClearFlash();
            _flashing = false;
            _pulsing = false;
            _shaking = false;
            RestoreSpritePosition();
        }

        private void SetFlash(float amount)
        {
            if (avatarRenderer == null || _properties == null) return;
            avatarRenderer.GetPropertyBlock(_properties);
            _properties.SetFloat(FlashAmountId, amount);
            _properties.SetColor(FlashColorId, Color.white);
            avatarRenderer.SetPropertyBlock(_properties);
            _appliedFlash = amount;
        }

        private void ClearFlash()
        {
            if (_appliedFlash <= 0f) return;
            if (avatarRenderer != null && _properties != null)
            {
                avatarRenderer.GetPropertyBlock(_properties);
                // another visual may already have replaced our value this frame.
                if (_properties.GetFloat(FlashAmountId) == _appliedFlash)
                {
                    _properties.SetFloat(FlashAmountId, 0f);
                    avatarRenderer.SetPropertyBlock(_properties);
                }
            }
            _appliedFlash = 0f;
        }

        private void RestoreSpritePosition()
        {
            if (!_offsetApplied) return;
            _offsetApplied = false;
            if (avatarRenderer != null && avatarRenderer.transform.localPosition.Equals(_shakenPosition))
                avatarRenderer.transform.localPosition = _basePosition;
        }
    }
}
