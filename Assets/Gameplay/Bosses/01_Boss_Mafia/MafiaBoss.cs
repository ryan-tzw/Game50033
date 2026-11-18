using NUnit.Framework.Constraints;
using UnityEngine;

namespace Game50033
{
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
            GameObject boundary = Instantiate(terrainBossBlockage, new Vector3(0f,0f,0f), Quaternion.identity);
            var attack = boundary.GetComponent<TerrainBossBlockage>();
            attack.Setup(this);
            attack.Attack();

            Attacks.SetPhase(phaseNumber);
            Attacks.SetAttackIntervalTime(2);
            Attacks.SetProjectileSpeed(1.5f);
        }
    }
}
