namespace Game50033
{
    using System.Globalization;
    using UnityEngine;
    using UnityEngine.UIElements;

    [UxmlElement]
    public partial class HudAbilityIcon : VisualElement
    {
        private readonly VisualElement _face;
        private readonly VisualElement _symbol;
        private readonly VisualElement _wipe;
        private readonly Label _countdown;
        private readonly Label _charges;
        private readonly VisualElement _prompt;
        private readonly Label _promptText;
        private readonly Image _promptImage;
        private GameplayHudTuning _tuning;
        private Color _accent;
        private int _glyph;
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
            _symbol = new VisualElement { pickingMode = PickingMode.Ignore }; _symbol.AddToClassList("hud-symbol"); _face.Add(_symbol);
            _symbol.generateVisualContent += PaintSymbol;
            _wipe = new VisualElement { pickingMode = PickingMode.Ignore }; _wipe.AddToClassList("hud-wipe"); _face.Add(_wipe);
            _wipe.generateVisualContent += PaintWipe;
            _countdown = new Label { pickingMode = PickingMode.Ignore }; _countdown.AddToClassList("hud-countdown"); _face.Add(_countdown);
            _charges = new Label { pickingMode = PickingMode.Ignore }; _charges.AddToClassList("hud-charges"); _face.Add(_charges);
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

        public void Configure(GameplayHudTuning tuning, bool dodge)
        {
            _tuning = tuning;
            _size = dodge ? tuning.dodgeIconSize : tuning.iconSize;
            var unit = _size / 72f;
            style.width = style.height = _size;
            style.alignSelf = Align.FlexEnd;
            var radius = dodge ? _size * 0.5f : tuning.cornerRadius;
            _face.style.borderTopLeftRadius = _face.style.borderTopRightRadius = radius;
            _face.style.borderBottomLeftRadius = _face.style.borderBottomRightRadius = radius;
            _face.style.borderTopWidth = _face.style.borderBottomWidth = tuning.abilityBorderWidth;
            _face.style.borderLeftWidth = _face.style.borderRightWidth = tuning.abilityBorderWidth;
            _face.style.backgroundColor = tuning.iconBackground;
            _countdown.style.fontSize = Mathf.Max(18f, tuning.countdownFontSize * unit);
            _charges.style.fontSize = Mathf.Max(16f, 21f * unit);
            _promptText.style.fontSize = tuning.promptFontSize;
            _promptText.style.unityFontDefinition = FontDefinition.FromFont(tuning.promptFont != null ? tuning.promptFont : tuning.font);
            _promptText.style.unityFontStyleAndWeight = FontStyle.Normal;
            _prompt.style.bottom = -tuning.promptHeight * 0.5f;
            _promptImage.style.position = Position.Absolute;
            _promptImage.style.left = _promptImage.style.right = tuning.promptPadding;
            _promptImage.style.top = _promptImage.style.bottom = tuning.promptPadding;
            _prompt.style.backgroundColor = tuning.promptBackground;
            _prompt.style.borderTopWidth = _prompt.style.borderBottomWidth = tuning.promptBorderWidth;
            _prompt.style.borderLeftWidth = _prompt.style.borderRightWidth = tuning.promptBorderWidth;
        }

        public void SetAppearance(Color accent, int glyph, string prompt, VectorImage image, bool circularPrompt)
        {
            _accent = accent; _glyph = glyph;
            var border = Color.black;
            _face.style.borderTopColor = _face.style.borderBottomColor = border;
            _face.style.borderLeftColor = _face.style.borderRightColor = border;
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
            _spacePrompt = glyph == 4 && image != null;
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
                _symbol.style.opacity = _supported ? 1f : 0.2f;
            }
            var timer = state.Supported && state.Remaining > 0.05f
                ? state.Remaining >= 1 ? Mathf.CeilToInt(state.Remaining) * 10 : Mathf.CeilToInt(state.Remaining * 10) : 0;
            if (_timer != timer)
            {
                _timer = timer;
                _countdown.text = timer == 0 ? string.Empty : timer >= 10 ? (timer / 10).ToString()
                    : (timer / 10f).ToString("0.0", CultureInfo.InvariantCulture);
            }
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

        private void PaintSymbol(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            var size = _symbol.contentRect.width;
            var center = new Vector2(size * 0.5f, size * 0.5f);
            var color = Color.Lerp(_accent, Color.white, 0.3f);
            painter.fillColor = color; painter.strokeColor = color; painter.lineWidth = 2.2f;
            painter.BeginPath();
            if (_glyph == 0)
            {
                painter.MoveTo(new Vector2(size * 0.18f, size * 0.74f));
                painter.BezierCurveTo(new Vector2(size * 0.2f, size * 0.4f), new Vector2(size * 0.68f, size * 0.15f), new Vector2(size * 0.84f, size * 0.2f));
                painter.BezierCurveTo(new Vector2(size * 0.54f, size * 0.39f), new Vector2(size * 0.45f, size * 0.7f), new Vector2(size * 0.18f, size * 0.74f));
                painter.ClosePath(); painter.Fill();
                painter.BeginPath(); painter.MoveTo(new Vector2(size * 0.24f, size * 0.8f)); painter.LineTo(new Vector2(size * 0.76f, size * 0.28f)); painter.Stroke();
            }
            else if (_glyph == 1)
            {
                for (var i = 0; i < 3; i++)
                {
                    painter.BeginPath(); painter.Arc(center, size * (0.19f + i * 0.1f), -70 + i * 30, 215 + i * 12, ArcDirection.Clockwise); painter.Stroke();
                }
            }
            else if (_glyph == 2)
            {
                painter.MoveTo(center + new Vector2(0, -size * 0.32f));
                painter.LineTo(center + new Vector2(size * 0.28f, -size * 0.15f));
                painter.BezierCurveTo(center + new Vector2(size * 0.27f, size * 0.15f), center + new Vector2(size * 0.1f, size * 0.3f), center + new Vector2(0, size * 0.35f));
                painter.BezierCurveTo(center + new Vector2(-size * 0.1f, size * 0.3f), center + new Vector2(-size * 0.27f, size * 0.15f), center + new Vector2(-size * 0.28f, -size * 0.15f));
                painter.ClosePath(); painter.Stroke();
            }
            else if (_glyph == 3)
            {
                for (var i = 0; i < 16; i++)
                {
                    var angle = (i * 22.5f - 90) * Mathf.Deg2Rad;
                    var point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * size * (i % 2 == 0 ? 0.35f : 0.1f);
                    if (i == 0) painter.MoveTo(point); else painter.LineTo(point);
                }
                painter.ClosePath(); painter.Fill();
            }
            else
            {
                painter.MoveTo(new Vector2(size * 0.27f, size * 0.25f));
                painter.LineTo(new Vector2(size * 0.68f, size * 0.49f));
                painter.LineTo(new Vector2(size * 0.27f, size * 0.76f));
                painter.LineTo(new Vector2(size * 0.45f, size * 0.49f)); painter.ClosePath(); painter.Fill();
                for (var i = 0; i < 3; i++)
                {
                    painter.BeginPath(); painter.MoveTo(new Vector2(size * 0.13f, size * (0.3f + i * 0.2f)));
                    painter.LineTo(new Vector2(size * 0.29f, size * (0.3f + i * 0.2f))); painter.Stroke();
                }
            }
        }
    }
}
