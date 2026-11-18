namespace Game50033
{
    using UnityEngine;
    public class TerrainBossBlockage : CircularBossAttack
    {
        protected override void Update()
        {
            // if (boss.Health.IsDead) Destroy(_attackHolder); 
        }

        protected override void AttackTowardPoint(Vector3 pointOnCircumference)
        {
            var blockage = Instantiate(lingeringObject, pointOnCircumference, Quaternion.identity);
            blockage.transform.SetParent(transform);
        }
    }
}
