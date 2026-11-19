namespace Game50033
{
    using System;
    using UnityEngine;

    [CreateAssetMenu(menuName = "Game50033/Tuning/Trombone", fileName = "TromboneTuning")]
    public sealed class TromboneTuning : ScriptableObject
    {
        [Min(0.01f)] public float actionDuration = 0.25f;
        public PrimarySettings primary = new();
        public AttackSettings special = new()
        {
            cooldown = 3f, minimumDamage = 2f, maximumDamage = 10f,
            projectile = new ProjectileSettings { speed = 16f, radius = 0.08f }
        };
        public CommandZoneSettings commandZone = new();
        public PressureSettings pressure = new();
        public DefensiveSettings defensive = new();
        public PlaceholderSettings placeholder = new();
        public PresentationSettings presentation = new();
        public PressureVisualSettings pressureVisuals = new();
        [Range(0.01f, 1f)] public float specialReverberationStrengthMultiplier = 0.5f;
        [Tooltip("Impact sprite, damage number and sound played where a Trombone projectile hits. Special hits use its big style.")]
        public HitEffect hitEffect;

        [Serializable]
        public sealed class PrimarySettings
        {
            [Tooltip("Shot interval at zero Pressure, in seconds. Cast duration also limits firing cadence.")]
            [Min(0.01f)] public float cooldown = 0.5f;
            [Tooltip("Shot interval at maximum Pressure, in seconds. Use a lower value for faster firing; values above Cooldown are capped. Cast duration still applies.")]
            [Min(0.01f)] public float maximumPressureCooldown = 0.3f;
            [Min(0f)] public float minimumDamage = 1f;
            [Min(0f)] public float maximumDamage = 5f;
            public ProjectileSettings projectile = new();
        }

        [Serializable]
        public sealed class AttackSettings
        {
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
            [Min(0.01f)] public float cooldown = 1f;
            [Min(0.01f)] public float radius = 1f;
            [Min(1f)] public float pressureAccumulationMultiplier = 1.5f;
            [Range(0f, 1f)] public float pressureDecayMultiplier = 0.5f;
        }

        [Serializable]
        public sealed class PressureSettings
        {
            [Min(0.01f)] public float maximum = 100f;
            [Tooltip("Pressure gained per second while planted. Command Zone multiplies this and the shot bonus.")]
            [Min(0f)] public float stationaryGainPerSecond = 8f;
            [Min(0f)] public float gainPerAttack = 1f;
            [Tooltip("Pressure lost per second while moving. Command Zone reduces this rate.")]
            [Min(0f)] public float decayPerSecond = 4f;
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
            [Min(0.001f)] public float trackWidth = 0.018f;
            [Min(0.001f)] public float fillWidth = 0.03f;
            [Min(0.01f)] public float response = 24f;
            public Color trackColor = new(0.4f, 0.32f, 0.2f, 0.3f);
            public Color losingColor = new(1f, 0.5f, 0.3f, 1f);
            public Color fullColor = new(1f, 0.88f, 0.45f, 1f);
            [Range(0f, 1f)] public float fullColorThreshold = 0.7f;

            [Header("Rising music notes")]
            public Sprite noteSprite;
            [Min(0f)] public float lowNoteRate = 1.2f;
            [Min(0f)] public float highNoteRate = 5f;
            [Min(0.01f)] public float noteLifetime = 1.2f;
            [Min(0.01f)] public float noteSize = 0.18f;
            [Min(0f)] public float spawnRadius = 0.25f;
            [Min(0f)] public float noteHeight = 0.15f;
            [Min(0f)] public float riseDistance = 0.5f;
            [Min(0f)] public float driftDistance = 0.12f;
            [Min(0.01f)] public float emissionResponse = 6f;
        }

        [Serializable]
        public sealed class PresentationSettings
        {
            [Header("Artwork and idle aim")]
            public Material weaponMaterial;
            public Material effectMaterial;
            [Min(0.01f)] public float weaponWidth = 0.95f;
            public Vector3 idleOffset = new(0.2f, 0.36f, 0.06f);
            [Tooltip("Bell position relative to the sprite centre at Weapon Width, in world units.")]
            public Vector2 muzzleOffset = new(0.125f, 0.055f);
            [Min(0.01f)] public float poseResponse = 24f;
            [Min(0.001f)] public float cueWidth = 0.018f;
            public Color gold = new(1f, 0.72f, 0.18f, 1f);
            public Color resonatingColor = new(0.55f, 0.95f, 1f, 1f);

            [Header("Primary recoil")]
            [Min(0.01f)] public float primaryRecovery = 0.22f;
            [Min(0f)] public float primaryRecoil = 0.1f;
            [Min(0f)] public float primaryKick = 5f;
            [Min(0.01f)] public float flashDuration = 0.065f;
            [Min(0.01f)] public float flashRadius = 0.075f;

            [Header("Command Zone sweep")]
            [Min(0.01f)] public float zoneSweepDuration = 0.42f;
            [Min(0f)] public float zoneWeaponReach = 0.4f;
            [Min(0.001f)] public float zoneLineWidth = 0.035f;
            [Range(0f, 1f)] public float zoneOpacity = 0.65f;
            [Min(0f)] public float zoneHeight = 0.035f;

            [Header("Guard brace and actual block feedback")]
            public Vector3 guardOffset = new(0.16f, 0.37f, 0.08f);
            [Range(0f, 90f)] public float guardDownAngle = 68f;
            [Min(0.01f)] public float guardRaiseTime = 0.08f;
            [Min(0.01f)] public float blockDuration = 0.16f;
            [Min(0f)] public float blockKick = 0.045f;
            [Min(0.01f)] public float guardCueRadius = 0.16f;

            [Header("Special release (no added gameplay windup)")]
            [Min(0.01f)] public float specialRecovery = 0.5f;
            [Min(0f)] public float specialRecoil = 0.22f;
            [Min(0f)] public float specialKick = 10f;
            [Min(1f)] public float specialFlashScale = 1.6f;

            [Header("Travelling shot visuals")]
            [Min(0.001f)] public float primaryShotWidth = 0.045f;
            [Min(0.001f)] public float specialShotWidth = 0.065f;
            [Min(0.01f)] public float primaryShotLength = 0.18f;
            [Min(0.01f)] public float specialShotLength = 0.45f;
            [Min(0.01f)] public float primaryTrailTime = 0.055f;
            [Min(0.01f)] public float specialTrailTime = 0.1f;
        }
    }
}
