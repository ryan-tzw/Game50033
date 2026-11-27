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
        private readonly HudAccentGradient _background;
        private readonly Image _border;
        private readonly VisualElement _wipe;
        private readonly VisualElement _interactionOutline;
        private readonly Label _countdown;
        private readonly Label _charges;
        private readonly VisualElement _prompt;
        private readonly Label _promptText;
        private readonly Image _promptImage;
        private readonly InteractionBadge _revBadge;
        private readonly InteractionBadge _resBadge;
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
            _background = new HudAccentGradient { name = "ability-background" };
            _face.Add(_background);
            _symbol = new Image { pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleToFit }; _symbol.AddToClassList("hud-symbol"); _face.Add(_symbol);
            _border = new Image { name = "shared-border", pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleToFit }; _border.AddToClassList("hud-symbol"); _face.Add(_border);
            _wipe = new VisualElement { pickingMode = PickingMode.Ignore }; _wipe.AddToClassList("hud-wipe"); _face.Add(_wipe);
            _wipe.generateVisualContent += PaintWipe;
            _countdown = new Label { pickingMode = PickingMode.Ignore }; _countdown.AddToClassList("hud-countdown"); _face.Add(_countdown);
            _interactionOutline = new VisualElement { name = "interaction-outline", pickingMode = PickingMode.Ignore };
            _interactionOutline.AddToClassList("hud-wipe");
            _interactionOutline.style.visibility = Visibility.Hidden;
            _interactionOutline.generateVisualContent += PaintInteractionOutline;
            Add(_interactionOutline);
            _charges = new Label { pickingMode = PickingMode.Ignore }; _charges.AddToClassList("hud-charges"); Add(_charges);
            var badges = new VisualElement { pickingMode = PickingMode.Ignore };
            badges.AddToClassList("hud-interaction-badges");
            _revBadge = new InteractionBadge(AbilityInteraction.Reverberation);
            _resBadge = new InteractionBadge(AbilityInteraction.Resonance);
            badges.Add(_revBadge);
            badges.Add(_resBadge);
            Add(badges);
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
            _revBadge.Configure(tuning, tuning.revBadgeColor);
            _resBadge.Configure(tuning, tuning.resBadgeColor);
            _interactionOutline.MarkDirtyRepaint();
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

        public void SetAppearance(Color accent, Texture2D artwork, Material palette, bool dodge, string prompt, VectorImage image, bool circularPrompt)
        {
            _background.SetAppearance(_tuning, accent);
            _symbol.image = artwork;
            _symbol.tintColor = dodge ? _tuning.dodgeTint : Color.white;
            _symbol.style.unityMaterial = new StyleMaterialDefinition(dodge ? null : palette);
            if (!dodge && palette != null)
            {
                var borderMaterial = MaterialDefinition.FromMaterial(palette);
                borderMaterial.SetFloat("_PaletteLayer", 2f);
                _border.style.unityMaterial = new StyleMaterialDefinition(borderMaterial);
            }
            else
            {
                _border.style.unityMaterial = new StyleMaterialDefinition((Material)null);
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

        internal void SetInteractionBadges(AbilityInteraction interactions, AbilityInteraction conditional = AbilityInteraction.None)
        {
            var wasPulsing = _revBadge.Pulse > 0f || _resBadge.Pulse > 0f;
            _revBadge.SetCapability(interactions, conditional);
            _resBadge.SetCapability(interactions, conditional);
            if (wasPulsing) RefreshInteractionOutline();
        }

        internal void RefreshReadyInteractions(AbilityInteraction ready)
        {
            var revPulse = _revBadge.Pulse;
            var resPulse = _resBadge.Pulse;
            _revBadge.Refresh(ready);
            _resBadge.Refresh(ready);
            if (revPulse != _revBadge.Pulse || resPulse != _resBadge.Pulse)
                RefreshInteractionOutline();
        }

        private void RefreshInteractionOutline()
        {
            _interactionOutline.style.visibility = _revBadge.Pulse > 0f || _resBadge.Pulse > 0f
                ? Visibility.Visible : Visibility.Hidden;
            _interactionOutline.MarkDirtyRepaint();
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

        private void PaintInteractionOutline(MeshGenerationContext context)
        {
            var rev = _revBadge.Pulse;
            var res = _resBadge.Pulse;
            var strength = Mathf.Max(rev, res);
            if (strength <= 0f) return;

            var rect = _interactionOutline.contentRect;
            if (rect.width <= 0f || rect.height <= 0f) return;

            var tint = Color.Lerp(_tuning.revBadgeColor, _tuning.resBadgeColor, res / (rev + res));
            var painter = context.painter2D;
            painter.BeginPath();
            painter.MoveTo(rect.min);
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(rect.max);
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();

            var halo = tint;
            halo.a *= strength * 0.32f;
            painter.strokeColor = halo;
            painter.lineWidth = 6f;
            painter.Stroke();

            var rim = Color.Lerp(tint, Color.white, 0.2f);
            rim.a = tint.a * strength;
            painter.strokeColor = rim;
            painter.lineWidth = 2f;
            painter.Stroke();
        }

        private sealed class InteractionBadge : VisualElement
        {
            private readonly AbilityInteraction _kind;
            private Color _color;
            private float _pulseDuration;
            private float _pulseStartedAt = float.NegativeInfinity;
            private float _pulse;
            private bool _shown;
            private bool _conditional;
            private bool _ready;
            public float Pulse => _pulse;

            public InteractionBadge(AbilityInteraction kind)
            {
                _kind = kind;
                pickingMode = PickingMode.Ignore;
                name = kind == AbilityInteraction.Reverberation ? "rev-badge" : "res-badge";
                style.display = DisplayStyle.None;
                generateVisualContent += Paint;
            }

            public void Configure(GameplayHudTuning tuning, Color color)
            {
                style.width = style.height = tuning.interactionBadgeSize;
                _color = color;
                _pulseDuration = tuning.interactionBadgePulseDuration;
                MarkDirtyRepaint();
            }

            public void SetCapability(AbilityInteraction interactions, AbilityInteraction conditional)
            {
                var shown = (interactions & _kind) != 0;
                var isConditional = (conditional & _kind) != 0;
                if (_shown == shown && _conditional == isConditional) return;
                _shown = shown;
                _conditional = isConditional;
                _ready = false;
                _pulse = 0f;
                _pulseStartedAt = float.NegativeInfinity;
                style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                MarkDirtyRepaint();
            }

            public void Refresh(AbilityInteraction ready)
            {
                if (!_shown || !_conditional) return;
                var available = (ready & _kind) != 0;
                var now = Time.unscaledTime;
                if (available && !_ready) _pulseStartedAt = now;
                var pulse = available && _pulseDuration > 0f
                    ? 1f - Mathf.Clamp01((now - _pulseStartedAt) / _pulseDuration) : 0f;
                if (_ready == available && _pulse == pulse) return;
                _ready = available;
                _pulse = pulse;
                MarkDirtyRepaint();
            }

            private void Paint(MeshGenerationContext context)
            {
                var center = contentRect.center;
                var radius = contentRect.width * 0.5f - 0.75f;
                if (radius <= 0f) return;
                var painter = context.painter2D;
                if (_pulse > 0f)
                {
                    var halo = _color;
                    halo.a *= _pulse * 0.75f;
                    painter.strokeColor = halo;
                    painter.lineWidth = 2f;
                    Circle(painter, center, radius + Mathf.Lerp(4f, 1f, _pulse));
                    painter.Stroke();
                }

                var ink = _color;
                if (_conditional && !_ready) ink.a *= 0.45f;
                painter.fillColor = new Color(0.035f, 0.04f, 0.065f);
                painter.strokeColor = ink;
                painter.lineWidth = 1.2f;
                Circle(painter, center, radius);
                painter.Fill();
                painter.Stroke();
                painter.fillColor = ink;
                if (_kind == AbilityInteraction.Reverberation)
                {
                    Circle(painter, center, radius * 0.52f);
                    painter.Stroke();
                    Circle(painter, center, radius * 0.16f);
                    painter.Fill();
                    return;
                }

                var tip = radius * 0.7f;
                var notch = radius * 0.18f;
                painter.BeginPath();
                painter.MoveTo(center + Vector2.up * tip);
                painter.LineTo(center + new Vector2(notch, notch));
                painter.LineTo(center + Vector2.right * tip);
                painter.LineTo(center + new Vector2(notch, -notch));
                painter.LineTo(center + Vector2.down * tip);
                painter.LineTo(center + new Vector2(-notch, -notch));
                painter.LineTo(center + Vector2.left * tip);
                painter.LineTo(center + new Vector2(-notch, notch));
                painter.ClosePath();
                painter.Fill();
            }

            private static void Circle(Painter2D painter, Vector2 center, float radius)
            {
                painter.BeginPath();
                painter.Arc(center, radius, 0f, 360f);
                painter.ClosePath();
            }
        }
    }
}
