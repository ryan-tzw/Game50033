namespace Game50033
{
    using System.Collections.Generic;
    using UnityEngine;

    public sealed class SpiralSurgeProjectile : MonoBehaviour
    {
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        private static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
        private static readonly int JaggednessId = Shader.PropertyToID("_Jaggedness");
        private static readonly int DistortionId = Shader.PropertyToID("_Distortion");
        private static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int StrikeHeadId = Shader.PropertyToID("_StrikeHead");
        private static readonly int EndingId = Shader.PropertyToID("_SparkEnding");

        [SerializeField] private LineRenderer bolt;
        [SerializeField] private LineRenderer[] branches;
        [SerializeField] private TrailRenderer trail;

        private readonly HashSet<Player> _contacted = new();
        private Boss3Controller _owner;
        private MaterialPropertyBlock _properties;
        private MaterialPropertyBlock _trailProperties;
        private Material _impactMaterial;
        private Bounds _arena;
        private Vector3 _direction;
        private float _speed;
        private float _radius;
        private int _damage;
        private float _remainingDistance;
        private bool _endsAtWall;
        private bool _impacting;
        private float _impactStartedAt;
        private float _impactDuration;
        private float _impactReach;
        private int _impactArmCount;
        private Vector3 _impactInward;
        private uint _visualSeed;

        internal void Launch(Boss3Controller owner, Vector3 center, Vector3 direction, float speed)
        {
            _owner = owner;
            _direction = direction;
            _speed = speed;
            var tuning = owner.Tuning;
            _radius = tuning.spiralRadius;
            _damage = tuning.spiralDamage;
            _arena = owner.ArenaFloor.bounds;
            center = ClampToArena(_arena, center, _radius);
            var toWall = DistanceToEdge(_arena, center, direction, _radius);
            var lifetimeDistance = speed * tuning.spiralLifetime;
            _endsAtWall = toWall <= lifetimeDistance;
            _remainingDistance = Mathf.Min(lifetimeDistance, toWall);
            _contacted.Clear();
            transform.SetPositionAndRotation(center, Quaternion.LookRotation(direction));
            _impacting = false;
            _visualSeed += (uint)GetInstanceID() | 1u;
            _properties ??= new MaterialPropertyBlock();
            _properties.SetFloat(ThicknessId, 0.38f);
            _properties.SetFloat(GlowIntensityId, 0.70f);
            // the impact lines supply the bends, with a little flicker inside the shader's core.
            _properties.SetFloat(JaggednessId, 0.07f);
            _properties.SetFloat(DistortionId, 0.015f);
            _properties.SetFloat(FadeId, 1f);
            foreach (var branch in branches)
            {
                branch.SetPropertyBlock(_properties);
                branch.enabled = false;
            }
            if (!_impactMaterial) _impactMaterial = bolt.sharedMaterial;
            _trailProperties ??= new MaterialPropertyBlock();
            _trailProperties.SetVector(CoreColorId, _impactMaterial.GetVector(CoreColorId));
            _trailProperties.SetVector(RimColorId, _impactMaterial.GetVector(GlowColorId));
            _trailProperties.SetVector(EndingId, new Vector4(0f, 0f, (_visualSeed & 65535u) / 1024f, 0f));
            // the head and wake share one shader. the head keeps a visible leading edge.
            bolt.sharedMaterial = trail.sharedMaterial;
            bolt.positionCount = 2;
            bolt.SetPosition(0, Vector3.back * (tuning.spiralBoltLength * 0.72f));
            bolt.SetPosition(1, Vector3.forward * (tuning.spiralBoltLength * 0.28f));
            bolt.widthMultiplier = tuning.spiralTrailWidth * 0.72f;
            _trailProperties.SetFloat(StrikeHeadId, 1f);
            bolt.SetPropertyBlock(_trailProperties);
            bolt.enabled = true;
            _trailProperties.SetFloat(StrikeHeadId, 0f);
            trail.SetPropertyBlock(_trailProperties);
            trail.time = tuning.spiralTrailDuration;
            trail.widthMultiplier = tuning.spiralTrailWidth;
            gameObject.SetActive(true);
            trail.Clear();
            trail.emitting = true;
        }

        private void OnDisable()
        {
            if (!trail) return;
            trail.emitting = false;
            trail.Clear();
        }

        private void Update()
        {
            if (!_owner || !_impacting) return;
            var tuning = _owner.Tuning;
            var elapsed = Time.time - _impactStartedAt;
            if (elapsed >= _impactDuration)
            {
                if (elapsed >= trail.time)
                {
                    _owner.ReturnSpiralBolt(this);
                    return;
                }
                bolt.enabled = false;
                foreach (var branch in branches) branch.enabled = false;
                return;
            }

            var rotation = Noise(44) * 360f;
            var interval = Mathf.Max(0.05f, tuning.lightningFlashInterval);
            for (var i = 0; i < _impactArmCount; i++)
            {
                var line = i == 0 ? bolt : branches[i - 1];
                var delay = i == 0 ? 0f : Noise(i + 48) * _impactDuration * 0.3f;
                var cycle = Mathf.FloorToInt((elapsed - delay) / interval);
                var age = elapsed - delay - cycle * interval;
                var seedOffset = cycle * 67;
                var duration = Mathf.Lerp(tuning.lightningFlashDuration.x, tuning.lightningFlashDuration.y,
                    Noise(i + 73 + seedOffset));
                line.enabled = cycle >= 0 && age < duration;
                if (!line.enabled) continue;
                var reach = _impactReach * Mathf.Lerp(0.52f, 1.0f, Noise(i + 84 + seedOffset));
                var angle = rotation + (i + Noise(i) * 0.68f) * 360f / _impactArmCount;
                var outward = Vector3.forward;
                if (_impactInward != Vector3.zero)
                {
                    // keep wall sparks on the visible side of the wall.
                    var span = Mathf.Lerp(43f, 73f, Noise(45));
                    angle = Mathf.Lerp(-span, span, (i + Noise(i) * 0.45f) / (_impactArmCount - 0.55f));
                    outward = _impactInward;
                }
                var direction = Quaternion.AngleAxis(angle, Vector3.up) * outward;
                var sideways = Vector3.Cross(Vector3.up, direction);
                var arm = reach * Mathf.Lerp(0.55f, 1f, Noise(i + 7));
                var bend = Mathf.Lerp(-0.34f, 0.34f, Noise(i + 56 + seedOffset));
                line.widthMultiplier = tuning.spiralBoltWidth * Mathf.Lerp(0.55f, 1.15f, Noise(i + 94 + seedOffset));
                line.SetPosition(0, direction * (arm * cycle * 0.08f));
                line.SetPosition(1, direction * (arm * 0.57f) + sideways * (arm * bend));
                line.SetPosition(2, direction * arm - sideways * (arm * bend * 0.4f));
                _properties.SetFloat(FadeId, Mathf.Lerp(0.57f, 1f, Noise(i + 112 + seedOffset)));
                line.SetPropertyBlock(_properties);
            }
        }

        private float Noise(int channel)
        {
            // variation is local to presentation so it cannot perturb the boss deck's random sequence.
            var value = _visualSeed ^ ((uint)channel * 2891336453u);
            value = ((value >> ((int)(value >> 28) + 4)) ^ value) * 277803737u;
            return ((value ^ (value >> 22)) & 65535u) / 65535f;
        }

        private void BeginImpact(Vector3 contact, Vector3 inward = default)
        {
            if (!isActiveAndEnabled) return;
            _impacting = true;
            _impactStartedAt = Time.time;
            var tuning = _owner.Tuning;
            _impactDuration = Mathf.Max(0.01f, tuning.spiralImpactDuration * Mathf.Lerp(0.82f, 1.18f, Noise(40)));
            _impactReach = tuning.spiralImpactReach * Mathf.Lerp(0.80f, 1.20f, Noise(41));
            _impactArmCount = Mathf.Min(2 + (int)(Noise(42) * branches.Length), branches.Length + 1);
            _impactInward = transform.InverseTransformDirection(inward);
            trail.emitting = false;
            transform.position = contact;
            bolt.sharedMaterial = _impactMaterial;
            bolt.positionCount = 3;
            foreach (var branch in branches) branch.enabled = false;
            Update();
        }

        internal static Vector3 ClampToArena(Bounds bounds, Vector3 position, float radius)
        {
            position.x = Mathf.Clamp(position.x, bounds.min.x + radius, bounds.max.x - radius);
            position.z = Mathf.Clamp(position.z, bounds.min.z + radius, bounds.max.z - radius);
            return position;
        }

        internal static float DistanceToEdge(Bounds bounds, Vector3 position, Vector3 direction, float radius)
        {
            var alongX = Mathf.Abs(direction.x) < 0.00001f ? float.PositiveInfinity
                : ((direction.x > 0f ? bounds.max.x - radius : bounds.min.x + radius) - position.x) / direction.x;
            var alongZ = Mathf.Abs(direction.z) < 0.00001f ? float.PositiveInfinity
                : ((direction.z > 0f ? bounds.max.z - radius : bounds.min.z + radius) - position.z) / direction.z;
            return Mathf.Max(0f, Mathf.Min(alongX, alongZ));
        }

        private void FixedUpdate()
        {
            if (!_owner) return;
            if (!_owner.isActiveAndEnabled || _owner.IsDefeated)
            {
                _owner.ReturnSpiralBolt(this);
                return;
            }
            if (_impacting) return;
            if (_remainingDistance <= 0f)
            {
                FinishFlight();
                return;
            }

            var start = transform.position;
            var distance = Mathf.Min(_remainingDistance, _speed * Time.fixedDeltaTime);
            var targets = _owner.Targets;
            // check the full step so overcharged bolts cannot skip past a combat capsule.
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
                var center = start + _direction * firstDistance;
                BeginImpact(Vector3.MoveTowards(center, firstCenter, _radius));
                return;
            }

            transform.position = start + _direction * distance;
            _remainingDistance -= distance;
            if (_remainingDistance <= 0f) FinishFlight();
        }

        private void FinishFlight()
        {
            if (!_endsAtWall)
            {
                _owner.ReturnSpiralBolt(this);
                return;
            }

            var contact = transform.position;
            if (_direction.x > 0f && contact.x >= _arena.max.x - _radius - 0.0001f) contact.x = _arena.max.x;
            if (_direction.x < 0f && contact.x <= _arena.min.x + _radius + 0.0001f) contact.x = _arena.min.x;
            if (_direction.z > 0f && contact.z >= _arena.max.z - _radius - 0.0001f) contact.z = _arena.max.z;
            if (_direction.z < 0f && contact.z <= _arena.min.z + _radius + 0.0001f) contact.z = _arena.min.z;
            BeginImpact(contact, (transform.position - contact).normalized);
        }
    }
}
