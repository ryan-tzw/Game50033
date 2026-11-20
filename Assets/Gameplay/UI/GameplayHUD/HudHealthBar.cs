namespace Game50033
{
    using UnityEngine;
    using UnityEngine.UIElements;

    [UxmlElement]
    public partial class HudHealthBar : VisualElement
    {
        private const int HeartCount = 5;
        private readonly VisualElement[] _hearts = new VisualElement[HeartCount];
        private readonly bool[] _filled = new bool[HeartCount];
        private readonly float[] _flashAmounts = new float[HeartCount];
        private readonly float[] _damagedAt = new float[HeartCount];
        private GameplayHudTuning _tuning;
        private PlayerHealth _health;
        private Color _accent;
        private bool _dirty = true;
        private bool _wasAnimating;
        private float _displayedFill, _targetFill, _fillFrom, _fillStarted;

        public HudHealthBar()
        {
            pickingMode = PickingMode.Ignore;
            for (var i = 0; i < HeartCount; i++)
            {
                _damagedAt[i] = float.NegativeInfinity;
                var heart = new VisualElement { name = "heart-" + i, pickingMode = PickingMode.Ignore };
                heart.style.position = Position.Absolute;
                Add(heart);
                _hearts[i] = heart;
                var index = i;
                heart.generateVisualContent += context => PaintHeart(context, index);
            }
            generateVisualContent += PaintBar;
            RegisterCallback<GeometryChangedEvent>(_ =>
            {
                _dirty = true;
                if (_health != null) Refresh(_health, _accent);
            });
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                if (_health != null)
                {
                    _health.HealthChanged -= OnHealthChanged;
                    _health.HealthReset -= ResetPresentation;
                }
                _health = null;
            });
        }

        public void Configure(GameplayHudTuning tuning)
        {
            _tuning = tuning;
            for (var i = 0; i < HeartCount; i++)
            {
                _hearts[i].style.width = tuning.heartWidth;
                _hearts[i].style.height = tuning.heartHeight;
            }
            _dirty = true;
        }

        public void Refresh(PlayerHealth health, Color accent)
        {
            if (_tuning == null || panel == null) return;
            if (_health != health)
            {
                if (_health != null)
                {
                    _health.HealthChanged -= OnHealthChanged;
                    _health.HealthReset -= ResetPresentation;
                }
                _health = health;
                if (_health != null)
                {
                    _health.HealthChanged += OnHealthChanged;
                    _health.HealthReset += ResetPresentation;
                }
                ResetPresentation();
            }

            if (_accent != accent) { _accent = accent; _dirty = true; }

            var now = Time.unscaledTime;
            var fill = EvaluateFill(now);
            if (_displayedFill != fill) { _displayedFill = fill; _dirty = true; }
            var width = _tuning.healthBarWidth > 0f ? _tuning.healthBarWidth
                : _tuning.iconSize * 4f + _tuning.dodgeIconSize + _tuning.iconGap * 4f;
            var left = (contentRect.width - width) * 0.5f;
            var filledHearts = Mathf.CeilToInt(_targetFill * HeartCount - 0.0001f);
            var active = false;
            for (var i = 0; i < HeartCount; i++)
            {
                _filled[i] = i < filledHearts;
                var elapsed = now - _damagedAt[i];
                var fade = _tuning.healthDamageFadeDuration > 0f ? Mathf.Clamp01(elapsed / _tuning.healthDamageFadeDuration) : 1f;
                var flash = fade < 1f ? 1f - Mathf.Clamp01(_tuning.healthDamageFadeEase != null ? _tuning.healthDamageFadeEase.Evaluate(fade) : fade) : 0f;
                if (_flashAmounts[i] != flash || _dirty) _hearts[i].MarkDirtyRepaint();
                _flashAmounts[i] = flash;
                var shake = Vector2.zero;
                if (elapsed < _tuning.healthShakeDuration)
                {
                    var progress = elapsed / _tuning.healthShakeDuration;
                    var strength = _tuning.healthShakeAmplitude * Mathf.Clamp01(_tuning.healthShakeDecay != null ? _tuning.healthShakeDecay.Evaluate(progress) : 1f - progress);
                    var phase = elapsed * _tuning.healthShakeFrequency * Mathf.PI * 2f;
                    shake = new Vector2(Mathf.Sin(phase + i * 0.8f), Mathf.Sin(phase * 1.3f + i * 1.1f)) * strength;
                    active = true;
                }
                active |= fade < 1f;
                _hearts[i].style.left = left + width * (i + 0.5f) / HeartCount - _tuning.heartWidth * 0.5f;
                _hearts[i].style.top = (contentRect.height - _tuning.heartHeight) * 0.5f;
                _hearts[i].style.translate = new Translate(shake.x, shake.y);
            }
            if (_dirty || active || _wasAnimating) MarkDirtyRepaint();
            _wasAnimating = active;
            _dirty = false;
        }

        private void OnHealthChanged(int current, int maximum)
        {
            var next = maximum > 0 ? Mathf.Clamp01((float)current / maximum) : 0f;
            var previousHearts = Mathf.CeilToInt(_targetFill * HeartCount - 0.0001f);
            var nextHearts = Mathf.CeilToInt(next * HeartCount - 0.0001f);
            var now = Time.unscaledTime;
            _displayedFill = _fillFrom = EvaluateFill(now);
            _targetFill = next;
            _fillStarted = now;
            for (var i = 0; i < HeartCount; i++)
            {
                if (i >= nextHearts && i < previousHearts) _damagedAt[i] = now;
                else if (i < nextHearts) _damagedAt[i] = float.NegativeInfinity;
            }
            _dirty = true;
        }

        private void ResetPresentation()
        {
            _targetFill = _health != null && _health.MaxHealth > 0 ? Mathf.Clamp01((float)_health.CurrentHealth / _health.MaxHealth) : 0f;
            _displayedFill = _fillFrom = _targetFill;
            _fillStarted = Time.unscaledTime;
            for (var i = 0; i < HeartCount; i++) _damagedAt[i] = float.NegativeInfinity;
            _dirty = true;
        }

        private float EvaluateFill(float now)
        {
            var progress = _tuning.healthMeterDuration > 0f ? Mathf.Clamp01((now - _fillStarted) / _tuning.healthMeterDuration) : 1f;
            return progress >= 1f ? _targetFill : Mathf.Lerp(_fillFrom, _targetFill, Mathf.Clamp01(_tuning.healthMeterEase != null ? _tuning.healthMeterEase.Evaluate(progress) : progress));
        }

        private void PaintHeart(MeshGenerationContext context, int index)
        {
            if (_tuning == null) return;
            var painter = context.painter2D;
            var w = _hearts[index].contentRect.width;
            var h = _hearts[index].contentRect.height;
            if (w <= 0f || h <= 0f) return;
            var center = w * 0.5f;
            var notch = Mathf.Min(_tuning.heartNotchDepth, h * 0.35f);
            var taper = _tuning.heartBottomTaper;
            painter.fillColor = Color.Lerp(_filled[index] ? _accent : _tuning.missingHeartColor, Color.white, _flashAmounts[index]);
            painter.strokeColor = Color.Lerp(_tuning.heartOutlineColor, Color.white, _flashAmounts[index]);
            painter.lineWidth = _tuning.heartOutlineWidth;
            painter.lineJoin = LineJoin.Round;
            painter.BeginPath();
            painter.MoveTo(new Vector2(center, notch));
            painter.BezierCurveTo(new Vector2(center - w * 0.16f, -notch * 0.5f), new Vector2(0f, -notch * 0.5f), new Vector2(0f, h * 0.32f));
            painter.BezierCurveTo(new Vector2(0f, h * 0.53f), new Vector2(center - w * 0.32f, h * 0.7f), new Vector2(center - w * taper, h * 0.8f));
            painter.LineTo(new Vector2(center, h));
            painter.LineTo(new Vector2(center + w * taper, h * 0.8f));
            painter.BezierCurveTo(new Vector2(center + w * 0.32f, h * 0.7f), new Vector2(w, h * 0.53f), new Vector2(w, h * 0.32f));
            painter.BezierCurveTo(new Vector2(w, -notch * 0.5f), new Vector2(center + w * 0.16f, -notch * 0.5f), new Vector2(center, notch));
            painter.ClosePath(); painter.Fill();
            if (_tuning.heartOutlineWidth > 0f) painter.Stroke();
        }

        private void PaintBar(MeshGenerationContext context)
        {
            if (_tuning == null) return;
            var painter = context.painter2D;
            var width = _tuning.healthBarWidth > 0f ? _tuning.healthBarWidth
                : _tuning.iconSize * 4f + _tuning.dodgeIconSize + _tuning.iconGap * 4f;
            var start = (contentRect.width - width) * 0.5f;
            var height = _tuning.healthBarHeight;
            var top = (contentRect.height - height) * 0.5f;
            var border = _tuning.healthBorderWidth;
            var innerHeight = Mathf.Max(0f, height - border * 2f);
            var innerWidth = Mathf.Max(0f, width - border * 2f);
            painter.fillColor = Color.black;
            RoundedRect(start, top, width, height, 3f);
            painter.fillColor = _tuning.emptyHeart;
            RoundedRect(start + border, top + border, innerWidth, innerHeight, 2f);
            painter.fillColor = _accent;
            RoundedRect(start + border, top + border, innerWidth * _displayedFill, innerHeight, 2f);
            for (var i = 0; i < HeartCount; i++)
            {
                var elapsed = Time.unscaledTime - _damagedAt[i];
                if (elapsed < _tuning.healthDamageFadeDuration)
                {
                    var progress = elapsed / _tuning.healthDamageFadeDuration;
                    var fade = Mathf.Clamp01(_tuning.healthDamageFadeEase != null ? _tuning.healthDamageFadeEase.Evaluate(progress) : progress);
                    var color = Color.Lerp(Color.white, _tuning.emptyHeart, fade);
                    color.a = 1f - fade;
                    painter.fillColor = color;
                    RoundedRect(start + width * i / HeartCount + border, top + border,
                        width / HeartCount - border * 2f, innerHeight, 1f);
                }
                if (i == 0) continue;
                painter.strokeColor = Color.black;
                painter.lineWidth = border;
                painter.BeginPath(); painter.MoveTo(new Vector2(start + width * i / HeartCount, top));
                painter.LineTo(new Vector2(start + width * i / HeartCount, top + height)); painter.Stroke();
            }

            void RoundedRect(float x, float y, float w, float h, float radius)
            {
                if (w <= 0f || h <= 0f) return;
                radius = Mathf.Min(radius, Mathf.Min(w, h) * 0.5f);
                painter.BeginPath(); painter.MoveTo(new Vector2(x + radius, y));
                painter.LineTo(new Vector2(x + w - radius, y)); painter.QuadraticCurveTo(new Vector2(x + w, y), new Vector2(x + w, y + radius));
                painter.LineTo(new Vector2(x + w, y + h - radius)); painter.QuadraticCurveTo(new Vector2(x + w, y + h), new Vector2(x + w - radius, y + h));
                painter.LineTo(new Vector2(x + radius, y + h)); painter.QuadraticCurveTo(new Vector2(x, y + h), new Vector2(x, y + h - radius));
                painter.LineTo(new Vector2(x, y + radius)); painter.QuadraticCurveTo(new Vector2(x, y), new Vector2(x + radius, y));
                painter.ClosePath(); painter.Fill();
            }
        }
    }
}
