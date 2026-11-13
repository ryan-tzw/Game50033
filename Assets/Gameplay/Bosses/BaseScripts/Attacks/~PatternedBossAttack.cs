// namespace Game50033
// {
//     using UnityEngine;
//     using System.Collections.Generic;
//     using System.Xml.Serialization;

//     public class PatternedBossAttack : BaseBossAttack
//     {
//         [SerializeField] private BaseBossProjectile _projectile;
//         [SerializeField] private int _numberOfProjectiles;
//         [SerializeField] private int _numberOfTargets;

//         public bool TriggerAttack;
//         private List<Player> _players;

//         [SerializeField] private List<Player> _targetedPlayers = new List<Player>();

//         [Header("Projectile Attributes for Projectile-based Targeted Attacks")]
//         public bool homing;
//         public bool destroyOnArrival;
//         [SerializeField, Min(0.1f)] public float projectileSpeed;
//         [SerializeField] public float projectileHomingHeight = 0f;

//         protected override void Awake()
//         {
//             base.Awake();
//         }

//         protected void BuildPattern()
//         {

//         }

//         public void Update()
//         {
//             // For Debugging Purposes (Remove Later)
//             if (TriggerAttack) { Attack(); TriggerAttack = false; }
//         }

//         protected void OnEnable()
//         {
//             // CalculateTargets();
//         }

//         public override void Cancel()
//         {
//             CancelInvoke();
//         }

//         public override void Attack()
//         {
//             base.Attack();

//             foreach (Player player in _targetedPlayers)
//             {
//                 for (int i = 0; i < _numberOfProjectiles; i++)
//                 {
//                     FireAttack(player);
//                 }
//             }
//         }

//         protected Vector3 CalculateBulletSpread(Vector3 currentPosition)
//         {
//             if (homing || _numberOfProjectiles == 1) { return currentPosition; }

//             Vector2 spread = Random.insideUnitCircle * 1.5f;
//             return currentPosition + new Vector3(spread.x, 0f, spread.y);
//         }

//         protected void FireAttack(Player player)
//         {
//             Vector3 currentPlayerPosition = RetrievePlayerPosition(player);
//             Vector3 target = CalculateBulletSpread(currentPlayerPosition);

//             var projectile = Instantiate(_projectile, transform.position, Quaternion.identity);
//             projectile.InitializeProjectile(player, target, this);
//         }

//         public Vector3 RetrievePlayerPosition(Player player)
//         {
//             return player.transform.position;
//         }
//     }
// }
