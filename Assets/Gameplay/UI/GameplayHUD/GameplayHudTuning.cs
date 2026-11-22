namespace Game50033
{
    using UnityEngine;
    using UnityEngine.Serialization;
    using UnityEngine.UIElements;

    [CreateAssetMenu(menuName = "Crescendo/UI/Gameplay HUD")]
    public sealed class GameplayHudTuning : ScriptableObject
    {
        [Header("Layout (1920 x 1080 reference)")]
        [Min(320f)] public float columnWidth = 440f;
        [Min(32f)] public float iconSize = 56f;
        [Min(24f)] public float dodgeIconSize = 40f;
        [Min(0f)] public float iconGap = 6f;
        [Min(0f)] public float panelPadding = 16f;
        [Min(0f)] public float playerGap = 16f;
        [Min(0f)] public float bottomMargin = 12f;
        [Min(24f)] public float instrumentSize = 50f;
        [Min(0f)] public float instrumentGap = 16f;
        public Color panelBackground = new(0.045f, 0.035f, 0.065f, 0.94f);
        [Min(0f)] public float panelBorderWidth = 1.4f;
        [Min(0f)] public float panelCornerRadius = 14f;
        [Header("Borders")]
        [Min(0f)] public float healthBorderWidth = 3f;
        [Min(0f)] public float promptBorderWidth = 1.5f;
        [Header("Health strip")]
        [Tooltip("Zero fits the health strip to the ability and dodge row.")]
        [Min(0f)] public float healthBarWidth;
        [Min(6f)] public float healthBarHeight = 16f;
        [Min(8f)] public float heartWidth = 18f;
        [Min(8f)] public float heartHeight = 20f;
        [Header("Heart shape")]
        [Min(0f)] public float heartOutlineWidth = 2.5f;
        public Color heartOutlineColor = Color.black;
        [Min(0f)] public float heartNotchDepth = 3f;
        [Range(0.05f, 0.4f)] public float heartBottomTaper = 0.18f;
        [Header("Health animation")]
        [Min(0f)] public float healthMeterDuration = 0.4f;
        public AnimationCurve healthMeterEase = new(new Keyframe(0f, 0f, 3f, 3f), new Keyframe(1f, 1f, 0f, 0f));
        [Min(0f)] public float healthDamageFadeDuration = 0.4f;
        public AnimationCurve healthDamageFadeEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [FormerlySerializedAs("brokenHeartHighlight")]
        [FormerlySerializedAs("brokenHeartColor")]
        public Color missingHeartColor = Color.black;
        [Min(0f)] public float healthShakeDuration = 0.2f;
        [Min(0f)] public float healthShakeAmplitude = 3f;
        [Min(0f)] public float healthShakeFrequency = 24f;
        public AnimationCurve healthShakeDecay = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        [Header("Typography")]
        public Font font;
        public Font headingFont;
        public float promptFontSize = 16f;
        [Tooltip("Used for input prompts and ability types. Also the fallback when Ability Number Font is unset.")]
        public Font promptFont;
        [Min(16f)] public float promptHeight = 24f;
        [Min(0f)] public float promptPadding = 4f;
        [Header("Ability numbers")]
        [FormerlySerializedAs("chargeFont")] public Font abilityNumberFont;
        [FormerlySerializedAs("chargeFontSize")]
        [Min(12f)] public float abilityNumberFontSize = 26f;
        public FontStyle abilityNumberFontStyle = FontStyle.Bold;
        [Tooltip("Optical offset for the centred countdown, in UI pixels. Positive X moves right; positive Y moves down.")]
        public Vector2 countdownOffset = new(0f, -3f);
        [Header("Charge counts")]
        [Min(0f)] public float chargeOutlineWidth = 1.25f;
        [Tooltip("X is the inset from the icon's right edge (positive moves left). Y is the offset from its top (positive moves down).")]
        public Vector2 chargeOffset = new(2f, -7f);
        [Header("Character selection")]
        [Min(0f)] public float previewIconInset = 12f;
        [Min(90f)] public float previewRowHeight = 124f;
        [Min(12f)] public float previewFontSize = 17f;
        [Min(24f)] public float portraitSize = 68f;
        [Range(1f, 1.25f)] public float portraitHighlightScale = 1.14f;
        [Min(0f)] public float portraitHighlightDuration = 0.22f;
        [Header("Ability Inspection")]
        public bool toggleAbilityInspection = true;
        [Min(32f)] public float inspectIconSize = 72f;
        [Min(0f)] public float inspectGap = 12f;
        [Min(0f)] public float inspectPadding = 16f;
        [Min(12f)] public float inspectStatFontSize = 16f;
        public Color inspectBackground = new(0.04f, 0.055f, 0.085f, 0.98f);
        [Header("Appearance")]
        public Color emptyHeart = new(0.025f, 0.025f, 0.035f, 1f);
        public Color promptBackground = new(0.015f, 0.018f, 0.025f, 0.98f);
        [Range(0f, 1f)] public float cooldownOpacity = 0.78f;
        [Header("Ability artwork")]
        [Range(0f, 1f)] public float abilityGradientTopLeftStrength = 0.4f;
        [Range(0f, 1f)] public float abilityGradientBottomRightStrength = 0.035f;
        public Texture2D dodgeIcon;
        public Color dodgeTint = new(0.94f, 0.9f, 0.8f, 1f);
        public Texture2D sharedAbilityBorder;
        [Header("Input prompt artwork")]
        public VectorImage leftMouse;
        public VectorImage rightMouse;
        public VectorImage space;

        public int Revision { get; private set; }
        private void OnValidate() { Revision++; }
    }
}
