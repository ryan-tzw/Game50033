namespace Game50033
{
    using System.Collections.Generic;
    using UnityEngine;

    public sealed class HomingSparkProjectile : MonoBehaviour
    {
        private enum Ending { None, Impact, Fizzle }

        [SerializeField] private Transform orb;
        [SerializeField] private TrailRenderer trail;
        [SerializeField] private Material lightningPalette;

        private static readonly int EndingId = Shader.PropertyToID("_SparkEnding");
        private static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int TrailIntensityId = Shader.PropertyToID("_TrailIntensity");
        // match the outer sphere radius in HomingSparkOrb.shader.
        private const float OrbMeshPadding = 2.25f;

        private readonly HashSet<Player> _contacted = new();
        private MeshRenderer _orbRenderer;
        private MaterialPropertyBlock _orbProperties;
        private Boss3Controller _owner;
        private Player _target;
        private Vector3 _direction;
        private Bounds _arena;
        private float _expiresAt;
        private float _speed;
        private float _radius;
        private float _turnRate;
        private int _damage;
        private Ending _ending;
        private float _endingStartedAt;
        private float _endingDuration;
        private uint _visualSequence;
        private float _visualSeed;

        internal static bool CanTrack(Player player) =>
            player != null && player.isActiveAndEnabled && player.Session != null && !player.Health.IsDowned;

        internal void Launch(Boss3Controller owner, Player target, Vector3 position, float speed)
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
            orb.localPosition = Vector3.zero;
            orb.localScale = Vector3.one * (_radius * 2f * OrbMeshPadding);
            if (_orbRenderer == null) _orbRenderer = orb.GetComponent<MeshRenderer>();
            _orbProperties ??= new MaterialPropertyBlock();
            // use a new visual seed even when the same pooled orb is relaunched.
            var bits = (uint)GetInstanceID() ^ (++_visualSequence * 2654435761u);
            bits = (bits ^ (bits >> 16)) * 2246822519u;
            _visualSeed = 1f + ((bits ^ (bits >> 13)) & 65535u) * (63f / 65535f);
            _orbProperties.SetVector(EndingId, new Vector4(0f, 0f, _visualSeed, 0f));
            // copy the stored HDR shader values directly, avoiding a second colour-space conversion.
            if (lightningPalette != null)
            {
                _orbProperties.SetVector(CoreColorId, lightningPalette.GetVector(CoreColorId));
                _orbProperties.SetVector(RimColorId, lightningPalette.GetVector(GlowColorId));
            }
            _orbRenderer.SetPropertyBlock(_orbProperties);
            _orbProperties.SetFloat(TrailIntensityId, tuning.sparkTrailIntensity);
            trail.SetPropertyBlock(_orbProperties);
            _orbProperties.SetFloat(TrailIntensityId, 1f);
            trail.time = tuning.sparkTrailDuration;
            trail.widthMultiplier = tuning.sparkTrailWidth;
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
            _ending = Ending.None;
        }

        private void BeginEnding(Ending ending, Vector3 position)
        {
            if (!isActiveAndEnabled) return;

            _ending = ending;
            _endingStartedAt = Time.time;
            var tuning = _owner.Tuning;
            var duration = ending == Ending.Impact ? tuning.sparkImpactDuration : tuning.sparkFizzleDuration;
            _endingDuration = Mathf.Max(0.01f, duration * Mathf.Lerp(0.84f, 1.16f, Mathf.Repeat(_visualSeed * 0.731f, 1f)));
            trail.emitting = false;
            _target = null;

            // move just the effect so the old trail ends at the last flight position.
            orb.position = position;
            if (ending == Ending.Impact)
                orb.localScale *= tuning.sparkImpactSizeMultiplier * Mathf.Lerp(0.86f, 1.14f, Mathf.Repeat(_visualSeed * 0.479f, 1f));
            Update();
        }

        private void Update()
        {
            if (_ending == Ending.None) return;

            var elapsed = Time.time - _endingStartedAt;
            var progress = elapsed / _endingDuration;
            if (!_owner.isActiveAndEnabled || _owner.IsDefeated || elapsed >= Mathf.Max(_endingDuration, trail.time))
            {
                _owner.ReturnSpark(this);
                return;
            }

            // the flight root stays at expiry while the fading orb carries its last velocity.
            if (_ending == Ending.Fizzle)
                orb.position = transform.position + _direction * (_speed * Mathf.Min(elapsed, _endingDuration));

            _orbProperties.SetVector(EndingId, new Vector4((float)_ending, Mathf.Clamp01(progress), _visualSeed, 0f));
            _orbRenderer.SetPropertyBlock(_orbProperties);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || _ending != Ending.None || _radius <= 0f) return;

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
            if (!_owner || _ending != Ending.None) return;
            if (!_owner.isActiveAndEnabled || _owner.IsDefeated || !CanTrack(_target))
            {
                _owner.ReturnSpark(this);
                return;
            }
            if (Time.time >= _expiresAt)
            {
                BeginEnding(Ending.Fizzle, orb.position);
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
                var firstCenter = Vector3.zero;
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
                    firstCenter = bottom;
                }

                if (first == null) break;
                _contacted.Add(first);
                if (!first.Health.TryTakeDamage(_damage, -_direction, _owner)) continue;
                var sparkCenter = start + _direction * firstDistance;
                BeginEnding(Ending.Impact, Vector3.MoveTowards(sparkCenter, firstCenter, _radius));
                return;
            }

            transform.SetPositionAndRotation(start + _direction * distance, Quaternion.LookRotation(_direction));
            if (toWall <= step)
            {
                var contact = transform.position;
                if (toXWall <= toZWall + 0.0001f) contact.x = _direction.x > 0f ? _arena.max.x : _arena.min.x;
                if (toZWall <= toXWall + 0.0001f) contact.z = _direction.z > 0f ? _arena.max.z : _arena.min.z;
                BeginEnding(Ending.Impact, contact);
            }
        }
    }
}
