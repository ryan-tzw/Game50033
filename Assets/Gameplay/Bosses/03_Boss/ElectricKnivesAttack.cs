namespace Game50033
{
    using UnityEngine;
    using UnityEngine.Rendering;

    public sealed class ElectricKnivesAttack : BaseBossAttack
    {
        internal const int KnivesPerCast = 8;
        private static readonly int TelegraphColorId = Shader.PropertyToID("_TelegraphColor");
        private static readonly int TelegraphId = Shader.PropertyToID("_Telegraph");
        private static readonly int ThrowState = Animator.StringToHash("Base Layer.ThrowKunai");
        private static readonly int IdleState = Animator.StringToHash("Base Layer.Idle");
        private static readonly int ThrowSpeed = Animator.StringToHash("KunaiSpeed");
        private const int ReleaseFrame = 4;
        private const int FallTransitionFrame = 8;
        private const int FirstFallLoopFrame = 9;
        private const float FallFrameDuration = 1f / 12f;
        private const float ThrowClipSamples = 12f;
        private const int CircleSegments = 48;
        private const float LowRed = 1.7f;
        private const float HighRed = 3.4f;

        private struct Landing
        {
            public Player Target;
            public Vector3 Position;
            public MeshFilter Marker;
        }

        [SerializeField] private ElectricKnifeProjectile projectilePrefab;
        [SerializeField] private Material lightningMaterial;

        private readonly Landing[] _landings = new Landing[KnivesPerCast];
        private System.Random _placementRandom;
        private MeshRenderer[] _warningRenderers;
        private readonly LineRenderer[] _warningOutlines = new LineRenderer[KnivesPerCast];
        private MaterialPropertyBlock _warningProperties;
        private Bounds _arena;
        private float _impactRadius;
        private float _targetingDuration;
        private float _reactionDuration;
        private float _lockedAt;
        private float _lockFlashDuration;
        private float _lockFlashIntensity;
        private float _frame02HoldFraction;
        private bool _trackingStarted;
        private bool _locked;
        private bool _canStart;
        private Boss3Controller _bot;
        private Animator _animator;
        private Vector3 _origin;
        private float _speed;
        private float _throwAt;
        private float _riseAt;
        private float _apexAt;
        private float _fallAt;
        private float _touchdownAt;
        private float _riseDuration;
        private float _preReleaseHold;
        private float _postReleaseHold;
        private float _dropDuration;
        private float _jumpHeight;
        private float _apexDrift;
        private float _spriteRestY;
        private int _pose = -1;
        private bool _animating;
        private bool _running;
        private bool _released;
        public override bool CanStart => _canStart;
        public override bool ControlsBoss => true;
        public override float BossControlTimeout => windUpTime + activeTime + 1f;

        public override void Setup(BaseBoss owner)
        {
            base.Setup(owner);
            _bot = owner as Boss3Controller;
            if (!_bot || !_bot.ArenaFloor || !_bot.Tuning || !projectilePrefab || !lightningMaterial) return;

            var tuning = _bot.Tuning;
            _arena = _bot.ArenaFloor.bounds;
            _impactRadius = Mathf.Max(0.01f, tuning.knifeBlastRadius);
            if (_arena.extents.x < _impactRadius || _arena.extents.z < _impactRadius) return;

            _animator = _bot.GetComponentInChildren<Animator>();
            _origin = _bot.transform.position;
            _origin.y = _arena.max.y + 0.05f;
            var meter = _bot.GetComponent<BossDiscordance>();
            _speed = tuning.knifeSpeed * (meter ? meter.ProjectileSpeedMultiplier : 1f);

            _riseDuration = Mathf.Max(0.01f, tuning.knifeRiseDuration);
            _targetingDuration = Mathf.Max(0.01f, tuning.knifeTargetingDuration);
            _reactionDuration = Mathf.Max(0.01f, tuning.knifeLockReactionDuration);
            _preReleaseHold = _targetingDuration + _reactionDuration;
            _postReleaseHold = Mathf.Max(0.01f, tuning.knifePostReleaseHold);
            _frame02HoldFraction = Mathf.Clamp01(tuning.knifeFrame02HoldFraction);
            windUpTime = _riseDuration + _preReleaseHold;
            _dropDuration = Mathf.Max(0.01f, tuning.knifeDropDuration);
            _jumpHeight = Mathf.Max(0f, tuning.knifeJumpHeight);
            _apexDrift = _jumpHeight * Mathf.Clamp01(tuning.knifeApexDriftFraction);
            activeTime = _postReleaseHold + _dropDuration;
            recoveryTime = Mathf.Max(0f, tuning.knifeRecoveryDuration - activeTime);
            _canStart = true;
        }

        public override void Attack()
        {
            _running = true;
            _released = false;
            _bot.ActiveElectricKnives = this;
            _riseAt = Time.time;
            _apexAt = _riseAt + _riseDuration;
            _animating = _animator && _animator.isActiveAndEnabled && _animator.HasState(0, ThrowState);
            if (_animating)
            {
                _spriteRestY = _animator.transform.localPosition.y;
                _animator.SetFloat(ThrowSpeed, 0f);
            }

        }

        private void BeginTracking()
        {
            _trackingStarted = true;
            _lockedAt = Time.time + _targetingDuration;
            _throwAt = _lockedAt + _reactionDuration;
            _placementRandom = new System.Random(GetInstanceID());
            _warningProperties = new MaterialPropertyBlock();
            _warningRenderers = new MeshRenderer[KnivesPerCast];
            var playerCount = 0;
            foreach (var player in _bot.Targets)
            {
                if (!player || !player.isActiveAndEnabled || player.Health.IsDowned) continue;
                ref var landing = ref _landings[playerCount++];
                landing.Target = player;
                landing.Position = ClampLanding(player.transform.position);
                if (playerCount == KnivesPerCast) break;
            }

            var startAngle = (float)_placementRandom.NextDouble() * 360f;
            var radialStep = 360f / Mathf.Max(1, KnivesPerCast - playerCount);
            for (var i = playerCount; i < KnivesPerCast; i++)
                _landings[i].Position = RadialLanding(startAngle + (i - playerCount) * radialStep, i, i);

            var vertices = new Vector3[1 + CircleSegments * 2];
            var uvs = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new int[CircleSegments * 9];
            uvs[0] = new Vector2(0.5f, 0.5f);
            colors[0] = Color.white;
            for (var i = 0; i < CircleSegments; i++)
            {
                var angle = i * (2f * Mathf.PI / CircleSegments);
                var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _impactRadius;
                var inner = i + 1;
                var outer = inner + CircleSegments;
                var next = (i + 1) % CircleSegments + 1;
                vertices[inner] = radial * 0.82f;
                vertices[outer] = radial;
                uvs[inner] = uvs[0];
                uvs[outer] = new Vector2(0.5f, 0.93f);
                colors[inner] = colors[outer] = Color.white;
                var triangle = i * 9;
                triangles[triangle] = 0;
                triangles[triangle + 1] = next;
                triangles[triangle + 2] = inner;
                triangles[triangle + 3] = inner;
                triangles[triangle + 4] = next;
                triangles[triangle + 5] = outer;
                triangles[triangle + 6] = outer;
                triangles[triangle + 7] = next;
                triangles[triangle + 8] = next + CircleSegments;
            }

            for (var i = 0; i < _landings.Length; i++)
            {
                ref var landing = ref _landings[i];
                var circle = new GameObject("Kunai Landing Warning") { layer = LayerMask.NameToLayer("PixelWorldVFX") };
                circle.transform.SetParent(transform, false);
                circle.transform.position = landing.Position;
                var mesh = new Mesh { name = "Kunai Landing Disc", vertices = vertices, uv = uvs, colors = colors, triangles = triangles };
                landing.Marker = circle.AddComponent<MeshFilter>();
                landing.Marker.sharedMesh = mesh;
                var renderer = circle.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = lightningMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                _warningRenderers[i] = renderer;

                // the outline follows the disc and transfers with it when the knife launches.
                var ring = new GameObject("Kunai Lock Outline") { layer = circle.layer };
                ring.transform.SetParent(circle.transform, false);
                ring.transform.SetLocalPositionAndRotation(Vector3.up * 0.015f, Quaternion.Euler(90f, 0f, 0f));
                var outline = ring.AddComponent<LineRenderer>();
                outline.sharedMaterial = lightningMaterial;
                outline.shadowCastingMode = ShadowCastingMode.Off;
                outline.receiveShadows = false;
                outline.sortingOrder = renderer.sortingOrder + 1;
                outline.useWorldSpace = false;
                outline.alignment = LineAlignment.TransformZ;
                outline.loop = true;
                outline.widthMultiplier = Mathf.Min(0.1f, _impactRadius * 0.3f);
                outline.positionCount = CircleSegments;
                outline.textureMode = LineTextureMode.Stretch;
                for (var vertex = 0; vertex < CircleSegments; vertex++)
                {
                    var point = vertices[vertex + 1 + CircleSegments];
                    outline.SetPosition(vertex, new Vector3(point.x, point.z, 0f));
                }
                _warningOutlines[i] = outline;
            }
            _warningProperties.SetFloat(TelegraphId, 1f);
        }

        private Vector3 ClampLanding(Vector3 point)
        {
            point.y = _origin.y;
            return SpiralSurgeProjectile.ClampToArena(_arena, point, _impactRadius);
        }

        private Vector3 RadialLanding(float angle, int existingCount, int skip)
        {
            var tuning = _bot.Tuning;
            angle += Mathf.Lerp(-tuning.knifeAngularVariation, tuning.knifeAngularVariation, (float)_placementRandom.NextDouble());
            var radius = tuning.knifeThrowRadius * Mathf.Lerp(1f - tuning.knifeRadiusVariation,
                1f + tuning.knifeRadiusVariation, (float)_placementRandom.NextDouble());
            var best = _origin;
            var bestClearance = -1f;
            // try neighboring bearings if a wall or another circle crowds the preferred placement.
            for (var attempt = 0; attempt < 16; attempt++)
            {
                var offset = (attempt % 2 == 0 ? 1f : -1f) * ((attempt + 1) / 2) * 12f;
                var direction = Quaternion.Euler(0f, angle + offset, 0f) * Vector3.forward;
                var candidate = ClampLanding(_origin + direction * radius);
                var clearance = float.PositiveInfinity;
                for (var i = 0; i < existingCount; i++)
                {
                    if (i == skip) continue;
                    clearance = Mathf.Min(clearance, (candidate - _landings[i].Position).sqrMagnitude);
                }
                if (clearance >= 4f * _impactRadius * _impactRadius) return candidate;
                if (clearance <= bestClearance) continue;
                best = candidate;
                bestClearance = clearance;
            }
            return best;
        }

        private void Update()
        {
            if (!_running) return;
            if (!_bot || !_bot.isActiveAndEnabled || _bot.IsDefeated)
            {
                Cancel();
                return;
            }
            var now = Time.time;
            if (!_trackingStarted && now >= _apexAt)
            {
                // measure both warning windows from the frames on which players actually see them.
                _apexAt = now;
                BeginTracking();
            }

            if (_trackingStarted && !_released)
            {
                var tuning = _bot.Tuning;
                if (!_locked)
                {
                    for (var i = 0; i < _landings.Length; i++)
                    {
                        ref var landing = ref _landings[i];
                        if (ReferenceEquals(landing.Target, null)) continue;
                        var eligible = false;
                        if (landing.Target && landing.Target.isActiveAndEnabled && !landing.Target.Health.IsDowned)
                        {
                            foreach (var player in _bot.Targets)
                            {
                                if (player != landing.Target) continue;
                                eligible = true;
                                break;
                            }
                        }

                        if (eligible)
                            landing.Position = ClampLanding(landing.Target.transform.position);
                        else
                        {
                            landing.Target = null;
                            landing.Position = RadialLanding((float)_placementRandom.NextDouble() * 360f, KnivesPerCast, i);
                        }
                        landing.Marker.transform.position = landing.Position;
                    }
                    if (now >= _lockedAt)
                    {
                        _locked = true;
                        _lockedAt = now;
                        _lockFlashDuration = Mathf.Max(0.01f, tuning.knifeLockFlashDuration);
                        _lockFlashIntensity = Mathf.Max(HighRed + 0.5f, tuning.knifeLockFlashIntensity);
                        _throwAt = now + _reactionDuration;
                    }
                }

                var elapsed = now - _apexAt;
                var trackingProgress = _locked ? 1f : Mathf.Clamp01(elapsed / _targetingDuration);
                var startHz = Mathf.Max(0f, tuning.knifeTrackingPulseHz.x);
                var endHz = Mathf.Max(startHz, tuning.knifeTrackingPulseHz.y);
                var cycles = elapsed * (startHz + 0.5f * (endHz - startHz) * trackingProgress);
                var red = _locked ? LowRed + 0.35f : Mathf.Lerp(LowRed, HighRed, 0.5f - 0.5f * Mathf.Cos(cycles * 2f * Mathf.PI));
                _warningProperties.SetVector(TelegraphColorId, new Vector4(red, red * 0.035f, red * 0.055f, 0.90f));
                foreach (var warning in _warningRenderers) warning.SetPropertyBlock(_warningProperties);

                var outlineRed = Mathf.SmoothStep(0.85f, HighRed, trackingProgress);
                var outlineColor = _locked
                    ? ElectricKnifeProjectile.EvaluateLockOutlineColor(now - _lockedAt, _lockFlashDuration, _lockFlashIntensity)
                    : new Vector4(outlineRed, outlineRed * 0.05f, outlineRed * 0.07f, 0.98f);
                _warningProperties.SetVector(TelegraphColorId, outlineColor);
                var outlineScale = Mathf.Lerp(Mathf.Max(1f, tuning.knifeLockOutlineStartScale), 1f, trackingProgress);
                foreach (var outline in _warningOutlines)
                {
                    outline.transform.localScale = new Vector3(outlineScale, outlineScale, 1f);
                    outline.SetPropertyBlock(_warningProperties);
                }
            }

            var apexElapsed = Mathf.Min(now, _throwAt) - _apexAt;
            if (_released) apexElapsed += now - (_fallAt - _postReleaseHold);
            var apexProgress = Mathf.Clamp01(apexElapsed / (_preReleaseHold + _postReleaseHold));
            var apexLift = _jumpHeight - _apexDrift * apexProgress * apexProgress;
            int pose;
            float spriteLift;
            if (now < _apexAt)
            {
                var riseProgress = Mathf.InverseLerp(_riseAt, _apexAt, now);
                pose = riseProgress < 0.5f ? 0 : 1;
                spriteLift = _jumpHeight * riseProgress;
            }
            else if (!_locked || now < _throwAt)
            {
                pose = now - _apexAt < _preReleaseHold * _frame02HoldFraction ? 2 : 3;
                spriteLift = apexLift;
            }
            else if (!_released)
            {
                // show the smear on the same update that spawns the airborne knives.
                pose = ReleaseFrame;
                spriteLift = apexLift;
                _fallAt = now + _postReleaseHold;
                _touchdownAt = _fallAt + _dropDuration;
            }
            else if (now < _fallAt)
            {
                pose = ReleaseFrame + Mathf.Min(3, Mathf.FloorToInt((now - (_fallAt - _postReleaseHold)) / _postReleaseHold * 4f));
                spriteLift = apexLift;
            }
            else if (now < _touchdownAt)
            {
                var fallProgress = Mathf.InverseLerp(_fallAt, _touchdownAt, now);
                var fallElapsed = now - _fallAt;
                var transitionDuration = Mathf.Min(FallFrameDuration, _dropDuration / 3f);
                // fit both loop poses into short falls, then keep alternating at the clip cadence.
                var loopFrameDuration = Mathf.Min(FallFrameDuration, (_dropDuration - transitionDuration) * 0.5f);
                pose = fallElapsed < transitionDuration ? FallTransitionFrame
                    : FirstFallLoopFrame + Mathf.FloorToInt((fallElapsed - transitionDuration) / loopFrameDuration) % 2;
                spriteLift = (_jumpHeight - _apexDrift) * (1f - Mathf.SmoothStep(0f, 1f, fallProgress));
            }
            else
            {
                Cancel();
                return;
            }

            if (_animating && _animator)
            {
                if (pose != _pose)
                {
                    _pose = pose;
                    _animator.Play(ThrowState, 0, (pose + 0.25f) / ThrowClipSamples);
                    _animator.Update(0f);
                }
                var visual = _animator.transform;
                var position = visual.localPosition;
                position.y = _spriteRestY + spriteLift;
                visual.localPosition = position;
            }

            if (_released || pose != ReleaseFrame) return;
            _released = true;
            var launch = _bot.transform.position;
            var releaseHeight = Mathf.Max(0f, launch.y + _spriteRestY + spriteLift + ElectricKnifeProjectile.ThrowHandHeight - _origin.y);
            launch.y = _origin.y;
            for (var i = 0; i < _landings.Length; i++)
            {
                ref var landing = ref _landings[i];
                _bot.LaunchKnife(projectilePrefab, launch, landing.Position, _speed, releaseHeight, _impactRadius, landing.Marker,
                    _lockedAt, _lockFlashDuration, _lockFlashIntensity);
                landing.Marker = null;
                _warningRenderers[i] = null;
            }
        }

        public override void Cancel()
        {
            if (!_running) return;
            _running = false;
            foreach (var landing in _landings)
            {
                if (!landing.Marker) continue;
                landing.Marker.gameObject.SetActive(false);
                Destroy(landing.Marker.sharedMesh);
                Destroy(landing.Marker.gameObject);
            }
            if (_animating && _animator)
            {
                var visual = _animator.transform;
                var position = visual.localPosition;
                position.y = _spriteRestY;
                visual.localPosition = position;
                _animator.SetFloat(ThrowSpeed, 1f);
                if (_animator.isActiveAndEnabled)
                {
                    _animator.Play(IdleState, 0, 0f);
                    _animator.Update(0f);
                }
                if (_bot && _bot.KunaiOverlay) _bot.KunaiOverlay.sprite = null;
            }
            if (_bot && _bot.ActiveElectricKnives == this) _bot.ActiveElectricKnives = null;
            NotifyBossControlReleased();
        }
    }
}
