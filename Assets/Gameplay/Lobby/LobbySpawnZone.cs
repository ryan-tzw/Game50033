namespace Game50033
{
    using UnityEngine;

    [DisallowMultipleComponent]
    public sealed class LobbySpawnZone : MonoBehaviour
    {
        private const int SlotCount = 4;

        [SerializeField, Min(0.1f)] private float width = 3.6f;
        [SerializeField, Min(0.1f)] private float depth = 0.8f;

        internal bool TryGetSpawnPosition(int playerIndex, out Vector3 position)
        {
            position = default;
            if (playerIndex < 0 || playerIndex >= SlotCount) return false;

            var slotCentre = (playerIndex + 0.5f) / SlotCount - 0.5f;
            position = transform.TransformPoint(new Vector3(slotCentre * width, 0f, 0f));
            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (Camera.current == null || Camera.current.cameraType != CameraType.SceneView) return;

            var previousMatrix = Gizmos.matrix;
            var previousColor = Gizmos.color;
            Gizmos.color = new Color(0.4f, 0.85f, 1f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(width, 0f, depth));
            Gizmos.matrix = Matrix4x4.identity;

            for (var slot = 0; slot < SlotCount; slot++)
            {
                TryGetSpawnPosition(slot, out var position);
                Gizmos.DrawWireSphere(position, 0.075f);
                UnityEditor.Handles.Label(position + transform.up * 0.12f, $"P{slot + 1}");
            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
#endif
    }
}
