using UnityEngine;

namespace Game50033
{
    [CreateAssetMenu(menuName = "Game50033/Tuning/Bosses/Dog Charge", fileName = "DogChargeTuning")]
    public sealed class DogChargeTuning : ScriptableObject
    {
        [Tooltip("Duration of the charge warning before the boss dashes.")]
        [Min(0f)] public float windUpDuration = 0.4f;

        [Tooltip("Distance beyond the player's captured position at which the charge stops to prevent the dog from flying across the entire map")]
        [Min(0f)] public float distancePastTarget = 2f;
    }
}
