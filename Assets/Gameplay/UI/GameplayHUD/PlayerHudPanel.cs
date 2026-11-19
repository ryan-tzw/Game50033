namespace Game50033
{
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.UIElements;

    public sealed class PlayerHudPanel : VisualElement
    {
        private enum PanelMode { Selecting, Ready, Gameplay }
        private static readonly PlayerAbility[] PreviewSlots =
            { PlayerAbility.Primary, PlayerAbility.Secondary, PlayerAbility.Special, PlayerAbility.Defensive };
        private Player _player;
        private PlayerInput _input;
        private PlayerCharacterSelect _selector;
        private readonly GameplayHudTuning _tuning;
        private readonly HudAbilityIcon[] _icons = new HudAbilityIcon[5];
        private readonly HudAbilityIcon[] _previews = new HudAbilityIcon[4];
        private readonly Label[] _previewTitles = new Label[4];
        private readonly Label[] _previewTypes = new Label[4];
        private readonly Label[] _previewDescriptions = new Label[4];
        private readonly PlayerAbility[] _slots = new PlayerAbility[4];
        private readonly VisualElement _hearts, _selection, _gameplay, _rosterRow;
        private readonly Label _highlightName, _selectionStatus;
        private VisualElement[] _portraits = System.Array.Empty<VisualElement>();
        private CharacterRoster _shownRoster;
        private CharacterDefinition _shownCharacter;
        private int _shownHighlight = -1, _shownTaken = -1, _shownPreviewRevision = -1;
        private string _scheme;
        private int _health = -1;
        private Color _accent;
        private PanelMode? _mode;

        private readonly VisualElement _inspector;
        private readonly Label _inspectHint, _inspectName, _inspectType, _inspectDescription;
        private readonly VisualElement[] _inspectMarkers = new VisualElement[4];
        private readonly (VisualElement Row, Label Name, Label Value)[] _inspectStats = new (VisualElement, Label, Label)[3];
        private InputActionAsset _inspectInputAsset;
        private InputAction _inspectHold, _inspectPrevious, _inspectNext;
        private bool _inspecting;
        private int _inspectSlot;
        private int _renderedInspectSlot = -1;

        public Player BoundPlayer => _player;

        public PlayerHudPanel(Player player, GameplayHudTuning tuning, VisualTreeAsset template)
        {
            _tuning = tuning;
            pickingMode = PickingMode.Ignore;
            AddToClassList("player-hud-panel");
            template.CloneTree(this);
            for (var i = 0; i < 5; i++) _icons[i] = this.Q<HudAbilityIcon>("slot-" + i);
            for (var i = 0; i < 4; i++)
            {
                _previews[i] = this.Q<HudAbilityIcon>("preview-icon-" + i);
                _previewTitles[i] = this.Q<Label>("preview-title-" + i);
                _previewTypes[i] = this.Q<Label>("preview-type-" + i);
                _previewDescriptions[i] = this.Q<Label>("preview-description-" + i);
            }
            _hearts = this.Q("hearts");
            _selection = this.Q("selection");
            _gameplay = this.Q("gameplay");
            _rosterRow = this.Q("roster");
            _highlightName = this.Q<Label>("highlight-name");
            _selectionStatus = this.Q<Label>("selection-status");
            _inspector = this.Q("ability-inspector");
            _inspectHint = this.Q<Label>("inspect-hint");
            _inspectName = this.Q<Label>("inspect-name");
            _inspectType = this.Q<Label>("inspect-type");
            _inspectDescription = this.Q<Label>("inspect-description");
            for (var i = 0; i < _inspectStats.Length; i++)
                _inspectStats[i] = (this.Q("inspect-stat-" + i), this.Q<Label>("inspect-stat-name-" + i),
                    this.Q<Label>("inspect-stat-value-" + i));
            for (var i = 0; i < _inspectMarkers.Length; i++)
            {
                var slot = i;
                var marker = new VisualElement { pickingMode = PickingMode.Ignore };
                marker.AddToClassList("ability-inspect-marker");
                marker.style.display = DisplayStyle.None;
                _inspectMarkers[i] = marker;
                _icons[i].Insert(0, marker);
                _icons[i].RegisterCallback<PointerEnterEvent>(_ =>
                {
                    if (_inspecting && _scheme == "Keyboard&Mouse") _inspectSlot = slot;
                });
            }

            _hearts.generateVisualContent += PaintHearts;
            ApplyTuning();
            BindPlayer(player);
        }

        public void BindPlayer(Player player)
        {
            _player = player;
            _input = player.GetComponent<PlayerInput>();
            _selector = player.GetComponent<PlayerCharacterSelect>();
            _scheme = null;
            _shownCharacter = null;
            _shownHighlight = _shownTaken = _health = -1;
            _mode = null;
            _inspecting = false;
            _inspector.style.display = DisplayStyle.None;
            for (var i = 0; i < _inspectMarkers.Length; i++)
            {
                _icons[i].pickingMode = PickingMode.Ignore;
                _inspectMarkers[i].style.display = DisplayStyle.None;
            }
            _inspectSlot = 0;
            _renderedInspectSlot = -1;
        }

        public void ApplyTuning()
        {
            // theme text elements can override the inherited font definition, so apply the authored font to each label.
            this.Query<TextElement>().ForEach(text => text.style.unityFontDefinition = FontDefinition.FromFont(_tuning.font));
            var width = Mathf.Max(_tuning.iconSize * 4 + _tuning.dodgeIconSize + _tuning.iconGap * 4, _tuning.healthBarWidth);
            var heartRowHeight = Mathf.Max(_tuning.heartHeight + _tuning.healthBorderWidth * 2f, _tuning.healthBarHeight) + 2f;
            var abilityRowHeight = Mathf.Max(_tuning.iconSize, _tuning.dodgeIconSize);
            var heartGap = _tuning.promptHeight * 0.5f + 8f;
            style.width = width + _tuning.panelPadding * 2;
            style.paddingLeft = style.paddingRight = _tuning.panelPadding;
            style.paddingTop = 10f;
            style.marginLeft = style.marginRight = _tuning.playerGap * 0.5f;
            _gameplay.style.left = _gameplay.style.right = _tuning.panelPadding;
            _gameplay.style.bottom = 0f;
            _gameplay.style.height = abilityRowHeight + heartGap + heartRowHeight;
            this.Q("abilities").style.height = abilityRowHeight;
            this.Q("abilities").style.justifyContent = Justify.Center;
            _hearts.style.height = heartRowHeight;
            _hearts.style.marginTop = heartGap;
            for (var i = 0; i < 5; i++)
            {
                _icons[i].Configure(_tuning, i == 4);
                if (i < 4) _icons[i].style.marginRight = _tuning.iconGap;
            }
            for (var i = 0; i < 4; i++)
            {
                _previews[i].Configure(_tuning, false);
                _previews[i].style.alignSelf = Align.FlexStart;
                _previews[i].SetState(new PlayerAbilityState(0, 1));
                var row = this.Q("preview-row-" + i);
                row.style.paddingLeft = _tuning.previewIconInset;
                row.style.paddingTop = _tuning.previewIconInset;
                row.style.alignItems = Align.FlexStart;
                row.style.height = _tuning.previewRowHeight;
                row.style.marginBottom = _tuning.iconGap;
                _previewTitles[i].style.fontSize = _tuning.previewFontSize * 1.2f;
                _previewTypes[i].text = PreviewSlots[i].ToString();
                _previewTypes[i].style.fontSize = _tuning.previewFontSize * 0.85f;
                _previewDescriptions[i].style.fontSize = _tuning.previewFontSize;
            }
            var inspectPadding = _tuning.inspectPadding;
            _inspector.style.paddingLeft = _inspector.style.paddingRight = inspectPadding;
            _inspector.style.paddingTop = _inspector.style.paddingBottom = inspectPadding;
            _inspector.style.backgroundColor = _tuning.inspectBackground;
            _inspectHint.style.fontSize = _inspectType.style.fontSize = _tuning.previewFontSize * 0.85f;
            _inspectName.style.fontSize = _tuning.previewFontSize * 1.35f;
            _inspectDescription.style.fontSize = _tuning.previewFontSize;
            foreach (var stat in _inspectStats)
            {
                stat.Name.style.fontSize = _tuning.previewFontSize;
                stat.Value.style.fontSize = _tuning.inspectStatFontSize;
            }
            _renderedInspectSlot = -1;
            _highlightName.style.height = 30f;
            _highlightName.style.fontSize = _tuning.previewFontSize * 1.3f;
            _rosterRow.style.height = _tuning.portraitSize;
            _selectionStatus.style.height = 22f;
            _selectionStatus.style.fontSize = _tuning.previewFontSize * 0.85f;
            _shownRoster = null;
            _scheme = null;
            _mode = null;
            MarkDirtyRepaint();
            _hearts.MarkDirtyRepaint();
        }

        public void Refresh(LobbyManager lobby = null)
        {
            if (_player == null) return;
            var mode = lobby == null ? PanelMode.Gameplay
                : _selector != null && _selector.State == PlayerCharacterSelect.SelectState.Selecting ? PanelMode.Selecting : PanelMode.Ready;
            if (_mode != mode)
            {
                _mode = mode;
                _renderedInspectSlot = -1;
                _inspector.style.bottom = _gameplay.style.height.value.value + _tuning.inspectGap;
                _shownCharacter = null;
                _selection.style.display = mode == PanelMode.Selecting ? DisplayStyle.Flex : DisplayStyle.None;
                _gameplay.style.display = mode != PanelMode.Selecting ? DisplayStyle.Flex : DisplayStyle.None;
                style.height = mode == PanelMode.Selecting
                    ? (4 * (_tuning.previewRowHeight + _tuning.iconGap) + _tuning.portraitSize + 72f)
                    : _gameplay.style.height.value.value + 18f;
                MarkDirtyRepaint();
            }
            var scheme = _input != null ? _input.currentControlScheme : string.Empty;
            var chosen = _player.Character;
            var accent = mode != PanelMode.Selecting && chosen != null ? chosen.Tint : _player.PlayerColor;
            if (_scheme != scheme || _accent != accent || mode != PanelMode.Selecting
                && (_shownCharacter != chosen || chosen != null && _shownPreviewRevision != chosen.PreviewRevision))
            {
                _scheme = scheme;
                _renderedInspectSlot = -1;
                _accent = accent;
                _shownCharacter = null;
                _shownHighlight = -1;
                var keyboard = scheme == "Keyboard&Mouse";
                _slots[0] = PlayerAbility.Primary;
                _slots[1] = PlayerAbility.Secondary;
                _slots[2] = keyboard ? PlayerAbility.Defensive : PlayerAbility.Special;
                _slots[3] = keyboard ? PlayerAbility.Special : PlayerAbility.Defensive;
                for (var i = 0; i < 5; i++)
                {
                    var text = keyboard ? i == 2 ? "Q" : i == 3 ? "E" : string.Empty
                        : i == 0 ? "X" : i == 1 ? "Y" : i == 2 ? "A" : i == 3 ? "B" : "RT";
                    var svg = keyboard ? i == 0 ? _tuning.leftMouse : i == 1 ? _tuning.rightMouse : i == 4 ? _tuning.space : null : null;
                    var artwork = i == 4 ? _tuning.dodgeIcon : chosen != null ? chosen.GetAbilityPreview(_slots[i]).icon : null;
                    _icons[i].SetAppearance(accent, artwork, i == 4, text, svg, !keyboard && i < 4);
                }
                _shownRoster = null;
                MarkDirtyRepaint();
                _hearts.MarkDirtyRepaint();
            }

            RefreshInspector(mode, scheme);

            if (mode == PanelMode.Selecting)
            {
                var roster = lobby.Roster;
                if (_shownRoster != roster || _portraits.Length != roster.Count)
                {
                    _shownRoster = roster;
                    _rosterRow.Clear();
                    _portraits = new VisualElement[roster.Count];
                    for (var i = 0; i < roster.Count; i++)
                    {
                        var characterIndex = i;
                        var frame = new VisualElement
                        {
                            name = "character-" + i,
                            pickingMode = _scheme == "Keyboard&Mouse" ? PickingMode.Position : PickingMode.Ignore
                        };
                        frame.RegisterCallback<PointerEnterEvent>(_ =>
                        {
                            if (_input != null && _input.currentControlScheme == "Keyboard&Mouse" && _selector != null)
                                _selector.PreviewCharacter(characterIndex);
                        });
                        frame.RegisterCallback<ClickEvent>(evt =>
                        {
                            if (evt.button != 0 || _input == null || _input.currentControlScheme != "Keyboard&Mouse" || _selector == null) return;
                            _selector.PreviewCharacter(characterIndex);
                            _selector.TryConfirm();
                            evt.StopPropagation();
                        });
                        frame.AddToClassList("roster-portrait");
                        frame.style.width = frame.style.height = _tuning.portraitSize;
                        frame.style.marginLeft = frame.style.marginRight = 3f;
                        var portrait = new Image { pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleToFit };
                        if (roster[i] != null) { portrait.sprite = roster[i].Sprite; portrait.tintColor = roster[i].Tint; }
                        frame.Add(portrait);
                        _portraits[i] = frame;
                        _rosterRow.Add(frame);
                    }
                    _shownHighlight = _shownTaken = -1;
                }
                if (roster.Count == 0)
                {
                    _highlightName.text = string.Empty;
                    _selectionStatus.text = "No characters configured";
                    return;
                }
                var highlight = Mathf.Clamp(_selector.HighlightIndex, 0, roster.Count - 1);
                var character = roster[highlight];
                var taken = 0;
                for (var i = 0; i < roster.Count; i++)
                    if (lobby.IsClaimedByOther(roster[i], _selector)) taken |= 1 << i;
                if (_shownHighlight != highlight || _shownTaken != taken)
                {
                    _shownHighlight = highlight;
                    _shownTaken = taken;
                    for (var i = 0; i < _portraits.Length; i++)
                    {
                        var frame = _portraits[i];
                        frame.style.opacity = (taken & (1 << i)) != 0 ? 0.3f : 1f;
                        var border = i == highlight ? Color.Lerp(accent, Color.white, 0.65f) : new Color(0.2f, 0.23f, 0.3f);
                        frame.style.borderTopColor = frame.style.borderBottomColor = border;
                        frame.style.borderLeftColor = frame.style.borderRightColor = border;
                    }
                    _selectionStatus.text = (taken & (1 << highlight)) != 0 ? "Taken by another player" : string.Empty;
                }
                if (_shownCharacter != character || character != null && _shownPreviewRevision != character.PreviewRevision)
                {
                    _shownCharacter = character;
                    _shownPreviewRevision = character != null ? character.PreviewRevision : -1;
                    _highlightName.text = character != null ? character.DisplayName : "Unavailable";
                    var previewAccent = character != null ? character.Tint : accent;
                    var titleColor = Color.Lerp(previewAccent, Color.white, 0.3f);
                    var keyboard = scheme == "Keyboard&Mouse";
                    _highlightName.style.color = titleColor;
                    for (var i = 0; i < 4; i++)
                    {
                        var ability = PreviewSlots[i];
                        var prompt = keyboard ? i == 2 ? "E" : i == 3 ? "Q" : string.Empty
                            : i == 0 ? "X" : i == 1 ? "Y" : i == 2 ? "A" : "B";
                        var image = keyboard ? i == 0 ? _tuning.leftMouse : i == 1 ? _tuning.rightMouse : null : null;
                        var preview = character != null ? character.GetAbilityPreview(ability) : default;
                        _previews[i].SetAppearance(previewAccent, preview.icon, false, prompt, image, !keyboard);
                        _previewTitles[i].style.color = titleColor;
                        _previewTitles[i].text = string.IsNullOrWhiteSpace(preview.name) ? ability.ToString() : preview.name;
                        _previewDescriptions[i].text = string.IsNullOrWhiteSpace(preview.description) ? "Details coming soon." : preview.description;
                    }
                }
                return;
            }

            if (_shownCharacter != chosen)
            {
                _shownCharacter = chosen;
                _shownPreviewRevision = chosen != null ? chosen.PreviewRevision : -1;
            }
            var health = _player.Health != null ? _player.Health.CurrentHealth : 0;
            if (_health != health) { _health = health; _hearts.MarkDirtyRepaint(); }
            for (var i = 0; i < 4; i++)
                _icons[i].SetState(_player.Abilities != null ? _player.Abilities.GetAbilityState(_slots[i]) : default);
            var movement = _player.Movement;
            _icons[4].SetState(movement != null ? new PlayerAbilityState(movement.DodgeRechargeRemaining,
                movement.DodgeRechargeTime, movement.DodgeCharges, movement.MaxDodgeCharges) : default);
        }

        private void RefreshInspector(PanelMode mode, string scheme)
        {
            // use the paired action asset again after a player rebinds.
            var actions = _input != null ? _input.actions : null;
            if (_inspectInputAsset != actions)
            {
                _inspectInputAsset = actions;
                _inspectHold = actions?.FindAction("Player/InspectAbilities");
                _inspectPrevious = actions?.FindAction("Player/InspectPrevious");
                _inspectNext = actions?.FindAction("Player/InspectNext");
            }

            var keyboard = scheme == "Keyboard&Mouse";
            var available = mode != PanelMode.Selecting && _player.Character != null && _player.isActiveAndEnabled
                && _player.HasActiveInput && _inspectHold != null && _inspectHold.enabled;
            _inspectHint.style.display = available ? DisplayStyle.Flex : DisplayStyle.None;
            var gesture = _tuning.toggleAbilityInspection ? "Press" : "Hold";
            _inspectHint.text = keyboard ? gesture + " Tab · Abilities" : gesture + " LT · Abilities";
            var open = available && (_tuning.toggleAbilityInspection
                ? _inspecting ^ _inspectHold.WasPressedThisFrame()
                : _inspectHold.IsPressed());
            if (open != _inspecting || open && _renderedInspectSlot < 0)
            {
                _inspecting = open;
                _inspector.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
                _renderedInspectSlot = -1;
                for (var i = 0; i < _inspectMarkers.Length; i++)
                {
                    _icons[i].pickingMode = open && keyboard ? PickingMode.Position : PickingMode.Ignore;
                    if (!open) _inspectMarkers[i].style.display = DisplayStyle.None;
                }
            }

            if (!open) return;

            if (!keyboard)
            {
                if (_inspectPrevious?.WasPressedThisFrame() == true) _inspectSlot = (_inspectSlot + 3) % 4;
                if (_inspectNext?.WasPressedThisFrame() == true) _inspectSlot = (_inspectSlot + 1) % 4;
            }

            if (_renderedInspectSlot == _inspectSlot) return;
            _renderedInspectSlot = _inspectSlot;
            var ability = _slots[_inspectSlot];
            var character = _player.Character;
            var preview = character.GetAbilityPreview(ability);
            var stats = _player.Abilities != null ? _player.Abilities.GetAbilityStats(ability) : default;
            var accent = Color.Lerp(character.Tint, Color.white, 0.25f);

            _inspectName.text = string.IsNullOrEmpty(preview.name) ? ability.ToString() : preview.name;
            _inspectName.style.color = accent;
            _inspectType.text = ability.ToString();
            _inspectDescription.text = preview.description;
            _inspector.style.borderTopColor = _inspector.style.borderBottomColor = accent;
            _inspector.style.borderLeftColor = _inspector.style.borderRightColor = accent;

            for (var i = 0; i < _inspectMarkers.Length; i++)
            {
                var marker = _inspectMarkers[i];
                marker.style.display = i == _inspectSlot ? DisplayStyle.Flex : DisplayStyle.None;
                marker.style.borderTopColor = marker.style.borderBottomColor = accent;
                marker.style.borderLeftColor = marker.style.borderRightColor = accent;
            }

            for (var i = 0; i < _inspectStats.Length; i++)
            {
                var stat = _inspectStats[i];
                var entry = i == 0 ? stats.First : i == 1 ? stats.Second : stats.Third;
                stat.Row.style.display = string.IsNullOrEmpty(entry.Label) ? DisplayStyle.None : DisplayStyle.Flex;
                stat.Name.text = entry.Label;
                stat.Value.text = entry.Value;
                stat.Value.style.color = accent;
            }
        }

        private void PaintHearts(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            var rowWidth = _hearts.contentRect.width;
            var width = _tuning.healthBarWidth > 0 ? _tuning.healthBarWidth
                : _tuning.iconSize * 4f + _tuning.dodgeIconSize + _tuning.iconGap * 4f;
            var start = (rowWidth - width) * 0.5f;
            var barHeight = _tuning.healthBarHeight;
            var y = _hearts.contentRect.height * 0.5f;
            var barY = y - barHeight * 0.5f;
            var border = _tuning.healthBorderWidth;
            var sectionWidth = width / 5f;

            // one continuous backing joins the five HP sections, with black dividers between them.
            painter.fillColor = Color.black;
            RoundedRect(start, barY, width, barHeight, 3f);
            for (var i = 0; i < 5; i++)
            {
                var filled = i < _health;
                painter.fillColor = filled ? _accent : _tuning.emptyHeart;
                var left = start + i * sectionWidth + border;
                var right = start + (i + 1) * sectionWidth - border;
                RoundedRect(left, barY + border, right - left, Mathf.Max(1f, barHeight - border * 2f), 2f);
            }

            for (var i = 0; i < 5; i++)
            {
                var x = start + sectionWidth * (i + 0.5f);
                var w = _tuning.heartWidth;
                var h = _tuning.heartHeight;
                var notch = Mathf.Min(_tuning.heartNotchDepth, h * 0.5f);
                var taper = _tuning.heartBottomTaper;
                painter.fillColor = i < _health ? _accent : _tuning.emptyHeart;
                painter.strokeColor = Color.black;
                painter.lineWidth = border;
                painter.lineJoin = LineJoin.Round;
                painter.BeginPath(); painter.MoveTo(new Vector2(x, y - h * 0.5f + notch));
                painter.BezierCurveTo(new Vector2(x - w * 0.15f, y - h * 0.65f), new Vector2(x - w * 0.5f, y - h * 0.65f), new Vector2(x - w * 0.5f, y - h * 0.15f));
                painter.BezierCurveTo(new Vector2(x - w * 0.5f, y + h * 0.08f), new Vector2(x - w * taper, y + h * 0.25f), new Vector2(x - w * 0.04f, y + h * 0.47f));
                painter.QuadraticCurveTo(new Vector2(x, y + h * 0.52f), new Vector2(x + w * 0.04f, y + h * 0.47f));
                painter.BezierCurveTo(new Vector2(x + w * taper, y + h * 0.25f), new Vector2(x + w * 0.5f, y + h * 0.08f), new Vector2(x + w * 0.5f, y - h * 0.15f));
                painter.BezierCurveTo(new Vector2(x + w * 0.5f, y - h * 0.65f), new Vector2(x + w * 0.15f, y - h * 0.65f), new Vector2(x, y - h * 0.5f + notch));
                painter.ClosePath(); painter.Fill(); painter.Stroke();
            }

            void RoundedRect(float x, float y, float w, float h, float radius)
            {
                radius = Mathf.Min(radius, h * 0.5f);
                painter.BeginPath();
                painter.MoveTo(new Vector2(x + radius, y));
                painter.LineTo(new Vector2(x + w - radius, y));
                painter.QuadraticCurveTo(new Vector2(x + w, y), new Vector2(x + w, y + radius));
                painter.LineTo(new Vector2(x + w, y + h - radius));
                painter.QuadraticCurveTo(new Vector2(x + w, y + h), new Vector2(x + w - radius, y + h));
                painter.LineTo(new Vector2(x + radius, y + h));
                painter.QuadraticCurveTo(new Vector2(x, y + h), new Vector2(x, y + h - radius));
                painter.LineTo(new Vector2(x, y + radius));
                painter.QuadraticCurveTo(new Vector2(x, y), new Vector2(x + radius, y));
                painter.ClosePath(); painter.Fill();
            }
        }

    }
}
