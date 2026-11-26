using UnityEngine;

namespace Game50033
{
    public class EntryPoint : MonoBehaviour
    {
        [SerializeField] private BaseBoss bossPrefab;
        [SerializeField] private CameraMovement _camera;
        [SerializeField] private float panTime;
        [SerializeField] private float focusTime;

        private void OnTriggerEnter(Collider other)
        {
            Player player = other.GetComponentInParent<Player>();
            if (!player) return;
            player.Movement.enabled = false;
            _camera.FocusOn(bossPrefab.transform, panTime, focusTime);

        }
    }
}
