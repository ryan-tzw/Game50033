namespace Game50033
{
    using System.Globalization;
    using UnityEngine;
    using UnityEngine.UIElements;

    [UxmlElement]
    public partial class HudAbilityIcon : VisualElement
    {
        private readonly VisualElement _face;
        private readonly Image _symbol;
        private readonly Image _background;
        private readonly Image _border;
        private readonly VisualElement _wipe;
        private readonly Label _countdown;
        private readonly Label _charges;
        private readonly VisualElement _prompt;
        private readonly Label _promptText;
        private readonly Image _promptImage;
        private GameplayHudTuning _tuning;
        private float _size;
        private float _fraction = -1;
        private float _wipeOpacity;
        private int _timer = -1;
        private int _chargeCount = -1;
        private int _maxCharges = -1;
        private bool _supported = true;
        private bool _spacePrompt;

        public HudAbilityIcon()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("hud-ability");
            _face = new VisualElement { pickingMode = PickingMode.Ignore }; _face.AddToClassList("hud-face"); Add(_face);
            _background = new Image { name = "ability-background", pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleToFit }; _background.AddToClassList("hud-symbol"); _face.Add(_background);
            _symbol = new Image { pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleToFit }; _symbol.AddToClassList("hud-symbol"); _face.Add(_symbol);
            _border = new Image { name = "shared-border", pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleToFit }; _border.AddToClassList("hud-symbol"); _face.Add(_border);
            _wipe = new VisualElement { pickingMode = PickingMode.Ignore }; _wipe.AddToClassList("hud-wipe"); _face.Add(_wipe);
            _wipe.generateVisualContent += PaintWipe;
            _countdown = new Label { pickingMode = PickingMode.Ignore }; _countdown.AddToClassList("hud-countdown"); _face.Add(_countdown);
            _charges = new Label { pickingMode = PickingMode.Ignore }; _charges.AddToClassList("hud-charges"); Add(_charges);
            _prompt = new VisualElement { pickingMode = PickingMode.Ignore }; _prompt.AddToClassList("hud-prompt"); Add(_prompt);
            _prompt.generateVisualContent += context =>
            {
                if (!_spacePrompt) return;
                var rect = _prompt.contentRect;
                var painter = context.painter2D;
                painter.strokeColor = Color.white;
                painter.lineWidth = 1.5f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.x + rect.width * 0.22f, rect.y + rect.height * 0.35f));
                painter.LineTo(new Vector2(rect.x + rect.width * 0.22f, rect.y + rect.height * 0.65f));
                painter.LineTo(new Vector2(rect.x + rect.width * 0.78f, rect.y + rect.height * 0.65f));
                painter.LineTo(new Vector2(rect.x + rect.width * 0.78f, rect.y + rect.height * 0.35f));
                painter.Stroke();
            };
            _promptText = new Label { pickingMode = PickingMode.Ignore }; _prompt.Add(_promptText);
            _promptImage = new Image { pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleToFit }; _prompt.Add(_promptImage);
        }

        public void Configure(GameplayHudTuning tuning, bool dodge, float sizeOverride = 0f, bool showPrompt = true)
        {
            _tuning = tuning;
            _size = sizeOverride > 0f ? sizeOverride : dodge ? tuning.dodgeIconSize : tuning.iconSize;
            _background.style.display = dodge ? DisplayStyle.None : DisplayStyle.Flex;
            _prompt.style.display = showPrompt ? DisplayStyle.Flex : DisplayStyle.None;
            _border.image = tuning.sharedAbilityBorder;
            _border.style.display = dodge ? DisplayStyle.None : DisplayStyle.Flex;
            style.width = style.height = _size;
            style.alignSelf = Align.FlexEnd;
            // the artwork supplies its own frame; only the cooldown overlay needs rounded clipping.
            var radius = dodge ? _size * 0.5f : 0f;
            _face.style.borderTopLeftRadius = _face.style.borderTopRightRadius = radius;
            _face.style.borderBottomLeftRadius = _face.style.borderBottomRightRadius = radius;
            _countdown.style.fontSize = _charges.style.fontSize = tuning.abilityNumberFontSize;
            _countdown.style.translate = new Translate(tuning.countdownOffset.x, tuning.countdownOffset.y);
            // keep the label unclipped vertically, while anchoring its right edge using the authored inset.
            _charges.style.right = tuning.chargeOffset.x;
            _charges.style.top = tuning.chargeOffset.y;
            _charges.style.unityTextOutlineWidth = tuning.chargeOutlineWidth;
            _promptText.style.fontSize = tuning.promptFontSize;
            var labelFont = FontDefinition.FromFont(tuning.promptFont != null ? tuning.promptFont : tuning.font);
            _promptText.style.unityFontDefinition = labelFont;
            var numberFont = tuning.abilityNumberFont != null ? FontDefinition.FromFont(tuning.abilityNumberFont) : labelFont;
            _countdown.style.unityFontDefinition = _charges.style.unityFontDefinition = numberFont;
            _countdown.style.unityFontStyleAndWeight = _charges.style.unityFontStyleAndWeight = tuning.abilityNumberFontStyle;
            _promptText.style.unityFontStyleAndWeight = FontStyle.Normal;
            _prompt.style.bottom = -tuning.promptHeight * 0.5f;
            _promptImage.style.position = Position.Absolute;
            _promptImage.style.left = _promptImage.style.right = tuning.promptPadding;
            _promptImage.style.top = _promptImage.style.bottom = tuning.promptPadding;
            _prompt.style.backgroundColor = tuning.promptBackground;
            _prompt.style.borderTopWidth = _prompt.style.borderBottomWidth = tuning.promptBorderWidth;
            _prompt.style.borderLeftWidth = _prompt.style.borderRightWidth = tuning.promptBorderWidth;
        }

        public void SetAppearance(Color accent, Texture2D artwork, Material palette, bool dodge, string prompt, VectorImage image, bool circularPrompt, Texture2D background = null)
        {
            _background.image = background;
            _symbol.image = artwork;
            _symbol.tintColor = dodge ? _tuning.dodgeTint : Color.white;
            _symbol.style.unityMaterial = new StyleMaterialDefinition(dodge ? null : palette);
            if (!dodge && palette != null)
            {
                var backgroundMaterial = MaterialDefinition.FromMaterial(palette);
                backgroundMaterial.SetFloat("_PaletteLayer", 1f);
                var borderMaterial = MaterialDefinition.FromMaterial(palette);
                borderMaterial.SetFloat("_PaletteLayer", 2f);
                _background.style.unityMaterial = new StyleMaterialDefinition(backgroundMaterial);
                _border.style.unityMaterial = new StyleMaterialDefinition(borderMaterial);
            }
            else
            {
                _background.style.unityMaterial = _border.style.unityMaterial = new StyleMaterialDefinition((Material)null);
            }
            _prompt.style.borderTopColor = _prompt.style.borderBottomColor = Color.Lerp(accent, Color.white, 0.75f);
            _prompt.style.borderLeftColor = _prompt.style.borderRightColor = Color.Lerp(accent, Color.white, 0.75f);
            var height = _tuning.promptHeight;
            var width = height;
            _prompt.style.width = width; _prompt.style.height = height;
            _prompt.style.left = (_size - width) * 0.5f;
            var radius = circularPrompt ? height * 0.5f : 5f;
            _prompt.style.borderTopLeftRadius = _prompt.style.borderTopRightRadius = radius;
            _prompt.style.borderBottomLeftRadius = _prompt.style.borderBottomRightRadius = radius;
            _promptText.text = prompt;
            _spacePrompt = dodge && image != null;
            _promptText.style.display = image == null && !_spacePrompt ? DisplayStyle.Flex : DisplayStyle.None;
            _promptImage.vectorImage = image;
            _promptImage.style.display = image != null && !_spacePrompt ? DisplayStyle.Flex : DisplayStyle.None;
            _prompt.MarkDirtyRepaint();
            _symbol.MarkDirtyRepaint();
        }

        public void SetState(PlayerAbilityState state)
        {
            var fraction = state.Supported ? Mathf.Clamp01(state.Remaining / state.Duration) : 1f;
            var opacity = state.MaxCharges > 0 && state.Charges > 0 ? _tuning.cooldownOpacity * 0.45f : _tuning.cooldownOpacity;
            if (_fraction != fraction || _wipeOpacity != opacity)
            {
                _fraction = fraction; _wipeOpacity = opacity; _wipe.MarkDirtyRepaint();
            }
            if (_supported != state.Supported)
            {
                _supported = state.Supported;
                _symbol.style.opacity = _border.style.opacity = _background.style.opacity = _supported ? 1f : 0.2f;
            }
            var timer = state.Supported && state.Remaining > 0f ? Mathf.FloorToInt(state.Remaining) : -1;
            if (_timer != timer)
            {
                _timer = timer;
                _countdown.text = timer < 0 ? string.Empty : timer.ToString(CultureInfo.InvariantCulture);
            }
            // keep zero legible through most of the final second, then fade faster near the end.
            _countdown.style.opacity = timer == 0 ? Mathf.Sqrt(state.Remaining) : 1f;
            if (_chargeCount != state.Charges || _maxCharges != state.MaxCharges)
            {
                _chargeCount = state.Charges; _maxCharges = state.MaxCharges;
                _charges.text = state.MaxCharges > 1 ? state.Charges.ToString() : string.Empty;
                _charges.style.color = state.Charges == 0 ? new Color(0.4f, 0.4f, 0.45f) : Color.white;
            }
        }

        private void PaintWipe(MeshGenerationContext context)
        {
            if (_fraction <= 0) return;
            var painter = context.painter2D;
            var size = _wipe.contentRect.size;
            var center = size * 0.5f;
            var radius = size.magnitude * 0.5f + 2;
            painter.fillColor = new Color(0, 0, 0.015f, _wipeOpacity);
            painter.BeginPath();
            if (_fraction >= 0.999f)
            {
                painter.MoveTo(Vector2.zero); painter.LineTo(new Vector2(size.x, 0));
                painter.LineTo(size); painter.LineTo(new Vector2(0, size.y));
            }
            else
            {
                painter.MoveTo(center); painter.LineTo(center + Vector2.down * radius);
                painter.Arc(center, radius, -90, -90 + 360 * _fraction, ArcDirection.Clockwise);
            }
            painter.ClosePath(); painter.Fill();
        }

    }
}
