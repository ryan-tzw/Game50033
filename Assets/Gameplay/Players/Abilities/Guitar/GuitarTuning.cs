namespace Game50033
{
    using System;
    using UnityEngine;

    [CreateAssetMenu(menuName = "Game50033/Tuning/Guitar", fileName = "GuitarTuning")]
    public sealed class GuitarTuning : ScriptableObject
    {
        public AttackSettings attack = new();
        public WeaponSettings weapon = new();
        [InspectorName("VFX")] public VFXSettings vfx = new();

        [Serializable]
        public struct Swing
        {
            [Min(0.01f)] public float windup;
            [Min(0.01f)] public float active;
            [Min(0.01f)] public float recovery;
            [Range(1f, 300f)] public float angle;
            [Min(0f)] public float lungeDistance;
            [Min(0.01f)] public float reachMultiplier;
        }

        [Serializable]
        public sealed class AttackSettings
        {
            public Swing[] combo =
            {
                new Swing { windup = 0.14f, active = 0.18f, recovery = 0.16f, angle = 140f, lungeDistance = 0.22f, reachMultiplier = 1f },
                new Swing { windup = 0.12f, active = 0.18f, recovery = 0.18f, angle = 140f, lungeDistance = 0.22f, reachMultiplier = 1f },
                new Swing { windup = 0.22f, active = 0.22f, recovery = 0.28f, angle = 180f, lungeDistance = 0.3f, reachMultiplier = 1.4f },
            };
            [Min(0f)] public float comboCooldown = 0.4f;
            [Min(0f)] public float comboResetDelay = 0.8f;
            [Tooltip("Seconds after the active phase begins until the whole crescent hits and appears.")]
            [Min(0f)] public float impactDelay = 0.035f;
            [Min(0.01f)] public float reach = 1.05f;
            [Tooltip("Fixed world XZ axis to stretch. (0, 1) follows world Z, the current arena's depth direction.")]
            public Vector2 stretchAxis = Vector2.up;
            [Tooltip("Scales the attack, inner hole and sweet-spot boundary along Stretch Axis. 1 keeps a circular sector.")]
            [Min(1f)] public float depthStretch = 1.5f;
            [Min(0f)] public float innerRadius = 0.12f;
            [Min(0f)] public float sweetSpotRadius = 0.7f;
            [Min(0.01f)] public float hitThickness = 0.08f;
            public float attackHeight = 0.25f;
            [Min(0f)] public float damage = 2f;
            [Min(1f)] public float sweetSpotMultiplier = 1.5f;
            public LayerMask hitMask = ~0;
            public AnimationCurve swingEasing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            public AnimationCurve lungeEasing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            public AnimationCurve windupEasing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            public AnimationCurve recoveryEasing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            public float anticipationAngle = 22f;
            [Min(0.01f)] public float windupScale = 0.9f;
            [Min(0.01f)] public float swingScale = 1.05f;
        }

        [Serializable]
        public sealed class WeaponSettings
        {
            [Tooltip("Resting offset: camera-right shoulder offset, world height, then movement-facing offset.")]
            public Vector3 gripOffset = new Vector3(0.04f, 0.18f, 0f);
            [Tooltip("Grip in sprite-local units before scaling. Put it outside the sprite bounds to move the pivot beyond the artwork.")]
            public Vector2 spriteGripPoint = new Vector2(0f, 0.45f);
            [Min(0f)] public float shoulderLift = 0.65f;
            [Min(0.01f)] public float followSharpness = 22f;
            [Min(0.01f)] public float rotationSharpness = 28f;
            [Min(0.01f)] public float spriteScale = 1f;
            public float spriteOutwardAngle = -90f;
        }

        [Serializable]
        public sealed class VFXSettings
        {
            [Tooltip("Seconds per pose: X broad, Y follow-through, Z broken, W streaks. Independent of rendering FPS.")]
            public Vector4 frameDurations = new(0.045f, 0.045f, 0.055f, 0.065f);
            public Color smearColor = new(0.9f, 0.78f, 0.57f, 1f);
            public Color smearSweetSpotColor = new(1f, 0.94f, 0.72f, 1f);
            public ParticleSystem impactPrefab;
            public Color normalHitColor = new(0.65f, 0.9f, 1f);
            public Color sweetSpotColor = new(1f, 0.8f, 0.25f);
            [Min(0.01f)] public float normalImpactSize = 0.06f;
            [Min(0.01f)] public float sweetSpotImpactSize = 0.1f;
        }
    }
}
