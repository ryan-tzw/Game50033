namespace Game50033
{
    using UnityEngine;
    using UnityEngine.UIElements;

    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class BossEncounterHUD : MonoBehaviour
    {
        [SerializeField] private BaseBoss boss;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private GameplayHudTuning typography;
        [SerializeField] private StyleSheet styleSheet;

        private BossHealth _health;
        private BossDiscordance _discordance;
        private VisualElement _panel, _healthFill, _phaseBreak, _discordanceBar, _discordanceFill;
        private Label _healthValue, _discordanceValue;
        private Label[] _statuses;
        private VisualElement[] _markers;
        private IVisualElementScheduledItem[] _markerPulses;
        private bool _started;

        private void Start()
        {
            _started = true;
            Bind();
        }

        private void OnEnable()
        {
            if (_started) Bind();
        }

        private void Bind()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            _panel = root.Q("boss-hud");
            if (_panel == null || boss == null || boss.Health == null || styleSheet == null)
            {
                Debug.LogError("Boss encounter HUD requires a boss, its UXML and a stylesheet.", this);
                if (_panel != null) _panel.style.display = DisplayStyle.None;
                return;
            }

            if (!root.styleSheets.Contains(styleSheet)) root.styleSheets.Add(styleSheet);
            if (typography != null && typography.font != null)
                root.style.unityFontDefinition = FontDefinition.FromFont(typography.font);

            var bossName = root.Q<Label>("boss-name");
            bossName.text = boss.DisplayName;
            if (typography != null && typography.headingFont != null)
                bossName.style.unityFontDefinition = FontDefinition.FromFont(typography.headingFont);

            _healthFill = root.Q("health-fill");
            _phaseBreak = root.Q("phase-break");
            _healthValue = root.Q<Label>("health-value");
            _discordanceBar = root.Q("discordance");
            _discordanceFill = root.Q("discordance-fill");
            _discordanceValue = root.Q<Label>("discordance-value");
            _statuses = new[]
            {
                root.Q<Label>("resistance-status"),
                root.Q<Label>("haste-status"),
                root.Q<Label>("overcharge-status")
            };

            _health = boss.Health;
            _health.HealthChanged += UpdateHealth;
            boss.PhaseChanged += UpdatePhase;
            UpdateHealth(_health.CurrentHealth, _health.MaxHealth);
            UpdatePhase(boss.Phase);

            _discordance = boss.GetComponent<BossDiscordance>();
            _discordanceBar.style.display = _discordance != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (_discordance != null)
            {
                var markerContainer = root.Q("discordance-markers");
                markerContainer.Clear();
                var thresholds = _discordance.Thresholds;
                _markers = new VisualElement[thresholds.Count];
                _markerPulses = new IVisualElementScheduledItem[thresholds.Count];
                for (var i = 0; i < thresholds.Count; i++)
                {
                    var marker = new VisualElement { pickingMode = PickingMode.Ignore };
                    marker.AddToClassList("boss-meter__marker");
                    marker.AddToClassList("boss-discordance__marker");
                    marker.style.left = Length.Percent(100f * Mathf.Clamp01(thresholds[i] / _discordance.Maximum));
                    markerContainer.Add(marker);
                    _markers[i] = marker;
                    _markerPulses[i] = marker.schedule.Execute(() => marker.RemoveFromClassList("boss-marker--pulse"));
                    _markerPulses[i].Pause();
                }

                _discordance.ValueChanged += UpdateDiscordance;
                _discordance.LevelChanged += UpdateStatuses;
                UpdateDiscordance(_discordance.Value);
                UpdateStatuses(_discordance.Level, _discordance.Level);
            }

            if (gameManager != null)
            {
                gameManager.StateChanged += UpdateVisibility;
                UpdateVisibility(gameManager.State, gameManager.State);
            }
        }

        private void OnDisable()
        {
            if (_health != null) _health.HealthChanged -= UpdateHealth;
            if (boss != null) boss.PhaseChanged -= UpdatePhase;
            if (_discordance != null)
            {
                _discordance.ValueChanged -= UpdateDiscordance;
                _discordance.LevelChanged -= UpdateStatuses;
            }
            if (gameManager != null) gameManager.StateChanged -= UpdateVisibility;
            if (_markerPulses != null)
                foreach (var pulse in _markerPulses) pulse.Pause();
        }

        private void UpdateHealth(float current, float maximum)
        {
            _healthFill.style.width = Length.Percent(100f * Mathf.Clamp01(current / maximum));
            _healthValue.text = $"{Mathf.CeilToInt(current):N0} / {Mathf.CeilToInt(maximum):N0}";
        }

        private void UpdatePhase(int phase)
        {
            var threshold = _health.FirstPhaseBreak;
            _phaseBreak.style.display = threshold.HasValue ? DisplayStyle.Flex : DisplayStyle.None;
            if (threshold.HasValue)
                _phaseBreak.style.left = Length.Percent(100f * Mathf.Clamp01(threshold.Value / _health.MaxHealth));
            _phaseBreak.EnableInClassList("boss-marker--reached", phase >= 2);
        }

        private void UpdateDiscordance(float value)
        {
            var fraction = Mathf.Clamp01(value / _discordance.Maximum);
            _discordanceFill.style.width = Length.Percent(100f * fraction);
            _discordanceValue.text = $"{Mathf.FloorToInt(100f * fraction)}%";
        }

        private void UpdateStatuses(int previous, int current)
        {
            _discordanceBar.EnableInClassList("boss-discordance--overcharged", current >= 3);
            for (var i = 0; i < _statuses.Length; i++)
                _statuses[i].style.display = i < current ? DisplayStyle.Flex : DisplayStyle.None;
            for (var i = 0; i < _markers.Length; i++)
            {
                _markers[i].EnableInClassList("boss-marker--reached", i < current);
                if ((i < previous) == (i < current)) continue;
                _markers[i].AddToClassList("boss-marker--pulse");
                _markerPulses[i].ExecuteLater(320);
            }
        }

        private void UpdateVisibility(GameState previous, GameState next) =>
            _panel.style.display = next == GameState.Encounter ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
