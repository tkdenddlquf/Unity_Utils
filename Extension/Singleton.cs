using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : Component
{
    public static T _Instance;
    public static T Instance
    {
        get
        {
            if (_Instance == null)
            {
                _Instance = FindFirstObjectByType<T>();

                if (_Instance == null)
                {
                    GameObject obj = new() { name = typeof(T).Name };

                    _Instance = obj.AddComponent<T>();
                }
            }

            return _Instance;
        }
    }
}
