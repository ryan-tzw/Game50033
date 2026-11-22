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
        private readonly VisualElement _selection, _gameplay, _rosterRow;
        private readonly HudHealthBar _hearts;
        private readonly Label _highlightName, _selectionStatus;
        private readonly Image _instrument;
        private readonly HudAbilityIcon _inspectArtwork;
        private readonly VisualElement[] _surfaces;
        private VisualElement[] _portraits = System.Array.Empty<VisualElement>();
        private CharacterRoster _shownRoster;
        private CharacterDefinition _shownCharacter;
        private int _shownHighlight = -1, _shownTaken = -1, _shownPreviewRevision = -1;
        private string _scheme;
        private Color _accent;
        private PanelMode? _mode;

        private readonly VisualElement _inspector;
        private readonly VisualElement _inspectHint;
        private readonly Label _inspectKey, _inspectCaption, _inspectName, _inspectType, _inspectDescription;
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
            _hearts = this.Q<HudHealthBar>("hearts");
            _selection = this.Q("selection");
            _gameplay = this.Q("gameplay");
            _rosterRow = this.Q("roster");
            _highlightName = this.Q<Label>("highlight-name");
            _selectionStatus = this.Q<Label>("selection-status");
            _inspector = this.Q("ability-inspector");
            _instrument = this.Q<Image>("instrument-icon");
            _inspectArtwork = this.Q<HudAbilityIcon>("inspect-icon");
            _surfaces = new[] { _selection, _inspector };
            _inspectHint = this.Q("inspect-hint");
            _inspectKey = this.Q<Label>("inspect-key");
            _inspectCaption = this.Q<Label>("inspect-caption");
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
            _shownHighlight = _shownTaken = -1;
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
            var headingFont = FontDefinition.FromFont(_tuning.headingFont != null ? _tuning.headingFont : _tuning.font);
            var labelFont = FontDefinition.FromFont(_tuning.promptFont != null ? _tuning.promptFont : _tuning.font);
            var iconWidth = _tuning.iconSize * 4 + _tuning.dodgeIconSize + _tuning.iconGap * 4;
            var heartRowHeight = Mathf.Max(_tuning.heartHeight + _tuning.heartOutlineWidth * 2f, _tuning.healthBarHeight) + 2f;
            var abilityRowHeight = Mathf.Max(_tuning.iconSize, _tuning.dodgeIconSize);
            var heartGap = _tuning.promptHeight * 0.5f + 8f;
            var controlsHeight = abilityRowHeight + heartGap + heartRowHeight;
            var groupWidth = Mathf.Max(iconWidth, _tuning.healthBarWidth) + _tuning.instrumentSize + _tuning.instrumentGap;
            style.width = _tuning.columnWidth;
            style.height = StyleKeyword.Auto;
            style.marginLeft = style.marginRight = _tuning.playerGap * 0.5f;
            foreach (var surface in _surfaces)
            {
                surface.style.paddingLeft = surface.style.paddingRight = _tuning.panelPadding;
                surface.style.paddingTop = surface.style.paddingBottom = _tuning.panelPadding;
                surface.style.backgroundColor = _tuning.panelBackground;
                surface.style.borderTopWidth = surface.style.borderBottomWidth = _tuning.panelBorderWidth;
                surface.style.borderLeftWidth = surface.style.borderRightWidth = _tuning.panelBorderWidth;
                surface.style.borderTopLeftRadius = surface.style.borderTopRightRadius = _tuning.panelCornerRadius;
                surface.style.borderBottomLeftRadius = surface.style.borderBottomRightRadius = _tuning.panelCornerRadius;
            }
            _gameplay.style.height = controlsHeight;
            var group = this.Q("gameplay-group");
            group.style.width = groupWidth;
            group.style.height = controlsHeight;
            group.style.left = (_tuning.columnWidth - groupWidth) * 0.5f;
            group.style.bottom = 0f;
            var controls = this.Q("gameplay-controls");
            controls.style.width = Mathf.Max(iconWidth, _tuning.healthBarWidth);
            controls.style.height = controlsHeight;
            this.Q("abilities").style.height = abilityRowHeight;
            this.Q("abilities").style.justifyContent = Justify.Center;
            _hearts.style.height = heartRowHeight;
            _hearts.style.marginTop = heartGap;
            _instrument.style.width = _instrument.style.height = _tuning.instrumentSize;
            _instrument.style.bottom = 1f;
            _inspectHint.style.width = _tuning.instrumentSize;
            _inspectKey.style.unityFontDefinition = labelFont;
            _inspectKey.style.fontSize = _tuning.promptFontSize;
            _inspectKey.parent.style.height = _tuning.promptHeight;
            _inspectKey.parent.style.backgroundColor = _tuning.promptBackground;
            _inspectKey.parent.style.borderTopWidth = _inspectKey.parent.style.borderBottomWidth = _tuning.promptBorderWidth;
            _inspectKey.parent.style.borderLeftWidth = _inspectKey.parent.style.borderRightWidth = _tuning.promptBorderWidth;
            _inspector.style.marginBottom = _tuning.inspectGap;
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
                row.style.paddingTop = row.style.paddingBottom = _tuning.previewIconInset;
                row.style.alignItems = Align.FlexStart;
                row.style.height = StyleKeyword.Auto;
                row.style.minHeight = _tuning.previewRowHeight;
                row.style.marginBottom = _tuning.iconGap;
                _previewTitles[i].style.unityFontDefinition = headingFont;
                _previewTitles[i].style.unityFontStyleAndWeight = FontStyle.Normal;
                _previewTypes[i].style.unityFontDefinition = labelFont;
                _previewTitles[i].style.fontSize = _tuning.previewFontSize * 1.2f;
                _previewTypes[i].text = PreviewSlots[i].ToString();
                _previewTypes[i].style.fontSize = _tuning.previewFontSize * 0.85f;
                _previewDescriptions[i].style.fontSize = _tuning.previewFontSize;
            }
            var inspectPadding = _tuning.inspectPadding;
            _inspector.style.paddingLeft = _inspector.style.paddingRight = inspectPadding;
            _inspector.style.paddingTop = _inspector.style.paddingBottom = inspectPadding;
            _inspector.style.backgroundColor = _tuning.inspectBackground;
            _inspectCaption.style.fontSize = _inspectType.style.fontSize = _tuning.previewFontSize * 0.85f;
            _inspectArtwork.Configure(_tuning, false, _tuning.inspectIconSize, false);
            _inspectArtwork.style.alignSelf = Align.FlexStart;
            _inspectArtwork.SetState(new PlayerAbilityState(0f, 1f));
            _inspectName.style.unityFontDefinition = headingFont;
            _inspectName.style.unityFontStyleAndWeight = FontStyle.Normal;
            _inspectType.style.unityFontDefinition = labelFont;
            _inspectName.style.fontSize = _tuning.previewFontSize * 1.35f;
            _inspectDescription.style.fontSize = _tuning.previewFontSize;
            foreach (var stat in _inspectStats)
            {
                stat.Name.style.fontSize = _tuning.previewFontSize;
                stat.Value.style.fontSize = _tuning.inspectStatFontSize;
            }
            _renderedInspectSlot = -1;
            _highlightName.style.unityFontDefinition = headingFont;
            _highlightName.style.unityFontStyleAndWeight = FontStyle.Normal;
            _highlightName.style.height = 34f;
            _highlightName.style.fontSize = _tuning.previewFontSize * 1.3f;
            _rosterRow.style.height = _tuning.portraitSize;
            _selectionStatus.style.height = 22f;
            _selectionStatus.style.fontSize = _tuning.previewFontSize * 0.85f;
            _shownRoster = null;
            _scheme = null;
            _mode = null;
            MarkDirtyRepaint();
            _hearts.Configure(_tuning);
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
                _shownCharacter = null;
                _selection.style.display = mode == PanelMode.Selecting ? DisplayStyle.Flex : DisplayStyle.None;
                _gameplay.style.display = mode != PanelMode.Selecting ? DisplayStyle.Flex : DisplayStyle.None;
                MarkDirtyRepaint();
            }
            var scheme = _input != null ? _input.currentControlScheme : string.Empty;
            var chosen = _player.Character;
            var previewed = mode == PanelMode.Selecting && lobby.Roster != null && lobby.Roster.Count > 0
                ? lobby.Roster[Mathf.Clamp(_selector.HighlightIndex, 0, lobby.Roster.Count - 1)] : chosen;
            var accent = previewed != null ? previewed.Tint : _player.PlayerColor;
            _hearts.Refresh(_player.Health, accent);
            if (_scheme != scheme || _accent != accent || mode != PanelMode.Selecting
                && (_shownCharacter != chosen || chosen != null && _shownPreviewRevision != chosen.PreviewRevision))
            {
                _scheme = scheme;
                _renderedInspectSlot = -1;
                _accent = accent;
                _shownCharacter = null;
                _shownHighlight = -1;
                foreach (var surface in _surfaces)
                {
                    var borderColor = Color.Lerp(accent, Color.white, 0.18f);
                    surface.style.borderTopColor = surface.style.borderBottomColor = borderColor;
                    surface.style.borderLeftColor = surface.style.borderRightColor = borderColor;
                }
                SetInstrumentImage(_instrument, chosen);
                if (chosen != null && chosen.InstrumentIcon != null)
                {
                    // fit the image element itself to the sprite so horizontal instruments also sit on the bottom edge.
                    var rect = chosen.InstrumentIcon.rect;
                    var scale = _tuning.instrumentSize / Mathf.Max(1f, Mathf.Max(rect.width, rect.height));
                    _instrument.style.width = rect.width * scale;
                    _instrument.style.height = rect.height * scale;
                    _instrument.style.left = (_tuning.instrumentSize - rect.width * scale) * 0.5f;
                }
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
                    _icons[i].SetAppearance(accent, artwork, chosen != null ? chosen.AbilityPalette : null, i == 4, text, svg, !keyboard && i < 4, chosen != null ? chosen.AbilityBackground : null);
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
                        SetInstrumentImage(portrait, roster[i]);
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
                        var classColor = roster[i] != null ? roster[i].Tint : accent;
                        var border = i == highlight ? Color.Lerp(classColor, Color.white, 0.55f) : classColor * 0.7f;
                        border.a = 1f;
                        var radius = i == highlight ? 10f : _tuning.portraitSize * 0.5f;
                        frame.style.borderTopLeftRadius = frame.style.borderTopRightRadius = radius;
                        frame.style.borderBottomLeftRadius = frame.style.borderBottomRightRadius = radius;
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
                        _previews[i].SetAppearance(previewAccent, preview.icon, character != null ? character.AbilityPalette : null, false, prompt, image, !keyboard, character != null ? character.AbilityBackground : null);
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
            for (var i = 0; i < 4; i++)
                _icons[i].SetState(_player.Abilities != null ? _player.Abilities.GetAbilityState(_slots[i]) : default);
            var movement = _player.Movement;
            _icons[4].SetState(movement != null ? new PlayerAbilityState(movement.DodgeRechargeRemaining,
                movement.DodgeRechargeTime, movement.DodgeCharges, movement.MaxDodgeCharges) : default);
        }

        private static void SetInstrumentImage(Image image, CharacterDefinition character)
        {
            image.sprite = character != null ? character.InstrumentIcon : null;
            image.scaleMode = ScaleMode.ScaleToFit;
            image.tintColor = Color.white;
            if (character == null || character.AbilityPalette == null)
            {
                image.style.unityMaterial = new StyleMaterialDefinition((Material)null);
                return;
            }
            var silhouette = MaterialDefinition.FromMaterial(character.AbilityPalette);
            silhouette.SetColor("_Shadows", character.Tint);
            silhouette.SetColor("_DarkMidtones", character.Tint);
            silhouette.SetColor("_LightMidtones", character.Tint);
            silhouette.SetColor("_Highlights", character.Tint);
            image.style.unityMaterial = new StyleMaterialDefinition(silhouette);
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
            _inspectKey.text = keyboard ? "Tab" : "LT";
            _inspectKey.parent.style.width = _tuning.promptHeight * (keyboard ? 1.5f : 1f);
            _inspectHint.tooltip = gesture + (keyboard ? " Tab" : " LT") + " to inspect abilities";
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

            _inspectArtwork.SetAppearance(character.Tint, preview.icon, character.AbilityPalette, false, string.Empty, null, false, character.AbilityBackground);
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

    }
}
