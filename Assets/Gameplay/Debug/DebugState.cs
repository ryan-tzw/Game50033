namespace Game50033
{
    using System.Collections.Generic;
    using UnityEngine;

    // runtime data belongs to one session, not a shared tuning asset or the menu's visibility.
    public sealed class DebugState
    {
        private readonly IReadOnlyList<Player> _players;
        private readonly List<DebugAttackSnapshot> _attackSnapshots = new();
        private IReadOnlyList<DebugAttackSnapshot> _attackSnapshotsView;
        private readonly List<DebugProjectileTrace> _projectileTraces = new();
        private IReadOnlyList<DebugProjectileTrace> _projectileTracesView;

        public IReadOnlyList<DebugAttackSnapshot> AttackSnapshots => _attackSnapshotsView ??= _attackSnapshots.AsReadOnly();
        public IReadOnlyList<DebugProjectileTrace> ProjectileTraces => _projectileTracesView ??= _projectileTraces.AsReadOnly();

        public bool NoCooldowns { get; private set; }
        public bool Invincibility { get; set; }
        public bool InfiniteDodge { get; set; }
        public bool ShowHitboxes { get; set; }
        public bool ShowEnemyHurtboxes { get; set; } = true;
        public bool ShowPlayerHurtboxes { get; set; } = true;
        public bool ShowPlayerAttacks { get; set; } = true;
        public bool ShowSpecialZones { get; set; } = true;
        public bool BlocksMouseCombat { get; internal set; }

        public DebugState(IReadOnlyList<Player> players)
        {
            _players = players;
        }

        public void RecordAttack(in DebugAttackSnapshot snapshot)
        {
            if (!ShowHitboxes || !ShowPlayerAttacks && !(ShowSpecialZones && snapshot.HasSweetSpot)) return;
            _attackSnapshots.Add(snapshot);
        }

        public void ExpireAttackSnapshots(float lifetime)
        {
            for (var i = _attackSnapshots.Count - 1; i >= 0; i--)
            {
                var snapshot = _attackSnapshots[i];
                if (!snapshot.Scene.isLoaded || Time.time - snapshot.RecordedAt >= Mathf.Max(0f, lifetime))
                    _attackSnapshots.RemoveAt(i);
            }

            for (var i = _projectileTraces.Count - 1; i >= 0; i--)
            {
                var trace = _projectileTraces[i];
                if (!trace.IsFinished && (trace.Owner == null || !trace.Owner.isActiveAndEnabled)) trace.Finish();
                if (!trace.Scene.isLoaded || trace.IsFinished && Time.time - trace.RecordedAt >= Mathf.Max(0f, lifetime))
                    _projectileTraces.RemoveAt(i);
            }
        }

        public void RecordProjectileTrace(DebugProjectileTrace trace)
        {
            if (trace == null || !ShowHitboxes || !ShowPlayerAttacks || _projectileTraces.Contains(trace)) return;
            _projectileTraces.Add(trace);
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
