using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

namespace Game50033
{
    public class MinionBehaviour : TargetedBossAttack
    {
        [SerializeField] private Vector3 _startPosition;
        [SerializeField] private Vector3 _endPosition;
        public float MovementSpeed;

        private Animator _animator;
        private static readonly int AttackId = Animator.StringToHash("Attack");

        private bool _isWalking;

        private bool _locked;
        private Vector3 _lockedPosition;

        private LineRenderer _lineRenderer;
        private Color _opaque = new Color(255, 255, 255, 1);
        private Color _translucent = new Color(255, 255, 255, 0.3f);
        private Color _transparent = new Color(255, 255, 255, 0);
        public LayerMask layerMask;

        private RaycastHit _hitInfo;

        // public override void Setup(BaseBoss owner)
        void Awake()
        {
            // base.Setup(owner);
            _startPosition = transform.position;
            _endPosition = new Vector3(_startPosition.x, _startPosition.y, -_startPosition.z);

            _animator = GetComponentInChildren<Animator>();
            _isWalking = true;
            boss = FindFirstObjectByType<BaseBoss>();

            _lineRenderer = GetComponentInChildren<LineRenderer>();
        }

        new void Update()
        {
            if (_targetedPlayers.Count == 0) CalculateTargets();
            if (TriggerAttack) Attack(); _isWalking = false; TriggerAttack = false;
            Move();
            _animator.SetBool("IsWalking", _isWalking);
        }

        void Move()
        {
            if (_locked) transform.position = _lockedPosition;
            else
            {
                if (Mathf.Abs(transform.position.z) >= Mathf.Abs(_startPosition.z)) _endPosition.z *= -1;
                transform.position = Vector3.MoveTowards(transform.position, _endPosition, MovementSpeed * Time.deltaTime);
            }
        }

        protected override IEnumerator FireAfterWindUp(Player player)
        {
            _animator.SetTrigger(AttackId);
            yield return null;

            _locked = true;
            _lockedPosition = transform.position;

            Vector3 currentPlayerPosition = RetrievePlayerPosition(player);
            Vector3 rayEndPosition = (currentPlayerPosition - _lockedPosition).normalized * maxDistance;

            _lineRenderer.SetPosition(1, rayEndPosition);

            for (int i = 0; i < 3; i++)
            {
                yield return new WaitForSeconds(windUpTime / 6);
                SetLRColor(_translucent);
                yield return new WaitForSeconds(windUpTime / 6);
                SetLRColor(_opaque);
            }

            var hit = Physics.Linecast(_lockedPosition, rayEndPosition, out _hitInfo, layerMask);
            var session = player != null ? player.Session : FindAnyObjectByType<GameSession>();
            if (session?.Debug is { ShowHitboxes: true, ShowBossAttacks: true } debug)
                debug.RecordAttack(DebugAttackSnapshot.BossLine(_lockedPosition, hit ? _hitInfo.point : rayEndPosition));
            if (hit)
            {
                var hitPlayer = _hitInfo.collider.GetComponentInParent<Player>();
                if (hitPlayer != null && hitPlayer.Health.TryTakeDamage(1, _lockedPosition - hitPlayer.transform.position))
                    Debug.Log("Damaging Player");
            }

            // Debug.DrawLine(_lockedPosition, rayEndPosition, new Color(0f, 0f, 1f), 2f);

            yield return new WaitUntil(() => !_animator.GetCurrentAnimatorStateInfo(0).IsName("MafiaMinion_Attacking"));
            _isWalking = true;
            _locked = false;
            SetLRColor(_transparent);
        }

        void SetLRColor(Color color)
        {
            _lineRenderer.startColor = color;
            _lineRenderer.endColor = color;
        }
    }
}
