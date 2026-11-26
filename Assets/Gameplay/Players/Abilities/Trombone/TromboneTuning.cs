namespace Game50033
{
    using System;
    using UnityEngine;

    [CreateAssetMenu(menuName = "Game50033/Tuning/Trombone", fileName = "TromboneTuning")]
    public sealed class TromboneTuning : ScriptableObject
    {

        [Min(0.01f)] public float actionDuration = 0.25f;
        [Header("Primary")]
        public PrimarySettings primary = new();

        [Header("Special")]
        public AttackSettings special = new()
        {
            cooldown = 6f,
            minimumDamage = 24f,
            maximumDamage = 80f,
            projectile = new ProjectileSettings { speed = 16f, radius = 0.08f }
        };
        [Range(0.01f, 1f)] public float specialReverberationStrengthMultiplier = 0.5f;

        [Header("Secondary — Command Zone")]
        public CommandZoneSettings commandZone = new();
        [Header("Pressure")]
        public PressureSettings pressure = new();
        [Header("Defensive — Guard")]
        public DefensiveSettings defensive = new();

        [Header("Hit feedback")]
        public HitEffect hitEffect;

        [Header("Presentation")]
        public PlaceholderSettings placeholder = new();
        public PresentationSettings presentation = new();
        public PressureVisualSettings pressureVisuals = new();

        [Serializable]
        public sealed class PrimarySettings
        {
            [Tooltip("Shot interval at zero Pressure, in seconds")]
            [Min(0.01f)] public float cooldown = 2f;
            [Tooltip("Shot interval at maximum Pressure, in seconds. Use a lower value for faster firing. Cast duration still applies.")]
            [Min(0.01f)] public float maximumPressureCooldown = 1f;

            [Min(0f)] public float minimumDamage = 8f;
            [Min(0f)] public float maximumDamage = 40f;
            public ProjectileSettings projectile = new();
        }

        [Serializable]
        public sealed class AttackSettings
        {

            [Min(0.01f)] public float windup = 0.45f;
            [Tooltip("Movement, dodge and other abilities remain blocked after release for this long.")]
            [Min(0.01f)] public float recovery = 1.15f;

            [Min(0.01f)] public float cooldown;
            [Min(0f)] public float minimumDamage;
            [Min(0f)] public float maximumDamage;
            public ProjectileSettings projectile = new();
        }

        [Serializable]
        public sealed class ProjectileSettings
        {
            [Min(0.01f)] public float speed = 8f;
            [Min(0.01f)] public float maximumDistance = 16f;
            [Min(0.01f)] public float lifetime = 3f;
            public LayerMask collisionMask = ~0;
            [Min(0.01f)] public float radius = 0.1f;
        }

        [Serializable]
        public sealed class CommandZoneSettings
        {
            [Tooltip("Cooldown starts when the Command Zone ends.")]
            [Min(0.01f)] public float cooldown = 10f;
            [Min(0.01f)] public float radius = 1f;
            [Min(1f)] public float pressureAccumulationMultiplier = 1.5f;
            [Range(0f, 1f)] public float pressureDecayMultiplier = 0.5f;
        }

        [Serializable]
        public sealed class PressureSettings
        {
            [Min(0.01f)] public float maximum = 100f;
            [Tooltip("Pressure gained per second while planted. Zero means Pressure comes only from shots. Command Zone multiplies this and the shot gain.")]
            [Min(0f)] public float stationaryGainPerSecond = 0f;
            [Tooltip("Pressure gained per accepted Primary shot, Command Zone multiplies it. Special does not add or spend Pressure.")]
            [Min(0f)] public float gainPerAttack = 15f;
            [Tooltip("Slow Pressure lost per second while planted. Command Zone reduces this rate.")]
            [Min(0f)] public float idleDecayPerSecond = 3f;
            [Tooltip("Faster Pressure lost per second while moving. Command Zone reduces this rate.")]
            [Min(0f)] public float decayPerSecond = 8f;
            [Min(0.01f)] public float minimumMovementSpeed = 0.1f;
            [Tooltip("Larger XZ jumps are treated as teleports and do not gain or drain Pressure.")]
            [Min(0.1f)] public float teleportDistance = 3f;
        }

        [Serializable]
        public sealed class PlaceholderSettings
        {
            public Material material;
            public Vector3 offset = new(0.3f, 0.3f, 0f);
            public Vector2 size = new(0.5f, 0.1f);
            public Color color = new(1f, 0.75f, 0.2f, 1f);
        }

        [Serializable]
        public sealed class DefensiveSettings
        {
            [Min(0.01f)] public float cooldown = 2f;
            [Min(0.01f)] public float guardLifespan = 5f;
            [Range(0f, 1f)] public float movementSpeedMultiplier = 0.5f;
        }

        [Serializable]
        public sealed class PressureVisualSettings
        {
            [Header("Pressure ring")]
            [Min(0.01f)] public float radius = 0.55f;
            [Min(0f)] public float height = 0.055f;
            [Min(0.001f)] public float trackWidth = 0.014f;
            [Min(0.001f)] public float fillWidth = 0.022f;
            [Min(0.01f)] public float response = 5f;
            public Color trackColor = new(0.75f, 0.6f, 0.32f, 0.22f);
            public Color losingColor = new(1f, 0.5f, 0.3f, 1f);
            public Color fullColor = new(1f, 0.88f, 0.45f, 1f);
            [Range(0f, 1f)] public float fullColorThreshold = 0.7f;

            [Header("Rising music notes")]
            public Sprite noteSprite;
            [Min(0f)] public float lowNoteRate = 0.35f;
            [Min(0f)] public float highNoteRate = 1.6f;
            [Min(0.01f)] public float noteLifetime = 2.6f;
            [Min(0.01f)] public float noteSize = 0.14f;
            [Min(0f)] public float spawnRadius = 0.55f;
            [Min(0f)] public float noteHeight = 0.1f;
            [Min(0f)] public float riseDistance = 0.2f;
            [Min(0f)] public float driftDistance = 0.12f;
            [Min(0.01f)] public float emissionResponse = 1.5f;
        }

        [Serializable]
        public sealed class PresentationSettings
        {
            [Header("Artwork and idle aim")]
            public Material weaponMaterial;
            public Material effectMaterial;
            [Min(0.01f)] public float weaponWidth = 0.95f;
            public Vector3 idleOffset = new(0.2f, 0.36f, 0.06f);
            public Vector2 gripPoint = Vector2.zero;
            [Range(0f, 60f)] public float restingTilt = 0f;
            [Tooltip("Bell position relative to the sprite centre at Weapon Width, in world units.")]
            public Vector2 muzzleOffset = new(0.125f, 0.055f);
            [Min(0.01f)] public float poseResponse = 24f;
            [Min(0.001f)] public float cueWidth = 0.018f;
            public Color gold = new(1f, 0.72f, 0.18f, 1f);
            public Color resonatingColor = new(0.55f, 0.95f, 1f, 1f);

            [Header("Pressure build-up on the weapon")]

            [Tooltip("Overall strength of the glow, sparks and tremble that follow Pressure.")]
            [Range(0f, 2f)] public float pressureGlow = 1f;

            [Header("Shot anticipation and weight")]

            [Tooltip("Time to settle into the ready pose")]
            [Min(0.01f)] public float anticipationTime = 1.1f;
            [Min(0f)] public float anticipationPullback = 0.34f;
            [Min(0f)] public float anticipationLift = 0.16f;
            [Range(0f, 75f)] public float anticipationTilt = 55f;

            [Tooltip("Weapon compression during preparation")]
            [Range(0f, 0.4f)] public float shotSquash = 0.12f;

            [Tooltip("Strain shake and heave as the weapon is loaded, in world units.")]
            [Min(0f)] public float anticipationTremble = 0.025f;

            [Tooltip("Squash applied at the peak of the post-shot recoil, kept separate so the wind-up compression is unchanged.")]
            [Range(0f, 0.2f)] public float recoilSquash = 0.03f;

            [Header("Primary recoil")]
            [Min(0.01f)] public float primaryRecovery = 1.5f;

            [Min(0.01f)] public float primaryRecoilBuild = 0.18f;
            [Min(0f)] public float primaryRecoil = 0.18f;
            [Min(0f)] public float primaryKick = 8f;
            [Min(0.01f)] public float flashDuration = 0.11f;
            [Min(0.01f)] public float flashRadius = 0.12f;

            [Header("Command Zone sweep")]
            [Min(0.01f)] public float zoneSweepDuration = 1.5f;
            [Min(0f)] public float zoneWeaponReach = 0.4f;
            [Min(0.001f)] public float zoneLineWidth = 0.035f;
            [Range(0f, 1f)] public float zoneOpacity = 0.65f;
            [Min(0f)] public float zoneHeight = 0.035f;

            [Header("Command Zone deployment")]

            [Tooltip("Duration of the opening weapon shudder and boundary stamp, on the player's action clock.")]
            [Min(0.01f)] public float zoneDeployDuration = 0.32f;

            [Tooltip("Weapon-only shake distance during deployment.")]
            [Min(0f)] public float zoneShakeDistance = 0.045f;
            [Min(0f)] public float zoneShakeFrequency = 9f;
            [Min(1f)] public float zoneDeployWidthMultiplier = 2.6f;

            [Tooltip("Length of the four directional marks, extending inward from the zone boundary.")]
            [Min(0f)] public float zoneMarkerReach = 0.18f;
            [Min(0f)] public float zoneMarkerWidth = 0.045f;

            [Header("Guard brace and actual block feedback")]
            public Vector3 guardOffset = new(0.16f, 0.37f, 0.08f);
            [Range(0f, 90f)] public float guardDownAngle = 68f;
            [InspectorName("Guard Slam Time")]
            [Min(0.01f)] public float guardRaiseTime = 0.08f;
            [Min(0.01f)] public float blockDuration = 0.16f;
            [Min(0f)] public float blockKick = 0.045f;
            [Min(0.01f)] public float guardCueRadius = 0.16f;
            [Tooltip("Visual-only lift and cock-back before the slam.")]
            [Min(0f)] public float guardWindupTime = 0.07f;

            [Tooltip("Scales the ground-slam shockwave")]
            [Range(0.25f, 2f)] public float guardImpactScale = 1f;

            [Tooltip("Furthest reach of the ground shockwave")]
            [Min(0f)] public float guardWaveRadius = 0.9f;

            [Tooltip("Seconds for the shockwave to travel out and fade")]
            [Min(0.05f)] public float guardShockwaveDuration = 0.45f;
            [Min(0.001f)] public float guardWaveWidth = 0.05f;

            [Header("Special release (gameplay owns windup and recovery)")]
            [Min(0.01f)] public float specialRecovery = 1.6f;

            [Tooltip("Time for the recoil to build to its peak.")]
            [Min(0.01f)] public float specialRecoilBuild = 0.45f;
            [Min(0f)] public float specialRecoil = 0.5f;
            [Min(0f)] public float specialKick = 16f;
            [Min(1f)] public float specialFlashScale = 2.6f;

            [Header("Travelling shot visuals")]
            [Min(0.001f)] public float primaryShotWidth = 0.045f;
            [Min(0.001f)] public float specialShotWidth = 0.095f;
            [Min(0.01f)] public float primaryShotLength = 0.18f;
            [Min(0.01f)] public float specialShotLength = 0.65f;
            [Min(0.01f)] public float primaryTrailTime = 0.055f;
            [Min(0.01f)] public float specialTrailTime = 0.12f;
        }
    }
}
