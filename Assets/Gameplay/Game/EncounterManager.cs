namespace Game50033
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.UI;

    /// <summary>
    /// Scene-local owner of one encounter: places the players carried over from the previous scene,
    /// starts the fight through the scene's GameManager, decides victory (every boss defeated) or
    /// defeat (every player downed at once), and keeps the fight's stats.
    ///
    /// It also runs the Discordance meter: the value, its fill and its levels, raising
    /// <see cref="DiscordanceChanged"/> and <see cref="DiscordanceLevelChanged"/>. It does not apply any
    /// level's effect; bosses listen to the level and change their own stats.
    ///
    /// The results screen is two plain GameObject groups switched on when the fight ends; any Text
    /// listed under Results is filled with the stats or the defeat ready-up count.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EncounterManager : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;

        [Tooltip("This boss's Discordance numbers. Without one, the meter stays at 0.")]
        [SerializeField] private DiscordanceTuning discordance;

        [Tooltip("Bosses that must all be defeated to win. Each must implement IEncounterBoss (BaseBoss does).")]
        [SerializeField] private MonoBehaviour[] bosses = System.Array.Empty<MonoBehaviour>();

        [Tooltip("Where players start the fight, in player index order.")]
        [SerializeField] private Transform[] spawnPoints = System.Array.Empty<Transform>();

        [Header("Results")]
        [SerializeField] private GameObject victoryGroup;
        [SerializeField] private GameObject defeatGroup;
        [Tooltip("Filled with the fight time and each player's damage when the fight ends.")]
        [SerializeField] private Text[] statsLabels = System.Array.Empty<Text>();
        [Tooltip("Filled with the ready-up prompt and count on defeat.")]
        [SerializeField] private Text[] readyLabels = System.Array.Empty<Text>();

        private readonly List<IEncounterBoss> _bosses = new();
        private readonly List<BossDiscordance> _bossMeters = new();
        private GameSession _session;
        private readonly Dictionary<Player, float> _damage = new();
        private readonly Dictionary<Player, int> _lastHealth = new();
        private float _startedAt;
        private float _endedAt = -1f;
        private int _bossPhase = 1;
        private int _startingPlayers = 1;
        private bool UsesLegacyDiscordance => discordance != null && _bossMeters.Count == 0;

        public IReadOnlyList<Player> LivingPlayers => _session != null ? _session.LivingPlayers : Array.Empty<Player>();
        private IReadOnlyList<Player> Players => _session != null ? _session.Players : Array.Empty<Player>();

        public float FightTime =>_endedAt >= 0f ? _endedAt - _startedAt : Time.time - _startedAt;
        public float DamageDealtBy(Player player) => _damage.TryGetValue(player, out var damage) ? damage : 0f;

        public DiscordanceTuning DiscordanceSettings => discordance;
        public float Discordance { get; private set; }
        public float DiscordanceMax => discordance != null ? discordance.max : 100f;

        /// <summary>0 below the first threshold, then 1, 2, 3... as each threshold is reached.</summary>
        public int DiscordanceLevel { get; private set; }

        /// <summary>The meter's new value, whenever it moves.</summary>
        public event Action<float> DiscordanceChanged;

        /// <summary>Previous level, new level. Fires going up and coming back down; treat the level as a state.</summary>
        public event Action<int, int> DiscordanceLevelChanged;

        private void Awake()
        {
            _session = FindFirstObjectByType<GameSession>();
            if (victoryGroup) victoryGroup.SetActive(false);
            if (defeatGroup) defeatGroup.SetActive(false);

            foreach (var behaviour in bosses)
            {
                if (behaviour is IEncounterBoss boss)
                {
                    _bosses.Add(boss);
                    if (behaviour.TryGetComponent(out BossDiscordance meter)) _bossMeters.Add(meter);
                }
                else if (behaviour != null) Debug.LogError($"{behaviour.name} does not implement {nameof(IEncounterBoss)}.", behaviour);
            }

            if (gameManager == null)
            {
                // test scenes: still place players, but run no fight (no win/loss, stats, Discordance or targets).
                Debug.LogWarning($"{nameof(EncounterManager)} has no {nameof(GameManager)}: players are placed, but the fight has no win/loss. Bosses target the session's players.", this);
                return;
            }

            if (discordance == null && _bossMeters.Count == 0)
                Debug.LogWarning($"{nameof(EncounterManager)} has no {nameof(DiscordanceTuning)}; the Discordance meter stays at 0.", this);
        }

        private void OnEnable()
        {
            if (gameManager == null) return;
            gameManager.StateChanged += HandleStateChanged;
            gameManager.ReadyChanged += RefreshReadyLabels;
            if (UsesLegacyDiscordance) ResonanceResolver.Activated += HandleResonance;
            foreach (var boss in _bosses)
            {
                boss.Defeated += HandleBossDefeated;
                boss.Damaged += HandleBossDamaged;
                boss.PhaseChanged += HandleBossPhaseChanged;
            }
        }

        private void OnDisable()
        {
            if (gameManager == null) return;
            gameManager.StateChanged -= HandleStateChanged;
            gameManager.ReadyChanged -= RefreshReadyLabels;
            ResonanceResolver.Activated -= HandleResonance;
            foreach (var meter in _bossMeters)
                if (meter) meter.StopEncounter();
            foreach (var boss in _bosses)
            {
                boss.Defeated -= HandleBossDefeated;
                boss.Damaged -= HandleBossDamaged;
                boss.PhaseChanged -= HandleBossPhaseChanged;
            }
        }

        private void Start()
        {
            PlacePlayers();
            if (gameManager == null)
            {
                enabled = false;
                return;
            }

            foreach (var boss in _bosses) boss.SetTargets(LivingPlayers);
            gameManager.BeginEncounter();
        }

        private void Update()
        {
            if (gameManager.State != GameState.Encounter) return;

            // defeat: everyone down at the same moment. Polled so players who join mid-fight count too.
            var players = 0;
            var downed = 0;
            var heartsLost = 0;
            var roster = Players;
            for (var i = 0; i < roster.Count; i++)
            {
                var player = roster[i];
                if (player == null) continue;
                players++;
                if (player.Health.IsDowned) downed++;

                if (!UsesLegacyDiscordance) continue;
                // a boss hit costs one heart, so every heart lost since last frame is one hit.
                var health = player.Health.CurrentHealth;
                if (_lastHealth.TryGetValue(player, out var last) && health < last) heartsLost += last - health;
                _lastHealth[player] = health;
            }

            if (UsesLegacyDiscordance)
                AddDiscordance(discordance.RateForPhase(_bossPhase) * Time.deltaTime + heartsLost * discordance.gainPerHit);

            if (players > 0 && downed == players) gameManager.EndEncounter(victory: false);
        }

        private void PlacePlayers()
        {
            if (Players.Count == 0)
            {
                Debug.LogWarning($"No players were carried over. Start from the {SceneNames.Lobby} scene to join and pick characters first.");
                return;
            }

            var spawnIndex = 0;
            foreach (var player in Players.Where(player => player != null).OrderBy(player => player.PlayerIndex))
            {
                if (spawnPoints.Length > 0)
                {
                    var spawn = spawnPoints[spawnIndex % spawnPoints.Length];
                    var characterController = player.GetComponent<CharacterController>();
                    if (characterController) characterController.enabled = false;
                    player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
                    if (characterController) characterController.enabled = true;
                }

                player.GetComponent<PlayerInput>().ActivateInput();
                player.enabled = true;
                spawnIndex++;
            }
        }

        private void HandleStateChanged(GameState previous, GameState next)
        {
            if (previous == GameState.Encounter && next != GameState.Encounter)
                foreach (var meter in _bossMeters)
                    if (meter) meter.StopEncounter();

            switch (next)
            {
                case GameState.Encounter:
                    _damage.Clear();
                    _startedAt = Time.time;
                    _endedAt = -1f;
                    if (UsesLegacyDiscordance) StartDiscordance();
                    foreach (var meter in _bossMeters)
                        if (meter) meter.StartEncounter(_session);
                    break;

                case GameState.Victory:
                case GameState.Defeat:
                    _endedAt = Time.time;
                    FreezeBosses();
                    if (victoryGroup) victoryGroup.SetActive(next == GameState.Victory);
                    if (defeatGroup) defeatGroup.SetActive(next == GameState.Defeat);
                    var stats = BuildStats();
                    foreach (var label in statsLabels) if (label) label.text = stats;
                    RefreshReadyLabels();
                    break;
            }
        }

        private void HandleBossDefeated(IEncounterBoss _)
        {
            if (gameManager.State != GameState.Encounter) return;
            foreach (var boss in _bosses)
                if (!boss.IsDefeated) return;
            gameManager.EndEncounter(victory: true);
        }

        private void HandleBossDamaged(HitInfo hit, float applied)
        {
            if (gameManager.State != GameState.Encounter || applied <= 0f || hit.Attacker == null) return;
            var player = hit.Attacker.GetComponentInParent<Player>();
            if (player == null) return;
            _damage.TryGetValue(player, out var total);
            _damage[player] = total + applied;
        }

        private void HandleBossPhaseChanged(int phase) => _bossPhase = Mathf.Max(1, phase);

        private void HandleResonance(ResonanceActivation activation)
        {
            if (discordance == null || gameManager.State != GameState.Encounter) return;
            var scale = discordance.referencePlayers / (float)Mathf.Max(1, _startingPlayers);
            AddDiscordance(-discordance.reductionPerResonance * scale);
        }

        // every encounter starts the meter at 0, in the boss's current phase, with the team at fight start.
        private void StartDiscordance()
        {
            _bossPhase = 1;
            foreach (var boss in _bosses) _bossPhase = Mathf.Max(_bossPhase, boss.Phase);

            _lastHealth.Clear();
            _startingPlayers = 0;
            foreach (var player in Players)
            {
                if (player == null) continue;
                _startingPlayers++;
                _lastHealth[player] = player.Health.CurrentHealth;
            }

            SetDiscordance(0f);
            DiscordanceChanged?.Invoke(Discordance);
        }

        private void AddDiscordance(float delta)
        {
            if (delta != 0f) SetDiscordance(Discordance + delta);
        }

        private void SetDiscordance(float value)
        {
            value = Mathf.Clamp(value, 0f, DiscordanceMax);
            if (value != Discordance)
            {
                Discordance = value;
                DiscordanceChanged?.Invoke(value);
            }

            var level = discordance != null ? discordance.LevelFor(value) : 0;
            if (level == DiscordanceLevel) return;
            var previous = DiscordanceLevel;
            DiscordanceLevel = level;
            DiscordanceLevelChanged?.Invoke(previous, level);
        }

        private void FreezeBosses()
        {
            // a boss still standing after a defeat stops attacking while the results are up.
            foreach (var boss in bosses)
                if (boss != null) boss.enabled = false;
        }

        private string BuildStats()
        {
            var text = new StringBuilder();
            var seconds = Mathf.Max(0f, FightTime);
            text.Append($"Time  {(int)(seconds / 60f)}:{(int)(seconds % 60f):00}");

            foreach (var player in Players.Where(player => player != null).OrderBy(player => player.PlayerIndex))
            {
                var name = player.Character != null ? player.Character.DisplayName : $"Player {player.PlayerIndex + 1}";
                var colour = ColorUtility.ToHtmlStringRGB(player.PlayerColor);
                text.Append($"\n<color=#{colour}>P{player.PlayerIndex + 1} {name}</color>  {Mathf.RoundToInt(DamageDealtBy(player))} damage");
            }

            return text.ToString();
        }

        private void RefreshReadyLabels()
        {
            if (gameManager.State != GameState.Defeat) return;
            var text = $"Press A / E to return to the lobby  ({gameManager.ReadyCount}/{gameManager.PlayerCount} ready)";
            foreach (var label in readyLabels) if (label) label.text = text;
        }
    }
}
