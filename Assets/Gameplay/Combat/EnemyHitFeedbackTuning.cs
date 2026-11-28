namespace Game50033
{
    using UnityEngine;

    [CreateAssetMenu(menuName = "Game50033/Tuning/Enemy Hit Feedback")]
    public sealed class EnemyHitFeedbackTuning : ScriptableObject
    {
        [Min(0.01f)] public float flashDuration = 0.15f;
        [Min(0f)] public float shakeDistance = 0.1f;
        [Min(0f)] public float shakeDuration = 0.2f;
    }
}
