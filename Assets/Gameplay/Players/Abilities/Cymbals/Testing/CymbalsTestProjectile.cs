namespace Game50033
{
    using UnityEngine;

    // test only: built like the boss projectiles on the bosses branch (trigger sphere, no Rigidbody, moved in
    // Update, damages a Player it touches through PlayerHealth.TryTakeDamage), so the Cymbals parry can be
    // tested before those are merged. Spawned by CymbalsTestTurret.
    [DisallowMultipleComponent]
    public sealed class CymbalsTestProjectile : MonoBehaviour
    {
        private Vector3 _velocity;
        private float _dieAt;

        public void Launch(Vector3 velocity, float lifetime)
        {
            _velocity = velocity;
            _dieAt = Time.time + lifetime;
        }

        private void Update()
        {
            transform.position += _velocity * Time.deltaTime;
            if (Time.time >= _dieAt) Destroy(gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent(out Player player)) return;
            player.Health.TryTakeDamage(1);
            Destroy(gameObject);
        }
    }
}
