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
        private readonly List<BossCollider> _bossColliders = new();

        internal readonly struct BossCollider
        {
            internal readonly Behaviour Owner;
            internal readonly Collider Collider;
            internal readonly Vector3[] MeshVertices;

            internal BossCollider(Behaviour owner, Collider collider)
            {
                Owner = owner;
                Collider = collider;
                MeshVertices = collider is MeshCollider { convex: true, sharedMesh: { isReadable: true } mesh }
                    ? mesh.vertices : null;
            }
        }

        public IReadOnlyList<DebugAttackSnapshot> AttackSnapshots => _attackSnapshotsView ??= _attackSnapshots.AsReadOnly();
        public IReadOnlyList<DebugProjectileTrace> ProjectileTraces => _projectileTracesView ??= _projectileTraces.AsReadOnly();
        internal IReadOnlyList<BossCollider> BossColliders => _bossColliders;

        public bool NoCooldowns { get; private set; }
        public bool Invincibility { get; set; }
        public bool InfiniteDodge { get; set; }
        public bool ShowHitboxes { get; set; }
        public bool ShowEnemyHurtboxes { get; set; } = true;
        public bool ShowPlayerHurtboxes { get; set; } = true;
        public bool ShowPlayerAttacks { get; set; } = true;
        public bool ShowBossAttacks { get; set; } = true;
        public bool ShowReverberationAreas { get; set; } = true;
        public bool ShowSpecialZones { get; set; } = true;
        public bool BlocksMouseCombat { get; internal set; }

        public DebugState(IReadOnlyList<Player> players)
        {
            _players = players;
        }

        public void RecordAttack(in DebugAttackSnapshot snapshot)
        {
            if (!ShowHitboxes) return;
            var visible = snapshot.Shape == DebugAttackSnapshot.Geometry.BossLine ? ShowBossAttacks
                : snapshot.IsReverberation ? ShowReverberationAreas
                : ShowPlayerAttacks || ShowSpecialZones && snapshot.HasSweetSpot;
            if (!visible) return;
            _attackSnapshots.Add(snapshot);
        }

        internal void RegisterBossCollider(Behaviour owner, Collider collider) =>
            _bossColliders.Add(new BossCollider(owner, collider));

        public void ExpireAttackSnapshots(float lifetime)
        {
            for (var i = _bossColliders.Count - 1; i >= 0; i--)
            {
                var shape = _bossColliders[i];
                if (shape.Owner == null || shape.Collider == null || !shape.Collider.gameObject.scene.isLoaded)
                    _bossColliders.RemoveAt(i);
            }

            for (var i = _attackSnapshots.Count - 1; i >= 0; i--)
            {
                var snapshot = _attackSnapshots[i];
                if (!snapshot.Scene.isLoaded || (snapshot.LiveFrame >= 0
                    ? snapshot.LiveFrame != Time.frameCount : Time.time - snapshot.RecordedAt >= Mathf.Max(0f, lifetime)))
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

        internal void BeginSphereSweep(Behaviour owner, ref DebugProjectileTrace trace,
            Vector3 centre, float radius, Vector3 motion)
        {
            trace?.BeginFrame();
            if (!ShowHitboxes || !ShowPlayerAttacks) return;
            if (trace == null)
            {
                trace = new DebugProjectileTrace(owner, keepLastStep: true);
                trace.BeginFrame();
            }
            trace.AddSweep(centre, centre, radius, motion);
            trace.CompleteFrame(0f);
            RecordProjectileTrace(trace);
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
