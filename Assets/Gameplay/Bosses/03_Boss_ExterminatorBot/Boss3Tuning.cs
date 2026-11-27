namespace Game50033
{
    using UnityEngine;

    [CreateAssetMenu(menuName = "Game50033/Tuning/Boss 3", fileName = "ExterminatorBot")]
    public sealed class Boss3Tuning : ScriptableObject
    {
        [Header("Encounter")]
        [Min(1f)] public float maxHealth = 30000f;
        [Min(0f)] public float phaseTwoHealth = 20000f;

        [Header("Bouncing Arc")]
        [Min(1)] public int beamDamage = 1;
        [Min(0.01f)] public float beamWidth = 0.36f;
        [Min(1f)] public float beamSpeed = 200f;
        [Range(0, 12)] public int maxRicochets = 3;
        [Min(0.01f)] public float beamLifetime = 0.33f;
        [Min(0.01f)] public float telegraphDuration = 0.8f;
        [Min(0f)] public float dissipationDuration = 0.16f;
        [Min(0f)] public float recoveryDuration = 0.5f;

        [Header("Homing Spark")]
        [Min(1)] public int sparkDamage = 1;
        [Min(0.01f)] public float sparkRadius = 0.2f;
        [Min(0.01f)] public float sparkSpeed = 5f;
        [Min(0f)] public float sparkTurnRate = 105f;
        [Min(0.01f)] public float sparkLifetime = 3f;
        [Min(0.01f)] public float sparkTelegraphDuration = 0.6f;
        [Min(0f)] public float sparkRecoveryDuration = 0.55f;

        [Header("Homing Spark End Effects")]
        [Min(0.01f)] public float sparkImpactDuration = 0.22f;
        [Min(0.1f)] public float sparkImpactSizeMultiplier = 2.4f;
        [Min(0.01f)] public float sparkFizzleDuration = 0.28f;

        [Header("Spiral Surge")]
        [Range(1, 48)] public int spiralShotCount = 14;
        [Min(1)] public int spiralDamage = 1;
        [Min(0.01f)] public float spiralRadius = 0.16f;
        [Min(0.01f)] public float spiralSpeed = 5.75f;
        [Min(0.05f)] public float spiralLifetime = 3f;
        [Min(0.05f)] public float spiralShotInterval = 0.14f;
        [Range(1f, 180f)] public float spiralDegreesPerShot = 32f;
        [Min(0.05f)] public float spiralTelegraphDuration = 1f;
        [Min(0f)] public float spiralRecoveryDuration = 0.65f;

        [Header("Spiral Surge VFX")]
        [Min(0.01f)] public float spiralBoltLength = 0.74f;
        [Min(0.01f)] public float spiralBoltWidth = 0.12f;
        [Min(0f)] public float spiralBoltBend = 0.14f;
        [Min(1f)] public float spiralBoltFlickerRate = 24f;
        [Min(0.01f)] public float spiralImpactDuration = 0.2f;
        [Min(0.01f)] public float spiralImpactReach = 0.58f;
    }
}
