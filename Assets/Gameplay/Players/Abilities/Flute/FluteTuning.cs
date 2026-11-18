namespace Game50033
{
    using System;
    using UnityEngine;

    [CreateAssetMenu(menuName = "Game50033/Tuning/Flute", fileName = "FluteTuning")]
    public sealed class FluteTuning : ScriptableObject
    {
        public PrimarySettings primary = new();
        public SecondarySettings secondary = new();
        public SpecialSettings special = new();
        public DefensiveSettings defensive = new();
        public FlowSettings flow = new();
        public VisualSettings visuals = new();
        public MelodySettings melody = new();

        [Serializable]
        public sealed class PrimarySettings
        {
            [Min(0.01f)] public float interval = 0.18f;
            [Min(0f)] public float minimumDamage = 0.55f;
            [Min(0f)] public float maximumDamage = 1.25f;
            [Min(0.01f)] public float speed = 11f;
            [Min(0f)] public float turnRate = 240f;
            [Min(0.01f)] public float lifetime = 2.5f;
            [Min(0.01f)] public float range = 18f;
            [Range(0f, 45f)] public float launchAngle = 16f;
            [Min(0f)] public float activeTime = 0.045f;
            [Min(0f)] public float recovery = 0.07f;
        }

        [Serializable]
        public sealed class SecondarySettings
        {
            [Min(0f)] public float cooldown = 0.5f;
            [Min(0f)] public float damage = 2f;
            [Min(0f)] public float resonanceFlowGain = 15f;
            [Min(0.01f)] public float speed = 26f;
            [Min(0.01f)] public float range = 14f;
            [Min(0.01f)] public float lifetime = 2f;
            [Range(1, 8)] public int pulseCount = 3;
            [Min(0.01f)] public float pulseSpacing = 0.12f;
            [Min(0f)] public float activeTime = 0.06f;
            [Min(0f)] public float recovery = 0.12f;
        }

        [Serializable]
        public sealed class SpecialSettings
        {
            [Min(0f)] public float cooldown = 2f;
            [Min(0.01f)] public float chargeDuration = 1.5f;
            [Min(0f)] public float minimumDamage = 1f;
            [Min(0f)] public float maximumDamage = 5f;
            [Min(0.01f)] public float minimumRevStrength = 0.5f;
            [Min(0.01f)] public float maximumRevStrength = 1.5f;
            [Min(0f)] public float flowCost = 25f;
            [Min(0.01f)] public float speed = 26f;
            [Min(0.01f)] public float range = 20f;
            [Min(0.01f)] public float lifetime = 2f;
            [Min(0f)] public float activeTime = 0.09f;
            [Min(0f)] public float recovery = 0.18f;
        }

        [Serializable]
        public sealed class DefensiveSettings
        {
            [Min(0f)] public float cooldown = 8f;
            [Min(0.01f)] public float invulnerabilityDuration = 0.32f;
            [Min(0f)] public float recovery = 0.1f;
            [Min(0f)] public float distance = 0.65f;
            [Min(0.01f)] public float movementDuration = 0.18f;
            public AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            [Min(0f)] public float successFlowGain = 15f;
            [Min(1f)] public float successSpeedMultiplier = 1.35f;
            [Min(0f)] public float successSpeedDuration = 1.5f;
        }

        [Serializable]
        public sealed class FlowSettings
        {
            [Min(0.01f)] public float maximum = 100f;
            [Min(0f)] public float initial;
            [Min(0f)] public float movementGain = 8f;
            [Min(0f)] public float stationaryLoss = 12f;
            [Min(0f)] public float turningLoss = 12f;
            [Range(1f, 180f)] public float abruptTurnAngle = 70f;
            [Min(0f)] public float abruptTurnLoss = 10f;
            [Min(0f)] public float maximumTurnRate = 120f;
            [Min(0.01f)] public float minimumMovementSpeed = 0.1f;
            [Min(0.1f)] public float teleportDistance = 3f;
        }

        [Serializable]
        public sealed class MelodySettings
        {
            public Sprite noteSprite;
            public HitEffect resonanceEffect;
            [Range(8, 24)] public int maximumNotes = 16;
            [Range(2, 8)] public int chargeNotes = 6;
            [Min(0f)] public float lowFlowRate = 1.2f;
            [Min(0f)] public float highFlowRate = 7f;
            [Min(0.01f)] public float noteLifetime = 1.2f;
            [Min(0.01f)] public float noteSize = 0.22f;
            [Min(0f)] public float riseDistance = 0.5f;
            [Min(0f)] public float driftDistance = 0.45f;
            [Min(0f)] public float curlRadius = 0.25f;
            [Min(0.01f)] public float emissionSmoothing = 6f;
            [Range(0, 4)] public int scatterCount = 3;
            [Range(0f, 1f)] public float goldThreshold = 0.7f;
            [Min(0.01f)] public float spiralRadius = 0.45f;
            [Min(0.01f)] public float primaryNoteSize = 0.3f;
            [Min(0.01f)] public float chargedNoteSize = 0.26f;
            [Min(0f)] public float primaryVisualOffset = 0.045f;
            [Min(0.001f)] public float primaryRibbonWidth = 0.055f;
            [Min(0.001f)] public float secondaryRibbonWidth = 0.045f;
            [Min(0.01f)] public float resonanceFlashDuration = 0.16f;
            [Tooltip("Shape only the visual gesture; ability timing stays authoritative.")]
            public AnimationCurve gestureCurve = new(
                new Keyframe(0f, 0f), new Keyframe(0.12f, -0.12f),
                new Keyframe(0.4f, 1f), new Keyframe(0.75f, -0.08f), new Keyframe(1f, 0f));
        }

        [Serializable]
        public sealed class VisualSettings
        {
            public Material material;
            public Material weaponMaterial;
            [Tooltip("Use a transparent sprite material on PixelWorldVFX; the weapon uses its own queue-2500 material.")]
            public Material noteMaterial;
            public HitEffect hitEffect;
            public HitEffect accentEffect;
            [Tooltip("Optional replacement for the simple flute placeholder built from sprites.")]
            public Sprite weaponSprite;
            public Color color = new(0.35f, 1f, 0.85f, 1f);
            public Color losingFlowColor = new(1f, 0.5f, 0.3f, 1f);
            public Color fullChargeColor = new(1f, 0.88f, 0.45f, 1f);
            public Vector3 weaponOffset = new(0.35f, 0.55f, 0f);
            public Vector2 weaponSize = new(0.64f, 0.08f);
            [Min(0.01f)] public float followSharpness = 24f;
            [Min(0f)] public float sway = 0.025f;
            [Min(0f)] public float swayFrequency = 2f;
            [Min(0f)] public float chargeLift = 0.12f;
            [Min(0f)] public float releaseRecoil = 0.15f;
            [Min(0.01f)] public float trailDuration = 0.1f;
            [Min(0.001f)] public float trailWidth = 0.09f;
            [Range(0f, 1f)] public float minimumTrailOpacity = 0.8f;
            [Range(0f, 1f)] public float projectileTint = 0.45f;
            [Min(0f)] public float primaryGestureAngle = 24f;
            [Min(0f)] public float primaryRecoil = 0.06f;
            [Min(1f)] public float chargedScale = 2.8f;
            [Min(0.01f)] public float flowRadius = 0.55f;
            [Min(0.01f)] public float chargeRadius = 0.34f;
            [Tooltip("Camera-right offset and world height above the player's feet; keep the ring outside the body.")]
            public Vector2 chargeIndicatorOffset = new(0.55f, 1.2f);
            [Min(0.01f)] public float minimumChargeRadius = 0.16f;
            [Min(0.001f)] public float chargeLineWidth = 0.055f;
            [Range(0f, 1f)] public float evasionEchoOpacity = 0.4f;
        }
    }
}
