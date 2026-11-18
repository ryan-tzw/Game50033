namespace Game50033
{
    using UnityEngine;

    /// <summary>
    /// One boss's Discordance numbers (GDD: Discordance Management). The meter itself runs in the
    /// encounter's EncounterManager, which only raises events; what each level does is boss code.
    /// </summary>
    [CreateAssetMenu(menuName = "Game50033/Tuning/Discordance", fileName = "Discordance")]
    public sealed class DiscordanceTuning : ScriptableObject
    {
        [Tooltip("Top of the meter. It starts every encounter at 0 and is clamped between 0 and this.")]
        [Min(1f)] public float max = 100f;

        [Tooltip("Gain per second for each boss phase: element 0 is phase 1. Later phases use the last entry.")]
        public float[] ratePerPhase = { 1.2f, 1.8f };

        [Tooltip("Gain for each heart a player loses to the boss.")]
        [Min(0f)] public float gainPerHit = 4f;

        [Tooltip("Reduction for each successful Resonance, for a full team (see Reference Players).")]
        [Min(0f)] public float reductionPerResonance = 5f;

        [Tooltip("Team size the reduction is balanced for. Each Resonance removes reduction x (this / players at fight start), so smaller teams get more per Resonance.")]
        [Min(1)] public int referencePlayers = 4;

        [Tooltip("Meter values where each level starts, ascending: element 0 starts level 1. Below the first, the level is 0.")]
        public float[] thresholds = { 33f, 66f, 100f };

        public float RateForPhase(int phase)
        {
            if (ratePerPhase == null || ratePerPhase.Length == 0) return 0f;
            return ratePerPhase[Mathf.Clamp(phase - 1, 0, ratePerPhase.Length - 1)];
        }

        public int LevelFor(float value)
        {
            var level = 0;
            if (thresholds == null) return level;
            for (var i = 0; i < thresholds.Length; i++)
                if (value >= thresholds[i]) level = i + 1;
            return level;
        }
    }
}
