using System;
using UnityEngine;

namespace Game50033
{
    public class BossHealth : MonoBehaviour, IHittable
    {
        [SerializeField, Min(0.01f)] private float _bossHealthPoints = 20000f;
        [SerializeField, Min(-1f)]
        [Tooltip("Use -1 to start at full HP. Shows current HP during Play.")]
        private float _bossCurrentHealth = -1f;

        private bool _initialized;

        public float MaxHealth => _bossHealthPoints;
        public float CurrentHealth => _bossCurrentHealth;
        public bool IsDead { get; private set; }
        public float? FirstPhaseBreak => _phaseThreshold
            ?? (_bossTotalPhases > 1 ? (float?)(MaxHealth / _bossTotalPhases * _bossCurrentPhase) : null);

        // IHittable
        private MaterialPropertyBlock _hitProperties;
        private float _lastHitTime = float.NegativeInfinity;
        [SerializeField, Min(0.01f)] private float hitFlashDuration = 0.15f;
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        private SpriteRenderer _sr;

        public event Action<float, float> HealthChanged;
        public event Action Dead;
        public event Action<int> ChangePhase;
        /// <summary>Every accepted hit, with HP lost after damage modifiers and the remaining HP cap.</summary>
        public event Action<HitInfo, float> Damaged;

        private int _bossNextPhase;
        private int _bossTotalPhases;
        private int _bossCurrentPhase;
        private float? _phaseThreshold;
        private BossDiscordance _discordance;

        internal void Initialize(int BossTotalPhases, float? maximumHealth = null, float? phaseThreshold = null)
        {
            if (_initialized) return;

            _bossHealthPoints = Mathf.Max(0.01f, maximumHealth ?? _bossHealthPoints);
            _phaseThreshold = phaseThreshold;
            _discordance = GetComponent<BossDiscordance>();
            _bossCurrentHealth = _bossCurrentHealth < 0f
                ? _bossHealthPoints
                : Mathf.Clamp(_bossCurrentHealth, 0f, _bossHealthPoints);

            _hitProperties = new MaterialPropertyBlock();
            _sr = GetComponentInChildren<SpriteRenderer>();

            _bossTotalPhases = BossTotalPhases;
            _bossNextPhase = GetComponent<BaseBoss>().BossNextPhase;
            _bossCurrentPhase = GetComponent<BaseBoss>().Phase;
            _initialized = true;
        }

        void Update()
        {
            var elapsed = Time.time - _lastHitTime;
            SetHitFlash(Mathf.Clamp01(1f - elapsed / Mathf.Max(0.01f, hitFlashDuration)));
        }

        public void TakeHit(in HitInfo hit)
        {
            if (!isActiveAndEnabled || IsDead || hit.Damage <= 0f
                || float.IsNaN(hit.Damage) || float.IsInfinity(hit.Damage))
            {
                return;
            }

            var damage = hit.Damage;
            var conventionalMultiplier = _discordance != null ? _discordance.ConventionalDamageMultiplier : 1f;
            if (conventionalMultiplier != 1f)
            {
                var resonance = float.IsNaN(hit.ResonanceDamage) ? 0f : Mathf.Clamp(hit.ResonanceDamage, 0f, damage);
                damage = (damage - resonance) * conventionalMultiplier + resonance;
            }
            if (damage <= 0f) return;

            var before = _bossCurrentHealth;
            _bossCurrentHealth = Mathf.Max(0f, _bossCurrentHealth - damage);
            _lastHitTime = Time.time;
            SetHitFlash(1f);

            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
            Damaged?.Invoke(hit, before - _bossCurrentHealth);

            var threshold = _phaseThreshold ?? MaxHealth / _bossTotalPhases * _bossCurrentPhase;
            if (_bossCurrentHealth <= threshold && _bossNextPhase != _bossCurrentPhase)
            {
                ChangePhase?.Invoke(_bossNextPhase);
            }


            if (_bossCurrentHealth <= 0f)
            {
                IsDead = true;
                Dead?.Invoke();
            }
        }

        private void SetHitFlash(float amount)
        {
            if (_sr == null)
            {
                return;
            }

            _sr.GetPropertyBlock(_hitProperties);
            _hitProperties.SetFloat(FlashAmountId, amount);
            _sr.SetPropertyBlock(_hitProperties);
        }
    }
}
