namespace Game50033.Examples
{
    using UnityEngine;

    [CreateAssetMenu(menuName = "Crescendo/Experiments/Reverberation Energy Field")]
    public sealed class ReverberationEnergyFieldTuning : ScriptableObject
    {
        [Header("Field shape")]
        [Tooltip("Width, ground depth and height in local Unity units, before scaling the prefab root.")]
        public Vector3 fieldDimensions = new(3f, 2f, 0.6f);
        [Range(1, 8)] public int layerCount = 4;
        [Range(0f, 10f)] public float layerTilt = 3f;
        [Range(0.05f, 0.95f)] public float edgeFalloff = 0.55f;

        [Header("Energy appearance")]
        public Color energyColor = new(0.62f, 0.22f, 0.95f, 1f);
        [Range(0.1f, 3f)] public float density = 1f;
        [Range(0f, 0.98f)] public float opacity = 0.65f;
        [Min(0f)] public float emission = 1.15f;
        [Range(2, 5)] public int brightnessBands = 3;

        [Header("Flow")]
        [Tooltip("Noise cycles per world unit. Lower values produce larger structures.")]
        [Min(0.05f)] public float noiseScale = 1.3f;
        [Min(0f)] public float flowSpeed = 0.12f;
        [Range(0f, 2f)] public float turbulence = 0.8f;

        public int Revision { get; private set; }

        private void OnValidate()
        {
            Revision++;
#if UNITY_EDITOR
            // queue the preview on the editor thread; OnValidate may also run during asset loading.
            UnityEditor.EditorApplication.delayCall += UnityEditor.EditorApplication.QueuePlayerLoopUpdate;
            UnityEditor.EditorApplication.delayCall += UnityEditor.SceneView.RepaintAll;
#endif
        }
    }
}
