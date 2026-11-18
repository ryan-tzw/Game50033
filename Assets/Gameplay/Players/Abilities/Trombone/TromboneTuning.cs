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
        [Range(0.01f, 1f)] public float specialReverberationStrengthMultiplier = 0.5f;
        [Tooltip("Impact sprite, damage number and sound played where a Trombone projectile hits. Special hits use its big style.")]
        public HitEffect hitEffect;

        [Serializable]
        public sealed class PrimarySettings
        {
            [Tooltip("Shot interval at zero Pressure, in seconds. Cast duration also limits firing cadence.")]
            [Min(0.01f)] public float cooldown = 0.5f;
            [Tooltip("Shot interval at maximum Pressure, in seconds. Use a lower value for faster firing; values above Cooldown are capped. Cast duration still applies.")]
            [Min(0.01f)] public float maximumPressureCooldown = 0.5f;
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
            [Min(0f)] public float gainPerAttack = 1f;
            [Min(0f)] public float decayPerSecond = 1f;
            [Min(0f)] public float consecutiveAttackInterval = 3f;
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
    }
}
