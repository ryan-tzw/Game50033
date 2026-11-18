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
        private readonly VisualElement _hearts, _selection, _gameplay, _readyHeader, _rosterRow;
        private readonly Label _highlightName, _selectionStatus, _readyName;
        private readonly Image _readyPortrait;
        private VisualElement[] _portraits = System.Array.Empty<VisualElement>();
        private CharacterRoster _shownRoster;
        private CharacterDefinition _shownCharacter;
        private int _shownHighlight = -1, _shownTaken = -1, _shownPreviewRevision = -1;
        private string _scheme;
        private int _health = -1;
        private Color _accent;
        private PanelMode? _mode;

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
            _readyHeader = this.Q("ready-header");
            _rosterRow = this.Q("roster");
            _highlightName = this.Q<Label>("highlight-name");
            _selectionStatus = this.Q<Label>("selection-status");
            _readyName = this.Q<Label>("ready-name");
            _readyPortrait = this.Q<Image>("ready-portrait");
            _hearts.generateVisualContent += PaintHearts;
            generateVisualContent += PaintFrame;
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
        }

        public void ApplyTuning()
        {
            // theme text elements can override the inherited font definition, so apply the authored font to each label.
            this.Query<TextElement>().ForEach(text => text.style.unityFontDefinition = FontDefinition.FromFont(_tuning.font));
            var unit = _tuning.iconSize / 72f;
            var width = _tuning.iconSize * 5 + _tuning.iconGap * 4;
            style.width = width + _tuning.panelPadding * 2;
            style.paddingLeft = style.paddingRight = _tuning.panelPadding;
            style.paddingTop = 10f * unit;
            style.marginLeft = style.marginRight = _tuning.playerGap * 0.5f;
            _gameplay.style.left = _gameplay.style.right = _tuning.panelPadding;
            _gameplay.style.bottom = 10f * unit;
            _gameplay.style.height = 137f * unit;
            this.Q("abilities").style.height = 92f * unit;
            _hearts.style.height = 36f * unit;
            _hearts.style.marginTop = 9f * unit;
            for (var i = 0; i < 5; i++)
            {
                _icons[i].Configure(_tuning, i == 4);
                if (i < 4) _icons[i].style.marginRight = _tuning.iconGap;
            }
            for (var i = 0; i < 4; i++)
            {
                _previews[i].Configure(_tuning, false);
                _previews[i].SetState(new PlayerAbilityState(0, 1));
                var row = this.Q("preview-row-" + i);
                row.style.paddingLeft = _tuning.previewIconInset * unit;
                row.style.height = _tuning.previewRowHeight * unit;
                row.style.marginBottom = _tuning.iconGap * unit;
                _previewTitles[i].style.fontSize = _tuning.previewFontSize * 1.2f * unit;
                _previewTypes[i].text = PreviewSlots[i].ToString();
                _previewTypes[i].style.fontSize = _tuning.previewFontSize * 0.85f * unit;
                _previewDescriptions[i].style.fontSize = _tuning.previewFontSize * unit;
            }
            _highlightName.style.height = 30f * unit;
            _highlightName.style.fontSize = _tuning.previewFontSize * 1.3f * unit;
            _rosterRow.style.height = _tuning.portraitSize * unit;
            _selectionStatus.style.height = 22f * unit;
            _selectionStatus.style.fontSize = _tuning.previewFontSize * 0.85f * unit;
            _readyHeader.style.top = 7f * unit;
            _readyHeader.style.left = _readyHeader.style.right = 0;
            _readyHeader.style.height = 30f * unit;
            _readyPortrait.style.width = _readyPortrait.style.height = 28f * unit;
            _readyName.style.fontSize = _tuning.previewFontSize * 1.2f * unit;
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
            var unit = _tuning.iconSize / 72f;
            if (_mode != mode)
            {
                _mode = mode;
                _shownCharacter = null;
                _selection.style.display = mode == PanelMode.Selecting ? DisplayStyle.Flex : DisplayStyle.None;
                _gameplay.style.display = mode != PanelMode.Selecting ? DisplayStyle.Flex : DisplayStyle.None;
                _readyHeader.style.display = mode == PanelMode.Ready ? DisplayStyle.Flex : DisplayStyle.None;
                style.height = mode == PanelMode.Selecting
                    ? (4 * (_tuning.previewRowHeight + _tuning.iconGap) + _tuning.portraitSize + 72f) * unit
                    : (mode == PanelMode.Ready ? 196f : 158f) * unit;
                MarkDirtyRepaint();
            }
            var scheme = _input != null ? _input.currentControlScheme : string.Empty;
            var accent = _player.PlayerColor;
            if (_scheme != scheme || _accent != accent)
            {
                _scheme = scheme;
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
                    _icons[i].SetAppearance(accent, i == 4 ? 4 : (int)_slots[i], text, svg, !keyboard && i < 4);
                    if (i < 4)
                    {
                        var previewAbility = PreviewSlots[i];
                        var previewPrompt = keyboard
                            ? previewAbility == PlayerAbility.Special ? "E" : previewAbility == PlayerAbility.Defensive ? "Q" : string.Empty
                            : text;
                        _previews[i].SetAppearance(accent, (int)previewAbility, previewPrompt, svg, !keyboard);
                        _previewTitles[i].style.color = Color.Lerp(accent, Color.white, 0.3f);
                    }
                }
                _highlightName.style.color = _readyName.style.color = Color.Lerp(accent, Color.white, 0.3f);
                _shownRoster = null;
                MarkDirtyRepaint();
                _hearts.MarkDirtyRepaint();
            }

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
                        frame.style.width = frame.style.height = _tuning.portraitSize * unit;
                        frame.style.marginLeft = frame.style.marginRight = 3f * unit;
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
                    for (var i = 0; i < 4; i++)
                    {
                        var preview = character != null ? character.GetAbilityPreview(PreviewSlots[i]) : default;
                        _previewTitles[i].text = string.IsNullOrWhiteSpace(preview.name) ? PreviewSlots[i].ToString() : preview.name;
                        _previewDescriptions[i].text = string.IsNullOrWhiteSpace(preview.description) ? "Details coming soon." : preview.description;
                    }
                }
                return;
            }

            var chosen = _player.Character;
            if (_shownCharacter != chosen)
            {
                _shownCharacter = chosen;
                _readyName.text = chosen != null ? chosen.DisplayName : "Ready";
                _readyPortrait.sprite = chosen != null ? chosen.Sprite : null;
                _readyPortrait.tintColor = chosen != null ? chosen.Tint : Color.white;
            }
            var health = _player.Health != null ? _player.Health.CurrentHealth : 0;
            if (_health != health) { _health = health; _hearts.MarkDirtyRepaint(); }
            for (var i = 0; i < 4; i++)
                _icons[i].SetState(_player.Abilities != null ? _player.Abilities.GetAbilityState(_slots[i]) : default);
            var movement = _player.Movement;
            _icons[4].SetState(movement != null ? new PlayerAbilityState(movement.DodgeRechargeRemaining,
                movement.DodgeRechargeTime, movement.DodgeCharges, movement.MaxDodgeCharges) : default);
        }

        private void PaintHearts(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            var step = _tuning.iconSize + _tuning.iconGap;
            var heartWidth = _tuning.iconSize / 72f * 0.8f;
            var unit = _tuning.iconSize / 72f;
            for (var i = 0; i < 5; i++)
            {
                var x = _tuning.iconSize * 0.5f + step * i;
                var filled = i < _health;
                painter.strokeColor = filled ? _accent : Color.Lerp(_tuning.emptyHeart, _accent, 0.25f);
                painter.lineWidth = 4 * unit;
                painter.BeginPath(); painter.MoveTo(new Vector2(x - 30 * unit, 19 * unit)); painter.LineTo(new Vector2(x + 30 * unit, 19 * unit)); painter.Stroke();
                painter.fillColor = filled ? _accent : _tuning.emptyHeart;
                painter.strokeColor = Color.Lerp(_accent, Color.white, filled ? 0.25f : 0.05f);
                painter.lineWidth = _tuning.borderWidth * 0.65f;
                painter.BeginPath(); painter.MoveTo(new Vector2(x, 10 * unit));
                painter.BezierCurveTo(new Vector2(x - 11 * heartWidth, -3 * unit), new Vector2(x - 24 * heartWidth, 8 * unit), new Vector2(x - 16 * heartWidth, 19 * unit));
                painter.BezierCurveTo(new Vector2(x - 11 * heartWidth, 25 * unit), new Vector2(x, 32 * unit), new Vector2(x, 32 * unit));
                painter.BezierCurveTo(new Vector2(x, 32 * unit), new Vector2(x + 11 * heartWidth, 25 * unit), new Vector2(x + 16 * heartWidth, 19 * unit));
                painter.BezierCurveTo(new Vector2(x + 24 * heartWidth, 8 * unit), new Vector2(x + 11 * heartWidth, -3 * unit), new Vector2(x, 10 * unit));
                painter.ClosePath(); painter.Fill(); painter.Stroke();
            }
        }

        private void PaintFrame(MeshGenerationContext context)
        {
            var w = contentRect.width + _tuning.panelPadding * 2;
            var unit = _tuning.iconSize / 72f;
            var height = layout.height - 4f * unit;
            var mesh = context.Allocate(4, 6);
            var top = new Color32(3, 5, 9, 0);
            var bottom = new Color32(3, 5, 9, 225);
            mesh.SetNextVertex(new Vertex { position = new Vector3(0, 0, Vertex.nearZ), tint = top });
            mesh.SetNextVertex(new Vertex { position = new Vector3(w, 0, Vertex.nearZ), tint = top });
            mesh.SetNextVertex(new Vertex { position = new Vector3(0, height, Vertex.nearZ), tint = bottom });
            mesh.SetNextVertex(new Vertex { position = new Vector3(w, height, Vertex.nearZ), tint = bottom });
            mesh.SetNextIndex(0); mesh.SetNextIndex(1); mesh.SetNextIndex(2);
            mesh.SetNextIndex(2); mesh.SetNextIndex(1); mesh.SetNextIndex(3);
            var painter = context.painter2D;
            for (var i = 4; i >= 0; i--)
            {
                var color = _accent; color.a = i == 0 ? 0.65f : 0.035f;
                painter.strokeColor = color; painter.lineWidth = i == 0 ? 1.5f : i * 3;
                painter.BeginPath(); painter.MoveTo(new Vector2(0, height - 3f * unit)); painter.LineTo(new Vector2(w, height - 3f * unit)); painter.Stroke();
            }
            // small accent diamonds echo the reference dividers without enclosing the whole card.
            for (var side = 0; side < 2; side++)
            {
                var x = side == 0 ? -12f * unit : w + 12f * unit;
                painter.strokeColor = new Color(_accent.r, _accent.g, _accent.b, 0.45f); painter.lineWidth = 1;
                painter.BeginPath(); painter.MoveTo(new Vector2(x, height - 132f * unit)); painter.LineTo(new Vector2(x, height - 12f * unit)); painter.Stroke();
                painter.fillColor = _accent;
                painter.BeginPath(); painter.MoveTo(new Vector2(x, height - 55f * unit)); painter.LineTo(new Vector2(x + 4 * unit, height - 46f * unit));
                painter.LineTo(new Vector2(x, height - 37f * unit)); painter.LineTo(new Vector2(x - 4 * unit, height - 46f * unit)); painter.ClosePath(); painter.Fill();
            }
        }
    }
}
