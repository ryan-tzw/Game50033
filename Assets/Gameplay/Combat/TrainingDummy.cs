namespace Game50033
{
    using UnityEngine;
    using UnityEngine.Events;

    /// <summary>
    /// A target that never dies. Flashes and wobbles when hit so players can try their attacks.
    /// </summary>
    public sealed class TrainingDummy : MonoBehaviour, IHittable
    {
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

        [SerializeField] private Renderer visual;
        [SerializeField, Min(0.01f)] private float flashDuration = 0.15f;
        [SerializeField, Min(0f)] private float shakeDistance = 0.08f;
        [SerializeField, Min(0.01f)] private float shakeDuration = 0.2f;
        [SerializeField] private UnityEvent<int> onHit = new();

        private MaterialPropertyBlock _properties;
        private Vector3 _visualRestPosition;
        private Vector3 _shakeDirection;
        private float _hitTime = float.NegativeInfinity;

        public int HitCount { get; private set; }

        private void Awake()
        {
            _properties = new MaterialPropertyBlock();
            if (visual) _visualRestPosition = visual.transform.localPosition;
        }

        public void TakeHit(in HitInfo hit)
        {
            HitCount++;
            _hitTime = Time.time;
            _shakeDirection = transform.InverseTransformDirection(hit.Direction);
            onHit.Invoke(HitCount);
        }

        private void Update()
        {
            if (!visual) return;

            var elapsed = Time.time - _hitTime;

            var flash = Mathf.Clamp01(1f - elapsed / flashDuration);
            visual.GetPropertyBlock(_properties);
            _properties.SetFloat(FlashAmountId, flash);
            visual.SetPropertyBlock(_properties);

            var shake = Mathf.Clamp01(1f - elapsed / shakeDuration);
            // Skip the shake calculation before the first hit to avoid NaN positions
            var wobble = shake > 0f ? Mathf.Sin(elapsed * 60f) * shake * shakeDistance : 0f;
            visual.transform.localPosition = _visualRestPosition + _shakeDirection * wobble;
        }
    }
}
