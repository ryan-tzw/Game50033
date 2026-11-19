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

        private enum Gesture { None, Primary, Zone, Special }
        private Player _player;
        private TromboneAbilities _abilities;
        private Camera _camera;
        private Gesture _gesture;
        private Vector3 _direction = Vector3.forward;
        private Vector3 _shotOffset;
        private Vector3 _blockOffset;
        private Vector3 _guardFrom;
        private Quaternion _guardRotation;
        private float _startedAt;
        private float _duration;
        private float _guardAt = float.NegativeInfinity;
        private float _blockedAt = float.NegativeInfinity;
        private bool _resonatingShot;
        private bool _resonatingBlock;
        private bool _placed;
        private bool _wasBracing;
        private float Clock => _player.Hitstop.ActionTime;

        private void Awake()
        {
            _player = GetComponent<Player>();
            _abilities = GetComponent<TromboneAbilities>();
            if (tuning == null || presentationRoot == null || weaponPivot == null || weaponSprite == null
                || weaponSprite.sprite == null || muzzleCue == null || guardCue == null || specialCue == null
                || tuning.presentation.weaponMaterial == null || tuning.presentation.effectMaterial == null)
            {
                Debug.LogError("TromboneWeaponVisual requires tuning and the authored weapon/cue references.", this);
                enabled = false;
                return;
            }
            var scale = tuning.presentation.weaponWidth / Mathf.Max(0.001f, weaponSprite.sprite.bounds.size.x);
            weaponSprite.transform.localScale = new Vector3(scale, scale, 1f);
            weaponSprite.sharedMaterial = tuning.presentation.weaponMaterial;
            // Use the same front pass as the weapon so character depth cannot hide its cues.
            muzzleCue.sharedMaterial = guardCue.sharedMaterial = specialCue.sharedMaterial = tuning.presentation.weaponMaterial;
            muzzleCue.widthMultiplier = guardCue.widthMultiplier = tuning.presentation.cueWidth;
            specialCue.widthMultiplier = tuning.presentation.cueWidth * 0.65f;
            ClearCues();
        }

        private void OnEnable() => SceneManager.activeSceneChanged += HandleSceneChanged;

        internal void PlayShot(Vector3 direction, Vector3 origin, bool special, bool resonates, float interval)
        {
            if (!isActiveAndEnabled) return;
            _gesture = special ? Gesture.Special : Gesture.Primary;
            _direction = direction;
            _shotOffset = origin - transform.position;
            _resonatingShot = resonates;
            _startedAt = Clock;
            _duration = special ? tuning.presentation.specialRecovery
                : Mathf.Min(tuning.presentation.primaryRecovery, Mathf.Max(0.01f, interval));
            ClearCues();
        }

        internal void PlayZone(Vector3 direction)
        {
            if (!isActiveAndEnabled) return;
            _gesture = Gesture.Zone;
            _direction = direction;
            _startedAt = Clock;
            _duration = tuning.presentation.zoneSweepDuration;
            ClearCues();
        }

        internal void PlayGuard(Vector3 direction)
        {
            if (!isActiveAndEnabled) return;
            _gesture = Gesture.None;
            _direction = direction;
            _guardAt = Clock;
            _guardFrom = weaponPivot.position - transform.position;
            _guardRotation = weaponPivot.rotation;
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
            if (presentationRoot == null || tuning == null) return;
            var visible = _player.isActiveAndEnabled && _player.HasActiveInput && !_player.Health.IsDowned
                && _abilities.isActiveAndEnabled;
            presentationRoot.gameObject.SetActive(visible);
            if (!visible)
            {
                Cancel();
                _placed = false;
                return;
            }
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            var settings = tuning.presentation;
            var age = Clock - _startedAt;
            if ((_gesture != Gesture.None && age >= _duration)
                || (_gesture == Gesture.Zone && !_abilities.IsInCommandZone)) _gesture = Gesture.None;
            var aiming = _gesture != Gesture.None || _abilities.IsGuarding;
            var direction = aiming ? _direction : _player.AimDirection;
            var viewDirection = new Vector2(Vector3.Dot(direction, _camera.transform.right),
                Vector3.Dot(direction, _camera.transform.up)).normalized;
            if (viewDirection.sqrMagnitude < 0.001f) viewDirection = Vector2.right;
            var side = viewDirection.x >= 0f ? 1f : -1f;
            var angle = Mathf.Atan2(viewDirection.y, viewDirection.x) * Mathf.Rad2Deg;
            var position = transform.position + Offset(settings.idleOffset, direction, side);
            var snap = false;
            var bracing = _abilities.IsGuarding && _gesture == Gesture.None;
            ClearCues();

            if (bracing)
            {
                // An intervening shot or sweep can finish while Guard is still active.
                if (!_wasBracing)
                {
                    _guardAt = Clock;
                    _guardFrom = weaponPivot.position - transform.position;
                    _guardRotation = weaponPivot.rotation;
                }
                var brace = Mathf.SmoothStep(0f, 1f, (Clock - _guardAt) / Mathf.Max(0.01f, settings.guardRaiseTime));
                position = Vector3.Lerp(transform.position + _guardFrom,
                    transform.position + Offset(settings.guardOffset, direction, side), brace);
                var guardAngle = side > 0f ? -settings.guardDownAngle : settings.guardDownAngle - 180f;
                var rotation = _camera.transform.rotation * Quaternion.AngleAxis(guardAngle, Vector3.forward);
                weaponPivot.SetPositionAndRotation(position, Quaternion.Slerp(_guardRotation, rotation, brace));
                angle = guardAngle;
                snap = true;
                DrawRing(guardCue, position, settings.guardCueRadius, 0.3f, settings.gold * new Color(1f, 1f, 1f, 0.45f));
            }

            if (_gesture == Gesture.Zone)
            {
                var progress = Mathf.Clamp01(age / Mathf.Max(0.01f, _duration));
                var sweep = Mathf.SmoothStep(0f, 1f, progress);
                var radial = Quaternion.AngleAxis(sweep * 360f, Vector3.up) * _direction;
                var envelope = Mathf.Sin(progress * Mathf.PI);
                position += radial * (settings.zoneWeaponReach * envelope);
                angle += side * 360f * sweep;
                snap = true;
            }
            else if (_gesture == Gesture.Primary || _gesture == Gesture.Special)
            {
                var special = _gesture == Gesture.Special;
                var progress = Mathf.Clamp01(age / Mathf.Max(0.01f, _duration));
                // Fire immediately, then recover. Another accepted attack replaces this pose.
                var recoil = Mathf.Pow(1f - progress, 3f);
                var aim = _camera.transform.right * viewDirection.x + _camera.transform.up * viewDirection.y;
                var baseRotation = _camera.transform.rotation * Quaternion.AngleAxis(angle, Vector3.forward);
                var muzzleOffset = new Vector3(settings.muzzleOffset.x, settings.muzzleOffset.y * side, 0f);
                var firingPosition = transform.position + _shotOffset - baseRotation * muzzleOffset;
                position = Vector3.Lerp(position, firingPosition, 1f - Mathf.SmoothStep(0f, 1f, progress));
                position -= aim * ((special ? settings.specialRecoil : settings.primaryRecoil) * recoil);
                angle += side * (special ? settings.specialKick : settings.primaryKick) * recoil;
                snap = true;
            }

            var block = 1f - Mathf.Clamp01((Clock - _blockedAt) / Mathf.Max(0.01f, settings.blockDuration));
            if (block > 0f)
            {
                position -= direction * (settings.blockKick * block);
                var color = _resonatingBlock ? settings.resonatingColor : Color.white;
                color.a *= block;
                DrawRing(guardCue, transform.position + _blockOffset,
                    settings.guardCueRadius * (2f - block) * (_resonatingBlock ? 1.25f : 1f), 1f, color);
            }

            var targetRotation = _camera.transform.rotation * Quaternion.AngleAxis(angle, Vector3.forward);
            var blend = snap || !_placed ? 1f : 1f - Mathf.Exp(-settings.poseResponse * _player.Hitstop.ActionDeltaTime);
            // Guard's raise already blended its rotation above.
            if (bracing) targetRotation = weaponPivot.rotation;
            weaponPivot.SetPositionAndRotation(Vector3.Lerp(weaponPivot.position, position, blend),
                Quaternion.Slerp(weaponPivot.rotation, targetRotation, blend));
            // Mirror from the displayed rotation, so smoothing across left/right aim never flips early.
            var displayedRight = Vector3.Dot(weaponPivot.right, _camera.transform.right);
            if (_gesture != Gesture.None || bracing) weaponSprite.flipY = side < 0f;
            else if (Mathf.Abs(displayedRight) > 0.001f) weaponSprite.flipY = displayedRight < 0f;
            if (_gesture == Gesture.Primary || _gesture == Gesture.Special)
                DrawRelease(age, _gesture == Gesture.Special, side);
            _wasBracing = bracing;
            _placed = true;
        }

        private void DrawRelease(float age, bool special, float side)
        {
            var settings = tuning.presentation;
            var bell = weaponPivot.TransformPoint(settings.muzzleOffset.x, settings.muzzleOffset.y * side, 0f);
            var flash = 1f - Mathf.Clamp01(age / Mathf.Max(0.01f, settings.flashDuration));
            if (flash > 0f)
            {
                var color = _resonatingShot ? settings.resonatingColor : Color.Lerp(settings.gold, Color.white, 0.8f);
                color.a *= flash;
                DrawRing(muzzleCue, bell, settings.flashRadius * (special ? settings.specialFlashScale : 1f)
                    * (1.2f - flash * 0.3f), 1f, color);
            }
            if (!special || age >= settings.flashDuration * 2f) return;
            var glint = settings.gold;
            glint.a *= 1f - age / (settings.flashDuration * 2f);
            specialCue.enabled = true;
            specialCue.positionCount = 2;
            specialCue.SetPosition(0, bell - _camera.transform.up * settings.flashRadius * 2f);
            specialCue.SetPosition(1, bell + _camera.transform.up * settings.flashRadius * 2f);
            specialCue.startColor = specialCue.endColor = glint;
        }

        private Vector3 Offset(Vector3 offset, Vector3 direction, float side) =>
            _camera.transform.right * (offset.x * side) + Vector3.up * offset.y + direction * offset.z;

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
            if (muzzleCue != null) muzzleCue.enabled = false;
            if (guardCue != null) guardCue.enabled = false;
            if (specialCue != null) specialCue.enabled = false;
        }

        public override void Cancel()
        {
            _gesture = Gesture.None;
            _wasBracing = false;
            _guardAt = _blockedAt = float.NegativeInfinity;
            ClearCues();
        }

        private void HandleSceneChanged(Scene previous, Scene current)
        {
            Cancel();
            _placed = false;
            _camera = null;
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleSceneChanged;
            Cancel();
            _placed = false;
            if (presentationRoot != null) presentationRoot.gameObject.SetActive(false);
        }
    }
}
