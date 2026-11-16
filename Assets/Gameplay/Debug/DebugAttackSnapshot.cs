namespace Game50033
{
    using UnityEngine;
    using UnityEngine.SceneManagement;

    public readonly struct DebugAttackSnapshot
    {
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
            float angle, Vector3 stretchAxis, float stretch, float sweetSpotRadius = -1f, bool sweetSpotInside = false)
        {
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
    }
}
