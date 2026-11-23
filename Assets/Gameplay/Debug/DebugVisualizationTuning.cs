namespace Game50033
{
    using UnityEngine;

    [CreateAssetMenu(menuName = "Crescendo/Debug/Visualization Tuning")]
    public sealed class DebugVisualizationTuning : ScriptableObject
    {
        [Header("Hurtboxes")]
        public Color playerHurtboxColor = new Color(89f / 255f, 213f / 255f, 1f);
        public Color enemyHurtboxColor = new Color(112f / 255f, 230f / 255f, 155f / 255f);

        [Header("Impact snapshots")]
        public Color playerAttackColor = new Color(1f, 155f / 255f, 66f / 255f);
        public Color bossAttackColor = new Color(1f, 80f / 255f, 106f / 255f);
        public Color reverberationAreaColor = new Color(1f, 219f / 255f, 105f / 255f);
        public Color specialZoneColor = new Color(170f / 255f, 140f / 255f, 1f);
        [Tooltip("World-time lifetime of a historical impact preview, independent of attacker hitstop.")]
        [Min(0f)] public float snapshotDuration = 0.12f;
        [Min(0.5f)] public float snapshotDashLength = 5f;
        [Min(0.5f)] public float snapshotDashGap = 3f;

        [Header("Rendering")]
        [Range(0f, 1f)] public float fillOpacity = 0.18f;
        [Range(0f, 1f)] public float outlineOpacity = 1f;
        [Tooltip("UI panel units; screen pixels with the debug panel's default scale of 1.")]
        [Min(0f)] public float outlineWidth = 1.5f;
    }
}
