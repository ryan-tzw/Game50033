using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game50033
{
    public class BossAttackStack : MonoBehaviour //In progess: Have not updated yet
    {
        [SerializeField] private int numberOfAttacks = 4;
        Stack<GameObject> bossAttackStack = new Stack<GameObject>();
        List<GameObject> bossAttackList = new List<GameObject>();

        public void RegisterAttacks(List<GameObject> attackList) // list format -> attackList[basicAttackOne, basicAttackTwo, secondaryAttack, specialAttack]
        {
            bossAttackList = attackList;
        }

       

        private void ShuffleStack()
        {
            List<GameObject> bossNormalAttacks = bossAttackList.GetRange(0, numberOfAttacks - 2);
            GameObject bossSpecialAttack = bossAttackList[numberOfAttacks - 1];

            var sequence = new List<GameObject>();

            for(int i = 0; i < 9; i++)
            {
                int randomAttack = UnityEngine.Random.Range(0, bossNormalAttacks.Count);
                sequence.Add(bossNormalAttacks[randomAttack]);
            }

            int randomPlacement = UnityEngine.Random.Range(0,bossAttackStack.Count + 1);
            sequence.Insert(randomPlacement, bossSpecialAttack);

            bossAttackList.Clear();
            for (int i = sequence.Count - 1; i >= 0; i--)
            {
                bossAttackStack.Push(sequence[i]);
            }
            
        }

        public void TriggerAttack()
        {
            if (bossAttackStack.Count == 0) ShuffleStack();

            GameObject attack = bossAttackStack.Pop();
            
        }

    }
}
