namespace Game50033
{
    using System.Collections.Generic;
    using UnityEngine;

    public sealed class HomingSparkProjectile : MonoBehaviour
    {
        [SerializeField] private Transform orb;
        [SerializeField] private TrailRenderer trail;

        // match the outer sphere radius in HomingSparkOrb.shader.
        private const float OrbMeshPadding = 1.75f;

        private readonly HashSet<Player> _contacted = new();
        private ExterminatorBotBoss _owner;
        private Player _target;
        private Vector3 _direction;
        private Bounds _arena;
        private float _expiresAt;
        private float _speed;
        private float _radius;
        private float _turnRate;
        private int _damage;

        internal static bool CanTrack(Player player) =>
            player != null && player.isActiveAndEnabled && player.Session != null && !player.Health.IsDowned;

        internal void Launch(ExterminatorBotBoss owner, Player target, Vector3 position, float speed)
        {
            _owner = owner;
            _target = target;
            _arena = owner.ArenaFloor.bounds;
            _contacted.Clear();
            var tuning = owner.Tuning;
            _expiresAt = Time.time + tuning.sparkLifetime;
            _speed = speed;
            _radius = tuning.sparkRadius;
            _turnRate = tuning.sparkTurnRate * Mathf.Deg2Rad;
            _damage = tuning.sparkDamage;

            position.x = Mathf.Clamp(position.x, _arena.min.x + _radius, _arena.max.x - _radius);
            position.z = Mathf.Clamp(position.z, _arena.min.z + _radius, _arena.max.z - _radius);
            _direction = target.transform.position - position;
            _direction.y = 0f;
            _direction = _direction.sqrMagnitude > 0.0001f ? _direction.normalized : Vector3.forward;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(_direction));
            // the inner sphere uses the collision diameter. the outer mesh leaves room for lightning.
            orb.localScale = Vector3.one * (_radius * 2f * OrbMeshPadding);
            trail.widthMultiplier = _radius * 2.0f;
            trail.emitting = false;
            gameObject.SetActive(true);
            trail.Clear();
            trail.emitting = true;
        }

        private void OnDisable()
        {
            if (trail != null)
            {
                trail.emitting = false;
                trail.Clear();
            }
            _target = null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || _radius <= 0f) return;

            Gizmos.color = Color.yellow;
            var center = transform.position;
            var previous = center + Vector3.right * _radius;
            for (var i = 1; i <= 32; i++)
            {
                var angle = i * (2f * Mathf.PI / 32f);
                var point = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _radius;
                Gizmos.DrawLine(previous, point);
                previous = point;
            }
        }
#endif

        private void FixedUpdate()
        {
            if (!_owner) return;
            if (!_owner.isActiveAndEnabled || _owner.IsDefeated || !CanTrack(_target) || Time.time >= _expiresAt)
            {
                _owner.ReturnSpark(this);
                return;
            }

            var start = transform.position;
            var towardTarget = _target.transform.position - start;
            towardTarget.y = 0f;
            if (towardTarget.sqrMagnitude > 0.0001f)
                _direction = Vector3.RotateTowards(_direction, towardTarget.normalized, _turnRate * Time.fixedDeltaTime, 0f);

            var step = _speed * Time.fixedDeltaTime;
            var toXWall = Mathf.Abs(_direction.x) < 0.00001f ? float.PositiveInfinity
                : ((_direction.x > 0f ? _arena.max.x - _radius : _arena.min.x + _radius) - start.x) / _direction.x;
            var toZWall = Mathf.Abs(_direction.z) < 0.00001f ? float.PositiveInfinity
                : ((_direction.z > 0f ? _arena.max.z - _radius : _arena.min.z + _radius) - start.z) / _direction.z;
            var toWall = Mathf.Max(0f, Mathf.Min(toXWall, toZWall));
            var distance = Mathf.Min(step, toWall);

            // check the full step against living players, resolving the nearest contact first.
            var targets = _owner.Targets;
            while (true)
            {
                Player first = null;
                var firstDistance = float.PositiveInfinity;
                for (var i = 0; i < targets.Count; i++)
                {
                    var player = targets[i];
                    if (!player || _contacted.Contains(player) || !player.Hurtbox || !player.Hurtbox.IsAvailable) continue;
                    player.Hurtbox.GetWorldCapsule(out var bottom, out _, out var playerRadius);
                    bottom.y = start.y;
                    var offset = bottom - start;
                    var along = Vector3.Dot(offset, _direction);
                    var beside = Mathf.Max(0f, offset.sqrMagnitude - along * along);
                    var reach = _radius + playerRadius;
                    if (beside > reach * reach) continue;
                    var halfChord = Mathf.Sqrt(reach * reach - beside);
                    var entry = Mathf.Max(0f, along - halfChord);
                    if (along + halfChord < 0f || entry > distance || entry >= firstDistance) continue;
                    first = player;
                    firstDistance = entry;
                }

                if (first == null) break;
                _contacted.Add(first);
                if (!first.Health.TryTakeDamage(_damage, -_direction, _owner)) continue;
                _owner.ReturnSpark(this);
                return;
            }

            transform.SetPositionAndRotation(start + _direction * distance, Quaternion.LookRotation(_direction));
            if (toWall <= step) _owner.ReturnSpark(this);
        }
    }
}
