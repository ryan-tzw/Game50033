namespace Game50033
{
    using UnityEngine;

    // presentation only: every strike is shaped so a swinging cymbal reaches the shared hitting point
    // exactly when the approach ends, which is when CymbalsAbilities resolves damage.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Player))]
    public sealed class CymbalsWeaponVisual : FloatingWeaponVisual
    {
        [SerializeField] private SpriteRenderer leftCymbal;
        [SerializeField] private SpriteRenderer rightCymbal;
        [Tooltip("Trail children of each cymbal, on the PixelWorldVFX layer. Optional.")]
        [SerializeField] private TrailRenderer leftTrail;
        [SerializeField] private TrailRenderer rightTrail;
        [SerializeField] private CymbalsTuning tuning;

        private Player _player;
        private bool _posing;
        private bool _placed;
        private int _hitIndex = -1;
        private Vector3 _direction = Vector3.forward;
        private CymbalsAbilities.Phase _phase;
        private float _progress;
        private bool _windupCaptured;
        private Vector3 _leftWindupFrom;
        private Vector3 _rightWindupFrom;

        // cymbal throw: the right cymbal follows the gameplay-owned flight position.
        private enum ThrowPose { None, Windup, Flying }
        private ThrowPose _throwPose;
        private Vector3 _throwDirection = Vector3.forward;
        private float _throwWindupProgress;
        private Vector3 _thrownPosition;
        private float _thrownSpeed;
        private bool _apexFlash;
        private bool _charging;
        private Vector3 _chargeDirection = Vector3.forward;
        private float _spinAngle;
        private MaterialPropertyBlock _rightProperties;
        private static readonly int SpriteRotationId = Shader.PropertyToID("_SpriteRotation");
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");

        private void Awake()
        {
            _player = GetComponent<Player>();
        }

        private void OnEnable()
        {
            if (tuning == null)
            {
                Debug.LogError("CymbalsWeaponVisual requires a CymbalsTuning asset.", this);
                enabled = false;
                return;
            }
            ApplyTrailSettings(leftTrail);
            ApplyTrailSettings(rightTrail);
            _rightProperties ??= new MaterialPropertyBlock();
        }

        private void ApplyTrailSettings(TrailRenderer trail)
        {
            if (trail == null) return;
            var weapon = tuning.weapon;
            if (weapon.trailMaterial != null) trail.sharedMaterial = weapon.trailMaterial;
            trail.time = weapon.trailTime;
            trail.startWidth = weapon.trailStartWidth;
            trail.endWidth = weapon.trailEndWidth;
            trail.colorGradient = weapon.trailColor;
            trail.alignment = LineAlignment.View;
            trail.emitting = false;
        }

        public void SetStrikePose(int hitIndex, Vector3 direction, CymbalsAbilities.Phase phase, float progress)
        {
            // a new strike begins its windup from wherever the cymbals are now, so chains and dash-cancels never snap.
            if (!_posing || hitIndex != _hitIndex || phase == CymbalsAbilities.Phase.Windup && _phase != CymbalsAbilities.Phase.Windup)
                _windupCaptured = false;
            _posing = true;
            _hitIndex = hitIndex;
            _direction = direction;
            _phase = phase;
            _progress = progress;
        }

        public override void Cancel()
        {
            _posing = false;
            _windupCaptured = false;
        }

        public void SetThrowWindup(Vector3 direction, float progress)
        {
            Cancel();
            _throwPose = ThrowPose.Windup;
            _throwDirection = direction;
            _throwWindupProgress = progress;
        }

        // position is owned by CymbalsAbilities; speed is 0..1 and drives the spin; flash marks the apex window.
        public void SetThrownCymbal(Vector3 worldPosition, float normalizedSpeed, bool apexFlash)
        {
            _throwPose = ThrowPose.Flying;
            _thrownPosition = worldPosition;
            _thrownSpeed = normalizedSpeed;
            _apexFlash = apexFlash;
        }

        // recast dash: the remaining cymbal leads at the hitting point.
        public void SetCharge(Vector3 direction)
        {
            _charging = true;
            _chargeDirection = direction;
        }

        public void EndThrow()
        {
            _throwPose = ThrowPose.None;
            _charging = false;
            _apexFlash = false;
            _spinAngle = 0f;
            ApplyRightCymbalProperties(0f, 0f);
        }

        private void LateUpdate()
        {
            if (leftCymbal == null || rightCymbal == null) return;

            var visible = _player.isActiveAndEnabled && _player.HasActiveInput;
            leftCymbal.enabled = visible;
            rightCymbal.enabled = visible;
            if (!visible)
            {
                _placed = false;
                SetTrail(leftTrail, false);
                SetTrail(rightTrail, false);
                return;
            }

            if (_throwPose != ThrowPose.None)
            {
                PlaceThrowPose();
                _placed = true;
                return;
            }

            var hits = tuning.combo.hits;
            var posing = _posing && hits != null && _hitIndex >= 0 && _hitIndex < hits.Length;
            var guardDirection = posing ? _direction : _player.Movement.FacingDirection;
            var follow = !_placed ? 1f : 1f - Mathf.Exp(-tuning.weapon.followSharpness * Time.deltaTime);

            var hit = posing ? hits[_hitIndex] : default;
            var bothSwing = posing && hit.left.swings && hit.right.swings;
            if (posing && _phase == CymbalsAbilities.Phase.Windup && !_windupCaptured)
            {
                _leftWindupFrom = leftCymbal.transform.position - transform.position;
                _rightWindupFrom = rightCymbal.transform.position - transform.position;
                _windupCaptured = true;
            }

            PlaceCymbal(leftCymbal, -1f, posing && hit.left.swings, hit.left, bothSwing, guardDirection, _leftWindupFrom, follow);
            PlaceCymbal(rightCymbal, 1f, posing && hit.right.swings, hit.right, bothSwing, guardDirection, _rightWindupFrom, follow);

            // the streak only draws while a cymbal sweeps through the hitting point.
            var sweeping = posing && (_phase == CymbalsAbilities.Phase.Approach || _phase == CymbalsAbilities.Phase.FollowThrough);
            SetTrail(leftTrail, sweeping && hit.left.swings);
            SetTrail(rightTrail, sweeping && hit.right.swings);
            _placed = true;
        }

        private void PlaceCymbal(SpriteRenderer cymbal, float side, bool swinging, CymbalsTuning.HandMotion motion,
            bool bothSwing, Vector3 guardDirection, Vector3 windupFrom, float follow)
        {
            var weapon = tuning.weapon;
            var guard = Offset(guardDirection, side * weapon.restAngle, weapon.restDistance, weapon.restHeight, 0f);
            var scale = weapon.spriteScale;

            if (!swinging)
            {
                // idle and non-swinging hands drift to their guard pose.
                cymbal.transform.position = Vector3.Lerp(cymbal.transform.position, transform.position + guard, follow);
                cymbal.transform.localScale = Vector3.one * scale;
                return;
            }

            // swinging hands are placed exactly, with no smoothing, so the hitting point is met on time.
            var combo = tuning.combo;
            var spacing = bothSwing ? side * weapon.clashSpacing * 0.5f : 0f;
            var start = Offset(_direction, motion.startPose.x, motion.startPose.y, combo.hitHeight, spacing);
            var hitPoint = Offset(_direction, 0f, combo.hitDistance, combo.hitHeight, spacing);
            var end = Offset(_direction, motion.endPose.x, motion.endPose.y, combo.hitHeight, spacing);
            Vector3 offset;
            switch (_phase)
            {
                case CymbalsAbilities.Phase.Windup:
                    offset = Vector3.LerpUnclamped(windupFrom, start, weapon.windupEasing.Evaluate(_progress));
                    break;
                case CymbalsAbilities.Phase.Approach:
                    offset = Sweep(motion.startPose, new Vector2(0f, combo.hitDistance), weapon.approachEasing.Evaluate(_progress), spacing);
                    break;
                case CymbalsAbilities.Phase.FollowThrough:
                    offset = Sweep(new Vector2(0f, combo.hitDistance), motion.endPose, weapon.followThroughEasing.Evaluate(_progress), spacing);
                    scale *= Mathf.Lerp(weapon.impactScale, 1f, _progress);
                    break;
                default:
                    offset = Vector3.LerpUnclamped(end, guard, weapon.recoveryEasing.Evaluate(_progress));
                    break;
            }

            // keep the endpoints exact regardless of how the easing curves are authored.
            if (_phase == CymbalsAbilities.Phase.Approach && _progress >= 1f
                || _phase == CymbalsAbilities.Phase.FollowThrough && _progress <= 0f) offset = hitPoint;

            cymbal.transform.position = transform.position + offset;
            cymbal.transform.localScale = Vector3.one * scale;
        }

        private void PlaceThrowPose()
        {
            var weapon = tuning.weapon;
            var throwSettings = tuning.cymbalThrow;
            var combo = tuning.combo;
            var follow = 1f - Mathf.Exp(-weapon.followSharpness * Time.deltaTime);

            // left hand: guard, or leading at the hitting point during the recast dash.
            if (_charging)
            {
                leftCymbal.transform.position = transform.position + Offset(_chargeDirection, 0f, combo.hitDistance, combo.hitHeight, 0f);
                leftCymbal.transform.localScale = Vector3.one * weapon.spriteScale * weapon.impactScale;
                SetTrail(leftTrail, true);
            }
            else
            {
                var guard = Offset(_player.Movement.FacingDirection, -weapon.restAngle, weapon.restDistance, weapon.restHeight, 0f);
                leftCymbal.transform.position = Vector3.Lerp(leftCymbal.transform.position, transform.position + guard, follow);
                leftCymbal.transform.localScale = Vector3.one * weapon.spriteScale;
                SetTrail(leftTrail, false);
            }

            var scale = weapon.spriteScale;
            if (_throwPose == ThrowPose.Windup)
            {
                // pull the right cymbal back behind the throwing shoulder.
                var from = Offset(_throwDirection, weapon.restAngle, weapon.restDistance, weapon.restHeight, 0f);
                var back = Offset(_throwDirection, 140f, weapon.restDistance + 0.15f, combo.hitHeight, 0f);
                rightCymbal.transform.position = transform.position + Vector3.LerpUnclamped(from, back, weapon.windupEasing.Evaluate(_throwWindupProgress));
                rightCymbal.transform.localScale = Vector3.one * scale;
                SetTrail(rightTrail, false);
                ApplyRightCymbalProperties(0f, 0f);
                return;
            }

            // flying: exact gameplay position; spin follows travel speed, so it visibly slows at the apex.
            _spinAngle = Mathf.Repeat(_spinAngle + throwSettings.spinSpeed * Mathf.Lerp(throwSettings.minimumSpin, 1f, _thrownSpeed) * Time.deltaTime, 360f);
            var flash = 0f;
            if (_apexFlash)
            {
                flash = throwSettings.apexFlashAmount * (0.5f + 0.5f * Mathf.Sin(Time.time * throwSettings.apexFlashRate * Mathf.PI * 2f));
                scale *= 1.15f;
            }
            rightCymbal.transform.position = _thrownPosition;
            rightCymbal.transform.localScale = Vector3.one * scale;
            SetTrail(rightTrail, true);
            ApplyRightCymbalProperties(_spinAngle, flash);
        }

        private void ApplyRightCymbalProperties(float rotation, float flash)
        {
            if (rightCymbal == null || _rightProperties == null) return;
            rightCymbal.GetPropertyBlock(_rightProperties);
            _rightProperties.SetFloat(SpriteRotationId, rotation);
            _rightProperties.SetFloat(FlashAmountId, flash);
            _rightProperties.SetColor(FlashColorId, tuning.cymbalThrow.apexFlashColor);
            rightCymbal.SetPropertyBlock(_rightProperties);
        }

        private void SetTrail(TrailRenderer trail, bool emitting)
        {
            if (trail == null || trail.emitting == emitting) return;
            // starting a new streak clears the last one so it never connects through the guard pose.
            if (emitting)
            {
                ApplyTrailSettings(trail);
                trail.Clear();
            }
            trail.emitting = emitting;
        }

        // sweeps around the player (angle and distance interpolate separately), so slashes travel in an arc.
        private Vector3 Sweep(Vector2 from, Vector2 to, float t, float spacing) =>
            Offset(_direction, Mathf.LerpUnclamped(from.x, to.x, t), Mathf.LerpUnclamped(from.y, to.y, t), tuning.combo.hitHeight, spacing);

        private static Vector3 Offset(Vector3 aim, float angle, float distance, float height, float sideways)
        {
            var right = Vector3.Cross(Vector3.up, aim);
            return Quaternion.AngleAxis(angle, Vector3.up) * aim * distance + right * sideways + Vector3.up * height;
        }

        private void OnDisable()
        {
            Cancel();
            EndThrow();
            if (leftCymbal != null) leftCymbal.enabled = false;
            if (rightCymbal != null) rightCymbal.enabled = false;
            SetTrail(leftTrail, false);
            SetTrail(rightTrail, false);
        }
    }
}
