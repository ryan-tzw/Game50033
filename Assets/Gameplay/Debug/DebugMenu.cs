namespace Game50033
{
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.SceneManagement;
    using UnityEngine.UIElements;

    [DefaultExecutionOrder(-1100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class DebugMenu : MonoBehaviour
    {
        [SerializeField] private StyleSheet styleSheet;
        [SerializeField] private DebugVisualizationTuning visualizationTuning;
        private GameSession _session;
        private UIDocument _document;
        private VisualElement _panel;
        private HitboxDebugOverlay _hitboxOverlay;
        private readonly Button[] _damageButtons = new Button[4];

        private readonly List<BaseBoss> _bosses = new();
        private readonly List<GameObject> _attackPrefabs = new();
        private VisualElement _bossControls;
        private DropdownField _bossChoice;
        private DropdownField _attackChoice;
        private Button _queueAttack;
        private Toggle _repeatAttack;
        private Label _queuedAttack;
        private Scene _bossScene;
        private int _shownAttackPhase;

        private BaseBoss SelectedBoss => _bossChoice.index >= 0 && _bossChoice.index < _bosses.Count
            ? _bosses[_bossChoice.index] : null;
        private GameObject SelectedAttack => _attackChoice.index >= 0 && _attackChoice.index < _attackPrefabs.Count
            ? _attackPrefabs[_attackChoice.index] : null;

        public bool IsOpen { get; private set; }

        public void Initialize(GameSession session)
        {
            _session = session;
            BuildPanel();
        }

        private void OnEnable()
        {
            if (_session != null) BuildPanel();
        }

        private void BuildPanel()
        {
            _document = GetComponent<UIDocument>();
            var root = _document.rootVisualElement;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;
            if (styleSheet != null && !root.styleSheets.Contains(styleSheet)) root.styleSheets.Add(styleSheet);
            if (visualizationTuning == null) Debug.LogError("DebugMenu requires a DebugVisualizationTuning asset.", this);
            // keep the overlay below the controls and outside the panel whose visibility F1 toggles.
            _hitboxOverlay = new HitboxDebugOverlay(_session.Debug, visualizationTuning, _session.Players);
            root.Add(_hitboxOverlay);
            _panel = new VisualElement { name = "debug-panel", pickingMode = PickingMode.Position };
            _panel.AddToClassList("debug-panel");
            root.Add(_panel);
            _panel.style.maxHeight = new Length(96f, LengthUnit.Percent);
            var content = new ScrollView { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            content.style.flexShrink = 1f;
            _panel.Add(content);
            _bossScene = default;
            var title = new Label("Debug / Cheats");
            title.AddToClassList("debug-title");
            content.Add(title);
            var hint = new Label("F1 to close · game keeps running");
            hint.AddToClassList("debug-hint");
            content.Add(hint);
            var heading = new Label("Players");
            heading.AddToClassList("debug-section");
            content.Add(heading);

            // controls write session data; closing the panel never restores or disables cheats.
            var cooldowns = new Toggle("No cooldowns") { name = "no-cooldowns", focusable = false };
            cooldowns.SetValueWithoutNotify(_session.Debug.NoCooldowns);
            cooldowns.RegisterValueChangedCallback(evt => _session.Debug.SetNoCooldowns(evt.newValue));
            content.Add(cooldowns);
            var invincibility = new Toggle("Invincibility") { name = "invincibility", focusable = false };
            invincibility.SetValueWithoutNotify(_session.Debug.Invincibility);
            invincibility.RegisterValueChangedCallback(evt => _session.Debug.Invincibility = evt.newValue);
            content.Add(invincibility);
            var dodge = new Toggle("Infinite dodge") { name = "infinite-dodge", focusable = false };
            dodge.SetValueWithoutNotify(_session.Debug.InfiniteDodge);
            dodge.RegisterValueChangedCallback(evt => _session.Debug.InfiniteDodge = evt.newValue);
            content.Add(dodge);
            content.Add(new Button(() => _session.Debug.HealToFull())
            {
                name = "heal-to-full", text = "Heal living players to full", focusable = false
            });
            var damageRow = new VisualElement();
            damageRow.style.flexDirection = FlexDirection.Row;
            damageRow.style.flexWrap = Wrap.Wrap;
            for (var i = 0; i < _damageButtons.Length; i++)
            {
                var playerIndex = i;
                var button = new Button(() => GetDamageTarget(playerIndex)?.Health.TryTakeDamage(1))
                {
                    name = "damage-player-" + (i + 1),
                    text = $"Damage P{i + 1}",
                    focusable = false,
                    tooltip = "Apply 1 damage through the normal damage rules, including invulnerability."
                };
                button.style.display = DisplayStyle.None;
                _damageButtons[i] = button;
                damageRow.Add(button);
            }
            content.Add(damageRow);

            _bossControls = new VisualElement { name = "debug-boss-controls" };
            _bossControls.style.display = DisplayStyle.None;
            var bossHeading = new Label("Boss attack controls");
            bossHeading.AddToClassList("debug-section");
            _bossControls.Add(bossHeading);

            _bossChoice = new DropdownField("Boss") { name = "debug-boss-choice" };
            _bossChoice.RegisterValueChangedCallback(_ => RefreshAttackPicker());
            _bossControls.Add(_bossChoice);
            _attackChoice = new DropdownField("Attack") { name = "debug-attack-choice" };
            _attackChoice.RegisterValueChangedCallback(_ =>
            {
                var boss = SelectedBoss;
                if (boss != null && _repeatAttack.value) boss.Attacks.SetDebugRepeat(SelectedAttack);
            });
            _bossControls.Add(_attackChoice);
            _queueAttack = new Button(() =>
            {
                var boss = SelectedBoss;
                if (boss != null) boss.Attacks.QueueDebugAttack(SelectedAttack);
            })
            {
                name = "queue-boss-attack", text = "Queue next", focusable = false,
                tooltip = "Use this attack at the next legal cast, after the current cast, recovery and cooldown."
            };
            _bossControls.Add(_queueAttack);
            _repeatAttack = new Toggle("Repeat selected") { name = "repeat-boss-attack", focusable = false };
            _repeatAttack.RegisterValueChangedCallback(evt =>
            {
                var boss = SelectedBoss;
                if (boss != null) boss.Attacks.SetDebugRepeat(evt.newValue ? SelectedAttack : null);
            });
            _bossControls.Add(_repeatAttack);
            _queuedAttack = new Label { name = "queued-boss-attack" };
            _queuedAttack.AddToClassList("debug-hint");
            _bossControls.Add(_queuedAttack);
            content.Add(_bossControls);

            var invisibleWalls = new Toggle("Invisible walls") { name = "invisible-walls", focusable = false };
            invisibleWalls.SetValueWithoutNotify(_session.Debug.ShowInvisibleWalls);
            invisibleWalls.RegisterValueChangedCallback(evt => _session.Debug.ShowInvisibleWalls = evt.newValue);
            content.Add(invisibleWalls);

            var hitboxHeading = new Label("Hitboxes");
            hitboxHeading.AddToClassList("debug-section");
            content.Add(hitboxHeading);
            var showHitboxes = new Toggle("Show hitboxes") { name = "show-hitboxes", focusable = false };
            showHitboxes.SetEnabled(visualizationTuning != null);
            showHitboxes.SetValueWithoutNotify(_session.Debug.ShowHitboxes);
            showHitboxes.RegisterValueChangedCallback(evt => _session.Debug.ShowHitboxes = evt.newValue);
            content.Add(showHitboxes);
            var playerHurtboxes = new Toggle("Player hurtboxes")
            {
                name = "player-hurtboxes", focusable = false,
                tooltip = "Shows damage-receiving players; hidden while downed or invulnerable (including Invincibility)."
            };
            playerHurtboxes.SetValueWithoutNotify(_session.Debug.ShowPlayerHurtboxes);
            playerHurtboxes.RegisterValueChangedCallback(evt => _session.Debug.ShowPlayerHurtboxes = evt.newValue);
            content.Add(playerHurtboxes);
            var enemyHurtboxes = new Toggle("Enemy / dummy hurtboxes") { name = "enemy-hurtboxes", focusable = false };
            enemyHurtboxes.SetValueWithoutNotify(_session.Debug.ShowEnemyHurtboxes);
            enemyHurtboxes.RegisterValueChangedCallback(evt => _session.Debug.ShowEnemyHurtboxes = evt.newValue);
            content.Add(enemyHurtboxes);
            var playerAttacks = new Toggle("Player attacks") { name = "player-attacks", focusable = false };
            playerAttacks.SetValueWithoutNotify(_session.Debug.ShowPlayerAttacks);
            playerAttacks.RegisterValueChangedCallback(evt => _session.Debug.ShowPlayerAttacks = evt.newValue);
            content.Add(playerAttacks);
            var bossAttacks = new Toggle("Boss attacks") { name = "boss-attacks", focusable = false };
            bossAttacks.SetValueWithoutNotify(_session.Debug.ShowBossAttacks);
            bossAttacks.RegisterValueChangedCallback(evt => _session.Debug.ShowBossAttacks = evt.newValue);
            content.Add(bossAttacks);
            var reverberationAreas = new Toggle("REV/RES areas") { name = "reverberation-areas", focusable = false };
            reverberationAreas.SetValueWithoutNotify(_session.Debug.ShowReverberationAreas);
            reverberationAreas.RegisterValueChangedCallback(evt => _session.Debug.ShowReverberationAreas = evt.newValue);
            content.Add(reverberationAreas);
            var specialZones = new Toggle("Special zones") { name = "special-zones", focusable = false };
            specialZones.SetValueWithoutNotify(_session.Debug.ShowSpecialZones);
            specialZones.RegisterValueChangedCallback(evt => _session.Debug.ShowSpecialZones = evt.newValue);
            content.Add(specialZones);
            var snapshotHint = new Label("Dashed areas show past impacts, not active damage.");
            snapshotHint.AddToClassList("debug-hint");
            snapshotHint.style.whiteSpace = WhiteSpace.Normal;
            content.Add(snapshotHint);
            _panel.style.display = IsOpen ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RefreshBossPicker()
        {
            var previous = SelectedBoss;
            _bossScene = SceneManager.GetActiveScene();
            _bosses.Clear();
            var names = new List<string>();
            foreach (var boss in FindObjectsByType<BaseBoss>(FindObjectsSortMode.InstanceID))
            {
                if (boss.gameObject.scene != _bossScene || !boss.isActiveAndEnabled || boss.IsDefeated
                    || boss.Attacks == null || !boss.Attacks.isActiveAndEnabled) continue;
                _bosses.Add(boss);
                names.Add($"{_bosses.Count}. {boss.DisplayName}");
            }
            _bossChoice.choices = names;
            _bossChoice.style.display = _bosses.Count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            var index = _bosses.IndexOf(previous);
            _bossChoice.index = _bosses.Count == 0 ? -1 : Mathf.Max(0, index);
            _bossControls.style.display = _bosses.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            RefreshAttackPicker();
        }

        private void RefreshAttackPicker()
        {
            var previous = SelectedAttack;
            var boss = SelectedBoss;
            var attacks = boss != null ? boss.Attacks : null;
            _shownAttackPhase = attacks != null ? attacks.CurrentPhase : 0;
            _attackPrefabs.Clear();
            var labels = new List<string>();
            if (attacks != null)
            {
                foreach (var entry in attacks.DebugAttackOptions)
                {
                    if (entry.attack == null || entry.count <= 0 || _attackPrefabs.Contains(entry.attack)) continue;
                    _attackPrefabs.Add(entry.attack);
                    var label = AttackLabel(entry.attack);
                    labels.Add(labels.Contains(label) ? $"{label} ({labels.Count + 1})" : label);
                }
            }

            _attackChoice.choices = labels;
            var selected = attacks == null ? null : attacks.DebugRepeatedAttack ?? attacks.DebugQueuedAttack ?? previous;
            var index = _attackPrefabs.IndexOf(selected);
            // changing bosses or phase must not edit a repeat request as a dropdown side effect.
            _attackChoice.SetValueWithoutNotify(labels.Count == 0 ? string.Empty : labels[Mathf.Max(0, index)]);
            _repeatAttack.SetValueWithoutNotify(attacks != null && attacks.DebugRepeatedAttack != null);
            _attackChoice.SetEnabled(labels.Count > 0);
            _queueAttack.SetEnabled(labels.Count > 0);
            _repeatAttack.SetEnabled(labels.Count > 0);
        }

        private static string AttackLabel(GameObject prefab) =>
            Regex.Replace(prefab.name.Replace('_', ' '), "(?<=[a-z0-9])(?=[A-Z])", " ");

        private Player GetDamageTarget(int playerIndex)
        {
            var players = _session.Players;
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player != null && player.PlayerIndex == playerIndex && player.isActiveAndEnabled
                    && player.Health != null && player.Health.isActiveAndEnabled) return player;
            }
            return null;
        }

        private void LateUpdate()
        {
            if (_session == null || _hitboxOverlay == null) return;
            // expire even while hidden, so toggling the menu/filters cannot accumulate or replay old impacts.
            if (visualizationTuning != null) _session.Debug.ExpireAttackSnapshots(visualizationTuning.snapshotDuration);
            var show = visualizationTuning != null && (_session.Debug.ShowInvisibleWalls || _session.Debug.ShowHitboxes
                && (_session.Debug.ShowPlayerHurtboxes || _session.Debug.ShowEnemyHurtboxes
                    || _session.Debug.ShowPlayerAttacks || _session.Debug.ShowBossAttacks
                    || _session.Debug.ShowSpecialZones || _session.Debug.ShowReverberationAreas));
            _hitboxOverlay.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            // sample after gameplay movement; no polling or path generation is needed while hidden.
            if (show) _hitboxOverlay.MarkDirtyRepaint();
        }

        private void Update()
        {
            if (_session == null || _panel == null) return;
            var openedThisFrame = false;
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            {
                IsOpen = !IsOpen;
                openedThisFrame = IsOpen;
                _panel.style.display = IsOpen ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (IsOpen)
            {
                var boss = SelectedBoss;
                if (openedThisFrame || SceneManager.GetActiveScene() != _bossScene || _bossChoice.index >= 0
                    && (boss == null || !boss.isActiveAndEnabled || boss.IsDefeated || !boss.Attacks || !boss.Attacks.isActiveAndEnabled))
                {
                    RefreshBossPicker();
                    boss = SelectedBoss;
                }
                if (boss != null)
                {
                    if (boss.Attacks.CurrentPhase != _shownAttackPhase) RefreshAttackPicker();
                    var queued = boss.Attacks.DebugQueuedAttack;
                    var status = queued == null ? string.Empty : $"Queued: {AttackLabel(queued)}";
                    if (_queuedAttack.text != status) _queuedAttack.text = status;
                }

                for (var i = 0; i < _damageButtons.Length; i++)
                {
                    var target = GetDamageTarget(i);
                    _damageButtons[i].style.display = target != null ? DisplayStyle.Flex : DisplayStyle.None;
                    _damageButtons[i].SetEnabled(target != null && !target.Health.IsDowned);
                }
            }

            // run before player input routing. UI event consumption alone does not block InputAction.IsPressed().
            // block the opening frame too, while the previously hidden panel is waiting for its first layout.
            var blockMouse = false;
            if (IsOpen && Mouse.current != null && _panel.panel != null)
            {
                var screenPoint = Mouse.current.position.ReadValue();
                screenPoint.y = Screen.height - screenPoint.y;
                var panelPoint = RuntimePanelUtils.ScreenToPanel(_panel.panel, screenPoint);
                blockMouse = openedThisFrame || _panel.worldBound.Contains(panelPoint);
            }
            _session.Debug.BlocksMouseCombat = blockMouse;
        }

        private void OnDisable()
        {
            if (_session != null) _session.Debug.BlocksMouseCombat = false;
        }
    }
}
