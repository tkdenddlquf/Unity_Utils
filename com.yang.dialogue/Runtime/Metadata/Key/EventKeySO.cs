using System.Collections.Generic;
using UnityEngine;

namespace Yang.Dialogue
{
    [System.Serializable, CreateAssetMenu(fileName = "NewEventKey", menuName = "Dialogue/Key/Event")]
    public class EventKeySO : ScriptableObject
    {
        public string key;

        public override string ToString() => key;

        public static int FindIndex(List<EventKeySO> list, string target)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].key == target) return i;
            }

            return -1;
        }
    }
}