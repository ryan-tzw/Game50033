namespace Game50033
{
    using UnityEngine;

    [CreateAssetMenu(menuName = "Game50033/Tuning/Boss 3", fileName = "Boss3Tuning")]
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

        [Header("Lightning Flashes")]
        public Vector2 lightningFlashDuration = new(0.025f, 0.055f);
        [Min(0.05f)] public float lightningFlashInterval = 0.095f;
        [Min(0f)] public float beamSideFlashReach = 0.75f;

        [Header("Homing Spark")]
        [Min(1)] public int sparkDamage = 1;
        [Min(0.01f)] public float sparkRadius = 0.2f;
        [Min(0.01f)] public float sparkSpeed = 5f;
        [Min(0f)] public float sparkTurnRate = 105f;
        [Min(0.01f)] public float sparkLifetime = 3f;
        [Min(0.01f)] public float sparkTelegraphDuration = 0.6f;
        [Min(0f)] public float sparkRecoveryDuration = 0.55f;

        [Header("Homing Spark Trail")]
        [Min(0.01f)] public float sparkTrailDuration = 0.30f;
        [Min(0.01f)] public float sparkTrailWidth = 0.68f;
        [Range(0f, 1f)] public float sparkTrailIntensity = 0.8f;

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
        [Min(0.05f)] public float spiralTelegraphDuration = 1.35f;
        [Min(0.05f)] public float spiralBoltWarningLead = 0.4f;
        [Min(0f)] public float spiralRecoveryDuration = 0.65f;
        [Min(1)] public int spiralLandingDamage = 1;
        [Min(0.1f)] public float spiralLandingRadius = 1.4f;

        [Header("Spiral Surge Jump")]
        [Min(0f)] public float spiralJumpHeight = 0.82f;
        [Min(0f)] public float spiralTakeoffDelay = 0.15f;
        [Min(0.01f)] public float spiralRiseDuration = 0.05f;
        [Min(0f)] public float spiralApexHoldDuration = 0.65f;
        [Min(0.01f)] public float spiralDropDuration = 0.05f;

        [Header("Spiral Surge VFX")]
        [Min(0.01f)] public float spiralLandingBurstDuration = 0.30f;
        [Min(0f)] public float spiralLandingFlashIntensity = 4.5f;
        [Min(0.1f)] public float spiralLandingSparkReach = 1.45f;
        public Vector2 spiralWarningGlow = new(1.7f, 3.4f);
        [Min(0f)] public float spiralWarningPulseHz = 3.6f;
        [Min(0.01f)] public float spiralBoltLength = 0.74f;
        [Min(0.01f)] public float spiralBoltWidth = 0.12f;
        [Min(0.01f)] public float spiralImpactDuration = 0.2f;
        [Min(0.01f)] public float spiralImpactReach = 0.58f;
        [Min(0.01f)] public float spiralTrailDuration = 0.25f;
        [Min(0.01f)] public float spiralTrailWidth = 0.56f;

        [Header("Electric Knives")]
        [Range(1, 8)] public int knifeThrowCount = 6;
        [Min(1)] public int knifeThrowDamage = 1;
        [Min(1)] public int knifeBlastDamage = 1;
        [Min(0.01f)] public float knifeLaneWidth = 0.55f;
        [Min(0.01f)] public float knifeBlastRadius = 0.62f;
        [Min(0.01f)] public float knifeSpeed = 14f;
        [Min(0.01f)] public float knifeThrowRadius = 4.25f;
        [Min(0.01f)] public float knifeTelegraphDuration = 0.6f;
        [Min(0f)] public float knifeRecoveryDuration = 0.55f;
        [Min(0.01f)] public float knifeImpactDuration = 0.22f;
        [Min(0.01f)] public float knifeAnchorLifetime = 12f;
        [Range(1, 24)] public int knifeMaxActive = 12;
    }
}
