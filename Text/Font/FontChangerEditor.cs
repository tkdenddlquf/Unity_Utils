#if UNITY_EDITOR
using UnityEngine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

[CustomEditor(typeof(FontChanger))]
public class FontChangerEditor : Editor
{
    private int count;

    private readonly List<GameObject> prefabs = new();

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        count = 0;

        if (GUILayout.Button("Change Fonts"))
        {
            FontChanger changer = (FontChanger)target;

            prefabs.Clear();

            foreach (TextMeshPro tmp in FindObjectsByType<TextMeshPro>(FindObjectsSortMode.None)) ProcessTMP(changer, tmp);

            foreach (TextMeshProUGUI tmp in FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None)) ProcessTMP(changer, tmp);

            // 프리팹 인스턴스에서 원본에 변경사항 적용
            foreach (GameObject instance in prefabs) PrefabUtility.ApplyPrefabInstance(instance, InteractionMode.UserAction);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log($"{count}건 변경 완료 (프리팹 {prefabs.Count}건 적용됨)");
        }
    }

    private void ProcessTMP<T>(FontChanger changer, T tmp) where T : TMP_Text
    {
        if (tmp.font != changer.checkFont) return;

        tmp.font = changer.changeFont;
        EditorUtility.SetDirty(tmp);

        GameObject prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(tmp.gameObject);

        if (prefabRoot != null && !prefabs.Contains(prefabRoot))
        {
            prefabs.Add(prefabRoot);
        }

        count++;
    }
}
#endif
