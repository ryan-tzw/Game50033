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
    }
}
