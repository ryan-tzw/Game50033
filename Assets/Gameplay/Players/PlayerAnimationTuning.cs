namespace Game50033
{
    using UnityEngine;

    [CreateAssetMenu(menuName = "Game50033/Tuning/Player Animation", fileName = "PlayerAnimationTuning")]
    public sealed class PlayerAnimationTuning : ScriptableObject
    {
        [Tooltip("Horizontal speeds at or below this use Idle, in world units per second.")]
        [Min(0f)] public float idleSpeed = 0.05f;
        [Tooltip("Horizontal speeds above this use Run, in world units per second. Lower moving speeds use Walk.")]
        [Min(0.01f)] public float runSpeed = 1.5f;
    }
}
