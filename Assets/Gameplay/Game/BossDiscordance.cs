namespace Game50033
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    [DisallowMultipleComponent]
    public sealed class BossDiscordance : MonoBehaviour
    {
        [SerializeField] private DiscordanceTuning tuning;

        private readonly HashSet<PlayerHealth> _subscribedHealth = new();
        private IEncounterBoss _boss;
        private GameSession _session;

        public float Value { get; private set; }
        public float Maximum => tuning != null ? tuning.max : 100f;
        public IReadOnlyList<float> Thresholds => tuning != null ? tuning.thresholds ?? Array.Empty<float>() : Array.Empty<float>();
        public int Level { get; private set; }
        public float ConventionalDamageMultiplier => _session != null && Level >= 1 ? tuning.conventionalDamageMultiplier : 1f;
        public float AttackDelayMultiplier => _session != null && Level >= 2 ? tuning.attackDelayMultiplier : 1f;
        public float ProjectileSpeedMultiplier => _session != null && Level >= 3 ? tuning.projectileSpeedMultiplier : 1f;
        public float ProjectileCountMultiplier => _session != null && Level >= 3 ? tuning.projectileCountMultiplier : 1f;
        public event Action<float> ValueChanged;
        public event Action<int, int> LevelChanged;

        private void Awake()
        {
            _boss = GetComponent<IEncounterBoss>();
        }

        private void OnDisable() => StopEncounter();

        internal void StartEncounter(GameSession session)
        {
            StopEncounter();
            SetValue(0f);
            if (!isActiveAndEnabled || session == null || tuning == null || _boss == null || _boss.IsDefeated) return;

            _session = session;
            _session.PlayerRegistered += SubscribeToDamage;
            _session.PlayerUnregistered += UnsubscribeFromDamage;
            foreach (var player in _session.Players) SubscribeToDamage(player);
            ResonanceResolver.Activated += HandleResonance;
        }

        internal void StopEncounter()
        {
            if (ReferenceEquals(_session, null)) return;

            ResonanceResolver.Activated -= HandleResonance;
            _session.PlayerRegistered -= SubscribeToDamage;
            _session.PlayerUnregistered -= UnsubscribeFromDamage;
            _session = null;

            foreach (var health in _subscribedHealth)
                health.DamagedByBoss -= HandleBossDamage;
            _subscribedHealth.Clear();
        }

        private void Update()
        {
            if (_session != null && !_boss.IsDefeated)
                SetValue(Value + tuning.RateForPhase(_boss.Phase) * Time.deltaTime);
        }

        private void SubscribeToDamage(Player player)
        {
            if (player != null && player.Health != null && _subscribedHealth.Add(player.Health))
                player.Health.DamagedByBoss += HandleBossDamage;
        }

        private void UnsubscribeFromDamage(Player player)
        {
            // a destroyed Unity object can still hold the managed event subscription.
            if (!ReferenceEquals(player, null) && !ReferenceEquals(player.Health, null) && _subscribedHealth.Remove(player.Health))
                player.Health.DamagedByBoss -= HandleBossDamage;
        }

        private void HandleBossDamage(IEncounterBoss bossSource, int pipsLost)
        {
            if (ReferenceEquals(_boss, bossSource) && !_boss.IsDefeated)
                SetValue(Value + pipsLost * tuning.gainPerHit);
        }

        private void HandleResonance(ResonanceActivation activation)
        {
            if (_session != null && !_boss.IsDefeated
                && activation.Creator != null && activation.Resonator != null
                && activation.Creator.Session == _session && activation.Resonator.Session == _session)
                SetValue(Value - tuning.reductionPerResonance);
        }

        private void SetValue(float value)
        {
            value = Mathf.Clamp(value, 0f, Maximum);
            var previousValue = Value;
            var previousLevel = Level;
            Value = value;
            Level = tuning != null ? tuning.LevelFor(value) : 0;

            if (Value != previousValue) ValueChanged?.Invoke(Value);
            if (Level != previousLevel) LevelChanged?.Invoke(previousLevel, Level);
        }
    }
}
