namespace Game50033
{
    using UnityEngine;
    using UnityEngine.InputSystem;
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
            _hitboxOverlay = new HitboxDebugOverlay(_session.Debug, visualizationTuning);
            root.Add(_hitboxOverlay);
            _panel = new VisualElement { name = "debug-panel", pickingMode = PickingMode.Position };
            _panel.AddToClassList("debug-panel");
            root.Add(_panel);
            var title = new Label("Debug / Cheats");
            title.AddToClassList("debug-title");
            _panel.Add(title);
            var hint = new Label("F1 to close · applies to all players");
            hint.AddToClassList("debug-hint");
            _panel.Add(hint);
            var heading = new Label("Players");
            heading.AddToClassList("debug-section");
            _panel.Add(heading);

            // controls write session data; closing the panel never restores or disables cheats.
            var cooldowns = new Toggle("No cooldowns") { name = "no-cooldowns", focusable = false };
            cooldowns.SetValueWithoutNotify(_session.Debug.NoCooldowns);
            cooldowns.RegisterValueChangedCallback(evt => _session.Debug.SetNoCooldowns(evt.newValue));
            _panel.Add(cooldowns);
            var invincibility = new Toggle("Invincibility") { name = "invincibility", focusable = false };
            invincibility.SetValueWithoutNotify(_session.Debug.Invincibility);
            invincibility.RegisterValueChangedCallback(evt => _session.Debug.Invincibility = evt.newValue);
            _panel.Add(invincibility);
            var dodge = new Toggle("Infinite dodge") { name = "infinite-dodge", focusable = false };
            dodge.SetValueWithoutNotify(_session.Debug.InfiniteDodge);
            dodge.RegisterValueChangedCallback(evt => _session.Debug.InfiniteDodge = evt.newValue);
            _panel.Add(dodge);
            _panel.Add(new Button(() => _session.Debug.HealToFull())
            {
                name = "heal-to-full", text = "Heal living players to full", focusable = false
            });
            var hitboxHeading = new Label("Hitboxes");
            hitboxHeading.AddToClassList("debug-section");
            _panel.Add(hitboxHeading);
            var showHitboxes = new Toggle("Show hitboxes") { name = "show-hitboxes", focusable = false };
            showHitboxes.SetEnabled(visualizationTuning != null);
            showHitboxes.SetValueWithoutNotify(_session.Debug.ShowHitboxes);
            showHitboxes.RegisterValueChangedCallback(evt => _session.Debug.ShowHitboxes = evt.newValue);
            _panel.Add(showHitboxes);
            var enemyHurtboxes = new Toggle("Enemy / dummy hurtboxes") { name = "enemy-hurtboxes", focusable = false };
            enemyHurtboxes.SetValueWithoutNotify(_session.Debug.ShowEnemyHurtboxes);
            enemyHurtboxes.RegisterValueChangedCallback(evt => _session.Debug.ShowEnemyHurtboxes = evt.newValue);
            _panel.Add(enemyHurtboxes);
            var playerAttacks = new Toggle("Player attacks") { name = "player-attacks", focusable = false };
            playerAttacks.SetValueWithoutNotify(_session.Debug.ShowPlayerAttacks);
            playerAttacks.RegisterValueChangedCallback(evt => _session.Debug.ShowPlayerAttacks = evt.newValue);
            _panel.Add(playerAttacks);
            var specialZones = new Toggle("Special zones") { name = "special-zones", focusable = false };
            specialZones.SetValueWithoutNotify(_session.Debug.ShowSpecialZones);
            specialZones.RegisterValueChangedCallback(evt => _session.Debug.ShowSpecialZones = evt.newValue);
            _panel.Add(specialZones);
            var snapshotHint = new Label("Dashed areas show past impacts, not active damage.");
            snapshotHint.AddToClassList("debug-hint");
            snapshotHint.style.whiteSpace = WhiteSpace.Normal;
            _panel.Add(snapshotHint);
            _panel.style.display = IsOpen ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void LateUpdate()
        {
            if (_session == null || _hitboxOverlay == null) return;
            // expire even while hidden, so toggling the menu/filters cannot accumulate or replay old impacts.
            if (visualizationTuning != null) _session.Debug.ExpireAttackSnapshots(visualizationTuning.snapshotDuration);
            var show = _session.Debug.ShowHitboxes && visualizationTuning != null
                && (_session.Debug.ShowEnemyHurtboxes || _session.Debug.ShowPlayerAttacks || _session.Debug.ShowSpecialZones);
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
