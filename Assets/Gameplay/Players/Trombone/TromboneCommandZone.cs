namespace Game50033
{
    using UnityEngine;
    public class TromboneCommandZone : MonoBehaviour
    {
        private bool _tromboneInZone = true;
        private float _commandZoneRadius = 1f;
        private Transform _tromboneTransform;

        private void Start()
        {
            _tromboneTransform = gameObject.transform.parent;
            gameObject.transform.SetParent(null);
        }

        private void Update()
        {
            if (gameObject.transform != null && _tromboneTransform != null)
            {
                _tromboneInZone = TromboneInZone();
            }
                

            if (!_tromboneInZone)
            {
                // TODO: Active rev here
                Destroy(gameObject);
            }
                
        }
        private bool TromboneInZone()
        {
            float distanceFromZone = (_tromboneTransform.position - gameObject.transform.position).sqrMagnitude;
            if (distanceFromZone > _commandZoneRadius * _commandZoneRadius)
                return false;
            Debug.Log(distanceFromZone);
            return true;
        }
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _commandZoneRadius);
        }
    }
}
