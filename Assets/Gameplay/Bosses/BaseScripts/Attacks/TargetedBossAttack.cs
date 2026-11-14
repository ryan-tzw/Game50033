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
        public bool homing;
        public bool destroyOnArrival;
        public bool bouncy;
        public bool blockedByObstacles;
        [SerializeField, Min(0.1f)] public float projectileSpeed;
        [SerializeField] public float projectileHomingHeight = 0f;

        protected override void Awake()
        {
            base.Awake();

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
                for (int i = 0; i < _numberOfProjectiles; i++)
                {
                    FireAttack(player);
                }
            }
        }

        protected Vector3 CalculateBulletSpread(Vector3 currentPosition)
        {
            if (homing || _numberOfProjectiles == 1) { return currentPosition; }

            Vector2 spread = Random.insideUnitCircle * 1.5f;
            return currentPosition + new Vector3(spread.x, 0f, spread.y);
        }

        protected void FireAttack(Player player)
        {
            Vector3 currentPlayerPosition = RetrievePlayerPosition(player);
            Vector3 target = CalculateBulletSpread(currentPlayerPosition);

            var projectile = Instantiate(_projectile, transform.position, Quaternion.identity);
            projectile.InitializeProjectile(player, target, this);
        }

        public Vector3 RetrievePlayerPosition(Player player)
        {
            return player.transform.position;
        }
    }
}
