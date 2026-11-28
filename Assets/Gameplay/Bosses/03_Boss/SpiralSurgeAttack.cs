namespace Game50033
{
    using UnityEngine;
    using UnityEngine.Rendering;

    public sealed class SpiralSurgeAttack : BaseBossAttack
    {
        private static readonly int TelegraphId = Shader.PropertyToID("_Telegraph");
        private static readonly int TelegraphColorId = Shader.PropertyToID("_TelegraphColor");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
        private const int LandingSegments = 32;
        private const int LandingSparkCount = 16;

        private struct BoltWarning
        {
            public LineRenderer Lane;
            public Vector3 Direction;
            public float FiresAt;
        }

        [SerializeField] private SpiralSurgeProjectile projectilePrefab;
        [SerializeField] private Material lightningMaterial;

        private Boss3Controller _bot;
        private BoltWarning[] _boltWarnings;
        private MeshRenderer _landingDisc;
        private Mesh _landingMesh;
        private Color[] _landingBurstColors;
        private readonly LineRenderer[] _landingSparks = new LineRenderer[LandingSparkCount];
        private readonly Vector3[] _sparkDirections = new Vector3[LandingSparkCount];
        private MaterialPropertyBlock _landingProperties;
        private Bounds _arena;
        private Vector3 _origin;
        private Vector3 _firstDirection;
        private float _speed;
        private int _shotCount;
        private int _shotsFired;
        private int _shotsWarned;
        private float _shotInterval;
        private float _warningLead;
        private float _nextWarningAt;
        private float _nextShotAt;
        private Vector3 _landingCenter;
        private float _landingRadius;
        private float _landsAt;
        private float _landingBurstStartedAt;
        private bool _landed;
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
            _shotInterval = tuning.spiralShotInterval;
            _warningLead = Mathf.Clamp(tuning.spiralBoltWarningLead, 0.05f, windUpTime);
            activeTime = Mathf.Max(0, _shotCount - 1) * _shotInterval;
            recoveryTime = tuning.spiralRecoveryDuration;
        }

        public override void Attack()
        {
            _running = true;
            _bot.ActiveSpiralSurge = this;
            startTime = Time.time;
            _landsAt = startTime + _bot.BeginSpiralCharge(windUpTime);
            _nextShotAt = startTime + windUpTime;
            _nextWarningAt = _nextShotAt - _warningLead;

            var tuning = _bot.Tuning;
            _landingRadius = tuning.spiralLandingRadius;
            _landingCenter = _bot.transform.position;
            _landingCenter.y = _arena.max.y + 0.025f;
            CreateLandingDisc();
            _landingProperties = new MaterialPropertyBlock();
            var random = new System.Random(GetInstanceID());
            for (var i = 0; i < LandingSparkCount; i++)
            {
                var angle = (i + (float)random.NextDouble() * 0.7f) * (360f / LandingSparkCount);
                _sparkDirections[i] = Quaternion.Euler(0f, angle, 0f) * Vector3.forward
                    * Mathf.Lerp(0.50f, 1f, (float)random.NextDouble());
                var spark = CreateLightningLine("Spiral Landing Spark", 3, 0.15f);
                spark.alignment = LineAlignment.View;
                spark.enabled = false;
                _landingSparks[i] = spark;
            }

            _boltWarnings = new BoltWarning[_shotCount];
            var warningOrigin = new Vector3(_origin.x, _arena.max.y + 0.04f, _origin.z);
            var lifetimeDistance = _speed * tuning.spiralLifetime;
            for (var i = 0; i < _boltWarnings.Length; i++)
            {
                var direction = Quaternion.AngleAxis(i * tuning.spiralDegreesPerShot, Vector3.up) * _firstDirection;
                var distance = Mathf.Min(lifetimeDistance,
                    SpiralSurgeProjectile.DistanceToEdge(_arena, _origin, direction, tuning.spiralRadius));
                var lane = CreateLightningLine("Spiral Bolt Warning", 2, tuning.spiralRadius * 2f);
                lane.SetPosition(0, warningOrigin);
                lane.SetPosition(1, warningOrigin + direction * distance);
                lane.enabled = false;
                _boltWarnings[i] = new BoltWarning { Lane = lane, Direction = direction };
            }
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

            var tuning = _bot.Tuning;
            if (!_landed && Time.time >= _landsAt)
            {
                _landed = true;
                _landingBurstStartedAt = Time.time;
                _landingMesh.colors = _landingBurstColors;
                var targets = _bot.Targets;
                for (var i = targets.Count - 1; i >= 0 && _running; i--)
                {
                    if (i >= targets.Count) continue;
                    var player = targets[i];
                    if (!player || !player.Hurtbox || !player.Hurtbox.IsAvailable) continue;
                    player.Hurtbox.GetWorldCapsule(out var bottom, out _, out var radius);
                    bottom.y = _landingCenter.y;
                    var toCenter = _landingCenter - bottom;
                    var reach = _landingRadius + radius;
                    if (toCenter.sqrMagnitude <= reach * reach)
                        player.Health.TryTakeDamage(tuning.spiralLandingDamage, toCenter.normalized, _bot);
                }
                if (!_running) return;
            }

            // the disc flashes briefly at contact, while sparks continue outward after it fades.
            if (_landed && (_landingDisc.enabled || _landingSparks[0].enabled))
            {
                var progress = Mathf.Clamp01((Time.time - _landingBurstStartedAt) / tuning.spiralLandingBurstDuration);
                _landingProperties.Clear();
                var remaining = 1f - progress;
                _landingProperties.SetFloat(FadeId, remaining * remaining);
                _landingProperties.SetFloat(GlowIntensityId, 2.4f);
                var travel = 1f - (1f - progress) * (1f - progress);
                for (var i = 0; i < LandingSparkCount; i++)
                {
                    var spark = _landingSparks[i];
                    var sparkDirection = _sparkDirections[i] * _landingRadius;
                    var height = 4f * travel * (1f - travel) * _landingRadius * (i % 2 == 0 ? 0.48f : 0.30f);
                    var root = _landingCenter + Vector3.up * (0.08f + height);
                    var reach = Mathf.Lerp(0.38f, tuning.spiralLandingSparkReach, travel);
                    var length = Mathf.Lerp(0.34f, 0.04f, progress);
                    var side = Vector3.Cross(Vector3.up, sparkDirection) * (i % 2 == 0 ? 0.12f : -0.12f);
                    spark.widthMultiplier = Mathf.Lerp(i % 2 == 0 ? 0.23f : 0.15f, 0.05f, progress);
                    spark.SetPosition(0, root + sparkDirection * (reach - length));
                    spark.SetPosition(1, root + sparkDirection * (reach - length * 0.55f) + side + Vector3.up * length * 0.5f);
                    spark.SetPosition(2, root + sparkDirection * reach);
                    spark.SetPropertyBlock(_landingProperties);
                    spark.enabled = progress < 1f;
                }

                var flash = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.55f));
                _landingProperties.SetFloat(FadeId, flash);
                _landingProperties.SetFloat(TelegraphId, 1f);
                var light = tuning.spiralLandingFlashIntensity;
                _landingProperties.SetVector(TelegraphColorId, new Vector4(light, light, light, 1f));
                _landingDisc.SetPropertyBlock(_landingProperties);
                _landingDisc.enabled = flash > 0f;
            }

            var now = Time.time;
            if (_shotsWarned < _shotCount && now >= _nextWarningAt)
            {
                // a delayed frame can extend a warning, but a bolt still gets its full lead time.
                ref var warning = ref _boltWarnings[_shotsWarned++];
                warning.Lane.enabled = true;
                warning.FiresAt = now + _warningLead;
                _nextWarningAt = now + _shotInterval;
            }

            if (!_landed || _shotsWarned > _shotsFired)
            {
                var pulse = 0.5f - 0.5f * Mathf.Cos((now - startTime) * tuning.spiralWarningPulseHz * 2f * Mathf.PI);
                var red = Mathf.Lerp(tuning.spiralWarningGlow.x, tuning.spiralWarningGlow.y, pulse);
                _landingProperties.Clear();
                _landingProperties.SetFloat(TelegraphId, 1f);
                _landingProperties.SetVector(TelegraphColorId, new Vector4(red, red * 0.035f, red * 0.055f, 0.90f));
                if (!_landed) _landingDisc.SetPropertyBlock(_landingProperties);
                for (var i = _shotsFired; i < _shotsWarned; i++)
                    _boltWarnings[i].Lane.SetPropertyBlock(_landingProperties);
            }

            if (_shotsFired >= _shotsWarned || now < _nextShotAt || now < _boltWarnings[_shotsFired].FiresAt) return;

            var shot = _boltWarnings[_shotsFired];
            shot.Lane.enabled = false;
            _bot.LaunchSpiralBolt(projectilePrefab, _origin, shot.Direction, _speed);
            _shotsFired++;
            if (_shotsFired >= _shotCount)
                Cancel();
            else
                _nextShotAt = now + _shotInterval;
        }

        private void CreateLandingDisc()
        {
            var disc = new GameObject("Spiral Landing Circle") { layer = LayerMask.NameToLayer("PixelWorldVFX") };
            disc.transform.SetParent(transform, false);
            disc.transform.position = _landingCenter;
            _landingDisc = disc.AddComponent<MeshRenderer>();
            _landingDisc.sharedMaterial = lightningMaterial;
            _landingDisc.shadowCastingMode = ShadowCastingMode.Off;
            _landingDisc.receiveShadows = false;

            // shared shader UVs keep the fill solid. the extra vertex ring shapes the blue-white flash.
            var vertices = new Vector3[1 + LandingSegments * 2];
            var uv = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            _landingBurstColors = new Color[vertices.Length];
            var indices = new int[LandingSegments * 9];
            uv[0] = new Vector2(0.5f, 0.5f);
            colors[0] = _landingBurstColors[0] = Color.white;
            for (var i = 0; i < LandingSegments; i++)
            {
                var direction = Quaternion.Euler(0f, i * (360f / LandingSegments), 0f) * Vector3.forward;
                var inner = 1 + i;
                var outer = inner + LandingSegments;
                var innerNext = 1 + (i + 1) % LandingSegments;
                var outerNext = innerNext + LandingSegments;
                vertices[inner] = direction * (_landingRadius * 0.35f);
                vertices[outer] = direction * _landingRadius;
                uv[inner] = new Vector2(0.5f, 0.60f);
                uv[outer] = new Vector2(0.5f, 0.78f);
                colors[inner] = colors[outer] = Color.white;
                _landingBurstColors[inner] = new Color(0.55f, 0.84f, 1f);
                _landingBurstColors[outer] = new Color(0.02f, 0.36f, 1f);
                var start = i * 9;
                indices[start] = 0;
                indices[start + 1] = inner;
                indices[start + 2] = innerNext;
                indices[start + 3] = inner;
                indices[start + 4] = outer;
                indices[start + 5] = outerNext;
                indices[start + 6] = inner;
                indices[start + 7] = outerNext;
                indices[start + 8] = innerNext;
            }
            _landingMesh = new Mesh { name = "Spiral Landing Disc", vertices = vertices, uv = uv, colors = colors, triangles = indices };
            disc.AddComponent<MeshFilter>().sharedMesh = _landingMesh;
        }

        private LineRenderer CreateLightningLine(string objectName, int pointCount, float width)
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
            return line;
        }

        public override void Cancel()
        {
            if (!_running) return;
            _running = false;
            foreach (var warning in _boltWarnings) warning.Lane.enabled = false;
            _landingDisc.enabled = false;
            foreach (var spark in _landingSparks) spark.enabled = false;
            if (_bot && _bot.ActiveSpiralSurge == this)
            {
                _bot.FinishSpiralCharge(_shotsFired >= _shotCount ? recoveryTime : 0f);
                _bot.ActiveSpiralSurge = null;
            }
            NotifyBossControlReleased();
        }

        private void OnDestroy()
        {
            if (_landingMesh) Destroy(_landingMesh);
        }
    }
}
