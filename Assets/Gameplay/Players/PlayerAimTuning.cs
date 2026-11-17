namespace Game50033
{
    using UnityEngine;

    [CreateAssetMenu(menuName = "Crescendo/Players/Aim Tuning")]
    public sealed class PlayerAimTuning : ScriptableObject
    {
        [Header("Auto cursor")]
        public Texture2D autoCursor;
        [Tooltip("Click position in pixels from the texture's top-left corner.")]
        public Vector2 autoHotspot = new(5f, 5f);

        [Header("Manual cursor")]
        public Texture2D manualCursor;
        [Tooltip("Aim position in pixels from the texture's top-left corner.")]
        public Vector2 manualHotspot = new(16f, 16f);
    }
}
