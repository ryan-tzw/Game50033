using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game50033
{
    public class BossAttackStack : MonoBehaviour
    {
        Stack<GameObject> bossAttackStack = new Stack<GameObject>();
        List<AttackEntry> bossAttackList = new List<AttackEntry>();

        // Enough attempts to finish the current stack and check one fresh shuffle.
        public int SelectionAttemptLimit
        {
            get
            {
                int count = bossAttackStack.Count;
                foreach (var entry in bossAttackList)
                    if (entry.attack != null) count += Mathf.Max(0, entry.count);
                return count;
            }
        }

        public void RegisterAttacks(List<AttackEntry> attacks) // list format -> attackList[basicAttackOne, basicAttackTwo, secondaryAttack, specialAttack]
        {
            bossAttackList = attacks ?? new List<AttackEntry>();
            bossAttackStack.Clear();

            if (bossAttackList.Count == 0)
            {
                Debug.LogWarning("No attacks registered in boss stack");
            }
        }

        private void ShuffleStack()
        {
            var sequence = new List<GameObject>();

            foreach(AttackEntry entry in bossAttackList)
            {
                if (entry.attack == null) continue;
                for (int c = 0; c < entry.count; c++)
                {
                    sequence.Add(entry.attack);
                }
            }

            for (int i = sequence.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (sequence[i], sequence[j]) = (sequence[j], sequence[i]);
            }

            bossAttackStack.Clear();
            foreach(GameObject attack in sequence)
            {
                bossAttackStack.Push(attack);
            }
        }

        public GameObject GetNext()
        {
            if (bossAttackStack.Count == 0) ShuffleStack();
            if (bossAttackStack.Count == 0) return null;
            return bossAttackStack.Pop();
            
        }

    }
}
