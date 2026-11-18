namespace Game50033
{
    using UnityEngine;

    [CreateAssetMenu(menuName = "Crescendo/Rendering/Pixel World Outline")]
    public sealed class PixelWorldOutlineTuning : ScriptableObject
    {
        public bool outlineEnabled = true;
        public Color color = new(0.045f, 0.008f, 0.09f, 0.9f);
        [Tooltip("Outline width in internal render pixels, before upscaling.")]
        [Range(1, 4)] public int width = 1;
        [Tooltip("Boundary coverage used to define the silhouette. Lower values follow fainter outer edges.")]
        [Range(0.01f, 0.95f)] public float threshold = 0.15f;
    }
}
