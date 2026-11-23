namespace Game50033
{
    using UnityEngine;

    // Lives under the zone's visualRoot, so the gameplay lifecycle owns its visibility.
    [DisallowMultipleComponent]
    public sealed class TromboneZoneVisual : MonoBehaviour
    {
        [SerializeField] private TromboneTuning tuning;
        [SerializeField] private LineRenderer boundary;
        [SerializeField] private LineRenderer sweep;
        private readonly LineRenderer[] _markers = new LineRenderer[4];
        private float _startedAt;
        private Player _owner;
        private float _startAngle;
        private float Clock => _owner.Hitstop.ActionTime;

        private void Awake()
        {
            for (var i = 0; i < _markers.Length; i++)
            {
                var marker = new GameObject("Command Marker " + i).AddComponent<LineRenderer>();
                marker.transform.SetParent(transform, false);
                marker.gameObject.layer = boundary.gameObject.layer;
                marker.sharedMaterial = tuning.presentation.effectMaterial;
                marker.sortingLayerID = boundary.sortingLayerID;
                marker.sortingOrder = boundary.sortingOrder;
                marker.alignment = boundary.alignment;
                marker.useWorldSpace = false;
                marker.positionCount = 3;
                marker.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                marker.receiveShadows = false;
                _markers[i] = marker;
            }
        }

        private void OnEnable()
        {
            var zone = GetComponentInParent<TromboneCommandZone>();
            _owner = zone.PresentationOwner;
            if (_owner == null) return;
            _startedAt = Clock;
            var direction = _owner.AimDirection;
            _startAngle = Mathf.Atan2(direction.x, direction.z);
            boundary.sharedMaterial = sweep.sharedMaterial = tuning.presentation.effectMaterial;
            Draw();
        }

        private void LateUpdate()
        {
            if (_owner == null) return;
            Draw();
        }

        private void Draw()
        {
            var settings = tuning.presentation;
            var age = Clock - _startedAt;
            var progress = Mathf.Clamp01(age / settings.zoneSweepDuration);
            var deploy = 1f - Mathf.Clamp01(age / settings.zoneDeployDuration);
            var stamp = deploy * deploy;
            var color = Color.Lerp(settings.gold, Color.white, stamp);
            color.a *= settings.zoneOpacity;
            var width = Mathf.Lerp(1f, settings.zoneDeployWidthMultiplier, stamp);
            DrawArc(boundary, 1f, color, settings.zoneLineWidth * width);
            DrawMarkers(color, stamp);

            color = Color.Lerp(settings.gold, Color.white, 0.5f + stamp * 0.5f);
            color.a *= 1f - Mathf.SmoothStep(0.75f, 1f, progress);
            DrawArc(sweep, Mathf.SmoothStep(0f, 1f, progress), color, settings.zoneLineWidth * 1.5f * width);
            sweep.enabled = progress < 1f;
        }

        private void DrawMarkers(Color color, float stamp)
        {
            var settings = tuning.presentation;
            var reach = settings.zoneMarkerReach * (1f + stamp);
            for (var i = 0; i < _markers.Length; i++)
            {
                var angle = _startAngle + (i + 0.5f) * Mathf.PI * 0.5f;
                var radial = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                var tangent = new Vector3(radial.z, 0f, -radial.x);
                var tip = radial * tuning.commandZone.radius + Vector3.up * settings.zoneHeight;
                var heel = tip - radial * reach;
                var marker = _markers[i];
                marker.widthMultiplier = settings.zoneMarkerWidth * (1f + stamp);
                marker.startColor = marker.endColor = color;
                marker.SetPosition(0, heel + tangent * (reach * 0.5f));
                marker.SetPosition(1, tip);
                marker.SetPosition(2, heel - tangent * (reach * 0.5f));
            }
        }

        private void DrawArc(LineRenderer line, float fraction, Color color, float width)
        {
            const int segments = 64;
            line.useWorldSpace = false;
            line.positionCount = segments + 1;
            line.widthMultiplier = width;
            line.startColor = line.endColor = color;
            for (var i = 0; i <= segments; i++)
            {
                var angle = _startAngle + fraction * i / segments * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Sin(angle) * tuning.commandZone.radius,
                    tuning.presentation.zoneHeight, Mathf.Cos(angle) * tuning.commandZone.radius));
            }
        }
    }
}
