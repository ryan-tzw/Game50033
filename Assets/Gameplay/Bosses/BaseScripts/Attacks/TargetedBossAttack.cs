namespace Game50033
{
    using System.Collections;
    using System.Collections.Generic;
    using Unity.VisualScripting;
    using UnityEngine;

    public class TargetedBossAttack : BaseBossAttack
    {
        [SerializeField] protected BaseBossProjectile _projectile;
        [SerializeField] private int _numberOfProjectiles;
        [SerializeField] private int _numberOfTargets;

        [Header("Audio")]
        [SerializeField] private BossAttackAudioTuning audioTuning;
        private AudioSource _sfxSource;
        private bool _releaseSoundPlayed;

        public bool TriggerAttack;
        private readonly List<Player> _targetCandidates = new(4);

        [SerializeField] protected List<Player> _targetedPlayers = new List<Player>();

        [Header("Projectile Attributes for Projectile-based Targeted Attacks")]
        public GameObject lingeringObject;
        public bool homing, destroyOnArrival, bouncy, blockedByObstacles, lingeringEffect, piercing;
        [Min(0)] public float projectileSpeed;
        public float projectileSpeedMultiplier = 1f;
        public float projectileHomingHeight = 0f;
        [SerializeField] private float spreadAngle;
        [SerializeField] private float spreadRadius;
        [SerializeField] private BaseTelegraph telegraphPrefab;
        [SerializeField] private bool extendToWall = false;
        [SerializeField] protected float maxDistance = 30f;
        [SerializeField] private string bossWindUpAnimationName;
        [SerializeField] private string bossAttackAnimationName;
        protected int bossWindUpAnimationId;
        protected int bossAttackAnimationId;

        private int projectileCount;

        private readonly List<BaseBossProjectile> _controlledProjectiles = new();
        private readonly List<BaseTelegraph> _controlTelegraphs = new();
        private int _pendingControlShots;
        private bool _controlReleased;
        private bool _controlStarted;

        public override bool ControlsBoss => _projectile != null && _projectile.ControlsBoss;
        public override bool CanStart => !ControlsBoss || _targetedPlayers.Count > 0;

        public override float BossControlTimeout
        {
            get
            {
                // Charge normally ends when its projectile releases the boss
                // this the boss recover if the charge doesn't reach a wall
                float speed = projectileSpeed * projectileSpeedMultiplier;

                float travel = speed > 0f ? Mathf.Max(0f, maxDistance) / speed : 0f;

                if (float.IsNaN(travel) || float.

                IsInfinity(travel)) travel = 0f;

                return Mathf.Max(0f, windUpTime) + travel + Time.maximumDeltaTime;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            if (bossWindUpAnimationName != null) bossWindUpAnimationId = Animator.StringToHash(bossWindUpAnimationName);
            if (bossAttackAnimationName != null) bossAttackAnimationId = Animator.StringToHash(bossAttackAnimationName);

        }

        public override void Setup(BaseBoss owner)
        {
            base.Setup(owner);
            _sfxSource = owner.GetComponent<AudioSource>();
            CalculateTargets();
        }

        protected void CalculateTargets()
        {
            _targetedPlayers.Clear();
            //if (_numberOfTargets == 0) return;

            _targetCandidates.Clear();
            var targets = boss.Targets;
            var count = targets.Count;
            for (var i = 0; i < count; i++)
            {
                var target = targets[i];
                if (target == null) continue;
                if (ControlsBoss)
                {
                    var from = boss.transform.position;
                    var end = GetTelegraphEnd(from, RetrievePlayerPosition(target));

                    // Exclude blocked targets
                    if (!_projectile.CanLaunch(from, (end - from).normalized)) continue;
                }
                _targetCandidates.Add(target);
            }
            if (_targetCandidates.Count == 0) return;

            int clampedTargets = Mathf.Clamp(_numberOfTargets, 1, _targetCandidates.Count);

            for (int i = 0; i < clampedTargets; i++)
            {
                Player _targetPlayer = _targetCandidates[Random.Range(0, _targetCandidates.Count)];
                _targetedPlayers.Add(_targetPlayer);
                _targetCandidates.Remove(_targetPlayer);
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
            if (!ControlsBoss) return;

            StopAllCoroutines();
            _pendingControlShots = 0;
            foreach (var telegraph in _controlTelegraphs)
                if (telegraph != null) Destroy(telegraph.gameObject);
            _controlTelegraphs.Clear();

            while (_controlledProjectiles.Count > 0)
            {
                int last = _controlledProjectiles.Count - 1;
                var projectile = _controlledProjectiles[last];
                _controlledProjectiles.RemoveAt(last);
                if (projectile != null) projectile.InterruptBossControl();
            }
            TryReleaseBossControl();
        }

        public override void Attack()
        {

            if (ControlsBoss && _controlStarted) return;
            base.Attack();
            _releaseSoundPlayed = false;
            if (audioTuning != null) PlaySound(audioTuning.windUpSound);

            if (ControlsBoss)
            {
                _controlStarted = true;
                foreach (var player in _targetedPlayers)
                    if (player != null) _pendingControlShots += Mathf.Max(0, _numberOfProjectiles);
            }

            var facingSet = false;
            foreach (Player player in _targetedPlayers)
            {
                if (player == null) continue;
                if (!facingSet)
                {
                    boss.Movement.FaceTowards(RetrievePlayerPosition(player));
                    facingSet = true;
                }

                for (projectileCount = 1; projectileCount <= _numberOfProjectiles; projectileCount++)
                {
                    FireAttack(player);
                }
                projectileCount = 1;
            }
            if (ControlsBoss) TryReleaseBossControl();
        }

        internal void ReleaseBossControl(BaseBossProjectile projectile)
        {
            if (!_controlledProjectiles.Remove(projectile)) return;
            TryReleaseBossControl();
        }

        private void TryReleaseBossControl()
        {
            if (_controlReleased || _pendingControlShots > 0 || _controlledProjectiles.Count > 0) return;
            _controlReleased = true;
            NotifyBossControlReleased();
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

        protected virtual void FireAttack(Player player)
        {
            StartCoroutine(FireAfterWindUp(player));
        }

        protected virtual IEnumerator FireAfterWindUp(Player player)
        {
            Vector3 target = CalculateBulletSpread(RetrievePlayerPosition(player));
            Vector3 from = boss.transform.position;
            Vector3 end = GetTelegraphEnd(from, target);

            Vector3 basePosition = player != null ? RetrievePlayerPosition(player) : from;

            BaseTelegraph telegraph = null;
            if (telegraphPrefab != null && windUpTime > 0f)
            {
                telegraph = Instantiate(telegraphPrefab);
                telegraph.Begin(from, end, windUpTime);
                if (ControlsBoss) _controlTelegraphs.Add(telegraph);
            }

            if (bossWindUpAnimationId != 0 & windUpTime > 0f)
            {
                boss.PlayAnimationTrigger(bossWindUpAnimationId);
            }

            //yield return new WaitForSeconds(windUpTime);
            float waited = 0f;
            while (waited < windUpTime)
            {
                waited += Time.deltaTime;

                if (homing && player != null)
                {
                    end = RetrievePlayerPosition(player);   // follow the player during the wind-up
                    if (telegraph != null) telegraph.SetTarget(from, end);
                }
                yield return null;
            }


            if (telegraph != null)
            {
                _controlTelegraphs.Remove(telegraph);
                Destroy(telegraph.gameObject);
            }

            if (!_releaseSoundPlayed)
            {
                _releaseSoundPlayed = true;
                if (audioTuning != null) PlaySound(audioTuning.releaseSound);
            }
            if (bossAttackAnimationId != 0) boss.PlayAnimationTrigger(bossAttackAnimationId);
            var projectile = Instantiate(_projectile, transform.position, Quaternion.identity);
            if (ControlsBoss)
            {
                _controlledProjectiles.Add(projectile);
                _pendingControlShots--;
            }
            projectile.InitializeProjectile(player, boss, end, this);
        }

        private void PlaySound(AudioClip clip)
        {
            // The boss owns the source so attack cleanup does not cut off the sound.
            if (_sfxSource != null && _sfxSource.isActiveAndEnabled && clip != null)
                _sfxSource.PlayOneShot(clip, audioTuning.volume);
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

            // If the player overlaps the boss, charge in the direction the boss is already facing, to prevent it being zero direction and cancelling the charge
            if (direction == Vector3.zero)
                direction = boss.Movement.FlipX ? Vector3.right : Vector3.left;

            float closest = maxDistance;
            foreach (var hit in Physics.RaycastAll(from, direction, maxDistance))
            {
                bool isWall = hit.collider.CompareTag("Wall") || (hit.collider.transform.parent != null && hit.collider.transform.parent.CompareTag("Wall"));
                if (isWall && hit.distance - 0.5f < closest)
                    closest = Mathf.Max(hit.distance - 0.5f, 0f);
            }
            return from + direction * closest;
        }

        public void SetProjectileSpeedMultiplier(float multiplierValue)
        {
            projectileSpeedMultiplier = multiplierValue;
        }
    }
}
