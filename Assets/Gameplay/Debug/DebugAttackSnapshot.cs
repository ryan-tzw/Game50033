namespace Game50033
{
    using UnityEngine;
    using UnityEngine.SceneManagement;

    public readonly struct DebugAttackSnapshot
    {
        internal enum Geometry { Sector, Capsule, Triangle }

        internal readonly Geometry Shape;
        internal readonly Vector3 Extrusion;
        internal readonly int LiveFrame;
        public readonly Vector3 Origin;
        public readonly Vector3 Direction;
        public readonly Vector3 StretchAxis;
        public readonly float InnerRadius;
        public readonly float OuterRadius;
        public readonly float Angle;
        public readonly float Stretch;
        public readonly float SweetSpotRadius;
        public readonly bool SweetSpotInside;
        public readonly float RecordedAt;
        public readonly Scene Scene;

        public bool HasSweetSpot => SweetSpotRadius >= 0f;

        public DebugAttackSnapshot(Vector3 origin, Vector3 direction, float innerRadius, float outerRadius,
            float angle, Vector3 stretchAxis, float stretch, float sweetSpotRadius = -1f, bool sweetSpotInside = false,
            bool live = false)
        {
            Shape = Geometry.Sector;
            Extrusion = Vector3.zero;
            LiveFrame = live ? Time.frameCount : -1;
            Origin = origin;
            Direction = direction;
            InnerRadius = innerRadius;
            OuterRadius = outerRadius;
            Angle = angle;
            StretchAxis = stretchAxis;
            Stretch = stretch;
            SweetSpotRadius = sweetSpotRadius;
            SweetSpotInside = sweetSpotInside;
            RecordedAt = Time.time;
            Scene = SceneManager.GetActiveScene();
        }

        private DebugAttackSnapshot(Geometry shape, Vector3 first, Vector3 second, Vector3 third,
            float radius, Vector3 extrusion, bool live)
            : this(first, second, 0f, radius, 0f, third, 1f, live: live)
        {
            Shape = shape;
            Extrusion = extrusion;
        }

        internal static DebugAttackSnapshot Capsule(Vector3 bottom, Vector3 top, float radius, bool live = false) =>
            new(Geometry.Capsule, bottom, top, default, radius, default, live);

        internal static DebugAttackSnapshot Triangle(Vector3 tip, Vector3 left, Vector3 right, Vector3 extrusion) =>
            new(Geometry.Triangle, tip, left, right, 0f, extrusion, false);
    }
}
