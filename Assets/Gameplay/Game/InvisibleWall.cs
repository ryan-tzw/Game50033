namespace Game50033
{
    using System.Collections.Generic;
    using UnityEngine;

    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class InvisibleWall : MonoBehaviour
    {
        private static readonly List<InvisibleWall> _active = new();
        internal static IReadOnlyList<InvisibleWall> Active => _active;
        internal static readonly Color OutlineColor = new(1f, 0.65f, 0.15f);
        internal BoxCollider Volume { get; private set; }

        private void OnEnable()
        {
            Volume = GetComponent<BoxCollider>();
            _active.Add(this);
        }

        private void OnDisable() => _active.Remove(this);

        internal static int ExcludeFrom(int layerMask)
        {
            var layer = LayerMask.NameToLayer("PlayerMovementWall");
            return layer >= 0 ? layerMask & ~(1 << layer) : layerMask;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (Camera.current == null || Camera.current.cameraType != CameraType.SceneView
                || !TryGetComponent<BoxCollider>(out var box) || !box.enabled || box.isTrigger) return;

            var previousMatrix = Gizmos.matrix;
            var previousColor = Gizmos.color;
            Gizmos.matrix = box.transform.localToWorldMatrix;
            Gizmos.color = OutlineColor;
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
#endif
    }
}
