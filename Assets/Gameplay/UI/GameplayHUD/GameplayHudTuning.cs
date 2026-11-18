namespace Game50033
{
    using UnityEngine;
    using UnityEngine.UIElements;

    [CreateAssetMenu(menuName = "Crescendo/UI/Gameplay HUD")]
    public sealed class GameplayHudTuning : ScriptableObject
    {
        [Header("Layout (1920 x 1080 reference)")]
        [Min(32f)] public float iconSize = 72f;
        [Min(0f)] public float iconGap = 6f;
        [Min(0f)] public float panelPadding = 12f;
        [Min(0f)] public float playerGap = 40f;
        [Min(0f)] public float bottomMargin = 20f;
        [Min(1f)] public float borderWidth = 2f;
        [Min(0f)] public float cornerRadius = 10f;
        [Header("Typography")]
        public Font font;
        public float countdownFontSize = 30f;
        public float promptFontSize = 16f;
        public Font promptFont;
        [Min(16f)] public float promptHeight = 24f;
        [Min(0f)] public float promptPadding = 4f;
        [Header("Character selection")]
        [Min(0f)] public float previewIconInset = 12f;
        [Min(90f)] public float previewRowHeight = 122f;
        [Min(12f)] public float previewFontSize = 18f;
        [Min(24f)] public float portraitSize = 52f;
        [Header("Appearance")]
        public Color emptyHeart = new(0.025f, 0.025f, 0.035f, 1f);
        public Color iconBackground = new(0.025f, 0.03f, 0.045f, 0.86f);
        public Color promptBackground = new(0.015f, 0.018f, 0.025f, 0.98f);
        [Range(0f, 1f)] public float cooldownOpacity = 0.78f;
        [Header("Input prompt artwork")]
        public VectorImage leftMouse;
        public VectorImage rightMouse;
        public VectorImage space;

        public int Revision { get; private set; }
        private void OnValidate() { Revision++; }
    }
}
