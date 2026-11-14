namespace Game50033
{
    using UnityEngine;

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Player))]
    public sealed class GuitarWeaponVisual : FloatingWeaponVisual
    {
        [SerializeField] private Transform weaponPivot;
        [SerializeField] private SpriteRenderer weaponSprite;
        [SerializeField] private Material restingMaterial;
        [SerializeField] private Material attackMaterial;
        [SerializeField] private GuitarTuning tuning;

        private Player _player;
        private Camera _camera;
        private bool _posing;
        private bool _active;
        private bool _placed;
        private Vector3 _direction = Vector3.forward;
        private float _angle;
        private float _height;
        private float _emphasis = 1f;
        private float _visualAngle;
        private Vector2 _gripPoint;
        private MaterialPropertyBlock _properties;
        private static readonly int SpriteFlatDepthId = Shader.PropertyToID("_SpriteFlatDepth");
        private static readonly int SpriteRotationId = Shader.PropertyToID("_SpriteRotation");
        private static readonly int SpritePivotId = Shader.PropertyToID("_SpritePivot");

        private void Awake()
        {
            _player = GetComponent<Player>();
        }

        private void OnEnable()
        {
            if (tuning == null || restingMaterial == null || attackMaterial == null)
            {
                Debug.LogError("GuitarWeaponVisual requires tuning and both resting/attack materials.", this);
                enabled = false;
                return;
            }
            _properties ??= new MaterialPropertyBlock();
        }

        public void SetSwingPose(Vector3 direction, float angle, float height, bool active, float emphasis)
        {
            _posing = true;
            _direction = direction;
            _angle = angle;
            _height = height;
            _active = active;
            _emphasis = emphasis;
        }

        public override void Cancel()
        {
            _posing = false;
            _active = false;
        }

        private void LateUpdate()
        {
            if (weaponPivot == null || weaponSprite == null) return;
            var weapon = tuning.weapon;
            if (_camera == null) _camera = Camera.main;

            var visible = _player.isActiveAndEnabled && _player.HasActiveInput;
            weaponSprite.enabled = visible;
            if (!visible)
            {
                _placed = false;
                return;
            }
            // show the impact pose on its request frame, then hold all weapon motion during stopped frames.
            if (_player.Hitstop.IsStopped && _player.Hitstop.ActionDeltaTime <= 0f) return;

            var facing = _player.Movement.FacingDirection;
            if (_camera == null) return;

            var handSide = Vector3.Dot(facing, _camera.transform.right) >= -0.0001f ? 1f : -1f;
            var position = transform.position + Vector3.up * weapon.gripOffset.y
                + _camera.transform.right * (weapon.gripOffset.x * handSide) + facing * weapon.gripOffset.z;
            var radial = Quaternion.AngleAxis(_angle, Vector3.up) * _direction;
            if (_posing) position = transform.position + Vector3.up * _height;

            // use the neck at rest, blending to the external swing pivot during windup.
            var blend = !_placed || _active ? 1f : 1f - Mathf.Exp(-weapon.followSharpness * _player.Hitstop.ActionDeltaTime);
            weaponPivot.position = Vector3.Lerp(weaponPivot.position, position, blend);
            var targetGrip = _posing ? weapon.spriteGripPoint : weapon.restingGripPoint;
            _gripPoint = Vector2.Lerp(_gripPoint, targetGrip, blend);
            var scale = weapon.spriteScale * (_posing ? _emphasis : 1f);
            weaponSprite.transform.localScale = Vector3.one * scale;
            weaponSprite.transform.localPosition = -(Vector3)_gripPoint * scale;
            weaponSprite.transform.localRotation = Quaternion.identity;
            var material = _posing ? attackMaterial : restingMaterial;
            if (weaponSprite.sharedMaterial != material) weaponSprite.sharedMaterial = material;

            // tilt the backward direction by a fixed screen-space angle, mirrored when facing left.
            var screenDirection = _posing
                ? new Vector2(Vector3.Dot(radial, _camera.transform.right), Vector3.Dot(radial, _camera.transform.up))
                : new Vector2(-Vector3.Dot(facing, _camera.transform.right), -Vector3.Dot(facing, _camera.transform.up));
            var spriteAngle = Mathf.Atan2(screenDirection.y, screenDirection.x) * Mathf.Rad2Deg - weapon.spriteOutwardAngle;
            if (!_posing) spriteAngle -= weapon.restingAngleOffset * handSide;
            var rotationBlend = !_placed || _active ? 1f : 1f - Mathf.Exp(-weapon.rotationSharpness * _player.Hitstop.ActionDeltaTime);
            _visualAngle = Mathf.LerpAngle(_visualAngle, spriteAngle, rotationBlend);

            // the original flat geometry supplies depth; the shader supplies the camera-facing image.
            var worldDirection = _posing ? radial : -facing;
            if (!_posing && Mathf.Abs(_camera.transform.forward.y) > 0.001f)
            {
                // project the already-smoothed visible direction onto XZ along the camera's view rays.
                // this keeps the idle depth geometry on the same axis instead of smoothing it a second time.
                var visibleAngle = (_visualAngle + weapon.spriteOutwardAngle) * Mathf.Deg2Rad;
                worldDirection = _camera.transform.right * Mathf.Cos(visibleAngle) + _camera.transform.up * Mathf.Sin(visibleAngle);
                worldDirection -= _camera.transform.forward * (worldDirection.y / _camera.transform.forward.y);
                worldDirection.y = 0f;
            }
            var yaw = weapon.spriteOutwardAngle - Mathf.Atan2(worldDirection.z, worldDirection.x) * Mathf.Rad2Deg;
            var rotation = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
            weaponPivot.rotation = _posing ? Quaternion.Slerp(weaponPivot.rotation, rotation, rotationBlend) : rotation;
            weaponSprite.GetPropertyBlock(_properties);
            _properties.SetFloat(SpriteFlatDepthId, 1f);
            _properties.SetFloat(SpriteRotationId, _visualAngle);
            _properties.SetVector(SpritePivotId, new Vector4(_gripPoint.x, _gripPoint.y, 0f, 0f));
            weaponSprite.SetPropertyBlock(_properties);
            _placed = true;
        }

        private void OnDisable()
        {
            Cancel();
            if (weaponSprite != null) weaponSprite.enabled = false;
        }
    }
}
