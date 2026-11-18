namespace Game50033
{
    using UnityEngine;
    using UnityEngine.Rendering;
    using UnityEngine.SceneManagement;

    [DefaultExecutionOrder(250)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FluteAbilities), typeof(FluteFlow))]
    public sealed class FluteWeaponVisual : FloatingWeaponVisual
    {
        [SerializeField] private FluteTuning tuning;

        private Player _player;
        private FluteAbilities _abilities;
        private FluteFlow _flow;
        private Transform _root;
        private Transform _weapon;
        private SpriteRenderer _tube;
        private Sprite _placeholder;
        private TrailRenderer _trail;
        private LineRenderer _flowTrack;
        private LineRenderer _flowFill;
        private LineRenderer _flowCost;
        private LineRenderer _chargeRing;
        private LineRenderer _chargeProgress;
        private LineRenderer _evasionRing;
        private FluteAbilities.Action _previousAction;
        private float _displayedFlow;
        private float _displayedCharge;
        private float _releaseStrength;
        private float _previousFlow;
        private bool _losingFlow;
        private float _successAt = float.NegativeInfinity;
        private bool _placed;
        private MelodyNote[] _notes;
        private int _driftingNoteCount;
        private int _noteSequence;
        private float _noteRate;
        private float _noteEmission;
        private float _chargeNoteVisibility;
        private LineRenderer _melodyRibbon;
        private MelodyNote[] _evasionEchoes;
        private int _echoesEmitted;
        private int _projectileVisualIndex;
        private float _fullChargeStartedAt = float.NegativeInfinity;
        private bool _wasFullyCharged;
        private float _scatterReadyAt;

        private struct MelodyNote
        {
            public SpriteRenderer Renderer;
            public Vector3 Origin;
            public Vector3 Drift;
            public Quaternion Rotation;
            public float BornAt;
            public float Lifetime;
            public float Phase;
            public float Size;
            public bool Gold;
        }

        private void Awake()
        {
            _player = GetComponent<Player>();
            _abilities = GetComponent<FluteAbilities>();
            _flow = GetComponent<FluteFlow>();
            if (tuning == null || tuning.visuals.material == null || tuning.visuals.weaponMaterial == null
                || tuning.visuals.noteMaterial == null)
            {
                Debug.LogError("FluteWeaponVisual requires Flute tuning and a sprite/trail material.", this);
                enabled = false;
                return;
            }
            BuildVisuals();
        }

        private void BuildVisuals()
        {
            var settings = tuning.visuals;

            _root = new GameObject("Flute presentation").transform;
            _root.SetParent(transform, false);

            _weapon = new GameObject("Floating flute").transform;
            _weapon.SetParent(_root, false);

            _placeholder = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 2f, 2f),
                new Vector2(0.5f, 0.5f), 2f, 0, SpriteMeshType.FullRect);
            _placeholder.name = "Replaceable flute placeholder";

            _tube = CreateSprite("Flute", Vector3.zero, settings.weaponSize, Color.white);

            if (settings.weaponSprite != null)
            {
                _tube.sprite = settings.weaponSprite;

                var bounds = settings.weaponSprite.bounds.size;

                _tube.transform.localScale = new Vector3(settings.weaponSize.x / Mathf.Max(0.001f, bounds.x),
                    settings.weaponSize.y / Mathf.Max(0.001f, bounds.y), 1f);
            }
            else
            {
                // Keep the placeholder recognisable without importing another art dependency.
                CreateSprite("Mouthpiece", new Vector3(-settings.weaponSize.x * 0.35f, 0f, -0.001f),
                    new Vector2(0.07f, settings.weaponSize.y * 1.3f), settings.fullChargeColor);
                for (var index = 0; index < 4; index++)
                {
                    CreateSprite("Tone hole", new Vector3(-0.04f + index * 0.065f, 0f, -0.002f),
                        new Vector2(0.015f, settings.weaponSize.y * 0.6f), new Color(0.1f, 0.35f, 0.3f));
                }
            }

            var tip = new GameObject("Flute ribbon");
            tip.transform.SetParent(_weapon, false);
            tip.transform.localPosition = Vector3.right * settings.weaponSize.x * 0.5f;
            tip.layer = LayerMask.NameToLayer("WeaponVisual");

            _trail = tip.AddComponent<TrailRenderer>();
            _trail.sharedMaterial = settings.material;
            _trail.time = settings.trailDuration;
            _trail.startWidth = 0.045f;
            _trail.endWidth = 0f;
            _trail.startColor = settings.color;
            _trail.endColor = new Color(settings.color.r, settings.color.g, settings.color.b, 0f);
            _trail.minVertexDistance = 0.02f;
            _trail.shadowCastingMode = ShadowCastingMode.Off;
            _trail.emitting = false;

            _flowTrack = CreateLine(_root, "FLOW capacity", settings.material, 0.018f);
            _flowFill = CreateLine(_root, "FLOW", settings.material, 0.03f);
            _flowCost = CreateLine(_root, "Special cost", settings.material, 0.035f);
            _chargeRing = CreateLine(_root, "Charge", settings.material, 0.035f);
            _chargeProgress = CreateLine(_root, "Charge progress", settings.material, 0.02f);
            _evasionRing = CreateLine(_root, "Evasive state", settings.material, 0.045f);

            WarmMelody();
            _root.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += HandleSceneChanged;
        }

        private void WarmMelody()
        {
            if (tuning.melody.noteSprite == null)
            {
                return;
            }

            // Reserve part of one fixed budget for charging; never allocate notes during play.
            var count = Mathf.Clamp(tuning.melody.maximumNotes, 8, 24);
            var chargeCount = Mathf.Clamp(tuning.melody.chargeNotes, 2, Mathf.Min(8, count / 2));
            _driftingNoteCount = count - chargeCount;
            _notes = new MelodyNote[count];
            for (var index = 0; index < count; index++)
            {
                _notes[index].Renderer = CreateNoteRenderer("Melody note");
            }

            _melodyRibbon = CreateLine(_root, "High Flow melody", tuning.visuals.material, 0.012f);
            _melodyRibbon.positionCount = 25;
            _melodyRibbon.enabled = false;

            _evasionEchoes = new MelodyNote[2];
            for (var index = 0; index < _evasionEchoes.Length; index++)

            {
                var echo = CreateNoteRenderer("Evasive flute echo");
                echo.sprite = _tube.sprite;
                _evasionEchoes[index].Renderer = echo;
            }
        }

        private SpriteRenderer CreateNoteRenderer(string objectName)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(_root, false);
            // Keep notes in the same visible front pass as the weapon.
            child.layer = LayerMask.NameToLayer("WeaponVisual");
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = tuning.melody.noteSprite;
            renderer.sharedMaterial = tuning.visuals.noteMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.enabled = false;
            return renderer;
        }

        private SpriteRenderer CreateSprite(string objectName, Vector3 position, Vector2 size, Color color)
        {
            var child = new GameObject(objectName);

            child.transform.SetParent(_weapon, false);
            child.transform.localPosition = position;
            child.transform.localScale = new Vector3(size.x, size.y, 1f);
            child.layer = LayerMask.NameToLayer("WeaponVisual");

            var renderer = child.AddComponent<SpriteRenderer>();

            renderer.sprite = _placeholder;
            renderer.sharedMaterial = tuning.visuals.weaponMaterial;
            renderer.color = color;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            return renderer;
        }

        private void LateUpdate()
        {
            if (_root == null)
            {
                return;
            }

            var visible = _player.isActiveAndEnabled && _player.HasActiveInput
                && !_player.Health.IsDowned && _abilities.isActiveAndEnabled && _flow.isActiveAndEnabled;
            _root.gameObject.SetActive(visible);

            if (!visible)
            {
                Cancel();
                ClearMelody();
                _placed = false;
                return;
            }

            var camera = Camera.main;

            if (camera == null)
            {
                return;
            }
            if (!_player.Hitstop.IsStopped)
            {
                var blend = 1f - Mathf.Exp(-tuning.visuals.followSharpness * _player.Hitstop.ActionDeltaTime);
                var action = _abilities.CurrentAction;
                if (action == FluteAbilities.Action.Evasive && _previousAction != action)
                {
                    _echoesEmitted = 0;
                    PlayAccent(transform.position + Vector3.up * 0.15f, _abilities.ActionDirection);
                }
                if (action == FluteAbilities.Action.Secondary && _previousAction != action)
                {
                    ScatterNotes(2, false);
                }
                _previousAction = action;
                PlaceWeapon(camera, blend);
                DrawMeters(camera, blend);
            }

            // Let detached notes drift on world time while the weapon holds its hitstop pose.
            UpdateMelody(camera);
        }

        private void PlaceWeapon(Camera camera, float blend)
        {
            var settings = tuning.visuals;
            var action = _abilities.CurrentAction;
            var direction = action == FluteAbilities.Action.None ? _player.AimDirection : _abilities.ActionDirection;
            var screenDirection = new Vector2(Vector3.Dot(direction, camera.transform.right),
                Vector3.Dot(direction, camera.transform.up));
            var side = screenDirection.x < -0.001f ? -1f : 1f;
            var angle = Mathf.Atan2(screenDirection.y, screenDirection.x) * Mathf.Rad2Deg - 12f * side;
            var now = _player.Hitstop.ActionTime;
            var wave = Mathf.Sin(now * settings.swayFrequency * Mathf.PI * 2f);
            var position = transform.position + Vector3.up * settings.weaponOffset.y
                + camera.transform.right * (settings.weaponOffset.x * side) + direction * settings.weaponOffset.z;
            position += camera.transform.up * (wave * settings.sway * (1f + _flow.Normalized * 0.5f));
            var progress = _abilities.ActionProgress;
            var gesture = tuning.melody.gestureCurve.Evaluate(progress);
            var emphasis = 1f;

            switch (action)
            {
                case FluteAbilities.Action.Primary:
                    angle += settings.primaryGestureAngle * gesture * side;
                    position -= direction * (settings.primaryRecoil * gesture);
                    break;
                case FluteAbilities.Action.Secondary:
                    angle -= 30f * gesture * side;
                    position -= direction * (0.075f * gesture);
                    break;
                case FluteAbilities.Action.Charge:
                    angle += (20f + _abilities.ChargeNormalized * 35f) * side;
                    position += Vector3.up * (settings.chargeLift * _abilities.ChargeNormalized);
                    emphasis += _abilities.ChargeNormalized * 0.12f;
                    if (_abilities.ChargeNormalized >= 1f)
                    {
                        emphasis += 0.025f * Mathf.Sin(now * 28f);
                    }
                    break;
                case FluteAbilities.Action.Release:
                    angle -= (20f + _releaseStrength * 30f) * gesture * side;
                    position -= direction * (settings.releaseRecoil * (0.4f + _releaseStrength * 0.6f) * gesture);
                    break;
                case FluteAbilities.Action.Evasive:
                    var glide = Mathf.Sin(Mathf.SmoothStep(0f, 1f, progress) * Mathf.PI);
                    angle -= 70f * glide * side;
                    position += camera.transform.right * (glide * 0.08f * side);
                    break;
            }

            var rotation = camera.transform.rotation * Quaternion.AngleAxis(angle, Vector3.forward);
            _weapon.position = _placed ? Vector3.Lerp(_weapon.position, position, blend) : position;
            _weapon.rotation = _placed ? Quaternion.Slerp(_weapon.rotation, rotation, blend) : rotation;
            _weapon.localScale = Vector3.one * emphasis;
            _placed = true;
            var restingTint = Color.Lerp(Color.white, settings.color, _displayedFlow * 0.25f);
            _tube.color = Color.Lerp(restingTint, settings.fullChargeColor,
                _abilities.IsCharging ? _abilities.ChargeNormalized * 0.8f : 0f);
            var ribbonColor = settings.color;
            ribbonColor.a = Mathf.Lerp(0.3f, 0.85f, _displayedFlow);
            _trail.startColor = ribbonColor;
            _trail.endColor = new Color(ribbonColor.r, ribbonColor.g, ribbonColor.b, 0f);
            _trail.emitting = _abilities.IsEvasive || action == FluteAbilities.Action.Primary
                || action == FluteAbilities.Action.Secondary
                || action == FluteAbilities.Action.Release;
        }

        private void DrawMeters(Camera camera, float blend)
        {
            var settings = tuning.visuals;
            if (!Mathf.Approximately(_previousFlow, _flow.Current))
            {
                _losingFlow = _flow.Current < _previousFlow;
                _previousFlow = _flow.Current;
            }
            _displayedFlow = Mathf.Lerp(_displayedFlow, _flow.Normalized, blend);
            var ground = Quaternion.Euler(90f, 0f, 0f);
            var center = transform.position + Vector3.up * 0.055f;
            var success = Mathf.Clamp01(1f - (_player.Hitstop.ActionTime - _successAt) / 0.4f);
            var flowColor = _losingFlow ? settings.losingFlowColor : settings.color;
            var color = Color.Lerp(flowColor, settings.fullChargeColor, success);
            DrawArc(_flowTrack, center, ground, settings.flowRadius, 1f, new Color(0.2f, 0.4f, 0.35f, 0.3f));
            DrawArc(_flowFill, center, ground, settings.flowRadius, _displayedFlow, color);
            var cost = Mathf.Clamp01(_abilities.SpecialFlowCost / _flow.Maximum);
            var radial = ground * new Vector3(Mathf.Sin(cost * Mathf.PI * 2f), Mathf.Cos(cost * Mathf.PI * 2f), 0f);
            _flowCost.positionCount = 2;
            _flowCost.SetPosition(0, center + radial * (settings.flowRadius - 0.04f));
            _flowCost.SetPosition(1, center + radial * (settings.flowRadius + 0.04f));
            var costColor = _flow.Current >= _abilities.SpecialFlowCost ? settings.fullChargeColor : settings.losingFlowColor;
            _flowCost.startColor = costColor;
            _flowCost.endColor = costColor;

            var releasing = _abilities.CurrentAction == FluteAbilities.Action.Release;
            var chargeCenter = transform.position + camera.transform.right * settings.chargeIndicatorOffset.x
                + Vector3.up * settings.chargeIndicatorOffset.y;
            _chargeRing.enabled = _abilities.IsCharging || releasing;
            _chargeProgress.enabled = _abilities.IsCharging;
            if (_abilities.IsCharging)
            {
                _displayedCharge = Mathf.Lerp(_displayedCharge, _abilities.ChargeNormalized, blend);
                var charge = _abilities.ChargeNormalized;
                var radius = Mathf.Lerp(settings.minimumChargeRadius, settings.chargeRadius, _displayedCharge);
                var full = charge >= 1f;
                if (full && !_wasFullyCharged)
                {
                    _fullChargeStartedAt = _player.Hitstop.ActionTime;
                }
                _wasFullyCharged = full;
                var shimmer = Mathf.Clamp01(1f - (_player.Hitstop.ActionTime - _fullChargeStartedAt) / 0.22f);
                _chargeRing.widthMultiplier = settings.chargeLineWidth * (1f + shimmer);
                _chargeProgress.widthMultiplier = settings.chargeLineWidth;
                if (full)
                {
                    radius *= 1f + 0.05f * Mathf.Sin(_player.Hitstop.ActionTime * 22f);
                }
                var chargeColor = full ? settings.fullChargeColor : settings.color;
                DrawArc(_chargeRing, chargeCenter, camera.transform.rotation, radius, 1f, chargeColor);
                DrawArc(_chargeProgress, chargeCenter, camera.transform.rotation,
                    radius * 1.3f, charge, chargeColor);
            }
            else if (releasing)
            {
                var progress = _abilities.ActionProgress;
                var releaseColor = Color.Lerp(settings.color, settings.fullChargeColor, _releaseStrength);
                releaseColor.a = 1f - progress;
                var radius = Mathf.Lerp(settings.minimumChargeRadius, settings.chargeRadius, _releaseStrength)
                    * (1f + progress * 0.5f);
                DrawArc(_chargeRing, chargeCenter + _abilities.ActionDirection * (progress * 0.35f),
                    camera.transform.rotation, radius, 1f, releaseColor);
            }
            else
            {
                _displayedCharge = 0f;
                _wasFullyCharged = false;
            }

            _evasionRing.enabled = _abilities.IsEvasive && _player.Health.AbilityInvulnerabilityRemaining > 0f;
            if (_evasionRing.enabled)
            {
                var progress = 1f - Mathf.Clamp01(_player.Health.AbilityInvulnerabilityRemaining
                    / Mathf.Max(0.01f, tuning.defensive.invulnerabilityDuration));
                var evadeColor = _abilities.EvasionSucceeded ? settings.fullChargeColor : settings.color;
                evadeColor.a = Mathf.Lerp(1f, 0.55f, progress);
                DrawArc(_evasionRing, center, ground, Mathf.Lerp(0.25f, 0.7f, progress), 1f, evadeColor);
            }
        }

        private void UpdateMelody(Camera camera)
        {
            if (_notes == null)
            {
                return;
            }

            var settings = tuning.melody;
            var targetRate = _flow.IsGainingFromMovement
                ? Mathf.Lerp(settings.lowFlowRate, settings.highFlowRate, _displayedFlow) : 0f;
            var blend = 1f - Mathf.Exp(-settings.emissionSmoothing * Time.deltaTime);
            _noteRate = Mathf.Lerp(_noteRate, targetRate, blend);
            _noteEmission = Mathf.Min(2f, _noteEmission + _noteRate * Time.deltaTime);
            while (_noteEmission >= 1f)
            {
                SpawnNote(false, false);
                _noteEmission -= 1f;
            }

            if (_flow.TurnedAbruptly && Time.time >= _scatterReadyAt)
            {
                ScatterNotes(settings.scatterCount, false);
                _scatterReadyAt = Time.time + settings.noteLifetime * 0.25f;
            }

            for (var index = 0; index < _driftingNoteCount; index++)
            {
                ref var note = ref _notes[index];
                if (!note.Renderer.enabled)
                {
                    continue;
                }
                var progress = (Time.time - note.BornAt) / note.Lifetime;
                if (progress >= 1f)
                {
                    note.Renderer.enabled = false;
                    continue;
                }

                var envelope = Mathf.Sin(progress * Mathf.PI);
                var angle = note.Phase + progress * 2.8f;
                var energy = Mathf.Lerp(0.7f, 1f, _displayedFlow);
                var curl = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle))
                    * (settings.curlRadius * envelope * energy);
                var bob = camera.transform.up * (Mathf.Sin(angle * 2f) * 0.025f * envelope * energy);
                var position = note.Origin + note.Drift * Mathf.SmoothStep(0f, 1f, progress)
                    + Vector3.up * (settings.riseDistance * progress) + curl + bob;
                var tilt = Mathf.Sin(angle) * 14f;
                note.Renderer.transform.SetPositionAndRotation(position,
                    camera.transform.rotation * Quaternion.AngleAxis(tilt, Vector3.forward));
                var color = note.Gold ? tuning.visuals.fullChargeColor : tuning.visuals.color;
                var shimmer = note.Gold ? 0.85f + 0.15f * Mathf.Sin(Time.time * 12f + note.Phase) : 1f;
                color.a = Mathf.Min(1f, progress / 0.12f) * (1f - progress) * (1f - progress)
                    * Mathf.Lerp(0.35f, 0.85f, _displayedFlow) * shimmer;
                note.Renderer.color = color;
                note.Renderer.transform.localScale = Vector3.one * (note.Size * (0.9f + envelope * 0.1f));
            }

            UpdateChargeNotes(camera);
            UpdateMelodyRibbon();
            UpdateEvasionEchoes();
        }

        private void SpawnNote(bool scatter, bool gold)
        {
            for (var index = 0; index < _driftingNoteCount; index++)
            {
                ref var note = ref _notes[index];
                if (note.Renderer.enabled)
                {
                    continue;
                }

                var sequence = _noteSequence++;
                var phase = sequence * 2.39996f;
                var direction = _player.Movement.FacingDirection;
                note.Origin = transform.position - direction * 0.12f + Vector3.up * 0.15f;
                note.Drift = -direction * tuning.melody.driftDistance;
                if (scatter)
                {
                    note.Drift += new Vector3(Mathf.Cos(phase), 0f, Mathf.Sin(phase)) * tuning.melody.driftDistance;
                }
                note.BornAt = Time.time;
                note.Lifetime = tuning.melody.noteLifetime * (0.9f + sequence % 3 * 0.1f);
                note.Phase = phase;
                note.Gold = gold || (_displayedFlow >= tuning.melody.goldThreshold && sequence % 4 == 0);
                note.Size = tuning.melody.noteSize / Mathf.Max(0.001f, note.Renderer.sprite.bounds.size.y);
                note.Renderer.enabled = true;
                note.Renderer.color = Color.clear;
                return;
            }
        }

        private void ScatterNotes(int count, bool gold)
        {
            if (_notes == null)
            {
                return;
            }
            for (var index = 0; index < Mathf.Min(count, 4); index++)
            {
                SpawnNote(true, gold);
            }
        }

        private void UpdateChargeNotes(Camera camera)
        {
            var deltaTime = _player.Hitstop.IsStopped ? 0f : _player.Hitstop.ActionDeltaTime;
            var blend = 1f - Mathf.Exp(-12f * deltaTime);
            _chargeNoteVisibility = Mathf.Lerp(_chargeNoteVisibility, _abilities.IsCharging ? 1f : 0f, blend);
            var charge = _displayedCharge;
            var radius = Mathf.Lerp(tuning.melody.spiralRadius, 0.1f, charge);
            var count = _notes.Length - _driftingNoteCount;
            for (var index = 0; index < count; index++)
            {
                var renderer = _notes[_driftingNoteCount + index].Renderer;
                renderer.enabled = _chargeNoteVisibility > 0.01f;
                if (!renderer.enabled)
                {
                    continue;
                }
                var fraction = (float)index / count;
                var angle = _player.Hitstop.ActionTime * 3f + fraction * Mathf.PI * 2f;
                var offset = camera.transform.right * (Mathf.Cos(angle) * radius)
                    + camera.transform.up * (Mathf.Sin(angle) * radius * 0.55f)
                    + Vector3.up * ((fraction - 0.5f) * 0.2f * (1f - charge));
                renderer.transform.SetPositionAndRotation(_weapon.position + offset,
                    camera.transform.rotation * Quaternion.AngleAxis(Mathf.Sin(angle) * 20f, Vector3.forward));
                renderer.transform.localScale = Vector3.one * (tuning.melody.noteSize
                    / Mathf.Max(0.001f, renderer.sprite.bounds.size.y));
                var color = Color.Lerp(tuning.visuals.color, tuning.visuals.fullChargeColor, charge);
                color.a = _chargeNoteVisibility * 0.65f;
                renderer.color = color;
            }
        }

        private void UpdateMelodyRibbon()
        {
            var strength = Mathf.InverseLerp(tuning.melody.goldThreshold, 1f, _displayedFlow)
                * Mathf.Clamp01(_noteRate / Mathf.Max(0.01f, tuning.melody.highFlowRate));
            _melodyRibbon.enabled = strength > 0.01f;
            if (!_melodyRibbon.enabled)
            {
                return;
            }
            var color = tuning.visuals.color;
            color.a = strength * 0.12f;
            _melodyRibbon.startColor = Color.clear;
            _melodyRibbon.endColor = color;
            for (var index = 0; index < 25; index++)
            {
                var fraction = index / 24f;
                var angle = Time.time * 1.4f + fraction * Mathf.PI * 2f;
                var radius = tuning.melody.spiralRadius * (0.8f + fraction * 0.2f);
                _melodyRibbon.SetPosition(index, transform.position
                    + new Vector3(Mathf.Cos(angle) * radius, 0.1f + fraction * 0.15f, Mathf.Sin(angle) * radius));
            }
        }

        private void UpdateEvasionEchoes()
        {
            if (_abilities.IsEvasive && !_player.Hitstop.IsStopped && _echoesEmitted < _evasionEchoes.Length
                && _abilities.ActionProgress >= _echoesEmitted * 0.25f)
            {
                ref var echo = ref _evasionEchoes[_echoesEmitted++];
                echo.BornAt = Time.time;
                echo.Origin = _tube.transform.position;
                echo.Rotation = _tube.transform.rotation;
                echo.Renderer.enabled = true;
                echo.Renderer.transform.localScale = _tube.transform.lossyScale;
            }
            for (var index = 0; index < _evasionEchoes.Length; index++)
            {
                ref var echo = ref _evasionEchoes[index];
                if (!echo.Renderer.enabled)
                {
                    continue;
                }
                var progress = Mathf.Clamp01((Time.time - echo.BornAt) / 0.22f);
                var color = _abilities.EvasionSucceeded ? tuning.visuals.fullChargeColor : tuning.visuals.color;
                color.a = tuning.visuals.evasionEchoOpacity * (1f - progress);
                // Hold the captured pose in world space while the player glides onward.
                echo.Renderer.transform.SetPositionAndRotation(echo.Origin, echo.Rotation);
                echo.Renderer.color = color;
                echo.Renderer.enabled = progress < 1f;
            }
        }

        private void ClearMelody()
        {
            if (_notes == null)
            {
                return;
            }
            for (var index = 0; index < _notes.Length; index++)
            {
                _notes[index].Renderer.enabled = false;
            }
            for (var index = 0; index < _evasionEchoes.Length; index++)
            {
                _evasionEchoes[index].Renderer.enabled = false;
            }
            _melodyRibbon.enabled = false;
            _noteRate = 0f;
            _noteEmission = 0f;
            _chargeNoteVisibility = 0f;
        }

        private void HandleSceneChanged(Scene previous, Scene current)
        {
            ClearMelody();
            _placed = false;
        }

        internal float TakeProjectileVisualSide()
        {
            return _projectileVisualIndex++ % 2 == 0 ? 1f : -1f;
        }

        public void PlayRelease(float charge, bool spentFlow)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }
            _releaseStrength = charge;
            if (spentFlow)
            {
                _successAt = _player.Hitstop.ActionTime;
            }
            PlayAccent(_abilities.ChargePosition, _abilities.ActionDirection);
        }

        public void PlayEvasionSuccess()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }
            _successAt = _player.Hitstop.ActionTime;
            ScatterNotes(tuning.melody.scatterCount, true);
            PlayAccent(transform.position + Vector3.up * 0.15f, _abilities.ActionDirection, true);
        }

        private void PlayAccent(Vector3 position, Vector3 direction, bool success = false)
        {
            var effect = success && tuning.melody.resonanceEffect != null
                ? tuning.melody.resonanceEffect : tuning.visuals.accentEffect;
            if (effect != null)
            {
                effect.Play(new HitEffectRequest(position, direction, 0f, success));
            }
        }

        public override void Cancel()
        {
            // Keep the current transform so interrupted gestures blend back into idle.
            _displayedCharge = 0f;
            _releaseStrength = 0f;
            _successAt = float.NegativeInfinity;
            _previousAction = FluteAbilities.Action.None;
            if (_trail != null)
            {
                _trail.emitting = false;
                _trail.Clear();
            }
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleSceneChanged;
            Cancel();
            ClearMelody();
            _placed = false;
            if (_root != null)
            {
                _root.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (_placeholder != null)
            {
                Destroy(_placeholder);
            }
        }

        internal static LineRenderer CreateLine(Transform parent, string objectName, Material material, float width)
        {
            var line = new GameObject(objectName).AddComponent<LineRenderer>();
            line.transform.SetParent(parent, false);
            line.gameObject.layer = LayerMask.NameToLayer("WeaponVisual");
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.widthMultiplier = width;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        internal static void DrawArc(LineRenderer line, Vector3 center, Quaternion rotation,
            float radius, float fraction, Color color)
        {
            const int segments = 40;
            line.positionCount = segments + 1;
            line.startColor = color;
            line.endColor = color;
            for (var index = 0; index <= segments; index++)
            {
                var angle = fraction * index / segments * Mathf.PI * 2f;
                line.SetPosition(index, center + rotation * new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f) * radius);
            }
        }
    }
}
