namespace Game50033
{
    using UnityEngine;
    using UnityEngine.UIElements;

    [CreateAssetMenu(menuName = "Crescendo/UI/Gameplay HUD")]
    public sealed class GameplayHudTuning : ScriptableObject
    {
        [Header("Layout (1920 x 1080 reference)")]
        [Min(32f)] public float iconSize = 56f;
        [Min(24f)] public float dodgeIconSize = 40f;
        [Min(0f)] public float iconGap = 6f;
        [Min(0f)] public float panelPadding = 12f;
        [Min(0f)] public float playerGap = 40f;
        [Min(0f)] public float bottomMargin = 3f;
        [Header("Borders")]
        [Min(0f)] public float healthBorderWidth = 3f;
        [Min(0f)] public float promptBorderWidth = 1.5f;
        [Header("Health strip")]
        [Tooltip("Zero fits the health strip to the ability and dodge row.")]
        [Min(0f)] public float healthBarWidth;
        [Min(6f)] public float healthBarHeight = 16f;
        [Min(8f)] public float heartWidth = 18f;
        [Min(8f)] public float heartHeight = 20f;
        [Min(0f)] public float heartNotchDepth = 3f;
        [Tooltip("Lower values taper the bottom sooner; higher values keep the lower sides fuller.")]
        [Range(0f, 0.5f)] public float heartBottomTaper = 0.3f;
        [Header("Typography")]
        public Font font;
        public Font headingFont;
        public float countdownFontSize = 30f;
        public float promptFontSize = 16f;
        [Tooltip("Used for input prompts, ability types, cooldowns and charge counts.")]
        public Font promptFont;
        [Min(16f)] public float promptHeight = 24f;
        [Min(0f)] public float promptPadding = 4f;
        [Header("Character selection")]
        [Min(0f)] public float previewIconInset = 12f;
        [Min(90f)] public float previewRowHeight = 122f;
        [Min(12f)] public float previewFontSize = 18f;
        [Min(24f)] public float portraitSize = 52f;
        [Header("Ability Inspection")]
        public bool toggleAbilityInspection = true;
        [Min(0f)] public float inspectGap = 28f;
        [Min(0f)] public float inspectPadding = 12f;
        [Min(12f)] public float inspectStatFontSize = 20f;
        public Color inspectBackground = new(0.04f, 0.055f, 0.085f, 0.98f);
        [Header("Appearance")]
        public Color emptyHeart = new(0.025f, 0.025f, 0.035f, 1f);
        public Color promptBackground = new(0.015f, 0.018f, 0.025f, 0.98f);
        [Range(0f, 1f)] public float cooldownOpacity = 0.78f;
        [Header("Ability artwork")]
        public Texture2D dodgeIcon;
        public Texture2D sharedAbilityBorder;
        [Header("Input prompt artwork")]
        public VectorImage leftMouse;
        public VectorImage rightMouse;
        public VectorImage space;

        public int Revision { get; private set; }
        private void OnValidate() { Revision++; }
    }
}
