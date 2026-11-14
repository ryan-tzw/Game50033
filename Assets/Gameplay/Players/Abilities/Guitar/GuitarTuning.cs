namespace Game50033
{
    using System;
    using UnityEngine;

    [CreateAssetMenu(menuName = "Game50033/Tuning/Guitar", fileName = "GuitarTuning")]
    public sealed class GuitarTuning : ScriptableObject
    {
        public AttackSettings attack = new();
        public SecondarySettings secondary = new();
        public WeaponSettings weapon = new();
        [InspectorName("VFX")] public VFXSettings vfx = new();

        [Serializable]
        public struct Swing
        {
            [Header("Timing")]
            [Min(0.01f)] public float windup;
            [Min(0.01f)] public float active;
            [Min(0.01f)] public float recovery;

            [Header("Shape and Motion")]
            [Range(1f, 300f)] public float angle;
            [Min(0f)] public float lungeDistance;
            [Min(0.01f)] public float reachMultiplier;

            [Header("Hitstop")]
            [Min(0f)] public float hitstopDuration;
            [Min(0f)] public float sweetSpotHitstopDuration;
        }

        [Serializable]
        public sealed class AttackSettings
        {
            [Header("Combo")]
            public Swing[] combo =
            {
                new Swing { windup = 0.14f, active = 0.18f, recovery = 0.16f, angle = 140f, lungeDistance = 0.22f, reachMultiplier = 1f, hitstopDuration = 0.04f, sweetSpotHitstopDuration = 0.06f },
                new Swing { windup = 0.12f, active = 0.18f, recovery = 0.18f, angle = 140f, lungeDistance = 0.22f, reachMultiplier = 1f, hitstopDuration = 0.05f, sweetSpotHitstopDuration = 0.075f },
                new Swing { windup = 0.22f, active = 0.22f, recovery = 0.28f, angle = 180f, lungeDistance = 0.3f, reachMultiplier = 1.4f, hitstopDuration = 0.06f, sweetSpotHitstopDuration = 0.09f },
            };
            [Min(0f)] public float comboCooldown = 0.4f;
            [Min(0f)] public float comboResetDelay = 0.8f;
            [Tooltip("Seconds after the active phase begins until the whole crescent hits and appears.")]
            [Min(0f)] public float impactDelay = 0.035f;

            [Header("Hit Area")]
            [Min(0.01f)] public float reach = 1.05f;
            [Min(0f)] public float innerRadius = 0.12f;
            [Min(0f)] public float sweetSpotRadius = 0.7f;
            [Tooltip("Fixed world XZ axis to stretch. (0, 1) follows world Z, the current arena's depth direction.")]
            public Vector2 stretchAxis = Vector2.up;
            [Tooltip("Scales the attack, inner hole and sweet-spot boundary along Stretch Axis. 1 keeps a circular sector.")]
            [Min(1f)] public float depthStretch = 1.5f;
            [Min(0.01f)] public float hitThickness = 0.08f;
            public float attackHeight = 0.25f;
            public LayerMask hitMask = ~0;

            [Header("Damage")]
            [Min(0f)] public float damage = 2f;
            [Min(1f)] public float sweetSpotMultiplier = 1.5f;

            [Header("Movement")]
            [Tooltip("Normal movement speed throughout Primary's windup, active and recovery phases. The forward lunge is added independently.")]
            [Range(0f, 1f)] public float movementSpeedMultiplier = 0.2f;
            public AnimationCurve lungeEasing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

            [Header("Animation")]
            public AnimationCurve windupEasing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            public AnimationCurve swingEasing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            public AnimationCurve recoveryEasing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            public float anticipationAngle = 22f;
            [Min(0.01f)] public float windupScale = 0.9f;
            [Min(0.01f)] public float swingScale = 1.05f;
        }

        [Serializable]
        public sealed class SecondarySettings
        {
            [Header("Timing")]
            [Min(0.001f)] public float windup = 0.2f;
            [Min(0.001f)] public float active = 0.12f;
            [Min(0.001f)] public float recovery = 0.08f;
            [Min(0f)] public float cooldown = 0.5f;

            [Header("Damage and Empowerment")]
            [Min(0f)] public float damage = 1f;
            [Min(1f)] public float primaryMultiplier = 1.5f;

            [Header("Wave Travel")]
            public GuitarStrum wavePrefab;
            [Min(0.01f)] public float travelDuration = 0.75f;
            [Tooltip("Distance travelled by the wave centre. The curved front extends beyond it.")]
            [Min(0.01f)] public float range = 6f;
            [Min(0f)] public float spawnOffset = 0.15f;
            [Tooltip("Normalized lifetime to normalized travel distance. Increasing values give forward travel.")]
            public AnimationCurve travelCurve = new(new Keyframe(0f, 0f, 2f, 2f), new Keyframe(1f, 1f, 0f, 0f));
            [Tooltip("Radius multiplier over normalized lifetime. Line thickness stays constant as the front expands.")]
            public AnimationCurve growthCurve = new(new Keyframe(0f, 1f, 1.6f, 1.6f), new Keyframe(1f, 1.8f, 0f, 0f));
            public AnimationCurve fadeCurve = new(new Keyframe(0f, 1f), new Keyframe(0.65f, 1f), new Keyframe(1f, 0f));

            [Header("Wave Shape and Collision")]
            [Min(0.01f)] public float waveRadius = 0.75f;
            [Min(0.01f)] public float waveWidth = 0.28f;
            [Range(1f, 180f)] public float waveAngle = 120f;
            [Range(0f, 1f)] public float waveTipTaper = 0.75f;
            [Min(0.01f)] public float hitThickness = 0.04f;
            public LayerMask collisionMask = ~0;

            [Header("Movement and Weapon Motion")]
            [Tooltip("Normal movement speed throughout Secondary's windup, active and recovery phases. Its backstep is added independently.")]
            [Range(0f, 1f)] public float movementSpeedMultiplier = 0.2f;
            [Tooltip("Retreat opposite the captured aim. Starts on release and lasts through the active phase.")]
            [Min(0f)] public float backstepDistance = 0.15f;
            public AnimationCurve backstepEasing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            [Range(1f, 300f)] public float swingAngle = 140f;
            public AnimationCurve swingEasing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        [Serializable]
        public sealed class WeaponSettings
        {
            [Header("Grip Placement")]
            [Tooltip("Resting offset: camera-right shoulder offset, world height, then movement-facing offset.")]
            public Vector3 gripOffset = new Vector3(0.04f, 0.18f, 0f);
            [InspectorName("Attack Grip Point")]
            [Tooltip("Attack grip in sprite-local units before scaling. Put it outside the sprite bounds for the swing pivot.")]
            public Vector2 spriteGripPoint = new Vector2(0f, 0.45f);
            [Tooltip("Grip in sprite-local units while resting. Position it inside the grey neck.")]
            public Vector2 restingGripPoint = new Vector2(0f, 0.1f);

            [Header("Resting Pose")]
            [Tooltip("Clockwise screen-space tilt from the backward direction at rest. Mirrored counterclockwise when facing left; straight up/down use clockwise.")]
            [Range(0f, 90f)] public float restingAngleOffset = 20f;

            [Header("Smoothing")]
            [Min(0.01f)] public float followSharpness = 22f;
            [Min(0.01f)] public float rotationSharpness = 28f;

            [Header("Sprite")]
            [Min(0.01f)] public float spriteScale = 1f;
            public float spriteOutwardAngle = -90f;
        }

        [Serializable]
        public sealed class VFXSettings
        {
            [Header("Primary Smear")]
            [Tooltip("Seconds per pose: X broad, Y follow-through, Z broken, W streaks. Independent of rendering FPS.")]
            public Vector4 frameDurations = new(0.045f, 0.045f, 0.055f, 0.065f);
            public Color smearColor = new(0.9f, 0.78f, 0.57f, 1f);
            public Color smearSweetSpotColor = new(1f, 0.94f, 0.72f, 1f);

            [Header("Strum")]
            public Color strumColor = new(0.95f, 0.82f, 0.55f, 1f);
            public Color strumEdgeColor = new(1f, 0.96f, 0.78f, 1f);
            public Color strumTailColor = new(0.68f, 0.45f, 0.21f, 1f);
            [Min(0f)] public float strumTailLength = 0.65f;
            [Range(0f, 1f)] public float strumTailOpacity = 0.4f;
            [Min(1f)] public float strumBreakupScale = 24f;
            [Min(0f)] public float strumFlowSpeed = 3f;
            [Range(1, 8)] public int strumFadeSteps = 4;

            [Header("Impact Effects")]
            public ParticleSystem impactPrefab;
            public Color normalHitColor = new(0.65f, 0.9f, 1f);
            public Color sweetSpotColor = new(1f, 0.8f, 0.25f);
            [Min(0.01f)] public float normalImpactSize = 0.06f;
            [Min(0.01f)] public float sweetSpotImpactSize = 0.1f;
        }
    }
}
