namespace Game50033
{
    using UnityEngine;

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Player))]
    public sealed class GuitarWeaponVisual : FloatingWeaponVisual
    {
        [SerializeField] private Transform weaponPivot;
        [SerializeField] private SpriteRenderer weaponSprite;
        [Tooltip("Resting offset: camera-right shoulder offset, world height, then movement-facing offset.")]
        [SerializeField] private Vector3 gripOffset = new Vector3(0.04f, 0.18f, 0f);
        [Tooltip("Grip in sprite-local units before scaling. Put it outside the sprite bounds to move the pivot beyond the artwork.")]
        [SerializeField] private Vector2 spriteGripPoint = new Vector2(0.55f, 0.55f);
        [SerializeField, Min(0f)] private float shoulderLift = 0.65f;
        [SerializeField, Min(0.01f)] private float followSharpness = 22f;
        [SerializeField, Min(0.01f)] private float rotationSharpness = 28f;
        [SerializeField, Min(0.01f)] private float spriteScale = 0.7f;
        [SerializeField, Min(0f)] private float surfaceClearance = 0.02f;
        [SerializeField] private float spriteOutwardAngle = -135f;

        private Player _player;
        private Camera _camera;
        private bool _posing;
        private bool _active;
        private bool _placed;
        private Vector3 _direction = Vector3.forward;
        private float _angle;
        private float _height;
        private float _emphasis = 1f;

        private void Awake()
        {
            _player = GetComponent<Player>();
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
            if (_camera == null) _camera = Camera.main;

            var visible = _player.isActiveAndEnabled && _player.HasActiveInput;
            weaponSprite.enabled = visible;
            if (!visible)
            {
                _placed = false;
                return;
            }

            var facing = _player.Movement.FacingDirection;
            if (_camera == null) return;

            var handSide = Vector3.Dot(facing, _camera.transform.right) >= 0f ? 1f : -1f;
            var position = transform.position + Vector3.up * gripOffset.y
                + _camera.transform.right * (gripOffset.x * handSide) + facing * gripOffset.z;
            var radial = Quaternion.AngleAxis(_angle, Vector3.up) * _direction;
            if (_posing) position = transform.position + Vector3.up * _height;

            // the pivot stays near the player's grip; the sprite is offset entirely outside it.
            var blend = !_placed || _active ? 1f : 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            weaponPivot.position = Vector3.Lerp(weaponPivot.position, position, blend);
            var scale = spriteScale * (_posing ? _emphasis : 1f);
            weaponSprite.transform.localScale = Vector3.one * scale;
            weaponSprite.transform.localPosition = -(Vector3)spriteGripPoint * scale;
            weaponSprite.transform.localRotation = Quaternion.identity;

            // resting body points behind the facing direction and lifts over the shoulder.
            var screenDirection = _posing
                ? new Vector2(Vector3.Dot(radial, _camera.transform.right), Vector3.Dot(radial, _camera.transform.up))
                : new Vector2(-Vector3.Dot(facing, _camera.transform.right), -Vector3.Dot(facing, _camera.transform.up) + shoulderLift);
            var spriteAngle = Mathf.Atan2(screenDirection.y, screenDirection.x) * Mathf.Rad2Deg - spriteOutwardAngle;
            var rotation = _camera.transform.rotation * Quaternion.AngleAxis(spriteAngle, Vector3.forward);
            var rotationBlend = !_placed || _active ? 1f : 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
            weaponPivot.rotation = Quaternion.Slerp(weaponPivot.rotation, rotation, rotationBlend);
            _placed = true;

            // depth-only lift keeps a downward-pointing billboard above the floor without shifting it on screen.
            var belowSurface = transform.position.y + surfaceClearance - weaponSprite.bounds.min.y;
            var viewDown = -_camera.transform.forward.y;
            if (belowSurface > 0f && viewDown > 0.1f)
                weaponSprite.transform.position -= _camera.transform.forward * (belowSurface / viewDown);
        }

        private void OnDisable()
        {
            Cancel();
            if (weaponSprite != null) weaponSprite.enabled = false;
        }
    }
}
