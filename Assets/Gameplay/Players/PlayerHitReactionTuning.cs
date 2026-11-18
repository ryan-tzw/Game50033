namespace Game50033
{
    using UnityEngine;

    [CreateAssetMenu(menuName = "Game50033/Tuning/Player Hit Reaction", fileName = "PlayerHitReactionTuning")]
    public sealed class PlayerHitReactionTuning : ScriptableObject
    {
        [Tooltip("Seconds before movement, dodge and abilities become available again.")]
        [Min(0f)] public float hitstunDuration = 0.25f;

        [Header("White Flash")]
        [Min(0f)] public float flashDuration = 0.2f;
        public AnimationCurve flashDecay = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [Header("Post-hit Immunity Pulse")]
        [Tooltip("White pulses per second after the initial hit flash. Set to zero to disable.")]
        [Min(0f)] public float immunityPulseRate = 2.5f;
        [Tooltip("Peak white blend of each pulse.")]
        [Range(0f, 1f)] public float immunityPulseIntensity = 0.55f;

        [Header("Sprite Shake")]
        [Min(0f)] public float shakeDuration = 0.15f;
        [Tooltip("Peak offset in world units.")]
        [Min(0f)] public float shakeAmplitude = 0.05f;
        [Tooltip("Oscillation cycles across the shake duration.")]
        [Min(1)] public int shakeCycles = 3;
        public AnimationCurve shakeDecay = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    }
}
