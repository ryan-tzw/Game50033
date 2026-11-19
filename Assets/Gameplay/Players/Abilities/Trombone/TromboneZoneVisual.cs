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
        private float _startedAt;
        private Player _owner;
        private float _startAngle;
        private float Clock => _owner != null ? _owner.Hitstop.ActionTime : Time.time;

        private void OnEnable()
        {
            if (tuning == null || boundary == null || sweep == null || tuning.presentation.effectMaterial == null)
            {
                Debug.LogError("TromboneZoneVisual requires tuning and its authored boundary/sweep.", this);
                enabled = false;
                return;
            }
            var zone = GetComponentInParent<TromboneCommandZone>();
            _owner = zone != null ? zone.PresentationOwner : null;
            _startedAt = Clock;
            var direction = _owner != null ? _owner.AimDirection : Vector3.forward;
            _startAngle = Mathf.Atan2(direction.x, direction.z);
            boundary.sharedMaterial = sweep.sharedMaterial = tuning.presentation.effectMaterial;
            Draw();
        }

        private void LateUpdate() => Draw();

        private void Draw()
        {
            var settings = tuning.presentation;
            var progress = Mathf.Clamp01((Clock - _startedAt) / Mathf.Max(0.01f, settings.zoneSweepDuration));
            var color = settings.gold;
            color.a *= settings.zoneOpacity * Mathf.Lerp(0.35f, 1f, progress);
            DrawArc(boundary, 1f, color, settings.zoneLineWidth);
            color = Color.Lerp(settings.gold, Color.white, 0.5f);
            color.a *= 1f - Mathf.SmoothStep(0.75f, 1f, progress);
            DrawArc(sweep, Mathf.SmoothStep(0f, 1f, progress), color, settings.zoneLineWidth * 1.5f);
            sweep.enabled = progress < 1f;
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
