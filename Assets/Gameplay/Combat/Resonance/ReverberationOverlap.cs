namespace Game50033
{
    using Game50033.Examples;
    using UnityEngine;

    internal static class ReverberationOverlap
    {
        internal static Vector3[] BuildEdges(ReverberationFieldShape shape, out float reach, out Vector3 aimOffset)
        {
            var outer = Mathf.Max(0.01f, shape.outerRadius);
            var sector = shape.kind == ReverberationFieldShapeKind.AnnularSector;
            var inner = sector ? Mathf.Clamp(shape.innerRadius, 0f, outer) : 0f;
            var angle = sector ? Mathf.Clamp(shape.sweepAngle, 0f, 360f) : 360f;
            var direction = shape.direction.sqrMagnitude > 0.0001f ? shape.direction.normalized : Vector2.up;
            var axis = shape.stretchAxis.sqrMagnitude > 0.0001f ? shape.stretchAxis.normalized : Vector2.up;
            var stretch = Mathf.Max(1f, shape.stretchFactor);
            reach = outer * stretch;
            aimOffset = Vector3.zero;
            if (angle <= 0f || inner >= outer) return System.Array.Empty<Vector3>();

            // tessellate once at spawn; pairing inner and outer edges keeps the hole and reflex arcs intact.
            var segments = Mathf.Clamp(Mathf.CeilToInt(angle / 4f), 1, 90);
            var edges = new Vector3[2 * (segments + 1)];
            for (var i = 0; i <= segments; i++)
            {
                var radians = Mathf.Lerp(-angle * 0.5f, angle * 0.5f, (float)i / segments) * Mathf.Deg2Rad;
                var radial = direction * Mathf.Cos(radians) + new Vector2(direction.y, -direction.x) * Mathf.Sin(radians);
                radial += axis * (Vector2.Dot(radial, axis) * (stretch - 1f));
                var offset = new Vector3(radial.x, 0f, radial.y);
                edges[2 * i] = offset * inner;
                edges[2 * i + 1] = offset * outer;
            }
            if (sector && (inner > 0f || angle < 360f))
            {
                var middle = direction + axis * (Vector2.Dot(direction, axis) * (stretch - 1f));
                aimOffset = new Vector3(middle.x, 0f, middle.y) * ((inner + outer) * 0.5f);
            }
            return edges;
        }

        internal static bool OverlapsGroundCircle(Reverberation field, Vector3 centre, float radius)
        {
            // both footprints are on the ground, so their overlap is unchanged by the camera projection.
            return OverlapsSweep(field, centre, centre, radius, null);
        }

        internal static Vector3 RevealAimOffset(Reverberation field)
        {
            var strip = new FootprintStrip(field);
            if (strip.SegmentCount == 0) return Vector3.zero;
            strip.GetEdge(strip.RevealedSegments * 0.5f, out var inner, out var outer);
            return (inner + outer) * 0.5f;
        }

        internal static bool OverlapsSweep(Reverberation field, Vector3 start, Vector3 end, float radius, Camera camera)
        {
            GetViewAxes(camera, out var right, out var up);
            // test in the full-size footprint's coordinates so spawning never rebuilds the cached edges.
            var inverseScale = 1f / field.FootprintScale;
            var a = Project(start - field.GroundOrigin, right, up) * inverseScale;
            var b = Project(end - field.GroundOrigin, right, up) * inverseScale;
            radius *= inverseScale;
            var reach = field.FootprintRadius + radius;
            if (PointSegmentDistanceSquared(Vector2.zero, a, b) > reach * reach) return false;

            var strip = new FootprintStrip(field);
            var count = strip.SegmentCount;
            if (count == 0) return false;
            strip.GetEdge(0, out var firstInner, out var firstOuter);
            var inner = Project(firstInner, right, up);
            var outer = Project(firstOuter, right, up);
            var radiusSquared = radius * radius;
            for (var i = 1; i <= count; i++)
            {
                strip.GetEdge(i, out var edgeInner, out var edgeOuter);
                var nextInner = Project(edgeInner, right, up);
                var nextOuter = Project(edgeOuter, right, up);
                if (CapsuleOverlapsTriangle(a, b, radiusSquared, inner, outer, nextOuter)
                    || (inner != nextInner && CapsuleOverlapsTriangle(a, b, radiusSquared, inner, nextOuter, nextInner)))
                    return true;
                inner = nextInner;
                outer = nextOuter;
            }
            return false;
        }

        internal static bool OverlapsTriangle(Reverberation field, Vector3 tip, Vector3 left, Vector3 right, Camera camera)
        {
            GetViewAxes(camera, out var viewRight, out var up);
            var inverseScale = 1f / field.FootprintScale;
            var a = Project(tip - field.GroundOrigin, viewRight, up) * inverseScale;
            var b = Project(left - field.GroundOrigin, viewRight, up) * inverseScale;
            var c = Project(right - field.GroundOrigin, viewRight, up) * inverseScale;
            if (!CapsuleOverlapsTriangle(Vector2.zero, Vector2.zero, field.FootprintRadius * field.FootprintRadius, a, b, c))
                return false;

            var strip = new FootprintStrip(field);
            var count = strip.SegmentCount;
            if (count == 0) return false;
            strip.GetEdge(0, out var firstInner, out var firstOuter);
            var inner = Project(firstInner, viewRight, up);
            var outer = Project(firstOuter, viewRight, up);
            for (var i = 1; i <= count; i++)
            {
                strip.GetEdge(i, out var edgeInner, out var edgeOuter);
                var nextInner = Project(edgeInner, viewRight, up);
                var nextOuter = Project(edgeOuter, viewRight, up);
                // check whether the tip is inside the strip or an edge touches the attack triangle.
                if (InsideTriangle(a, inner, outer, nextOuter)
                    || (inner != nextInner && InsideTriangle(a, inner, nextOuter, nextInner))
                    || CapsuleOverlapsTriangle(inner, outer, 0f, a, b, c)
                    || CapsuleOverlapsTriangle(outer, nextOuter, 0f, a, b, c)
                    || CapsuleOverlapsTriangle(inner, nextInner, 0f, a, b, c)
                    || (i == count && CapsuleOverlapsTriangle(nextInner, nextOuter, 0f, a, b, c)))
                    return true;
                inner = nextInner;
                outer = nextOuter;
            }
            return false;
        }

        internal readonly struct FootprintStrip
        {
            private readonly Vector3[] _edges;
            private readonly int _first;
            private readonly int _step;
            public readonly float RevealedSegments;
            public int SegmentCount => Mathf.CeilToInt(RevealedSegments);

            public FootprintStrip(Reverberation field)
            {
                _edges = field.FootprintEdges;
                var reverse = field.ReverseFootprintReveal;
                _first = reverse ? _edges.Length - 2 : 0;
                _step = reverse ? -2 : 2;
                RevealedSegments = Mathf.Max(0, _edges.Length / 2 - 1) * field.FootprintReveal;
            }

            public void GetEdge(float index, out Vector3 inner, out Vector3 outer)
            {
                // clip the leading segment against the reveal without rebuilding the cached footprint.
                index = Mathf.Min(index, RevealedSegments);
                var segment = Mathf.FloorToInt(index);
                var offset = _first + _step * segment;
                inner = _edges[offset];
                outer = _edges[offset + 1];
                var fraction = index - segment;
                if (fraction <= 0f) return;
                inner = Vector3.LerpUnclamped(inner, _edges[offset + _step], fraction);
                outer = Vector3.LerpUnclamped(outer, _edges[offset + _step + 1], fraction);
            }
        }

        private static void GetViewAxes(Camera camera, out Vector3 right, out Vector3 up)
        {
            var useCamera = camera != null && camera.orthographic && Mathf.Abs(camera.transform.forward.y) > 0.0001f;
            right = useCamera ? camera.transform.right : Vector3.right;
            up = useCamera ? camera.transform.up : Vector3.forward;
        }

        private static Vector2 Project(Vector3 point, Vector3 right, Vector3 up)
        {
            return new Vector2(Vector3.Dot(point, right), Vector3.Dot(point, up));
        }

        private static bool CapsuleOverlapsTriangle(Vector2 start, Vector2 end, float radiusSquared,
            Vector2 a, Vector2 b, Vector2 c)
        {
            return InsideTriangle(start, a, b, c) || InsideTriangle(end, a, b, c)
                || TouchesSegment(start, end, a, b, radiusSquared)
                || TouchesSegment(start, end, b, c, radiusSquared)
                || TouchesSegment(start, end, c, a, radiusSquared);
        }

        private static bool InsideTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            if (Cross(b - a, c - a) == 0f) return false;
            var ab = Cross(b - a, point - a);
            var bc = Cross(c - b, point - b);
            var ca = Cross(a - c, point - c);
            return (ab >= 0f && bc >= 0f && ca >= 0f) || (ab <= 0f && bc <= 0f && ca <= 0f);
        }

        private static bool TouchesSegment(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float radiusSquared)
        {
            var first = b - a;
            var second = d - c;
            var determinant = Cross(first, second);
            if (determinant != 0f)
            {
                var offset = c - a;
                var t = Cross(offset, second) / determinant;
                var u = Cross(offset, first) / determinant;
                if (t >= 0f && t <= 1f && u >= 0f && u <= 1f) return true;
            }

            return PointSegmentDistanceSquared(a, c, d) <= radiusSquared
                || PointSegmentDistanceSquared(b, c, d) <= radiusSquared
                || PointSegmentDistanceSquared(c, a, b) <= radiusSquared
                || PointSegmentDistanceSquared(d, a, b) <= radiusSquared;
        }

        private static float PointSegmentDistanceSquared(Vector2 point, Vector2 a, Vector2 b)
        {
            var edge = b - a;
            var lengthSquared = edge.sqrMagnitude;
            var t = lengthSquared > 0f ? Mathf.Clamp01(Vector2.Dot(point - a, edge) / lengthSquared) : 0f;
            return (point - (a + t * edge)).sqrMagnitude;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
