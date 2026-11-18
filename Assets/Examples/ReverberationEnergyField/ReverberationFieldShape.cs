namespace Game50033.Examples
{
    using System;
    using UnityEngine;

    public enum ReverberationFieldShapeKind { LegacyEllipse, Circle, AnnularSector }

    // a geometry snapshot in field-root local XZ units, independent of shared appearance tuning.
    [Serializable]
    public struct ReverberationFieldShape
    {
        public ReverberationFieldShapeKind kind;
        public Vector2 origin;
        public Vector2 direction;
        [Min(0f)] public float innerRadius;
        [Min(0.01f)] public float outerRadius;
        [Range(0f, 360f)] public float sweepAngle;
        public Vector2 stretchAxis;
        [Min(1f)] public float stretchFactor;
    }
}
