using System.Collections.Generic;
using UnityEngine;

namespace Game50033
{
    [System.Serializable]
    public struct AttackEntry
    {
        public GameObject attack; 
        [Min(0)] public int count;  
    }
    [CreateAssetMenu(fileName = "AttackSet", menuName = "Boss/AttackSet")]
    public class AttackSet : ScriptableObject
    {
        public List<AttackEntry> attacks = new();
    }
}
