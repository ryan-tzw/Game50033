namespace Game50033
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    public sealed class DebugProjectileTrace
    {
        public readonly struct Sweep
        {
            public readonly Vector3 Inside;
            public readonly Vector3 Outside;
            public readonly float Radius;
            public readonly Vector3 Motion;

            public Sweep(Vector3 inside, Vector3 outside, float radius, Vector3 motion)
            {
                Inside = inside;
                Outside = outside;
                Radius = radius;
                Motion = motion;
            }
        }

        private readonly List<Sweep> _sweeps = new(128);
        private readonly IReadOnlyList<Sweep> _sweepsView;

        public Behaviour Owner { get; }
        public Scene Scene { get; }
        public IReadOnlyList<Sweep> Sweeps => _sweepsView;
        public int Frame { get; private set; }
        public float RecordedAt { get; private set; }
        public float AllowedFraction { get; private set; } = 1f;
        public bool IsFinished { get; private set; }
        internal bool KeepLastStep { get; }

        public DebugProjectileTrace(Behaviour owner) : this(owner, false) { }

        internal DebugProjectileTrace(Behaviour owner, bool keepLastStep)
        {
            Owner = owner;
            KeepLastStep = keepLastStep;
            Scene = owner.gameObject.scene;
            _sweepsView = _sweeps.AsReadOnly();
        }

        public void BeginFrame()
        {
            _sweeps.Clear();
            Frame = Time.frameCount;
            RecordedAt = Time.time;
            AllowedFraction = 1f;
            IsFinished = false;
        }

        public void AddSweep(Vector3 inside, Vector3 outside, float radius, Vector3 motion)
        {
            _sweeps.Add(new Sweep(inside, outside, radius, motion));
        }

        public void CompleteFrame(float allowedFraction)
        {
            AllowedFraction = Mathf.Clamp01(allowedFraction);
        }

        public void Finish()
        {
            if (IsFinished) return;
            IsFinished = true;
            RecordedAt = Time.time;
        }
    }
}
