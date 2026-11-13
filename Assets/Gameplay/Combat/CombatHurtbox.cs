namespace Game50033
{
    using System.Collections.Generic;
    using UnityEngine;

    [DisallowMultipleComponent]
    public sealed class CombatHurtbox : MonoBehaviour
    {
        [SerializeField] private CombatHurtboxTuning tuning;
        [SerializeField] private Transform anchor;

        private static readonly List<CombatHurtbox> _active = new();
        public static IReadOnlyList<CombatHurtbox> Active => _active;
        public IHittable Target { get; private set; }

        private void OnEnable()
        {
            Target = GetComponent<IHittable>();
            if (tuning == null || Target == null)
            {
                Debug.LogError("CombatHurtbox requires tuning and an IHittable on the same object.", this);
                enabled = false;
                return;
            }
            // a world-space broadphase could discard exactly the projected overlaps we want to accept.
            if (!_active.Contains(this)) _active.Add(this);
        }

        private void OnDisable()
        {
            _active.Remove(this);
        }

        private void GetWorldCapsule(out Vector3 bottom, out Vector3 top, out float radius)
        {
            var basis = anchor != null ? anchor : transform;
            var scale = basis.lossyScale;
            // like a capsule collider, the wider perpendicular scale sets the round cross-section.
            radius = Mathf.Max(0.001f, tuning.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
            var halfLine = Mathf.Max(0f, tuning.height * Mathf.Abs(scale.y) * 0.5f - radius);
            var centre = basis.TransformPoint(tuning.center);
            bottom = centre - basis.up * halfLine;
            top = centre + basis.up * halfLine;
        }

        // queries geometry only; the caller owns hit masks, damage, timing and per-attack deduplication.
        public bool TryOverlapSector(Camera camera, Vector3 origin, Vector3 direction,
            float innerRadius, float outerRadius, float angle, Vector3 stretchAxis, float stretch,
            out Vector3 contact)
        {
            contact = default;
            if (!isActiveAndEnabled || tuning == null || camera == null || !camera.orthographic
                || outerRadius <= innerRadius || Mathf.Abs(camera.transform.forward.y) < 0.0001f) return false;

            // under orthographic projection a 3d capsule becomes its projected axis swept by a circle.
            // the round ends keep their radius; only the axis foreshortens with the viewing angle.
            GetWorldCapsule(out var worldBottom, out var worldTop, out var radius);
            var right = camera.transform.right;
            var up = camera.transform.up;
            var relativeBottom = worldBottom - origin;
            var relativeTop = worldTop - origin;
            var bottom = new Vector2(Vector3.Dot(relativeBottom, right), Vector3.Dot(relativeBottom, up));
            var top = new Vector2(Vector3.Dot(relativeTop, right), Vector3.Dot(relativeTop, up));
            var centre = (bottom + top) * 0.5f;
            if (centre.magnitude > outerRadius * Mathf.Max(1f, stretch) + Vector2.Distance(bottom, top) * 0.5f + radius) return false;

            // use the initial crescent's four-degree tessellation, including its hole and stretched edges.
            var segments = Mathf.Max(2, Mathf.CeilToInt(angle / 4f));
            var previousInner = Vector2.zero;
            var previousOuter = Vector2.zero;
            for (var i = 0; i <= segments; i++)
            {
                var radial = Quaternion.AngleAxis(Mathf.Lerp(-angle * 0.5f, angle * 0.5f, (float)i / segments), Vector3.up) * direction;
                radial += stretchAxis * (Vector3.Dot(radial, stretchAxis) * (stretch - 1f));
                var projected = new Vector2(Vector3.Dot(radial, right), Vector3.Dot(radial, up));
                var inside = projected * innerRadius;
                var outside = projected * outerRadius;
                if (i > 0 && (OverlapsTriangle(bottom, top, radius, previousInner, inside, previousOuter, out var point)
                    || OverlapsTriangle(bottom, top, radius, previousOuter, inside, outside, out point)))
                {
                    // map the accepted screen-plane point back onto the attack plane for impact feedback.
                    contact = origin + right * point.x + up * point.y;
                    contact -= camera.transform.forward * ((contact.y - origin.y) / camera.transform.forward.y);
                    return true;
                }
                previousInner = inside;
                previousOuter = outside;
            }
            return false;
        }

        private static bool OverlapsTriangle(Vector2 bottom, Vector2 top, float radius,
            Vector2 a, Vector2 b, Vector2 c, out Vector2 contact)
        {
            contact = default;
            if (Mathf.Abs(Cross(b - a, c - a)) < 0.00000001f) return false;
            if (InsideTriangle(bottom, a, b, c)) { contact = bottom; return true; }
            if (InsideTriangle(top, a, b, c)) { contact = top; return true; }
            if (SegmentDistanceSquared(a, b, bottom, top, out contact) <= radius * radius) return true;
            if (SegmentDistanceSquared(b, c, bottom, top, out contact) <= radius * radius) return true;
            return SegmentDistanceSquared(c, a, bottom, top, out contact) <= radius * radius;
        }

        private static bool InsideTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            var ab = Cross(b - a, point - a);
            var bc = Cross(c - b, point - b);
            var ca = Cross(a - c, point - c);
            return ab >= 0f && bc >= 0f && ca >= 0f || ab <= 0f && bc <= 0f && ca <= 0f;
        }

        private static float SegmentDistanceSquared(Vector2 a, Vector2 b, Vector2 bottom, Vector2 top, out Vector2 contact)
        {
            var edge = b - a;
            var axis = top - bottom;
            var denominator = Cross(edge, axis);
            if (Mathf.Abs(denominator) > 0.00000001f)
            {
                var t = Cross(bottom - a, axis) / denominator;
                var u = Cross(bottom - a, edge) / denominator;
                if (t >= 0f && t <= 1f && u >= 0f && u <= 1f)
                {
                    contact = a + edge * t;
                    return 0f;
                }
            }

            // disjoint segments are closest at an endpoint of at least one segment.
            contact = a;
            var distance = (a - ClosestPoint(bottom, top, a)).sqrMagnitude;
            var candidate = (b - ClosestPoint(bottom, top, b)).sqrMagnitude;
            if (candidate < distance) { distance = candidate; contact = b; }
            var point = ClosestPoint(a, b, bottom);
            candidate = (point - bottom).sqrMagnitude;
            if (candidate < distance) { distance = candidate; contact = point; }
            point = ClosestPoint(a, b, top);
            candidate = (point - top).sqrMagnitude;
            if (candidate < distance) { distance = candidate; contact = point; }
            return distance;
        }

        private static Vector2 ClosestPoint(Vector2 a, Vector2 b, Vector2 point)
        {
            var edge = b - a;
            return a + edge * (edge.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(point - a, edge) / edge.sqrMagnitude) : 0f);
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private void OnDrawGizmos()
        {
            if (tuning == null || !isActiveAndEnabled) return;
            GetWorldCapsule(out var bottom, out var top, out var radius);
            var previousColor = Gizmos.color;
            if (Camera.current != null && Camera.current.cameraType == CameraType.Game)
            {
                if (!tuning.showGameViewPreview) return;
                var camera = Camera.main;
                if (camera == null || !camera.orthographic) return;
                var forward = camera.transform.forward;
                // move the projection close to the camera so artwork cannot obscure its debug outline.
                // movement along view rays preserves screen positions in our orthographic camera.
                var plane = camera.transform.position + forward * (camera.nearClipPlane + 0.01f);
                bottom -= forward * Vector3.Dot(bottom - plane, forward);
                top -= forward * Vector3.Dot(top - plane, forward);
                var up = (top - bottom).sqrMagnitude > 0.00000001f ? (top - bottom).normalized : camera.transform.up;
                var right = Vector3.Cross(up, forward).normalized;
                Gizmos.color = Color.cyan;
                DrawCapsuleOutline(bottom, top, radius, right, up);
            }
            else
            {
                var basis = anchor != null ? anchor : transform;
                Gizmos.color = Color.yellow;
                DrawCapsuleOutline(bottom, top, radius, basis.right, basis.up);
                DrawCapsuleOutline(bottom, top, radius, basis.forward, basis.up);
                for (var i = 0; i < 32; i++)
                {
                    var a = i * Mathf.PI * 2f / 32;
                    var b = (i + 1) * Mathf.PI * 2f / 32;
                    var from = (basis.right * Mathf.Cos(a) + basis.forward * Mathf.Sin(a)) * radius;
                    var to = (basis.right * Mathf.Cos(b) + basis.forward * Mathf.Sin(b)) * radius;
                    Gizmos.DrawLine(bottom + from, bottom + to);
                    Gizmos.DrawLine(top + from, top + to);
                }
            }
            Gizmos.color = previousColor;
        }

        private static void DrawCapsuleOutline(Vector3 bottom, Vector3 top, float radius, Vector3 right, Vector3 up)
        {
            var previous = bottom + right * radius;
            for (var cap = 0; cap < 2; cap++)
            for (var i = 0; i <= 16; i++)
            {
                var angle = (cap * 16 + i) * Mathf.PI / 16;
                var point = (cap == 0 ? top : bottom) + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;
                Gizmos.DrawLine(previous, point);
                previous = point;
            }
        }
    }
}
