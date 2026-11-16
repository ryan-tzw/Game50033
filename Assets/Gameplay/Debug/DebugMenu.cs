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
        private GameSession _session;
        private UIDocument _document;
        private VisualElement _panel;

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
            _panel.style.display = IsOpen ? DisplayStyle.Flex : DisplayStyle.None;
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
