using System;
using UnityEngine;

namespace Yang.Network.Relay
{
    public class NetworkSingleton<T> : MonoBehaviour where T : Component
    {
        private static T instance;
        public static T Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<T>();

                    if (instance == null) throw new Exception("There is no instance of " + typeof(T).Name + " in the scene.");
                }

                return instance;
            }
        }
    }
}