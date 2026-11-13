namespace Game50033
{
    using UnityEngine;

    [CreateAssetMenu(menuName = "Crescendo/Combat/Hurtbox Tuning")]
    public sealed class CombatHurtboxTuning : ScriptableObject
    {
        [Tooltip("Capsule centre in the anchor's local space (the owning transform when no anchor is assigned).")]
        public Vector3 center;
        [Min(0.001f)] public float radius = 0.5f;
        [Tooltip("Total height along the anchor's local Y axis, including both rounded ends.")]
        [Min(0.001f)] public float height = 2f;

        [Tooltip("Show the projected outline in the Editor Game view. Its Gizmos toggle must also be enabled; damage and the Scene-view preview are unaffected.")]
        public bool showGameViewPreview;
    }
}
