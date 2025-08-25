using UnityEngine;
using System.Collections.Generic;
using System.IO;

public static class SimpleSaveManager
{
    private readonly static Dictionary<string, Dictionary<string, object>> dataObjectDict = new();
    private readonly static Dictionary<string, Dictionary<string, string>> dataJsonDict = new();

    public static void Add<T>(string category, T data) => Add(category, data.GetType().FullName, data);

    public static void Add<T>(string category, string name, T data)
    {
        if (!dataObjectDict.ContainsKey(category)) dataObjectDict[category] = new Dictionary<string, object>();

        dataObjectDict[category][name] = data;
    }

    public static void Remove<T>(string category, T data) => Remove(category, data.GetType().FullName);

    public static void Remove<T>(string category, string name)
    {
        if (!dataObjectDict.ContainsKey(category)) return;

        dataObjectDict[category].Remove(name);

        if (dataObjectDict[category].Count == 0) dataObjectDict.Remove(category);
    }

    public static T Get<T>(string category) => Get<T>(category, typeof(T).FullName);

    public static T Get<T>(string category, string name)
    {
        if (dataJsonDict.ContainsKey(category))
        {
            if (dataJsonDict[category].TryGetValue(name, out string json))
            {
                return JsonUtility.FromJson<T>(json);
            }
        }

        return default;
    }

    public static void Save(string category, string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return;
        if (!dataObjectDict.ContainsKey(category)) return;

        SaveContainer container = new();

        foreach (var kvp in dataObjectDict[category])
        {
            object data = kvp.Value;
            string key = kvp.Key;

            string json = JsonUtility.ToJson(data);

            SaveItem item = new()
            {
                key = key,
                data = json,
            };

            container.items.Add(item);
        }

        string finalJson = JsonUtility.ToJson(container, true);

        fileName = Path.Combine(Application.persistentDataPath, fileName);

        File.WriteAllText(fileName, finalJson);

        Debug.Log("Saved to: " + fileName);
    }

    public static void Load(string category, string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return;

        fileName = Path.Combine(Application.persistentDataPath, fileName);

        if (!File.Exists(fileName))
        {
            Debug.LogWarning("No save file found.");

            return;
        }

        string json = File.ReadAllText(fileName);
        SaveContainer container = JsonUtility.FromJson<SaveContainer>(json);

        foreach (SaveItem item in container.items)
        {
            if (!dataJsonDict.ContainsKey(category)) dataJsonDict[category] = new Dictionary<string, string>();

            dataJsonDict[category][item.key] = item.data;
        }
    }
}
