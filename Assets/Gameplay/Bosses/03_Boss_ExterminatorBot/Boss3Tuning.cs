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
    }
}
