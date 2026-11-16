namespace Game50033
{
    using UnityEngine;

    [CreateAssetMenu(menuName = "Crescendo/Debug/Visualization Tuning")]
    public sealed class DebugVisualizationTuning : ScriptableObject
    {
        [Header("Hurtboxes")]
        public Color enemyHurtboxColor = new Color(112f / 255f, 230f / 255f, 155f / 255f);

        [Header("Rendering")]
        [Range(0f, 1f)] public float fillOpacity = 0.18f;
        [Range(0f, 1f)] public float outlineOpacity = 1f;
        [Tooltip("UI panel units; screen pixels with the debug panel's default scale of 1.")]
        [Min(0f)] public float outlineWidth = 1.5f;
    }
}
