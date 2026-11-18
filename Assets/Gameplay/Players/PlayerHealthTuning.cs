namespace Game50033
{
    using UnityEngine;

    [CreateAssetMenu(menuName = "Game50033/Tuning/Player Health", fileName = "PlayerHealthTuning")]
    public sealed class PlayerHealthTuning : ScriptableObject
    {
        [Min(1)] public int maxHealth = 5;
        [Tooltip("Seconds of invulnerability after an accepted non-lethal hit.")]
        [Min(0f)] public float postHitInvulnerabilityDuration = 2f;
        [Tooltip("Seconds before a downed player automatically revives.")]
        [Min(0f)] public float downedDuration = 15f;
        [Min(1)] public int reviveHealth = 1;
        [Tooltip("Seconds of invulnerability after revival.")]
        [Min(0f)] public float revivalImmunityDuration = 1f;
    }
}
