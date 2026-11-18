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

        internal static bool OverlapsSweep(Reverberation field, Vector3 start, Vector3 end, float radius, Camera camera)
        {
            GetViewAxes(camera, out var right, out var up);
            var a = Project(start - field.GroundOrigin, right, up);
            var b = Project(end - field.GroundOrigin, right, up);
            var reach = field.FootprintRadius + radius;
            if (PointSegmentDistanceSquared(Vector2.zero, a, b) > reach * reach) return false;

            var edges = field.FootprintEdges;
            if (edges.Length < 4) return false;
            var inner = Project(edges[0], right, up);
            var outer = Project(edges[1], right, up);
            var radiusSquared = radius * radius;
            for (var i = 2; i < edges.Length; i += 2)
            {
                var nextInner = Project(edges[i], right, up);
                var nextOuter = Project(edges[i + 1], right, up);
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
            var a = Project(tip - field.GroundOrigin, viewRight, up);
            var b = Project(left - field.GroundOrigin, viewRight, up);
            var c = Project(right - field.GroundOrigin, viewRight, up);
            if (!CapsuleOverlapsTriangle(Vector2.zero, Vector2.zero, field.FootprintRadius * field.FootprintRadius, a, b, c))
                return false;

            var edges = field.FootprintEdges;
            if (edges.Length < 4) return false;
            var inner = Project(edges[0], viewRight, up);
            var outer = Project(edges[1], viewRight, up);
            for (var i = 2; i < edges.Length; i += 2)
            {
                var nextInner = Project(edges[i], viewRight, up);
                var nextOuter = Project(edges[i + 1], viewRight, up);
                // check whether the tip is inside the strip or an edge touches the attack triangle.
                if (InsideTriangle(a, inner, outer, nextOuter)
                    || (inner != nextInner && InsideTriangle(a, inner, nextOuter, nextInner))
                    || CapsuleOverlapsTriangle(inner, outer, 0f, a, b, c)
                    || CapsuleOverlapsTriangle(outer, nextOuter, 0f, a, b, c)
                    || CapsuleOverlapsTriangle(inner, nextInner, 0f, a, b, c)
                    || (i == edges.Length - 2 && CapsuleOverlapsTriangle(nextInner, nextOuter, 0f, a, b, c)))
                    return true;
                inner = nextInner;
                outer = nextOuter;
            }
            return false;
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
