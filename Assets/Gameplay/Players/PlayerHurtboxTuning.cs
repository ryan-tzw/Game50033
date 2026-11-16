namespace Game50033
{
    using UnityEngine;

    [CreateAssetMenu(menuName = "Crescendo/Players/Hurtbox Tuning")]
    public sealed class PlayerHurtboxTuning : ScriptableObject
    {
        [Tooltip("Uniform scale relative to the CharacterController, anchored at the bottom of its capsule.")]
        [Range(0.1f, 1f)] public float sizeScale = 0.9f;
    }
}
