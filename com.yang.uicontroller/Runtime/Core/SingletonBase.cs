using UnityEngine;

namespace Yang.UIController
{
    public abstract class SingletonBase<TSelf> : MonoBehaviour where TSelf : MonoBehaviour
    {
        private static TSelf instance;
        public static TSelf Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<TSelf>();

                    if (instance == null)
                    {
                        GameObject obj = new() { name = typeof(TSelf).Name };

                        instance = obj.AddComponent<TSelf>();
                    }
                }

                return instance;
            }
        }
    }
}
