namespace Game50033
{
    using System.Collections;
    using Unity.VisualScripting;
    using UnityEngine;
    using UnityEngine.AI;
    using UnityEngine.UIElements;

    [RequireComponent(typeof(Collider))]
    public class BaseBossProjectile : MonoBehaviour
    {
        private TargetedBossAttack _targetedBossAttack;
        private bool _initialized;

        private Vector3 _start;
        private Vector3 _target;
        private Vector3 _targetDirection;
        private float _windUpDuration;
        private float _activeDuration;
        private float _elapsed;
        private float _projectileSpeed;
        private Player _targetedPlayer;
        private bool _destroyOnArrival;
        private bool _homing;
        private bool _bouncy;
        private bool _blockedByObstacles;

        private Vector3 _lockedPosition;
        private bool _lockPosition;

        // If the projectile uses animator for animations
        private bool CanDestroy = false;
        private Animator _animator;
        private TrailRenderer _trail;

        public void InitializeProjectile(Player player, Vector3 target, TargetedBossAttack targetedBossAttack)
        {
            _targetedPlayer = player;
            _targetedBossAttack = targetedBossAttack;
            _destroyOnArrival = targetedBossAttack.destroyOnArrival;
            _homing = targetedBossAttack.homing;
            _projectileSpeed = targetedBossAttack.projectileSpeed;
            _bouncy = targetedBossAttack.bouncy;
            _blockedByObstacles = targetedBossAttack.blockedByObstacles;

            try { _animator = GetComponentInChildren<Animator>(); }
            catch { CanDestroy = true; }
            try { _trail = GetComponentInChildren<TrailRenderer>(); }
            catch { }

            Initialize(target);
        }

        protected void Initialize(Vector3 target)
        {
            _start = transform.position + new Vector3(0f, 0.15f, 0f);
            transform.position = _start;
            _target = target + new Vector3(0f, 0.15f, 0f);

            float _distanceToTarget = Vector3.Distance(_start, _target);
            _targetDirection = (_target - _start).normalized * Time.deltaTime;

            _windUpDuration = _homing ? _targetedBossAttack.windUpTime : 0f;
            _activeDuration = _destroyOnArrival ? Mathf.Max(_distanceToTarget / _projectileSpeed, 0.01f) : _targetedBossAttack.activeTime;
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

        private void LateUpdate()
        {
            if (_lockPosition) transform.position = _lockedPosition;
        }

        public virtual void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out Player player))
            {
                player.Health.TryTakeDamage(1);

                if (_destroyOnArrival) DestroyProjectile();
            }
            if (other.TryGetComponent(out BoxCollider collider))
            {
                Vector3 contactPoint = transform.position;
                Vector3 otherContactPoint = other.ClosestPoint(contactPoint);

                if (_bouncy)
                {
                    if ((_targetDirection.x * _targetDirection.z) > 0)
                    {
                        if (Mathf.Abs(contactPoint.x - otherContactPoint.x) < 0.001) _targetDirection = new Vector3(_targetDirection.x, _targetDirection.y, -_targetDirection.z);
                        else _targetDirection = new Vector3(-_targetDirection.x, _targetDirection.y, _targetDirection.z);
                    }
                    else if ((_targetDirection.x * _targetDirection.z) < 0)
                    {
                        if (Mathf.Abs(contactPoint.z - otherContactPoint.z) < 0.001) _targetDirection = new Vector3(-_targetDirection.x, _targetDirection.y, _targetDirection.z);
                        else _targetDirection = new Vector3(_targetDirection.x, _targetDirection.y, -_targetDirection.z);
                    }
                    else { _targetDirection = -_targetDirection; }
                }
                else if (_blockedByObstacles) DestroyProjectile();
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

            if (_homing) position = Vector3.Lerp(_target, _newTarget, t);
            else position = transform.position + (_targetDirection * _projectileSpeed);

            MoveTowards(position);
            // Debug.Log($"Projectile: {_target} to {_newTarget}, moving towards {position}");

            if (t >= 1f) if (_destroyOnArrival && CanDestroy) Destroy(gameObject);
            if (_elapsed >= (_windUpDuration + _activeDuration + 0.1f)) Destroy(gameObject); // 0.1f added to account for contact animations
        }

        protected void DestroyProjectile()
        {
            _lockedPosition = transform.position;
            _lockPosition = true;

            if (_trail) _trail.enabled = false;

            if (_animator)
            {
                StartCoroutine(WaitForAnimation());
                IEnumerator WaitForAnimation()
                {
                    _animator.Play("Hit");
                    yield return new WaitUntil(() => _animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1.0f);
                    CanDestroy = true;
                }
            }
            else CanDestroy = true;
        }
    }
}
