namespace Game50033
{
    using System.Collections.Generic;
    using UnityEngine;

    public sealed class SpiralSurgeProjectile : MonoBehaviour
    {
        private const int SpinePoints = 7;
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        private static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
        private static readonly int JaggednessId = Shader.PropertyToID("_Jaggedness");
        private static readonly int DistortionId = Shader.PropertyToID("_Distortion");

        [SerializeField] private LineRenderer bolt;
        [SerializeField] private LineRenderer[] branches;

        private readonly HashSet<Player> _contacted = new();
        private ExterminatorBotBoss _owner;
        private MaterialPropertyBlock _properties;
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
        private int _visualFrame;
        private uint _visualSeed;

        internal void Launch(ExterminatorBotBoss owner, Vector3 center, Vector3 direction, float speed)
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
            _visualFrame = -1;
            _visualSeed += (uint)GetInstanceID() | 1u;
            _properties ??= new MaterialPropertyBlock();
            _properties.SetFloat(ThicknessId, 0.38f);
            _properties.SetFloat(GlowIntensityId, 0.70f);
            // the polyline supplies the bends. leave a little movement inside the shared shader's core.
            _properties.SetFloat(JaggednessId, 0.07f);
            _properties.SetFloat(DistortionId, 0.015f);
            _properties.SetFloat(FadeId, 1f);
            bolt.SetPropertyBlock(_properties);
            bolt.positionCount = SpinePoints;
            bolt.enabled = true;
            foreach (var branch in branches)
            {
                branch.SetPropertyBlock(_properties);
                branch.enabled = false;
            }
            gameObject.SetActive(true);
            Update();
        }

        private void Update()
        {
            if (!_owner) return;
            var tuning = _owner.Tuning;
            var phase = (_visualSeed & 255u) / 256f;
            var frame = Mathf.FloorToInt(Time.time * tuning.spiralBoltFlickerRate + phase);
            if (_impacting)
            {
                var progress = Mathf.Clamp01((Time.time - _impactStartedAt) / _impactDuration);
                if (progress >= 1f)
                {
                    _owner.ReturnSpiralBolt(this);
                    return;
                }

                var rotation = Noise(44) * 360f;
                for (var i = 0; i < _impactArmCount; i++)
                {
                    var line = i == 0 ? bolt : branches[i - 1];
                    var delay = i == 0 ? 0f : Noise(i + 48) * 0.22f;
                    line.enabled = progress >= delay;
                    if (!line.enabled) continue;
                    var armProgress = (progress - delay) / (1f - delay);
                    var fade = 1f - armProgress;
                    var reach = _impactReach * (1f - fade * fade * 0.8f);
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
                    var bend = Mathf.Lerp(-0.34f, 0.34f, Noise(i + 56));
                    line.widthMultiplier = tuning.spiralBoltWidth * Mathf.Lerp(0.55f, 1.25f, fade);
                    line.SetPosition(0, direction * (arm * armProgress * 0.40f));
                    line.SetPosition(1, direction * (arm * 0.57f) + sideways * (arm * bend));
                    line.SetPosition(2, direction * arm - sideways * (arm * bend * 0.4f));
                    _properties.SetFloat(FadeId, Mathf.Sqrt(fade));
                    line.SetPropertyBlock(_properties);
                }
                return;
            }

            if (_visualFrame == frame) return;
            _visualFrame = frame;
            bolt.widthMultiplier = tuning.spiralBoltWidth * Mathf.Lerp(0.87f, 1.1f, Noise(11));
            for (var i = 0; i < SpinePoints; i++)
            {
                var t = i / (float)(SpinePoints - 1);
                var x = i == 0 || i == SpinePoints - 1 ? 0f : tuning.spiralBoltBend * (i % 2 == 0 ? -1f : 1f)
                    * Mathf.Lerp(0.24f, 1f, Noise(i));
                bolt.SetPosition(i, new Vector3(x, 0f, (t - 0.72f) * tuning.spiralBoltLength));
            }

            for (var i = 0; i < branches.Length; i++)
            {
                var branch = branches[i];
                branch.enabled = Noise(21 + i) > (i == 0 ? 0.30f : 0.68f);
                if (!branch.enabled) continue;
                var anchor = bolt.GetPosition(2 + i);
                var side = Mathf.Sign(anchor.x);
                branch.widthMultiplier = tuning.spiralBoltWidth * 0.72f;
                branch.SetPosition(0, anchor);
                branch.SetPosition(1, anchor + new Vector3(side * tuning.spiralBoltBend * 0.65f, 0f, -tuning.spiralBoltLength * 0.12f));
                branch.SetPosition(2, anchor + new Vector3(side * tuning.spiralBoltBend * 1.27f, 0f, -tuning.spiralBoltLength * 0.30f));
            }
        }

        private float Noise(int channel)
        {
            // variation is local to presentation so it cannot perturb the boss deck's random sequence.
            var value = _visualSeed ^ ((uint)_visualFrame * 747796405u) ^ ((uint)channel * 2891336453u);
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
            transform.position = contact;
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
