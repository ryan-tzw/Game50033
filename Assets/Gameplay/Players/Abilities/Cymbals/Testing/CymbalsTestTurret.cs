namespace Game50033
{
    using UnityEngine;

    // test only: fires CymbalsTestProjectiles at the nearest player on a timer, for trying the Cymbals parry
    // in test scenes. Not used in real encounters; boss projectiles come from the bosses' own attacks.
    [DisallowMultipleComponent]
    public sealed class CymbalsTestTurret : MonoBehaviour
    {
        [Tooltip("Seconds between shots.")]
        [SerializeField, Min(0.1f)] private float interval = 1.2f;
        [SerializeField, Min(0.1f)] private float speed = 6f;
        [SerializeField, Min(0.1f)] private float lifetime = 4f;
        [SerializeField, Min(0.05f)] private float radius = 0.2f;
        [Tooltip("Height the shots travel at.")]
        [SerializeField] private float height = 0.5f;
        [Tooltip("Tag given to each shot so the parry recognises it.")]
        [SerializeField] private string projectileTag = "BossProjectile";
        [Tooltip("Optional material for the shots.")]
        [SerializeField] private Material material;

        private float _nextShotAt;

        private void Update()
        {
            if (Time.time < _nextShotAt) return;
            _nextShotAt = Time.time + interval;

            var target = NearestPlayer();
            if (target == null) return;
            var from = transform.position;
            from.y = target.transform.position.y + height;
            var to = target.transform.position + Vector3.up * height;
            var direction = to - from;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;

            var shot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shot.name = "Cymbals test projectile";
            shot.tag = projectileTag;
            shot.transform.position = from;
            shot.transform.localScale = Vector3.one * (radius * 2f);
            shot.GetComponent<SphereCollider>().isTrigger = true;
            if (material != null) shot.GetComponent<MeshRenderer>().sharedMaterial = material;
            shot.AddComponent<CymbalsTestProjectile>().Launch(direction.normalized * speed, lifetime);
        }

        private Player NearestPlayer()
        {
            Player nearest = null;
            var best = float.PositiveInfinity;
            foreach (var player in FindObjectsByType<Player>(FindObjectsSortMode.None))
            {
                if (!player.isActiveAndEnabled || !player.HasActiveInput || player.Health.IsDowned) continue;
                var distance = (player.transform.position - transform.position).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                nearest = player;
            }
            return nearest;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * height, 0.3f);
        }
    }
}
