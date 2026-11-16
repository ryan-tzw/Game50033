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
            if (leftCymbal != null) leftCymbal.enabled = false;
            if (rightCymbal != null) rightCymbal.enabled = false;
            SetTrail(leftTrail, false);
            SetTrail(rightTrail, false);
        }
    }
}
