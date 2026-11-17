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

        [Header("Background")]
        public Color backgroundColor = new(0.12f, 0.035f, 0.22f, 1f);
        [Range(0f, 0.98f)] public float backgroundOpacity = 0.2f;
        [Range(0f, 1f)] public float backgroundVariation = 0.5f;

        [Header("Midground")]
        [Range(0f, 0.98f)] public float midgroundOpacity = 0.15f;
        [Tooltip("Additional contour width around the bright currents. Higher values make broader concentrations.")]
        [Range(0.01f, 1f)] public float midgroundWidth = 0.4f;

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
