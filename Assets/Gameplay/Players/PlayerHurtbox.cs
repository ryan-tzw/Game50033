namespace Game50033
{
    using UnityEngine;

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerHurtbox : MonoBehaviour
    {
        [SerializeField] private PlayerHurtboxTuning tuning;
        private CharacterController _controller;

        public bool IsAvailable => isActiveAndEnabled && _controller != null && _controller.enabled;

        private void OnEnable()
        {
            _controller = GetComponent<CharacterController>();
            if (tuning != null) return;
            Debug.LogError("PlayerHurtbox requires a PlayerHurtboxTuning asset.", this);
            enabled = false;
        }

        public void GetWorldCapsule(out Vector3 bottom, out Vector3 top, out float radius)
        {
            if (_controller == null) _controller = GetComponent<CharacterController>();
            var scale = transform.lossyScale;
            var sourceRadius = Mathf.Max(0.001f, _controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
            var sourceHeight = Mathf.Max(sourceRadius * 2f, _controller.height * Mathf.Abs(scale.y));
            var size = Mathf.Clamp(tuning.sizeScale, 0.1f, 1f);

            // shrink around the source capsule's bottom surface, not its centre, so the feet stay fixed.
            // read the controller each time; runtime resizing and per-prefab dimensions remain authoritative.
            var feet = transform.TransformPoint(_controller.center) - transform.up * (sourceHeight * 0.5f);
            radius = sourceRadius * size;
            var height = sourceHeight * size;
            var centre = feet + transform.up * (height * 0.5f);
            var halfLine = Mathf.Max(0f, height * 0.5f - radius);
            bottom = centre - transform.up * halfLine;
            top = centre + transform.up * halfLine;
        }
    }
}
