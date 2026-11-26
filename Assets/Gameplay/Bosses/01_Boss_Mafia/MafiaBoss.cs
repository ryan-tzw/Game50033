namespace Game50033
{
    using UnityEngine;
    using System.Collections;

    public class MafiaBoss : BaseBoss
    {
        // protected int BossTotalPhases = 2;
        // protected float BossTargetClearTime = 150f;

        [Header("Phase Changes")]
        public GameObject terrainBossBlockage; // Attack to run during phase change

        protected override void HandlePhaseChange(int phaseNumber)
        {
            foreach (MinionBehaviour minion in FindObjectsByType<MinionBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                minion.gameObject.SetActive(true);
            GameObject boundary = Instantiate(terrainBossBlockage, new Vector3(0f, 0f, 0f), Quaternion.identity);
            var attack = boundary.GetComponent<TerrainBossBlockage>();
            attack.Setup(this);
            attack.Attack();

            Attacks.SetPhase(phaseNumber);
            Attacks.SetAttackIntervalTime(2);
            Attacks.SetProjectileSpeed(1.5f);

            try { StartCoroutine(PlayAnimationInPlace(PhaseChangeId)); }
            catch { Debug.LogWarning("Animator does not include Phase Change Animation"); }
        }

        public override IEnumerator PlayAnimationInPlace(int animationId)
        {
            Debug.Log("Playing Phase Changing Animation");
            yield return new WaitUntil(() => Movement.MoveToCenter());
            Attacks.enabled = false;
            yield return null;
            _attackAnimator.SetTrigger(animationId);
            yield return null;
            yield return new WaitUntil(() => _attackAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f);
            Attacks.enabled = true;
        }
    }
}
