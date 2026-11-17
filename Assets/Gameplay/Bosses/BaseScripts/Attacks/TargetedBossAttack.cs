namespace Game50033
{
    using System.Collections;
    using System.Collections.Generic;
    using Unity.VisualScripting;
    using UnityEngine;

    public class TargetedBossAttack : BaseBossAttack
    {
        [SerializeField] private BaseBossProjectile _projectile;
        [SerializeField] private int _numberOfProjectiles;
        [SerializeField] private int _numberOfTargets;

        public bool TriggerAttack;
        private List<Player> _players;

        [SerializeField] private List<Player> _targetedPlayers = new List<Player>();

        [Header("Projectile Attributes for Projectile-based Targeted Attacks")]
        public GameObject lingeringObject;
        public bool homing, destroyOnArrival, bouncy, blockedByObstacles, lingeringEffect, piercing;
        [Min(0)] public float projectileSpeed;
        public float projectileHomingHeight = 0f;
        [SerializeField] private float spreadAngle;
        [SerializeField] private float spreadRadius;
        [SerializeField] private BaseTelegraph telegraphPrefab;
        [SerializeField] private bool extendToWall = false;
        [SerializeField] private float maxDistance = 30f;

        private int projectileCount;

        public override void Setup(BaseBoss owner)
        {
            base.Setup(owner);
            CalculateTargets();
        }

        private void CalculateTargets()
        {
            _targetedPlayers.Clear();
            //if (_numberOfTargets == 0) return;

            _players = new List<Player>(boss.players);
            if (_players.Count == 0) return;

            int clampedTargets = Mathf.Clamp(_numberOfTargets, 1, _players.Count);

            for (int i = 0; i < clampedTargets; i++)
            {
                Player _targetPlayer = _players[Random.Range(0, _players.Count)];   
                _targetedPlayers.Add(_targetPlayer);
                _players.Remove(_targetPlayer);
            }
        }

        public void Update()
        {
            // For Debugging Purposes (Remove Later)
            if (TriggerAttack) { Attack(); TriggerAttack = false; }
        }

        //protected void OnEnable()
        //{
        //    CalculateTargets();
        //}

        public override void Cancel()
        {
            CancelInvoke();
        }

        public override void Attack()
        {
            base.Attack();

            foreach (Player player in _targetedPlayers)
            {
                for (projectileCount = 1; projectileCount <= _numberOfProjectiles; projectileCount++)
                {
                    FireAttack(player);
                }
                projectileCount = 1;
            }
        }

        protected Vector3 CalculateBulletSpread(Vector3 currentPosition)
        {
            float angle;
            float spreadX;
            float spreadY;

            if (homing || _numberOfProjectiles == 1 || projectileCount == 1) { return currentPosition; }
            
            if (projectileCount % 2 == 0)
            {
                angle = spreadAngle * (projectileCount / 2) * Mathf.Deg2Rad;
                spreadX = spreadRadius * Mathf.Cos(angle);
                spreadY = spreadRadius * Mathf.Sin(angle);
            }
            else
            {
                angle = spreadAngle * (projectileCount / 2) * Mathf.Deg2Rad;
                spreadX = -spreadRadius * Mathf.Cos(angle);
                spreadY = -spreadRadius * Mathf.Sin(angle);
            }

            return currentPosition + new Vector3(spreadX, 0f, spreadY);
        }

        protected void FireAttack(Player player)
        {
            StartCoroutine(FireAfterWindUp(player));
        }

        private IEnumerator FireAfterWindUp(Player player)
        {
            Vector3 target = CalculateBulletSpread(RetrievePlayerPosition(player));
            Vector3 from = boss.transform.position;
            Vector3 end = GetTelegraphEnd(from, target);

            BaseTelegraph telegraph = null;
            if (telegraphPrefab != null && windUpTime > 0f)
            {
                telegraph = Instantiate(telegraphPrefab);
                telegraph.Begin(from, end, windUpTime);
            }

            yield return new WaitForSeconds(windUpTime);

            if (telegraph != null) Destroy(telegraph.gameObject);

            var projectile = Instantiate(_projectile, transform.position, Quaternion.identity);
            projectile.InitializeProjectile(player, boss, end, this);
        }

        public Vector3 RetrievePlayerPosition(Player player)
        {
            return player.transform.position;
        }

        protected virtual Vector3 GetTelegraphEnd(Vector3 from, Vector3 target)
        {
            if (_numberOfTargets == 0) return boss.transform.position;

            if (!extendToWall) return target;

            

            Vector3 direction = target - from;
            direction.y = 0f;
            direction.Normalize();

            float closest = maxDistance;
            foreach (var hit in Physics.RaycastAll(from, direction, maxDistance))
            {
                bool isWall = hit.collider.CompareTag("Wall") || (hit.collider.transform.parent != null && hit.collider.transform.parent.CompareTag("Wall"));
                if (isWall && hit.distance - 0.5f < closest)
                    closest = Mathf.Max(hit.distance - 0.5f, 0f);
            }
            return from + direction * closest;
        }
    }
}
