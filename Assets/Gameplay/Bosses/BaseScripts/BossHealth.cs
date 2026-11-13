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

        // IHittable
        private MaterialPropertyBlock _hitProperties;
        private float _lastHitTime = float.NegativeInfinity;
        [SerializeField, Min(0.01f)] private float hitFlashDuration = 0.15f;
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        private SpriteRenderer _sr;

        public event Action<float, float> HealthChanged;
        public event Action Dead;

        private void Awake()
        {
            Initialize();
        }

        internal void Initialize()
        {
            if (_initialized) return;

            _bossHealthPoints = Mathf.Max(0.01f, _bossHealthPoints);
            _bossCurrentHealth = _bossCurrentHealth < 0f
                ? _bossHealthPoints
                : Mathf.Clamp(_bossCurrentHealth, 0f, _bossHealthPoints);

            _hitProperties = new MaterialPropertyBlock();
            _sr = GetComponentInChildren<SpriteRenderer>();

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

            _bossCurrentHealth = Mathf.Max(0f, _bossCurrentHealth - hit.Damage);
            _lastHitTime = Time.time;
            SetHitFlash(1f);

            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
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
