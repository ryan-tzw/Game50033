namespace Game50033
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Rendering;

    public sealed class ElectricKnivesAttack : BaseBossAttack
    {
        private static readonly int TelegraphId = Shader.PropertyToID("_Telegraph");
        private static readonly int ThrowState = Animator.StringToHash("Base Layer.ThrowKunai");
        private static readonly int IdleState = Animator.StringToHash("Base Layer.Idle");
        private static readonly int ThrowSpeed = Animator.StringToHash("KunaiSpeed");
        // frame 04 is the release pose in the 12 fps clip, including its final held pose.
        private const float ReleaseTime = 4f / 12f;
        private const float ThrowClipDuration = 1f;
        private const int CircleSegments = 32;

        [SerializeField] private ElectricKnifeProjectile projectilePrefab;
        [SerializeField] private Material lightningMaterial;

        private readonly List<Vector3> _landings = new();
        private Boss3Controller _bot;
        private Animator _animator;
        private Vector3 _origin;
        private float _speed;
        private float _throwAt;
        private bool _running;
        private bool _released;
        private Mesh _warningMesh;
        private MeshRenderer _warningRenderer;

        public override bool CanStart => _landings.Count > 0;
        public override bool ControlsBoss => true;
        public override float BossControlTimeout => windUpTime + 1f;

        public override void Setup(BaseBoss owner)
        {
            base.Setup(owner);
            _bot = owner as Boss3Controller;
            if (!_bot || !_bot.ArenaFloor || !_bot.Tuning || !projectilePrefab || !lightningMaterial) return;

            var tuning = _bot.Tuning;
            var arena = _bot.ArenaFloor.bounds;
            var margin = Mathf.Max(tuning.knifeBlastRadius, tuning.knifeLaneWidth * 0.5f);
            if (arena.extents.x <= margin || arena.extents.z <= margin) return;

            _landings.Clear();
            _animator = _bot.GetComponentInChildren<Animator>();
            var center = _bot.transform.position;
            center.y = arena.max.y + 0.05f;
            // inset the center near walls so even outward spokes retain a nonzero lane.
            var extraInset = Mathf.Min(0.35f, (Mathf.Min(arena.extents.x, arena.extents.z) - margin) * 0.5f);
            _origin = SpiralSurgeProjectile.ClampToArena(arena, center, margin + extraInset);
            var meter = _bot.GetComponent<BossDiscordance>();
            var count = Mathf.Clamp(Mathf.CeilToInt(tuning.knifeThrowCount * (meter ? meter.ProjectileCountMultiplier : 1f)),
                1, Mathf.Max(1, tuning.knifeMaxActive));
            _speed = tuning.knifeSpeed * (meter ? meter.ProjectileSpeedMultiplier : 1f);
            var startAngle = 90f - 180f / count;
            for (var i = 0; i < count; i++)
            {
                var direction = Quaternion.Euler(0f, startAngle + i * (360f / count), 0f) * Vector3.forward;
                var reach = Mathf.Min(tuning.knifeThrowRadius,
                    SpiralSurgeProjectile.DistanceToEdge(arena, _origin, direction, margin));
                _landings.Add(_origin + direction * reach);
            }

            windUpTime = tuning.knifeTelegraphDuration;
            recoveryTime = tuning.knifeRecoveryDuration;
        }

        public override void Attack()
        {
            _running = true;
            _released = false;
            _bot.ActiveElectricKnives = this;
            _throwAt = Time.time + windUpTime;
            if (_animator && _animator.HasState(0, ThrowState))
            {
                _animator.SetFloat(ThrowSpeed, ReleaseTime / Mathf.Max(0.01f, windUpTime));
                _animator.Play(ThrowState, 0, 0f);
            }

            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            var tuning = _bot.Tuning;
            foreach (var landing in _landings)
            {
                // reduce fill at the tip so the circle can be read over the lane.
                var start = vertices.Count;
                var lateral = Vector3.Cross(Vector3.up, (landing - _origin).normalized) * (tuning.knifeLaneWidth * 0.5f);
                vertices.Add(_origin - lateral);
                vertices.Add(_origin + lateral);
                vertices.Add(landing - lateral);
                vertices.Add(landing + lateral);
                uv.Add(new Vector2(0f, 0f));
                uv.Add(new Vector2(0f, 1f));
                uv.Add(new Vector2(1f, 0f));
                uv.Add(new Vector2(1f, 1f));
                colors.Add(Color.white);
                colors.Add(Color.white);
                colors.Add(new Color(1f, 1f, 1f, 0.55f));
                colors.Add(colors[^1]);
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 1);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start + 3);

                start = vertices.Count;
                vertices.Add(landing + Vector3.up * 0.01f);
                uv.Add(new Vector2(0f, 0.5f));
                colors.Add(Color.white);
                for (var i = 0; i < CircleSegments; i++)
                {
                    var angle = i * (2f * Mathf.PI / CircleSegments);
                    var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    vertices.Add(vertices[start] + radial * tuning.knifeBlastRadius);
                    uv.Add(new Vector2(0f, 0.94f));
                    colors.Add(Color.white);
                    triangles.Add(start);
                    triangles.Add(start + 1 + (i + 1) % CircleSegments);
                    triangles.Add(start + 1 + i);
                }
            }

            var warning = new GameObject("Knife Lanes and Landing Areas") { layer = LayerMask.NameToLayer("PixelWorldVFX") };
            warning.transform.SetParent(transform, true);
            _warningMesh = new Mesh { name = "Electric Knife Warnings" };
            _warningMesh.SetVertices(vertices);
            _warningMesh.SetUVs(0, uv);
            _warningMesh.SetColors(colors);
            _warningMesh.SetTriangles(triangles, 0);
            warning.AddComponent<MeshFilter>().sharedMesh = _warningMesh;
            _warningRenderer = warning.AddComponent<MeshRenderer>();
            _warningRenderer.sharedMaterial = lightningMaterial;
            _warningRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _warningRenderer.receiveShadows = false;
            var properties = new MaterialPropertyBlock();
            properties.SetFloat(TelegraphId, 1f);
            _warningRenderer.SetPropertyBlock(properties);
        }

        private void Update()
        {
            if (!_running) return;
            if (!_bot || !_bot.isActiveAndEnabled || _bot.IsDefeated)
            {
                Cancel();
                return;
            }
            if (Time.time < _throwAt) return;

            _released = true;
            if (_animator && _animator.HasState(0, ThrowState))
            {
                _animator.SetFloat(ThrowSpeed, 1f);
                _animator.Play(ThrowState, 0, ReleaseTime / ThrowClipDuration);
            }
            foreach (var landing in _landings)
                _bot.LaunchKnife(projectilePrefab, _origin, landing, _speed);
            Cancel();
        }

        public override void Cancel()
        {
            if (!_running) return;
            _running = false;
            if (_warningRenderer) _warningRenderer.enabled = false;
            if (!_released && _animator && _animator.HasState(0, ThrowState))
            {
                _animator.SetFloat(ThrowSpeed, 1f);
                _animator.Play(IdleState, 0, 0f);
            }
            if (_bot && _bot.ActiveElectricKnives == this) _bot.ActiveElectricKnives = null;
            NotifyBossControlReleased();
        }

        private void OnDestroy()
        {
            if (_warningMesh) Destroy(_warningMesh);
        }
    }
}
