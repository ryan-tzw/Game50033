namespace Game50033
{
    using System.Collections;
    using NUnit.Framework;
    using UnityEngine;
    public class LingeringEffect : BaseBossProjectile
    {
        public float lifetime;
        public bool persistOnContact;

        void Awake()
        {
            _start = transform.position;
            _target = transform.position;

            _targetDirection = (_target - _start).normalized;

            _windUpDuration = 0f;
            _activeDuration = lifetime;
            _homing = false;
            _projectileSpeed = 1.0f;
            _elapsed = 0f;
            _initialized = true;
            if (persistOnContact) _piercing = true;

            try { _animator = GetComponentInChildren<Animator>(); }
            catch { }
            try { _trail = GetComponentInChildren<TrailRenderer>(); }
            catch { }
        }

        protected override void StrikePosition()
        {
            if (_elapsed >= _windUpDuration + _activeDuration + 0.1f) DestroyProjectile();
        }

        public override void DestroyProjectile()
        {
            StartCoroutine(PlayHitThenDestroy());
        }

        private IEnumerator PlayHitThenDestroy()
        {
            if (_animator)
            {
                _animator.Play("Hit");
                yield return new WaitUntil(() => _animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
