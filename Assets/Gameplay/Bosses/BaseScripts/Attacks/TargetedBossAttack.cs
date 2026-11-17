namespace Game50033
{
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
        public bool homing, destroyOnArrival, bouncy, blockedByObstacles, lingeringEffect;
        [Min(0.1f)] public float projectileSpeed;
        public float projectileHomingHeight = 0f;
        [SerializeField] private float spreadAngle;
        [SerializeField] private float spreadRadius;

        private int projectileCount;

        public override void Setup(BaseBoss owner)
        {
            base.Setup(owner);
            CalculateTargets();
        }

        private void CalculateTargets()
        {
            _players = new(FindFirstObjectByType<BaseBoss>().players);
            int clampedTargets = Mathf.Clamp(_numberOfTargets, 1, _players.Count);

            for (int i = 0; i < clampedTargets; i++)
            {
                Player _targetPlayer = _players[Random.Range(0, _players.Count)];
                if (!_targetedPlayers.Contains(_targetPlayer))
                {
                    _targetedPlayers.Add(_targetPlayer);
                    _players.Remove(_targetPlayer);
                }
            }
        }

        public void Update()
        {
            // For Debugging Purposes (Remove Later)
            if (TriggerAttack) { Attack(); TriggerAttack = false; }
        }

        protected void OnEnable()
        {
            CalculateTargets();
        }

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
            float spreadX;
            float spreadY;

            // Note: my math isn't great, please help to fix if there's any issues - Joyce
            if (homing || _numberOfProjectiles == 1 || projectileCount == 1) { return currentPosition; }

            if (projectileCount % 2 == 0)
            {
                spreadAngle = spreadAngle * (projectileCount / 2);
                spreadX = spreadRadius * Mathf.Cos(spreadAngle);
                spreadY = spreadRadius * Mathf.Sin(spreadAngle);
            }
            else
            {
                spreadAngle = spreadAngle * Mathf.Floor(projectileCount / 2);
                spreadX = -spreadRadius * Mathf.Cos(spreadAngle);
                spreadY = -spreadRadius * Mathf.Sin(spreadAngle);
            }

            return currentPosition + new Vector3(spreadX, 0f, spreadY);
        }

        protected void FireAttack(Player player)
        {
            Vector3 currentPlayerPosition = RetrievePlayerPosition(player);
            Vector3 target = CalculateBulletSpread(currentPlayerPosition);

            var projectile = Instantiate(_projectile, transform.position, Quaternion.identity);
            projectile.InitializeProjectile(player, boss, target, this);
        }

        public Vector3 RetrievePlayerPosition(Player player)
        {
            return player.transform.position;
        }
    }
}
