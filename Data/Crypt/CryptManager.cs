using UnityEngine;
using System.IO;
using UnityEditor;

public static class CryptManager
{
    public static void Save<T>(T data, string path, CryptType type = CryptType.Local)
    {
        switch (type)
        {
            case CryptType.Local:
                path = $"{Application.persistentDataPath}/{path}";
                break;

            case CryptType.Resources:
                path = $"Assets/Resources/{path}";
                break;
        }

        File.WriteAllText(path, Crypt.Encrypt(JsonUtility.ToJson(data)), System.Text.Encoding.UTF8);

#if UNITY_EDITOR
        if (type == CryptType.TextAsset)
        {
            File.WriteAllText(path + "_", JsonUtility.ToJson(data, true), System.Text.Encoding.UTF8);
            AssetDatabase.Refresh();
        }
#endif
    }

    public static T Load<T>(string path, CryptType type = CryptType.Local)
    {
        switch (type)
        {
            case CryptType.Local:
                path = $"{Application.persistentDataPath}/{path}";
                break;

            case CryptType.Resources:
                return JsonUtility.FromJson<T>(Crypt.Decrypt(Resources.Load<TextAsset>(path).text));

            case CryptType.TextAsset:
                return JsonUtility.FromJson<T>(Crypt.Decrypt(path));
        }

        if (File.Exists(path)) return JsonUtility.FromJson<T>(Crypt.Decrypt(File.ReadAllText(path, System.Text.Encoding.UTF8)));

        return default;
    }

    public static void Remove(string path, CryptType type = CryptType.Local)
    {
        switch (type)
        {
            case CryptType.Local:
                path = $"{Application.persistentDataPath}/{path}";
                break;

            case CryptType.Resources:
                path = $"Assets/Resources/{path}";
                break;
        }

        if (File.Exists(path)) File.Delete(path);
    }
}

