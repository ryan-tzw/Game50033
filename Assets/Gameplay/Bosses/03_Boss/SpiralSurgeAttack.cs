namespace Game50033
{
    using UnityEngine;
    using UnityEngine.Rendering;

    public sealed class SpiralSurgeAttack : BaseBossAttack
    {
        private static readonly int TelegraphId = Shader.PropertyToID("_Telegraph");
        private static readonly int ShootId = Animator.StringToHash("Attack");
        private const int ArcSegments = 20;

        [SerializeField] private SpiralSurgeProjectile projectilePrefab;
        [SerializeField] private Material lightningMaterial;

        private Boss3Controller _bot;
        private LineRenderer _initialLane;
        private LineRenderer _rotationCue;
        private LineRenderer _arrow;
        private Bounds _arena;
        private Vector3 _origin;
        private Vector3 _firstDirection;
        private float _speed;
        private int _shotCount;
        private int _shotsFired;
        private float _nextShotAt;
        private bool _running;

        public override bool CanStart => _shotCount > 0;
        public override bool ControlsBoss => true;
        public override float BossControlTimeout => windUpTime + activeTime + 1f;

        public override void Setup(BaseBoss owner)
        {
            base.Setup(owner);
            _bot = owner as Boss3Controller;
            if (_bot == null || !_bot.ArenaFloor || !_bot.Tuning || !projectilePrefab || !lightningMaterial) return;

            var tuning = _bot.Tuning;
            _arena = _bot.ArenaFloor.bounds;
            var center = _bot.transform.position;
            center.y = _arena.max.y + 0.15f;
            _origin = SpiralSurgeProjectile.ClampToArena(_arena, center, tuning.spiralRadius);
            var closestDistance = float.PositiveInfinity;
            foreach (var player in _bot.Targets)
            {
                var toPlayer = player.transform.position - _origin;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude >= closestDistance) continue;
                closestDistance = toPlayer.sqrMagnitude;
                _firstDirection = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector3.forward;
            }
            if (float.IsPositiveInfinity(closestDistance)) return;

            var meter = _bot.GetComponent<BossDiscordance>();
            // overcharge extends the volley while preserving both angular and temporal gaps.
            _shotCount = Mathf.CeilToInt(tuning.spiralShotCount * (meter ? meter.ProjectileCountMultiplier : 1f));
            _speed = tuning.spiralSpeed * (meter ? meter.ProjectileSpeedMultiplier : 1f);
            windUpTime = tuning.spiralTelegraphDuration;
            activeTime = Mathf.Max(0, _shotCount - 1) * tuning.spiralShotInterval;
            recoveryTime = tuning.spiralRecoveryDuration;
        }

        public override void Attack()
        {
            _running = true;
            _bot.ActiveSpiralSurge = this;
            _bot.FaceArc(_firstDirection.x);
            _nextShotAt = Time.time + windUpTime;

            _initialLane = CreateWarning("Spiral First Shot", 2, _bot.Tuning.spiralRadius * 2f);
            var distance = Mathf.Min(_speed * _bot.Tuning.spiralLifetime,
                SpiralSurgeProjectile.DistanceToEdge(_arena, _origin, _firstDirection, _bot.Tuning.spiralRadius));
            var warningOrigin = new Vector3(_origin.x, _arena.max.y + 0.04f, _origin.z);
            _initialLane.SetPosition(0, warningOrigin);
            _initialLane.SetPosition(1, warningOrigin + _firstDirection * distance);
            _rotationCue = CreateWarning("Spiral Rotation", ArcSegments + 1, 0.12f);
            _arrow = CreateWarning("Spiral Rotation Arrow", 3, 0.12f);
            Update();
        }

        private void Update()
        {
            if (!_running) return;
            if (!_bot || !_bot.isActiveAndEnabled || _bot.IsDefeated)
            {
                Cancel();
                return;
            }

            if (Time.time < _nextShotAt)
            {
                if (_shotsFired > 0) return;

                var center = new Vector3(_origin.x, _arena.max.y + 0.05f, _origin.z);
                var progress = Mathf.Clamp01(1f - (_nextShotAt - Time.time) / windUpTime);
                var angle = progress * 360f;
                const float radius = 0.88f;
                for (var i = 0; i <= ArcSegments; i++)
                {
                    var point = Quaternion.AngleAxis(angle - 125f * (1f - i / (float)ArcSegments), Vector3.up)
                        * _firstDirection;
                    _rotationCue.SetPosition(i, center + point * radius);
                }

                var radial = Quaternion.AngleAxis(angle, Vector3.up) * _firstDirection;
                var tangent = Vector3.Cross(Vector3.up, radial);
                var head = center + radial * radius;
                _arrow.SetPosition(0, head - tangent * 0.23f + radial * 0.14f);
                _arrow.SetPosition(1, head);
                _arrow.SetPosition(2, head - tangent * 0.23f - radial * 0.14f);
                return;
            }

            if (_shotsFired == 0)
            {
                _initialLane.enabled = _rotationCue.enabled = _arrow.enabled = false;
                _bot.PlayAnimationTrigger(ShootId);
            }

            var tuning = _bot.Tuning;
            var direction = Quaternion.AngleAxis(_shotsFired * tuning.spiralDegreesPerShot, Vector3.up) * _firstDirection;
            _bot.FaceArc(direction.x);
            _bot.LaunchSpiralBolt(projectilePrefab, _origin, direction, _speed);
            _shotsFired++;
            if (_shotsFired >= _shotCount)
                Cancel();
            else
                _nextShotAt = Time.time + tuning.spiralShotInterval;
        }

        private LineRenderer CreateWarning(string objectName, int pointCount, float width)
        {
            var warning = new GameObject(objectName) { layer = LayerMask.NameToLayer("PixelWorldVFX") };
            warning.transform.SetParent(transform, false);
            warning.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var line = warning.AddComponent<LineRenderer>();
            line.sharedMaterial = lightningMaterial;
            line.alignment = LineAlignment.TransformZ;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.positionCount = pointCount;
            line.widthMultiplier = width;
            var properties = new MaterialPropertyBlock();
            properties.SetFloat(TelegraphId, 1f);
            line.SetPropertyBlock(properties);
            return line;
        }

        public override void Cancel()
        {
            if (!_running) return;
            _running = false;
            _initialLane.enabled = _rotationCue.enabled = _arrow.enabled = false;
            if (_bot != null && _bot.ActiveSpiralSurge == this) _bot.ActiveSpiralSurge = null;
            NotifyBossControlReleased();
        }
    }
}
