namespace Game50033
{
    using UnityEngine;

    [DisallowMultipleComponent]
    public sealed class PlayerHitstop : MonoBehaviour
    {
        private float _remaining;

        public bool IsStopped => isActiveAndEnabled && _remaining > 0f;
        public float ActionTime { get; private set; }
        public float ActionDeltaTime { get; private set; }

        public void Request(float duration)
        {
            if (!isActiveAndEnabled || duration <= 0f || float.IsNaN(duration) || float.IsInfinity(duration)) return;
            _remaining = Mathf.Max(_remaining, duration);
        }

        internal void Tick(float deltaTime)
        {
            deltaTime = Mathf.Max(0f, deltaTime);
            var paused = isActiveAndEnabled ? Mathf.Min(_remaining, deltaTime) : 0f;
            _remaining = Mathf.Max(0f, _remaining - paused);
            // consume only the paused part of the frame; never catch up time discarded by hitstop.
            ActionDeltaTime = deltaTime - paused;
            ActionTime += ActionDeltaTime;
        }

        public void Clear()
        {
            _remaining = 0f;
            ActionDeltaTime = 0f;
        }

        private void OnDisable()
        {
            Clear();
        }
    }
}
