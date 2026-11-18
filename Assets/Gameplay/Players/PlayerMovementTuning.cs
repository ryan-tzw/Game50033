namespace Game50033
{
    using UnityEngine;

    [CreateAssetMenu(menuName = "Game50033/Tuning/Player Movement", fileName = "PlayerMovementTuning")]
    public sealed class PlayerMovementTuning : ScriptableObject
    {
        [Tooltip("Walking speed in world units per second.")]
        [Min(0f)] public float moveSpeed = 3f;
    }
}
