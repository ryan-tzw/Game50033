namespace Game50033
{
    using UnityEngine;
    using UnityEngine.SceneManagement;

    [DefaultExecutionOrder(250)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TromboneAbilities))]
    public sealed class TromboneWeaponVisual : FloatingWeaponVisual
    {
        [SerializeField] private TromboneTuning tuning;
        [SerializeField] private Transform presentationRoot;
        [SerializeField] private Transform weaponPivot;
        [SerializeField] private SpriteRenderer weaponSprite;
        [SerializeField] private LineRenderer muzzleCue;
        [SerializeField] private LineRenderer guardCue;
        [SerializeField] private LineRenderer specialCue;

        private LineRenderer[] _waves, _dust, _chips, _sparks, _bellSparks;
        private LineRenderer _bellGlow;
        private LineRenderer _glow, _hold;

        internal Vector3 BellPosition { get; private set; }

        private enum Gesture { None, Primary, Zone, Special }
        private Player _player;
        private TromboneAbilities _abilities;

        private Camera _camera;
        private Gesture _gesture;

        private Vector3 _direction = Vector3.forward;
        private Vector3 _shotOffset;
        private Vector3 _blockOffset;
        private Vector3 _guardFrom;
        private Vector3 _restScale = Vector3.one;
        private Vector3 _spriteRestPosition;

        private Quaternion _guardRotation;
        private Vector3 _fromOffset;
        private Vector3 _fromScale = Vector3.one;
        private Quaternion _fromRotation;

        private float _startedAt;
        private float _duration;
        private float _anticipation;
        private float _guardAt = float.NegativeInfinity;
        private float _blockedAt = float.NegativeInfinity;

        private float _impactAt = float.NegativeInfinity;
        private float _releaseUntil;
        private float _maxPulseAt = float.NegativeInfinity;
        private float _charge;
        private float _shotPressure;
        private bool _impacted;
        private bool _consumedZone;
        private bool _reloading;
        private float _readyAt = float.NegativeInfinity;
        private Vector3 _shotGround;
        private bool _maxed;
        private Vector3 _impactPoint;

        private bool _resonatingShot;
        private bool _resonatingBlock;
        private bool _placed;
        private bool _wasBracing;

        private float Clock => _player.Hitstop.ActionTime;

        private void Awake()
        {
            _player = GetComponent<Player>();
            _abilities = GetComponent<TromboneAbilities>();

            _restScale = weaponPivot.localScale;

            var scale = tuning.presentation.weaponWidth / weaponSprite.sprite.bounds.size.x;
            _spriteRestPosition = weaponSprite.transform.localPosition;
            weaponSprite.transform.localScale = new Vector3(scale, scale, 1f);
            weaponSprite.sharedMaterial = tuning.presentation.weaponMaterial;

            muzzleCue.sharedMaterial = guardCue.sharedMaterial = specialCue.sharedMaterial = tuning.presentation.weaponMaterial;
            muzzleCue.widthMultiplier = guardCue.widthMultiplier = tuning.presentation.cueWidth;
            specialCue.widthMultiplier = tuning.presentation.cueWidth * 0.65f;

            _waves = MakeLines("Guard Shockwave", 2);
            _dust = MakeLines("Guard Dust", 6);
            _chips = MakeLines("Guard Chip", 4);
            _sparks = MakeLines("Spark", 8);
            _glow = MakeLines("Pressure Glow", 1)[0];
            _glow.numCapVertices = 8;
            _bellSparks = MakeLines("Bell Spark", 4);
            _bellGlow = MakeLines("Bell Charge", 1)[0];
            _bellGlow.numCapVertices = 8;

            _hold = MakeLines("Guard Anchor", 1)[0];
            ClearCues();
        }

        private LineRenderer[] MakeLines(string label, int count)
        {
            var lines = new LineRenderer[count];
            for (var i = 0; i < count; i++)
            {
                var line = new GameObject(label + " " + i).AddComponent<LineRenderer>();
                line.gameObject.layer = presentationRoot.gameObject.layer;
                line.transform.SetParent(presentationRoot, false);
                line.sharedMaterial = tuning.presentation.weaponMaterial;
                line.sortingLayerID = guardCue.sortingLayerID;
                line.sortingOrder = guardCue.sortingOrder;
                line.alignment = guardCue.alignment;
                line.useWorldSpace = true;
                line.numCapVertices = 2;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                lines[i] = line;
            }
            return lines;
        }

        private void OnEnable() => SceneManager.activeSceneChanged += HandleSceneChanged;

        internal void PlayShot(Vector3 direction, Vector3 origin, bool special, bool resonates, float interval,
            float pressure, bool consumedZone = false)
        {
            if (!isActiveAndEnabled) return;

            _consumedZone = special && consumedZone;
            _shotGround = transform.position;
            _gesture = special ? Gesture.Special : Gesture.Primary;
            _anticipation = 0f;
            _direction = direction;
            _shotOffset = origin - transform.position;

            // preserve pose continuity when a shot replaces the current gesture
            CaptureStartPose();
            _resonatingShot = resonates;
            _shotPressure = pressure;
            _startedAt = Clock;

            _duration = Mathf.Min(special ? tuning.presentation.specialRecovery : tuning.presentation.primaryRecovery,
                interval);

            ClearCues();
        }

        internal void PlaySpecialWindup(Vector3 direction)
        {
            if (!isActiveAndEnabled) return;
            _gesture = Gesture.None;
            _anticipation = 0f;
            _direction = direction;
            CaptureStartPose();
            ClearCues();
        }

        private void CaptureStartPose()
        {
            _fromOffset = weaponPivot.position - transform.position;
            _fromRotation = weaponPivot.rotation;
            _fromScale = new Vector3(weaponPivot.localScale.x / _restScale.x,
                weaponPivot.localScale.y / _restScale.y, 1f);
        }

        internal void PlayZone(Vector3 direction)
        {
            if (!isActiveAndEnabled) return;
            _gesture = Gesture.Zone;
            _anticipation = 0f;
            _direction = direction;
            _startedAt = Clock;
            _duration = tuning.presentation.zoneSweepDuration;
            ClearCues();
        }

        internal void PlayGuard(Vector3 direction)
        {
            if (!isActiveAndEnabled) return;
            _gesture = Gesture.None;
            _anticipation = 0f;
            _direction = direction;
            _guardAt = Clock;
            _guardFrom = weaponPivot.position - transform.position;
            _guardRotation = weaponPivot.rotation;
            _impacted = false;
            _impactAt = float.NegativeInfinity;
            ClearCues();
        }

        internal void PlayGuardImpact(bool resonates)
        {
            if (!isActiveAndEnabled) return;
            _blockedAt = Clock;
            _resonatingBlock = resonates;
            _blockOffset = weaponPivot.position - transform.position;
        }

        private void LateUpdate()
        {
            var visible = _player.isActiveAndEnabled && _player.HasActiveInput && !_player.Health.IsDowned
                && _abilities.isActiveAndEnabled;
            presentationRoot.gameObject.SetActive(visible);
            if (!visible)
            {
                Cancel();
                return;
            }
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            var settings = tuning.presentation;
            var age = Clock - _startedAt;
            if ((_gesture != Gesture.None && age >= _duration)
                || (_gesture == Gesture.Zone && !_abilities.IsInCommandZone)) _gesture = Gesture.None;

            var preparingSpecial = _abilities.IsPreparingSpecial;
            var bracing = _abilities.IsGuarding && _gesture == Gesture.None;
            var aiming = _gesture != Gesture.None || _abilities.IsGuarding || preparingSpecial;
            var direction = aiming ? _direction : _player.AimDirection;

            // Project XZ gameplay aim into the camera plane where the weapon sprite rotates.
            var viewDirection = new Vector2(Vector3.Dot(direction, _camera.transform.right),
                Vector3.Dot(direction, _camera.transform.up)).normalized;
            var side = viewDirection.x >= 0f ? 1f : -1f;
            var aimAngle = Mathf.Atan2(viewDirection.y, viewDirection.x) * Mathf.Rad2Deg;
            var angle = aimAngle + side * settings.restingTilt;
            var position = transform.position + Offset(settings.idleOffset, direction, side);
            var poseScale = Vector3.one;
            var snap = false;
            ClearCues();

            UpdateAnticipation(preparingSpecial, bracing);
            if (_gesture == Gesture.None && !bracing && _anticipation > 0f)
                ApplyAnticipationPose(viewDirection, side, ref position, ref angle, ref poseScale);

            var rotation = CameraPlaneRotation(angle);
            if (bracing)
            {
                rotation = UpdateGuardPose(direction, side, out position, out poseScale);
                snap = true;
            }
            if (!bracing && _wasBracing) _releaseUntil = Clock + 0.3f;
            DrawShockwave();

            if (_gesture == Gesture.Zone)
            {
                angle = UpdateZonePose(age, aimAngle, side, ref position);
                rotation = CameraPlaneRotation(angle);
                snap = true;
            }
            else if (_gesture == Gesture.Primary || _gesture == Gesture.Special)
            {
                rotation = UpdateShotPose(age, viewDirection, side, aimAngle, ref position, out poseScale);
                snap = true;
            }

            var releasing = _gesture == Gesture.None && !bracing && Clock < _releaseUntil;
            if (releasing)
                position += Vector3.up * (settings.anticipationLift * 0.5f
                    * Mathf.Sin(Mathf.PI * (1f - (_releaseUntil - Clock) / 0.3f)));

            ApplyBlockFeedback(direction, ref position);
            ApplyWeaponPose(position, rotation, poseScale, snap, releasing);
            UpdateSpriteGrip(side, bracing);

            if (_gesture == Gesture.Primary || _gesture == Gesture.Special)
                DrawRelease(age, _gesture == Gesture.Special, side, viewDirection);
            else if (_gesture == Gesture.None && !bracing && _anticipation > 0f && preparingSpecial)
                DrawSpecialWindup(side);

            DrawPressure(side);
            DrawBellCharge(bracing);
            _wasBracing = bracing;
            _placed = true;
        }

        private void UpdateAnticipation(bool preparingSpecial, bool bracing)
        {
            var settings = tuning.presentation;
            var preparation = 0f;
            if (preparingSpecial) preparation = _abilities.SpecialWindupProgress;
            else if (_gesture == Gesture.None && !bracing && _player.CanUseAbilities
                && !_player.Movement.IsDodging && _player.Movement.MoveDirection.sqrMagnitude < 0.0001f)
            {
                var state = _abilities.GetAbilityState(PlayerAbility.Primary);
                preparation = 1f - Mathf.Clamp01(state.Remaining / state.Duration);
            }
            _anticipation = preparingSpecial ? preparation : Mathf.MoveTowards(_anticipation, preparation,
                _player.Hitstop.ActionDeltaTime / settings.anticipationTime);
        }

        private void ApplyAnticipationPose(Vector2 viewDirection, float side,
            ref Vector3 position, ref float angle, ref Vector3 poseScale)
        {
            var settings = tuning.presentation;
            var tension = 1f - Mathf.Pow(1f - _anticipation, 1.8f);
            var strain = Mathf.Clamp01((_anticipation - 0.3f) / 0.7f);

            strain *= strain;

            var aim = _camera.transform.right * viewDirection.x + _camera.transform.up * viewDirection.y;
            position -= aim * (settings.anticipationPullback * tension);

            var tremble = settings.anticipationTremble * strain * (1f + 0.5f * _charge * settings.pressureGlow);
            if (tremble > 0f)
            {
                var wanderX = Mathf.PerlinNoise(Clock * 5f, 0.37f) * 2f - 1f;
                var wanderY = Mathf.PerlinNoise(1.9f, Clock * 5f) * 2f - 1f;
                var swell = Mathf.Sin(Clock * Mathf.Lerp(1.1f, 1.7f, _charge)) * 0.5f + 0.5f;

                position += _camera.transform.right * (wanderX * tremble)
                    + _camera.transform.up * (wanderY * tremble * 0.7f)
                    - aim * (swell * tremble * 2f);
                angle += wanderX * tremble * 60f;
            }
            position += Vector3.up * (settings.anticipationLift * tension);
            angle += side * settings.anticipationTilt * tension;

            var compression = settings.shotSquash * tension;
            poseScale = new Vector3(1f - compression, 1f + compression * 0.5f, 1f);
        }

        private Quaternion UpdateGuardPose(Vector3 direction, float side,
            out Vector3 position, out Vector3 poseScale)
        {
            var settings = tuning.presentation;
            if (!_wasBracing)
            {
                _guardAt = Clock;
                _guardFrom = weaponPivot.position - transform.position;
                _guardRotation = weaponPivot.rotation;
            }
            poseScale = Vector3.one;
            Quaternion rotation;
            var guardAngle = side > 0f ? -settings.guardDownAngle : settings.guardDownAngle - 180f;
            var plantedPosition = transform.position + Offset(settings.guardOffset, direction, side);
            var plantedRotation = CameraPlaneRotation(guardAngle);
            var raisedPosition = plantedPosition + Vector3.up * (settings.anticipationLift * 1.2f * settings.guardImpactScale)
                - direction * (settings.anticipationPullback * 0.2f);
            var raisedRotation = CameraPlaneRotation(guardAngle + side * settings.anticipationTilt * 0.5f);
            var elapsed = Clock - _guardAt;
            var windup = settings.guardWindupTime;
            var slam = settings.guardRaiseTime;
            if (elapsed < windup)
            {
                var raiseProgress = Mathf.SmoothStep(0f, 1f, elapsed / windup);
                position = Vector3.Lerp(transform.position + _guardFrom, raisedPosition, raiseProgress);
                rotation = Quaternion.Slerp(_guardRotation, raisedRotation, raiseProgress);
            }
            else
            {
                var slamProgress = Mathf.Clamp01((elapsed - windup) / slam);
                var fall = slamProgress * slamProgress;
                position = Vector3.Lerp(raisedPosition, plantedPosition, fall);
                rotation = Quaternion.Slerp(raisedRotation, plantedRotation, fall);
                if (slamProgress >= 1f && !_impacted)
                {
                    _impacted = true;
                    _impactAt = Clock;
                    _impactPoint = GroundUnder(plantedPosition
                        + plantedRotation * Vector3.Scale(_restScale, BellOffset(side)));
                }
            }
            if (_impacted)
            {
                var settle = 1f - Mathf.SmoothStep(0f, 1f, (Clock - _impactAt) / 0.16f);
                position += Vector3.down * (0.05f * settings.guardImpactScale * settle);
                var squash = settings.recoilSquash * 2.5f * settings.guardImpactScale * settle;
                poseScale = new Vector3(1f - squash, 1f + squash * 0.5f, 1f);
                var hold = settings.gold;
                hold.a *= 0.22f + 0.06f * Mathf.Sin(Clock * 3f);
                DrawFloorRing(_hold, GroundUnder(plantedPosition
                    + plantedRotation * Vector3.Scale(_restScale, BellOffset(side))),
                    settings.guardCueRadius * 0.8f, settings.cueWidth, hold);
            }
            return rotation;
        }

        private float UpdateZonePose(float age, float aimAngle, float side, ref Vector3 position)
        {
            var settings = tuning.presentation;
            var progress = Mathf.Clamp01(age / _duration);
            var spinProgress = Mathf.SmoothStep(0f, 1f, progress);
            var orbitDirection = Quaternion.AngleAxis(spinProgress * 360f, Vector3.up) * _direction;
            var orbitWeight = Mathf.Sin(progress * Mathf.PI);
            position += orbitDirection * (settings.zoneWeaponReach * orbitWeight);

            // Match the opening shudder to the zone boundary's deployment stamp.
            var deploy = Mathf.Clamp01(age / settings.zoneDeployDuration);
            var shake = settings.zoneShakeDistance * (1f - deploy) * (1f - deploy);
            var beat = age * settings.zoneShakeFrequency * Mathf.PI * 2f;
            position += _camera.transform.right * (Mathf.Sin(beat) * shake)
                + _camera.transform.up * (Mathf.Sin(beat * 0.75f) * shake * 0.5f);
            // Keep the full turn separate from the fading orbit radius. LerpAngle would lose the spin.
            return aimAngle + side * 360f * spinProgress;
        }

        private Quaternion UpdateShotPose(float age, Vector2 viewDirection, float side, float aimAngle,
            ref Vector3 position, out Vector3 poseScale)
        {
            var settings = tuning.presentation;
            var special = _gesture == Gesture.Special;
            // Split the shot into thrust, recoil build and a slower return. SmoothStep brings recoil
            // to rest at its peak, while the separate return curve gives the weapon time to settle.
            var thrust = Mathf.SmoothStep(0f, 1f, age / settings.flashDuration);
            var recoilBuildTime = Mathf.Min(special ? settings.specialRecoilBuild : settings.primaryRecoilBuild, _duration * 0.6f);
            var settle = Mathf.SmoothStep(0f, 1f, (age - recoilBuildTime) / (_duration - recoilBuildTime));
            var recoil = age < recoilBuildTime ? Mathf.SmoothStep(0f, 1f, age / recoilBuildTime) : 1f - settle;
            var punch = 1f - Mathf.Clamp01(age / (settings.flashDuration * 2.5f));
            var deformation = -settings.recoilSquash * (recoil + (special ? 2f : 1.2f) * punch * punch);
            poseScale = Vector3.Lerp(_fromScale, new Vector3(1f + deformation, 1f - deformation * 0.5f, 1f), thrust);
            var aim = _camera.transform.right * viewDirection.x + _camera.transform.up * viewDirection.y;
            var weight = Mathf.Lerp(special ? 0.85f : 0.9f, special ? 1.2f : 1.1f, _shotPressure);
            var shotAngle = aimAngle + side * (special ? settings.specialKick : settings.primaryKick) * weight * recoil;
            // Solve the pivot from the gameplay launch point and rotated bell offset, then apply recoil.
            // This gives the shot pose a consistent muzzle reference as the weapon rotates.
            var baseRotation = CameraPlaneRotation(aimAngle);
            var muzzleOffset = Vector3.Scale(_restScale, BellOffset(side));
            var firingPosition = transform.position + _shotOffset - baseRotation * muzzleOffset;
            firingPosition -= aim * ((special ? settings.specialRecoil : settings.primaryRecoil) * weight * recoil);
            firingPosition += Vector3.up * (settings.anticipationLift * recoil
                - settings.anticipationLift * (special ? 0.5f : 0.2f) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(age / _duration)));
            var thrustPosition = Vector3.Lerp(transform.position + _fromOffset, firingPosition, thrust);
            position = Vector3.Lerp(thrustPosition, position, settle);
            var restingAngle = aimAngle + side * settings.restingTilt;
            var angle = shotAngle + Mathf.DeltaAngle(shotAngle, restingAngle) * settle;
            return Quaternion.Slerp(_fromRotation,
                CameraPlaneRotation(angle), thrust);
        }

        private void ApplyBlockFeedback(Vector3 direction, ref Vector3 position)
        {
            var settings = tuning.presentation;
            var block = 1f - Mathf.Clamp01((Clock - _blockedAt) / settings.blockDuration);
            if (block > 0f)
            {
                position -= direction * (settings.blockKick * block);
                var color = _resonatingBlock ? settings.resonatingColor : Color.white;
                color.a *= block;
                guardCue.widthMultiplier = settings.cueWidth;
                DrawRing(guardCue, transform.position + _blockOffset,
                    settings.guardCueRadius * (2f - block) * (_resonatingBlock ? 1.25f : 1f), 1f, color);
            }
        }

        private void ApplyWeaponPose(Vector3 position, Quaternion rotation, Vector3 poseScale, bool snap, bool releasing)
        {
            var settings = tuning.presentation;
            weaponPivot.localScale = Vector3.Scale(_restScale, poseScale);
            // Exponential smoothing keeps the same response at different frame rates.
            var blend = snap || !_placed ? 1f
                : 1f - Mathf.Exp(-settings.poseResponse * (releasing ? 0.3f : 1f) * _player.Hitstop.ActionDeltaTime);
            weaponPivot.SetPositionAndRotation(Vector3.Lerp(weaponPivot.position, position, blend),
                Quaternion.Slerp(weaponPivot.rotation, rotation, blend));
        }

        private void UpdateSpriteGrip(float side, bool bracing)
        {
            var settings = tuning.presentation;
            var displayedRight = Vector3.Dot(weaponPivot.right, _camera.transform.right);
            // Idle mirroring follows the displayed rotation during aim smoothing. Active gestures
            // keep the side captured by their aim, including during the full-turn sweep.
            if (_gesture != Gesture.None || bracing || _anticipation > 0f) weaponSprite.flipY = side < 0f;
            else if (Mathf.Abs(displayedRight) > 0.001f) weaponSprite.flipY = displayedRight < 0f;
            var gripSide = weaponSprite.flipY ? -1f : 1f;
            weaponSprite.transform.localPosition = _spriteRestPosition
                - new Vector3(settings.gripPoint.x, settings.gripPoint.y * gripSide, 0f);
        }

        private void DrawSpecialWindup(float side)
        {
            var settings = tuning.presentation;
            var tension = Mathf.SmoothStep(0f, 1f, _anticipation);
            var bell = weaponPivot.TransformPoint(BellOffset(side));
            var color = settings.gold;
            color.a *= tension * 0.6f;
            DrawRing(specialCue, bell, settings.flashRadius * settings.specialFlashScale
                * Mathf.Lerp(1.5f, 0.65f, tension), Mathf.Lerp(0.5f, 1f, tension), color);
        }

        private Quaternion CameraPlaneRotation(float angle) =>
            _camera.transform.rotation * Quaternion.AngleAxis(angle, Vector3.forward);

        private void DrawRelease(float age, bool special, float side, Vector2 viewDirection)
        {
            var settings = tuning.presentation;
            var bell = weaponPivot.TransformPoint(BellOffset(side));
            var flash = 1f - Mathf.Clamp01(age / settings.flashDuration);
            var hot = _resonatingShot ? settings.resonatingColor : Color.Lerp(settings.gold, Color.white, 0.8f);
            var power = 1f + (special ? 0.8f : 0.5f) * _shotPressure;
            var scale = (special ? settings.specialFlashScale : 1f) * power;
            if (flash > 0f)
            {
                var color = hot;
                color.a *= flash;
                _glow.enabled = true;
                _glow.positionCount = 2;
                _glow.SetPosition(0, bell);
                _glow.SetPosition(1, bell + _camera.transform.right * 0.001f);
                _glow.startWidth = _glow.endWidth = settings.flashRadius * 2f * scale * Mathf.Lerp(0.6f, 1.3f, flash);
                _glow.startColor = _glow.endColor = color;
            }
            var burst = settings.flashDuration * (special ? 2.4f : 1.4f);
            var t = Mathf.Clamp01(age / burst);
            if (t < 1f)
            {
                var aimAngle = Mathf.Atan2(viewDirection.y, viewDirection.x);
                var count = special ? _sparks.Length : 4;
                var spread = (special ? 40f : 18f) * Mathf.Deg2Rad;
                var reach = settings.flashRadius * (special ? settings.specialFlashScale * 1.6f : 2.6f) * power;
                var extend = 1f - (1f - t) * (1f - t) * (1f - t);
                for (var i = 0; i < count; i++)
                {
                    var spokes = i / (float)(count - 1) * 2f - 1f;
                    var a = aimAngle + spokes * spread;
                    var dir = _camera.transform.right * Mathf.Cos(a) + _camera.transform.up * Mathf.Sin(a);
                    var length = reach * (1f - 0.45f * Mathf.Abs(spokes));
                    var line = _sparks[i];
                    var color = hot;
                    color.a *= 1f - t;
                    line.enabled = true;
                    line.positionCount = 2;
                    line.SetPosition(0, bell + dir * (length * 0.25f * extend));
                    line.SetPosition(1, bell + dir * (length * (0.25f + 0.75f * extend)));
                    line.startWidth = settings.cueWidth * (special ? 3.2f : 2f) * power * (1f - t);
                    line.endWidth = 0f;
                    line.startColor = line.endColor = color;
                }
            }
            if (!special) return;
            if (flash > 0f)
            {
                var color = hot;
                color.a *= flash;
                muzzleCue.widthMultiplier = settings.cueWidth * 2f * Mathf.Lerp(0.5f, 1f, flash);
                DrawRing(muzzleCue, bell, settings.flashRadius * settings.specialFlashScale
                    * Mathf.Lerp(0.55f, 2.2f, 1f - flash), 1f, color);
            }
            if (_consumedZone)
            {
                // Zone consumption uses the captured release position as its floor-space origin.
                // The expanding cue stays at the spent zone while the weapon continues its recovery.
                var u = Mathf.Clamp01(age / 0.55f);
                if (u < 1f)
                {
                    var ring = Color.Lerp(Color.white, settings.gold, u);
                    ring.a = (1f - u) * (1f - u);
                    DrawFloorRing(_waves[1], _shotGround + Vector3.up * 0.02f,
                        settings.guardWaveRadius * 1.5f * (1f - Mathf.Pow(1f - u, 3f)),
                        settings.guardWaveWidth * Mathf.Lerp(1.6f, 0.2f, u), ring);
                }
            }
            if (age >= settings.flashDuration * 2f) return;
            var glint = settings.gold;
            glint.a *= 1f - age / (settings.flashDuration * 2f);
            specialCue.enabled = true;
            specialCue.positionCount = 2;
            specialCue.SetPosition(0, bell - _camera.transform.up * settings.flashRadius * 2f);
            specialCue.SetPosition(1, bell + _camera.transform.up * settings.flashRadius * 2f);
            specialCue.startColor = specialCue.endColor = glint;
        }

        // Map the live Primary deadline to bell energy, then use the complete activation check
        // for the ready transition. Pressure heat is drawn separately because it persists across shots.
        private void DrawBellCharge(bool bracing)
        {
            var state = _abilities.GetAbilityState(PlayerAbility.Primary);
            if (bracing || _abilities.IsPreparingSpecial
                || _gesture == Gesture.Special || _gesture == Gesture.Zone)
            {
                _reloading = false;
                return;
            }

            var settings = tuning.presentation;
            var ready = _abilities.IsPrimaryReady;
            var charge = 1f - Mathf.Clamp01(state.Remaining / state.Duration);

            if (!ready && state.Remaining <= 0f) charge = 0f;
            if (!ready) _reloading = true;
            else if (_reloading)
            {
                _reloading = false;
                _readyAt = Clock;
            }

            var pulse = ready ? 1f - Mathf.Clamp01((Clock - _readyAt) / 0.35f) : 0f;
            var energy = Mathf.SmoothStep(0f, 1f, charge);

            if (energy < 0.02f && pulse <= 0f) return;

            var bell = BellPosition;
            var bank = 1f + 0.3f * _abilities.PressureNormalized * settings.pressureGlow;
            var breath = ready ? 1f + 0.06f * Mathf.Sin(Clock * 1.4f) : 1f;
            var colour = Color.Lerp(settings.gold, new Color(1f, 0.95f, 0.7f, 1f), energy * energy * 0.7f + pulse * 0.5f);
            colour.a = Mathf.Lerp(0.1f, 0.65f, energy);
            _bellGlow.enabled = true;
            _bellGlow.positionCount = 2;
            _bellGlow.SetPosition(0, bell);
            _bellGlow.SetPosition(1, bell + _camera.transform.right * 0.001f);
            _bellGlow.startWidth = _bellGlow.endWidth = settings.flashRadius * bank * breath
                * (Mathf.Lerp(0.3f, 1.7f, energy) + 1.2f * pulse * pulse);
            _bellGlow.startColor = _bellGlow.endColor = colour;

            if (ready) return;

            // Spark travel uses cooldown progress, keeping its pace aligned
            // with the shot interval at each Pressure level.
            var count = Mathf.FloorToInt(energy * (_bellSparks.Length + 0.99f));
            for (var i = 0; i < count; i++)
            {
                var phase = Mathf.Repeat(charge * 2.2f + i / (float)_bellSparks.Length, 1f);
                var a = i * 2.39996f;
                var dir = _camera.transform.right * Mathf.Cos(a) + _camera.transform.up * Mathf.Sin(a);
                var distance = Mathf.Lerp(0.3f, 0.03f, phase * phase) * bank;
                var spark = settings.gold;
                spark.a = Mathf.Sin(phase * Mathf.PI) * Mathf.Lerp(0.3f, 0.7f, energy);
                var line = _bellSparks[i];
                line.enabled = true;
                line.positionCount = 2;
                line.SetPosition(0, bell + dir * (distance + 0.07f));
                line.SetPosition(1, bell + dir * distance);
                line.startWidth = 0f;
                line.endWidth = settings.cueWidth * Mathf.Lerp(1f, 2f, energy);
                line.startColor = line.endColor = spark;
            }
        }

        private void DrawPressure(float side)
        {
            var settings = tuning.presentation;
            var gain = settings.pressureGlow;
            // Publish the bell after applying the pose. PressureVisual runs later in the frame
            // and uses this world position as the moving destination for its notes.
            BellPosition = weaponPivot.TransformPoint(BellOffset(side));
            _charge = Mathf.Lerp(_charge, gain > 0f ? _abilities.PressureNormalized : 0f,
                1f - Mathf.Exp(-8f * _player.Hitstop.ActionDeltaTime));
            var reachedMax = _charge >= 0.995f;
            if (reachedMax && !_maxed) _maxPulseAt = Clock;
            _maxed = reachedMax;
            var heat = Mathf.Clamp01(_charge * gain);
            var high = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, heat));
            var throb = 0.5f + 0.5f * Mathf.Sin(Clock * Mathf.Lerp(0.9f, 1.8f, heat));
            var pulse = 1f - Mathf.Clamp01((Clock - _maxPulseAt) / 1f);
            var tint = Color.Lerp(Color.white, Color.Lerp(settings.gold, Color.white, 0.25f), heat * 0.85f);
            weaponSprite.color = Color.Lerp(tint, Color.white, high * throb * 0.35f + 0.5f * pulse * pulse);
        }

        // Project the bell along the fixed camera's view ray onto the player's ground plane,
        // placing the impact under its visible tip in the angled view.
        private Vector3 GroundUnder(Vector3 point)
        {
            var forward = _camera.transform.forward;
            var ground = point + forward * ((transform.position.y - point.y) / forward.y);
            ground.y = transform.position.y + 0.02f;
            return ground;
        }

        private static void DrawFloorRing(LineRenderer line, Vector3 centre, float radius, float width, Color color)
        {
            const int segments = 40;
            line.enabled = true;
            line.positionCount = segments + 1;
            line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = color;
            for (var i = 0; i <= segments; i++)
            {
                var a = i * Mathf.PI * 2f / segments;
                line.SetPosition(i, centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
            }
        }

        // All impact layers sample elapsed time from one captured point and timestamp.
        // Their staggered envelopes give the slam a single origin while debris outlasts the leading wave.
        private void DrawShockwave()
        {
            var settings = tuning.presentation;
            var duration = settings.guardShockwaveDuration;
            var age = Clock - _impactAt;
            if (!_impacted || age < 0f || age > duration * 1.4f) return;
            var scale = settings.guardImpactScale;
            var radius = settings.guardWaveRadius * scale;
            var u = Mathf.Clamp01(age / duration);
            var lead = radius * (1f - Mathf.Pow(1f - u, 4f));
            var edge = Color.Lerp(Color.white, settings.gold, Mathf.Sqrt(u));
            edge.a = Mathf.Pow(1f - u, 1.5f);
            DrawFloorRing(_waves[0], _impactPoint, lead,
                settings.guardWaveWidth * scale * Mathf.Lerp(1.8f, 0.15f, u), edge);

            var u2 = Mathf.Clamp01((age - duration * 0.14f) / (duration * 1.25f));
            if (age > duration * 0.14f && u2 < 1f)
            {
                var wave = settings.gold;
                wave.a = 0.55f * (1f - u2) * (1f - u2);
                DrawFloorRing(_waves[1], _impactPoint, radius * 0.6f * (1f - Mathf.Pow(1f - u2, 3f)),
                    settings.guardWaveWidth * scale * Mathf.Lerp(1.1f, 0.15f, u2), wave);
            }

            for (var i = 0; i < _dust.Length; i++)
            {
                if (u >= 1f) break;
                var a = (i + 0.5f * (i & 1)) * Mathf.PI * 2f / _dust.Length;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var line = _dust[i];
                var color = settings.gold;
                color.a = 0.75f * (1f - u) * (1f - u);
                line.enabled = true;
                line.positionCount = 2;
                line.SetPosition(0, _impactPoint + dir * (lead * (i % 2 == 0 ? 0.5f : 0.7f)));
                line.SetPosition(1, _impactPoint + dir * (lead * 0.97f));
                line.startWidth = 0f;
                line.endWidth = settings.guardWaveWidth * scale * 0.7f;
                line.startColor = line.endColor = color;
            }

            var airborne = Mathf.Clamp01(age / (duration * 0.9f));
            for (var i = 0; i < _chips.Length && airborne < 1f; i++)
            {
                var a = (i + 0.3f) * Mathf.PI * 2f / _chips.Length;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var line = _chips[i];
                var colour = Color.Lerp(settings.gold, Color.white, 0.4f);
                colour.a = 1f - airborne;
                var from = _impactPoint + dir * (radius * 0.4f * (1f - Mathf.Pow(1f - airborne, 2f)))
                    + Vector3.up * (0.3f * scale * 4f * airborne * (1f - airborne) * 0.5f);
                line.enabled = true;
                line.positionCount = 2;
                line.SetPosition(0, from);
                line.SetPosition(1, from + _camera.transform.up * (0.05f * scale));
                line.startWidth = line.endWidth = settings.guardWaveWidth * scale * 0.7f;
                line.startColor = line.endColor = colour;
            }
        }

        private Vector3 Offset(Vector3 offset, Vector3 direction, float side) =>
            _camera.transform.right * (offset.x * side) + Vector3.up * offset.y + direction * offset.z;

        private Vector3 BellOffset(float side)
        {
            var settings = tuning.presentation;
            return _spriteRestPosition + new Vector3(settings.muzzleOffset.x - settings.gripPoint.x,
                (settings.muzzleOffset.y - settings.gripPoint.y) * side, 0f);
        }

        private void DrawRing(LineRenderer line, Vector3 center, float radius, float fraction, Color color)
        {
            const int segments = 24;
            line.enabled = true;
            line.positionCount = segments + 1;
            line.startColor = line.endColor = color;
            for (var i = 0; i <= segments; i++)
            {
                var angle = (0.1f + fraction * i / segments) * Mathf.PI * 2f;
                line.SetPosition(i, center + _camera.transform.rotation
                    * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
            }
        }

        private void ClearCues()
        {
            muzzleCue.enabled = false;
            guardCue.enabled = false;
            specialCue.enabled = false;
            Disable(_waves);
            Disable(_dust);
            Disable(_chips);
            Disable(_sparks);
            Disable(_bellSparks);
            _bellGlow.enabled = false;
            _glow.enabled = false;
            _hold.enabled = false;
        }

        private static void Disable(LineRenderer[] lines)
        {
            foreach (var line in lines) line.enabled = false;
        }

        public override void Cancel()
        {
            _gesture = Gesture.None;
            _anticipation = 0f;
            weaponPivot.localScale = _restScale;
            _placed = false;
            _wasBracing = false;
            _guardAt = _blockedAt = _impactAt = _maxPulseAt = float.NegativeInfinity;
            _impacted = _maxed = _consumedZone = _reloading = false;
            _readyAt = float.NegativeInfinity;
            _charge = _releaseUntil = 0f;
            weaponSprite.color = Color.white;
            ClearCues();
        }

        private void HandleSceneChanged(Scene previous, Scene current)
        {
            Cancel();
            _camera = null;
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleSceneChanged;
            Cancel();
            presentationRoot.gameObject.SetActive(false);
        }
    }
}
