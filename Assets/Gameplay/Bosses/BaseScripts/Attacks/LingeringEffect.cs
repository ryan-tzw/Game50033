namespace Game50033
{
    using UnityEngine;
    public class LingeringEffect : BaseBossProjectile
    {
        public float lifetime;

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

            try { _animator = GetComponentInChildren<Animator>(); }
            catch { }
            try { _trail = GetComponentInChildren<TrailRenderer>(); }
            catch { }
        }
    }
}
