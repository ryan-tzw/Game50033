namespace Game50033
{
    using Unity.VisualScripting;
    using UnityEngine;

    public class TriggerMinionAttacks : TargetedBossAttack
    {
        [SerializeField] private MinionBehaviour[] minions;

        void Start()
        {
            minions = FindObjectsByType<MinionBehaviour>(FindObjectsSortMode.None);
        }

        protected override void FireAttack(Player player)
        {
            foreach (MinionBehaviour minion in minions) minion.TriggerAttack = true;
        }
    }
}
