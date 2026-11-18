namespace Game50033
{
    using UnityEngine;

    [CreateAssetMenu(menuName = "Crescendo/Combat/Lock-On Indicator Tuning")]
    public sealed class LockOnIndicatorTuning : ScriptableObject
    {
        [Header("Arcs")]
        [Min(0.01f)] public float radius = 0.5f;
        [Min(0.001f)] public float thickness = 0.035f;
        [Range(1, 8)] public int arcsPerPlayer = 4;
        [Tooltip("Fraction of each evenly spaced slot covered by its arc; the rest is a gap.")]
        [Range(0.05f, 0.95f)] public float coverage = 0.6f;
        public float rotationDegrees;
        public float groundOffset = 0.015f;
        [Range(0f, 1f)] public float opacity = 0.85f;

        [Header("Acquisition")]
        [Min(0f)] public float acquireDuration = 0.15f;
        [Min(1f)] public float acquireScale = 1.35f;

        [Header("Player colours")]
        public bool overridePlayerColors;
        [Tooltip("Used in the editor preview, and at runtime only when colour overrides are enabled. Index 0 is Player 1.")]
        public Color[] playerColors =
        {
            Color.HSVToRGB(0.6f, 0.6f, 0.9f), Color.HSVToRGB(0f, 0.6f, 0.9f),
            Color.HSVToRGB(0.1f, 0.6f, 0.9f), Color.HSVToRGB(0.3f, 0.6f, 0.9f)
        };

        [Header("Editor preview")]
        [Tooltip("Bit mask: 1 = P1, 2 = P2, 4 = P3, 8 = P4. Use 15 to preview all four. Ignored in Play mode.")]
        [Range(0, 15)] public int editorPreviewPlayers;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // an SO edit alone need not tick ExecuteAlways scene objects; request a preview refresh on the editor thread.
            UnityEditor.EditorApplication.delayCall += UnityEditor.EditorApplication.QueuePlayerLoopUpdate;
            UnityEditor.EditorApplication.delayCall += UnityEditor.SceneView.RepaintAll;
        }
#endif
    }
}
