namespace Game50033
{
    using UnityEngine;
    using UnityEngine.Rendering;

    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class PlayerFacingIndicator : MonoBehaviour
    {
        [SerializeField] private PlayerFacingIndicatorTuning tuning;
        [SerializeField] private Material material;

        private Player _player;
        private MeshRenderer[] _renderers;
        private Mesh[] _meshes;
        private MaterialPropertyBlock _properties;
        private Vector4 _geometry;
        private float _movementOpacity;
        private float _aimOpacity;
        private Vector2 _radii;
        private readonly float[] _angles = new float[2];
        private readonly Vector3[][] _vertices = new Vector3[2][];
        private readonly int[][] _triangles = new int[2][];
        private float _aimRadius;
        private float _movementRadius;
        private bool _wasVisible;
        private PlayerCharacterSelect _selector;
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            _player = GetComponent<Player>();
            _selector = GetComponent<PlayerCharacterSelect>();
            _properties = new MaterialPropertyBlock();
            _renderers = new MeshRenderer[2];
            _meshes = new Mesh[2];
            for (var i = 0; i < 2; i++)
            {
                var indicator = new GameObject(i == 0 ? "Movement direction" : "Aim direction");
                indicator.layer = LayerMask.NameToLayer("PixelWorldVFX");
                indicator.transform.SetParent(transform, false);
                var filter = indicator.AddComponent<MeshFilter>();
                _meshes[i] = new Mesh { name = indicator.name };
                filter.sharedMesh = _meshes[i];
                var renderer = indicator.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                _renderers[i] = renderer;
            }
            if (tuning == null || material == null)
            {
                Debug.LogError("PlayerFacingIndicator needs tuning and a material.", this);
                enabled = false;
                return;
            }
            _movementOpacity = tuning.stationaryOpacity;
            _aimOpacity = tuning.autoAimOpacity;
            _aimRadius = tuning.radius;
            _movementRadius = tuning.radius;
        }

        private void LateUpdate()
        {
            var visible = _player.isActiveAndEnabled && _player.HasActiveInput && !_player.Health.IsDowned
                && (_selector == null || _selector.State != PlayerCharacterSelect.SelectState.Selecting);
            var blend = tuning.transitionDuration > 0f ? 1f - Mathf.Exp(-Time.deltaTime * 5f / tuning.transitionDuration) : 1f;
            var moving = _player.Movement.MoveDirection.sqrMagnitude > 0.0001f;
            var targetMovementRadius = Mathf.Max(0.01f, moving ? tuning.movingRadius : tuning.radius);
            _movementRadius = visible && _wasVisible
                ? Mathf.Lerp(_movementRadius, targetMovementRadius, blend) : targetMovementRadius;
            var targetRadius = Mathf.Max(0.01f, _player.IsManualAiming ? tuning.manualAimRadius : tuning.radius);
            _aimRadius = visible && _wasVisible ? Mathf.Lerp(_aimRadius, targetRadius, blend) : targetRadius;
            var geometry = new Vector4(tuning.movementAngle, tuning.movementLength, tuning.aimAngle, tuning.aimLength);
            var radii = new Vector2(_movementRadius, _aimRadius);
            if (_geometry != geometry || _radii != radii)
            {
                _geometry = geometry;
                _radii = radii;
                // a fan from the tip to a sampled circular base makes a concave arc facing the player.
                for (var i = 0; i < 2; i++)
                {
                    const int segments = 16;
                    var angle = i == 0 ? tuning.movementAngle : tuning.aimAngle;
                    var length = i == 0 ? tuning.movementLength : tuning.aimLength;
                    // reuse buffers while the aim radius changes so the curved base follows its current circle.
                    var vertices = _vertices[i] ??= new Vector3[segments + 2];
                    var triangles = _triangles[i] ??= new int[segments * 3];
                    var radius = i == 0 ? radii.x : radii.y;
                    vertices[0] = new Vector3(0f, 0f, length);
                    for (var j = 0; j <= segments; j++)
                    {
                        var radians = Mathf.Lerp(-angle * 0.5f, angle * 0.5f, (float)j / segments) * Mathf.Deg2Rad;
                        vertices[j + 1] = new Vector3(Mathf.Sin(radians) * radius, 0f, (Mathf.Cos(radians) - 1f) * radius);
                        if (j == segments) continue;
                        triangles[j * 3] = 0;
                        triangles[j * 3 + 1] = j + 1;
                        triangles[j * 3 + 2] = j + 2;
                    }
                    _meshes[i].Clear();
                    _meshes[i].vertices = vertices;
                    _meshes[i].triangles = triangles;
                    _meshes[i].RecalculateBounds();
                }
            }

            _movementOpacity = Mathf.Lerp(_movementOpacity, moving ? tuning.movementOpacity : tuning.stationaryOpacity, blend);
            var targetAimOpacity = _player.IsManualAiming ? tuning.manualAimOpacity : tuning.autoAimOpacity;
            _aimOpacity = visible && _wasVisible ? Mathf.Lerp(_aimOpacity, targetAimOpacity, blend) : targetAimOpacity;
            var playerColor = _player.Character != null ? _player.Character.Tint : _player.PlayerColor;
            for (var i = 0; i < 2; i++)
            {
                var renderer = _renderers[i];
                renderer.enabled = visible;
                if (!visible) continue;
                var direction = i == 0 ? _player.Movement.FacingDirection : _player.AimDirection;
                direction.y = 0f;
                if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
                var targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                _angles[i] = _wasVisible ? Mathf.LerpAngle(_angles[i], targetAngle, blend) : targetAngle;
                var rotation = Quaternion.Euler(0f, _angles[i], 0f);
                var radius = i == 0 ? radii.x : radii.y;
                var position = transform.position + rotation * Vector3.forward * radius;
                position.y = transform.position.y + tuning.groundHeight + i * 0.001f;
                renderer.transform.SetPositionAndRotation(position, rotation);
                renderer.transform.localScale = Vector3.one;
                var color = i == 0 ? tuning.movementColor : tuning.aimColor;
                var multiplyWithPlayerColor = i == 0 ? tuning.movementMultiplyWithPlayerColor : tuning.aimMultiplyWithPlayerColor;
                if (multiplyWithPlayerColor) color *= playerColor;
                color.a = i == 0 ? _movementOpacity : _aimOpacity;
                _properties.SetColor(ColorId, color);
                renderer.SetPropertyBlock(_properties);
            }
            _wasVisible = visible;
        }

        private void OnDisable()
        {
            _wasVisible = false;
            if (_renderers == null) return;
            foreach (var renderer in _renderers)
                if (renderer != null) renderer.enabled = false;
        }

        private void OnDestroy()
        {
            if (_meshes == null) return;
            foreach (var mesh in _meshes)
                if (mesh != null) Destroy(mesh);
        }
    }
}
