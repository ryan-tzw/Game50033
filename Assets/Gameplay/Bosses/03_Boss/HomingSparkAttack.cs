namespace Game50033
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Rendering;

    public sealed class HomingSparkAttack : BaseBossAttack
    {
        private static readonly int TelegraphId = Shader.PropertyToID("_Telegraph");
        private static readonly int ShootId = Animator.StringToHash("Attack");
        private const int RingSegments = 32;

        [SerializeField] private HomingSparkProjectile projectilePrefab;
        [SerializeField] private Material lightningMaterial;

        private readonly List<Player> _targets = new(4);
        private Boss3Controller _bot;
        private LineRenderer[] _warnings;
        private LineRenderer _chargeRing;
        private float _speed;
        private float _nextReleaseAt;
        private bool _running;

        public override bool CanStart => _targets.Count > 0;
        public override bool ControlsBoss => true;
        public override float BossControlTimeout => windUpTime + 1f;

        public override void Setup(BaseBoss owner)
        {
            base.Setup(owner);
            _bot = owner as Boss3Controller;
            if (_bot == null || !_bot.ArenaFloor || !_bot.Tuning || !projectilePrefab || !lightningMaterial) return;

            var tuning = _bot.Tuning;
            var meter = _bot.GetComponent<BossDiscordance>();
            _speed = tuning.sparkSpeed * (meter != null ? meter.ProjectileSpeedMultiplier : 1f);
            foreach (var player in _bot.Targets)
                if (HomingSparkProjectile.CanTrack(player)) _targets.Add(player);

            windUpTime = tuning.sparkTelegraphDuration;
            recoveryTime = tuning.sparkRecoveryDuration;
        }

        public override void Attack()
        {
            _running = true;
            _bot.ActiveSparkAttack = this;
            _nextReleaseAt = Time.time + windUpTime;
            _warnings = new LineRenderer[_targets.Count];
            for (var i = 0; i < _warnings.Length; i++)
                _warnings[i] = CreateRing("Spark Target Warning");
            _chargeRing = CreateRing("Spark Charge");
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

            var floorHeight = _bot.ArenaFloor.bounds.max.y;
            var origin = _bot.transform.position;
            origin.y = floorHeight + 0.19f;

            if (Time.time >= _nextReleaseAt)
            {
                var fired = false;
                foreach (var target in _targets)
                {
                    if (!HomingSparkProjectile.CanTrack(target)) continue;
                    fired = true;
                    _bot.LaunchSpark(projectilePrefab, target, origin, _speed);
                }
                if (fired) _bot.PlayAnimationTrigger(ShootId);
                Cancel();
                return;
            }

            for (var i = 0; i < _warnings.Length; i++)
            {
                var player = _targets[i];
                var visible = HomingSparkProjectile.CanTrack(player);
                _warnings[i].enabled = visible;
                if (!visible) continue;

                var position = player.transform.position;
                position.y = floorHeight + 0.04f;
                DrawRing(_warnings[i], position, 0.55f);
            }

            origin.y = floorHeight + 0.04f;
            var progress = 1f - Mathf.Clamp01((_nextReleaseAt - Time.time) / windUpTime);
            DrawRing(_chargeRing, origin, Mathf.Lerp(0.64f, 0.45f, progress));
        }

        private LineRenderer CreateRing(string objectName)
        {
            var ring = new GameObject(objectName) { layer = LayerMask.NameToLayer("PixelWorldVFX") };
            ring.transform.SetParent(transform, false);
            ring.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var line = ring.AddComponent<LineRenderer>();
            line.sharedMaterial = lightningMaterial;
            line.alignment = LineAlignment.TransformZ;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = RingSegments;
            line.widthMultiplier = 0.10f;
            var properties = new MaterialPropertyBlock();
            properties.SetFloat(TelegraphId, 1f);
            line.SetPropertyBlock(properties);
            return line;
        }

        private static void DrawRing(LineRenderer line, Vector3 position, float radius)
        {
            for (var i = 0; i < RingSegments; i++)
            {
                var angle = i * (Mathf.PI * 2f / RingSegments);
                line.SetPosition(i, position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
            }
        }

        public override void Cancel()
        {
            if (!_running) return;
            _running = false;
            foreach (var warning in _warnings) warning.enabled = false;
            if (_chargeRing != null) _chargeRing.enabled = false;
            if (_bot != null && _bot.ActiveSparkAttack == this) _bot.ActiveSparkAttack = null;
            NotifyBossControlReleased();
        }
    }
}
