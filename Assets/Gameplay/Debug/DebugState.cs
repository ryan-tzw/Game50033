namespace Game50033
{
    using System.Collections.Generic;

    // runtime data belongs to one session, not a shared tuning asset or the menu's visibility.
    public sealed class DebugState
    {
        private readonly IReadOnlyList<Player> _players;

        public bool NoCooldowns { get; private set; }
        public bool Invincibility { get; set; }
        public bool InfiniteDodge { get; set; }
        public bool ShowHitboxes { get; set; }
        public bool ShowEnemyHurtboxes { get; set; } = true;
        public bool BlocksMouseCombat { get; internal set; }

        public DebugState(IReadOnlyList<Player> players)
        {
            _players = players;
        }

        public void SetNoCooldowns(bool enabled)
        {
            NoCooldowns = enabled;
            if (!enabled) return;

            foreach (var player in _players)
                if (player != null && player.Abilities != null) player.Abilities.ClearCooldowns();
        }

        public void HealToFull()
        {
            foreach (var player in _players)
                if (player != null && player.Health != null && !player.Health.IsDowned)
                    player.Health.Heal(player.Health.MaxHealth);
        }
    }
}
