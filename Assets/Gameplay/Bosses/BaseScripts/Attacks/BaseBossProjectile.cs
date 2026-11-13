namespace Game50033
{
    using System.Collections.Generic;
    using UnityEngine;

    [RequireComponent(typeof(Collider))]
    public class BaseBossProjectile : MonoBehaviour
    {
        private TargetedBossAttack _targetedBossAttack;
        private bool _initialized;

        protected Vector3 _start;
        protected Vector3 _target;
        private float _windUpDuration;
        private float _activeDuration;
        private bool _destroyOnArrival;
        protected float _elapsed;
        private Player _targetedPlayer;

        public void InitializeProjectile(Player player, Vector3 target, TargetedBossAttack targetedBossAttack)
        {
            _targetedPlayer = player;
            _targetedBossAttack = targetedBossAttack;
            _destroyOnArrival = targetedBossAttack.destroyOnArrival;
            Initialize(target);
        }

        protected void Initialize(Vector3 target)
        {
            _start = transform.position + Vector3.up;
            _target = target;

            float _distanceToTarget = Vector3.Distance(_start, _target);

            _windUpDuration = _targetedBossAttack.homing ? _targetedBossAttack.windUpTime : 0f;
            _activeDuration = _destroyOnArrival ? Mathf.Max(_distanceToTarget / _targetedBossAttack.projectileSpeed, 0.01f) : _targetedBossAttack.activeTime;
            _elapsed = 0f;
            _initialized = true;
        }

        protected void Update()
        {
            if (_initialized)
            {
                _elapsed += Time.deltaTime;
                WindUpAttack();
            }
        }

        public virtual void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out Player player))
            {
                player.Health.TryTakeDamage(1);
                if (_destroyOnArrival) Destroy(gameObject);
            }
        }

        protected void WindUpAttack()
        {
            if (_elapsed >= _windUpDuration) { StrikePosition(); }
            else
            {
                _target = _targetedBossAttack.RetrievePlayerPosition(_targetedPlayer) + new Vector3(0f, _targetedBossAttack.projectileHomingHeight, 0f);
                MoveTowards(_target);
            }
        }

        protected void MoveTowards(Vector3 position)
        {
            transform.position = position;
        }

        protected void StrikePosition()
        {
            float t = Mathf.Clamp01((_elapsed - _windUpDuration) / _activeDuration);

            Vector3 _newTarget = _target - new Vector3(0f, _targetedBossAttack.projectileHomingHeight, 0f);
            Vector3 position;

            if (_targetedBossAttack.homing) position = Vector3.Lerp(_target, _newTarget, t);
            else position = Vector3.Lerp(_start, _target, t);

            MoveTowards(position);
            // Debug.Log($"Projectile: {_target} to {_newTarget}, moving towards {position}");

            if (t >= 1f) if (_destroyOnArrival) Destroy(gameObject);
            if (_elapsed >= (_windUpDuration + _activeDuration)) Destroy(gameObject);
        }
    }
}
