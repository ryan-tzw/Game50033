namespace Game50033
{
    using System.Collections.Generic;
    using UnityEngine;

    [RequireComponent(typeof(LineRenderer))]
    public sealed class BouncingArcAttack : BaseBossAttack
    {
        private static readonly int TelegraphId = Shader.PropertyToID("_Telegraph");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int AttackAnimation = Animator.StringToHash("Attack");
        private const float EdgeInset = 0.001f;

        private readonly List<Vector3> _points = new(14);
        private readonly List<float> _distances = new(14);
        private readonly HashSet<Player> _contacted = new();
        private readonly LineRenderer[] _sideFlashes = new LineRenderer[3];

        private Boss3Controller _bot;
        private Boss3Tuning _tuning;
        private LineRenderer _line;
        private MaterialPropertyBlock _properties;
        private float _startedAt;
        private float _beamLength;
        private float _traversed;
        private bool _running;
        private bool _fired;

        public override bool CanStart => _points.Count >= 2;
        public override bool ControlsBoss => true;
        public override float BossControlTimeout => windUpTime + activeTime + 1f;

        protected override void Awake()
        {
            base.Awake();
            _line = GetComponent<LineRenderer>();
            _line.enabled = false;
            _properties = new MaterialPropertyBlock();
            // the shared scheduler instantiates attacks with identity rotation.
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        public override void Setup(BaseBoss owner)
        {
            base.Setup(owner);
            _bot = owner as Boss3Controller;
            if (_bot == null || !_bot.ArenaFloor || !_bot.Tuning) return;

            var target = Vector3.zero;
            var closest = float.PositiveInfinity;
            foreach (var player in boss.Targets)
            {
                if (player == null) continue;
                var distance = (player.transform.position - owner.transform.position).sqrMagnitude;
                if (distance >= closest) continue;
                closest = distance;
                target = player.transform.position;
            }

            if (float.IsPositiveInfinity(closest)) return;
            _tuning = _bot.Tuning;
            var bounds = _bot.ArenaFloor.bounds;
            var point = owner.transform.position;
            point.x = Mathf.Clamp(point.x, bounds.min.x + EdgeInset, bounds.max.x - EdgeInset);
            point.y = bounds.max.y + 0.10f;
            point.z = Mathf.Clamp(point.z, bounds.min.z + EdgeInset, bounds.max.z - EdgeInset);
            var direction = target - point;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
            direction.Normalize();

            windUpTime = _tuning.telegraphDuration;
            recoveryTime = _tuning.recoveryDuration;
            _points.Add(point);
            _distances.Add(0f);
            var remaining = _tuning.beamSpeed * _tuning.beamLifetime;
            // both the warning and the live sweep read this path, including exact corner reflections.
            for (var bounce = 0; bounce <= _tuning.maxRicochets && remaining > EdgeInset; bounce++)
            {
                var toX = Mathf.Abs(direction.x) < 0.00001f ? float.PositiveInfinity
                    : ((direction.x > 0f ? bounds.max.x : bounds.min.x) - point.x) / direction.x;
                var toZ = Mathf.Abs(direction.z) < 0.00001f ? float.PositiveInfinity
                    : ((direction.z > 0f ? bounds.max.z : bounds.min.z) - point.z) / direction.z;
                var toEdge = Mathf.Max(0f, Mathf.Min(toX, toZ));
                var travel = Mathf.Min(remaining, toEdge);
                if (travel > EdgeInset)
                {
                    point += direction * travel;
                    _beamLength += travel;
                    remaining -= travel;
                    _points.Add(point);
                    _distances.Add(_beamLength);
                }
                if (travel < toEdge) break;
                if (Mathf.Abs(toX - toEdge) <= EdgeInset) direction.x = -direction.x;
                if (Mathf.Abs(toZ - toEdge) <= EdgeInset) direction.z = -direction.z;
            }
            activeTime = _beamLength / _tuning.beamSpeed + _tuning.dissipationDuration;
        }

        public override void Attack()
        {
            _running = true;
            _startedAt = Time.time;
            _bot.ActiveArc = this;
            _bot.FaceArc(_points[1].x - _points[0].x);
            _line.widthMultiplier = _tuning.beamWidth;
            DrawPath(_beamLength, telegraph: true, opacity: 1f);
        }

        private void Update()
        {
            if (!_running) return;
            if (!boss || !boss.isActiveAndEnabled || boss.IsDefeated)
            {
                Cancel();
                return;
            }

            var elapsed = Time.time - _startedAt - windUpTime;
            if (elapsed < 0f) return;
            if (!_fired)
            {
                _fired = true;
                _bot.PlayAnimationTrigger(AttackAnimation);
            }

            var head = Mathf.Min(elapsed * _tuning.beamSpeed, _beamLength);
            if (head > _traversed)
            {
                var radius = _tuning.beamWidth * 0.5f;
                for (var segment = 1; segment < _points.Count; segment++)
                {
                    var from = Mathf.Max(_traversed, _distances[segment - 1]);
                    var to = Mathf.Min(head, _distances[segment]);
                    if (to <= from) continue;

                    var span = _distances[segment] - _distances[segment - 1];
                    var start = Vector3.Lerp(_points[segment - 1], _points[segment], (from - _distances[segment - 1]) / span);
                    var end = Vector3.Lerp(_points[segment - 1], _points[segment], (to - _distances[segment - 1]) / span);
                    var motion = end - start;
                    var motionLengthSquared = motion.sqrMagnitude;
                    if (motionLengthSquared <= 0f) continue;
                    var targets = boss.Targets;
                    // a hit can down a player and remove them from the live roster immediately.
                    for (var i = targets.Count - 1; i >= 0; i--)
                    {
                        var player = targets[i];
                        if (!player || _contacted.Contains(player) || !player.Hurtbox || !player.Hurtbox.IsAvailable) continue;

                        player.Hurtbox.GetWorldCapsule(out var bottom, out _, out var playerRadius);
                        bottom.y = start.y;
                        var along = Mathf.Clamp01(Vector3.Dot(bottom - start, motion) / motionLengthSquared);
                        var contact = start + motion * along;
                        var reach = radius + playerRadius;
                        if ((bottom - contact).sqrMagnitude > reach * reach) continue;

                        _contacted.Add(player);
                        player.Health.TryTakeDamage(_tuning.beamDamage, -motion, boss);
                    }
                }
                _traversed = head;
            }

            var fadeTime = elapsed - _beamLength / _tuning.beamSpeed;
            if (fadeTime > _tuning.dissipationDuration)
            {
                Cancel();
                return;
            }

            var opacity = fadeTime <= 0f ? 1f : 1f - fadeTime / _tuning.dissipationDuration;
            DrawPath(head, telegraph: false, opacity);
        }

        private void DrawPath(float length, bool telegraph, float opacity)
        {
            var segment = 1;
            while (segment < _points.Count - 1 && _distances[segment] < length) segment++;

            _line.enabled = length > EdgeInset;
            _line.positionCount = segment + 1;
            for (var i = 0; i < segment; i++) _line.SetPosition(i, _points[i]);
            var t = (length - _distances[segment - 1]) / (_distances[segment] - _distances[segment - 1]);
            _line.SetPosition(segment, Vector3.Lerp(_points[segment - 1], _points[segment], t));
            _properties.SetFloat(TelegraphId, telegraph ? 1f : 0f);
            _properties.SetFloat(FadeId, opacity);
            _line.SetPropertyBlock(_properties);
            if (telegraph) return;

            // keep each side strike fixed until it switches off. only decorate the portion already traversed.
            var interval = Mathf.Max(0.05f, _tuning.lightningFlashInterval);
            for (var i = 0; i < _sideFlashes.Length; i++)
            {
                var clock = (Time.time - _startedAt - windUpTime) / interval + i * 0.37f;
                var cycle = Mathf.FloorToInt(clock);
                var seed = Mathf.Repeat(GetInstanceID() * 0.03125f, 89f) + cycle * 17.3f + i * 53.1f;
                var sample = Mathf.Repeat(Mathf.Sin(seed * 11.31f) * 4731.83f, 1f);
                var duration = Mathf.Lerp(_tuning.lightningFlashDuration.x, _tuning.lightningFlashDuration.y, sample);
                var distance = (i + Mathf.Lerp(0.18f, 0.84f, sample)) / _sideFlashes.Length * _beamLength;
                var flash = _sideFlashes[i];
                if (_tuning.beamSideFlashReach <= 0f || (clock - cycle) * interval >= duration || distance > length)
                {
                    if (flash) flash.enabled = false;
                    continue;
                }

                if (flash == null)
                {
                    var effect = new GameObject("Arc Side Flash") { layer = gameObject.layer };
                    effect.transform.SetParent(transform, false);
                    flash = _sideFlashes[i] = effect.AddComponent<LineRenderer>();
                    flash.sharedMaterial = _line.sharedMaterial;
                    flash.alignment = LineAlignment.TransformZ;
                    flash.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    flash.receiveShadows = false;
                    flash.positionCount = 4;
                    flash.widthMultiplier = 0.10f;
                }

                var leg = 1;
                while (leg < _points.Count - 1 && _distances[leg] < distance) leg++;
                var forward = (_points[leg] - _points[leg - 1]).normalized;
                var side = Vector3.Cross(Vector3.up, forward) * (sample < 0.5f ? -1f : 1f);
                var origin = _points[leg - 1] + forward * (distance - _distances[leg - 1]);
                var reach = _tuning.beamSideFlashReach * Mathf.Lerp(0.55f, 1f, sample);
                flash.SetPosition(0, origin);
                flash.SetPosition(1, origin + reach * (side * 0.32f + forward * 0.24f));
                flash.SetPosition(2, origin + reach * (side * 0.62f + forward * 0.06f));
                flash.SetPosition(3, origin + reach * (side + forward * 0.32f));
                flash.SetPropertyBlock(_properties);
                flash.enabled = true;
            }
        }

        public override void Cancel()
        {
            if (!_running) return;
            _running = false;
            _line.enabled = false;
            foreach (var flash in _sideFlashes)
                if (flash) flash.enabled = false;
            if (_bot && _bot.ActiveArc == this) _bot.ActiveArc = null;
            NotifyBossControlReleased();
        }
    }
}
