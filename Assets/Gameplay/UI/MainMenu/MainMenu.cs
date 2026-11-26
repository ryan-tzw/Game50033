namespace Game50033
{
    using UnityEngine;
    using UnityEngine.Audio;
    using UnityEngine.SceneManagement;
    using UnityEngine.UIElements;

    /// <summary>
    /// Placeholder main menu: Start loads the lobby (where players join), Settings holds volume sliders,
    /// Quit closes the game. Works with no joined players: UI Toolkit reads the project-wide UI actions, so
    /// any keyboard, mouse or gamepad can drive it. An option is always focused (Start by default), so a
    /// controller only needs up/down and confirm; mouse hover is never required.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainMenu : MonoBehaviour
    {
        [Tooltip("Run order asset; Start loads its lobby scene.")]
        [SerializeField] private GameFlow flow;
        [Tooltip("Mixer whose exposed volume parameters the Settings sliders drive.")]
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private MainMenuTuning tuning;

        private const string MainHint = "Up / Down to move  ·  A / Enter to select";
        private const string SettingsHint = "Up / Down to move  ·  Left / Right to adjust  ·  B / Esc to go back";

        private VisualElement _mainPanel;
        private VisualElement _settingsPanel;
        private Button _start;
        private Button _settings;
        private Button _quit;
        private Button _back;
        private Slider _master;
        private Slider _music;
        private Slider _sfx;
        private Label _hint;
        private Focusable _defaultFocus;
        private bool _loading;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _mainPanel = root.Q("main-panel");
            _settingsPanel = root.Q("settings-panel");
            _start = root.Q<Button>("start");
            _settings = root.Q<Button>("settings");
            _quit = root.Q<Button>("quit");
            _back = root.Q<Button>("back");
            _master = root.Q<Slider>("master-volume");
            _music = root.Q<Slider>("music-volume");
            _sfx = root.Q<Slider>("sfx-volume");
            _hint = root.Q<Label>("hint");
            if (_mainPanel == null || _settingsPanel == null || _start == null || _settings == null || _quit == null
                || _back == null || _master == null || _music == null || _sfx == null)
            {
                Debug.LogError("MainMenu could not find its elements in the UXML.", this);
                enabled = false;
                return;
            }

            _start.clicked += StartGame;
            _settings.clicked += OpenSettings;
            _quit.clicked += Quit;
            _back.clicked += CloseSettings;
            root.RegisterCallback<NavigationCancelEvent>(HandleCancel);

            VolumeSettings.ApplyAll(mixer);
            BindSlider(_master, VolumeSettings.Channel.Master);
            BindSlider(_music, VolumeSettings.Channel.Music);
            BindSlider(_sfx, VolumeSettings.Channel.Sfx);

            ShowPanel(settings: false, _start);
        }

        private void OnDisable()
        {
            if (_start == null) return;
            _start.clicked -= StartGame;
            _settings.clicked -= OpenSettings;
            _quit.clicked -= Quit;
            _back.clicked -= CloseSettings;
            VolumeSettings.Save();
        }

        private void Update()
        {
            // a mouse click on empty space clears focus; give it back so a controller is never stranded.
            var focusController = _mainPanel?.focusController;
            if (focusController != null && focusController.focusedElement == null && _defaultFocus != null)
                _defaultFocus.Focus();
        }

        private void BindSlider(Slider slider, VolumeSettings.Channel channel)
        {
            var title = slider.label;
            slider.lowValue = 0f;
            slider.highValue = 1f;
            slider.SetValueWithoutNotify(VolumeSettings.Get(channel));
            slider.label = $"{title}  {Mathf.RoundToInt(slider.value * 100f)}%";
            slider.RegisterValueChangedCallback(evt =>
            {
                VolumeSettings.Set(mixer, channel, evt.newValue);
                slider.label = $"{title}  {Mathf.RoundToInt(evt.newValue * 100f)}%";
            });
            // left/right steps by the tuning's volume step (the built-in step is 1%, too slow on a controller);
            // up/down still moves focus.
            slider.RegisterCallback<NavigationMoveEvent>(evt =>
            {
                var direction = evt.direction == NavigationMoveEvent.Direction.Left ? -1f
                    : evt.direction == NavigationMoveEvent.Direction.Right ? 1f : 0f;
                if (direction == 0f) return;
                var step = tuning != null ? tuning.volumeStep : 0.1f;
                slider.value = Mathf.Clamp01(Mathf.Round((slider.value + direction * step) / step) * step);
                evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
        }

        private void ShowPanel(bool settings, Focusable focus)
        {
            _mainPanel.style.display = settings ? DisplayStyle.None : DisplayStyle.Flex;
            _settingsPanel.style.display = settings ? DisplayStyle.Flex : DisplayStyle.None;
            if (_hint != null) _hint.text = settings ? SettingsHint : MainHint;
            _defaultFocus = focus;
            // focus after layout so the newly shown panel can take it.
            _mainPanel.schedule.Execute(() => focus.Focus());
        }

        private void OpenSettings() => ShowPanel(settings: true, _master);

        private void CloseSettings()
        {
            VolumeSettings.Save();
            ShowPanel(settings: false, _settings);
        }

        private void HandleCancel(NavigationCancelEvent evt)
        {
            if (_settingsPanel.resolvedStyle.display == DisplayStyle.None) return;
            CloseSettings();
            evt.StopPropagation();
        }

        private void StartGame()
        {
            if (_loading) return;
            _loading = true;
            _start.SetEnabled(false);
            SceneManager.LoadScene(flow != null ? flow.lobbyScene : SceneNames.Lobby);
        }

        private void Quit()
        {
            VolumeSettings.Save();
#if UNITY_EDITOR
            Debug.Log("Quit pressed (ignored in the Editor).", this);
#else
            Application.Quit();
#endif
        }
    }
}
