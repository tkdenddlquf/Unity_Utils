using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Yang.Input.Editor
{
    /// <summary>
    /// 플레이 모드에서 기본 <see cref="InputStack"/>의 현재 상태(스택 구성과 최상단)를 보여주는 디버그 창.
    /// 메뉴: Window > Yang > Input Stack Debug.
    /// </summary>
    public sealed class InputStackDebugWindow : EditorWindow
    {
        [MenuItem("Window/Yang/Input Stack Debug")]
        private static void Open()
        {
            GetWindow<InputStackDebugWindow>("Input Stack");
        }

        private void OnEnable()
        {
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("기본 InputStack", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("플레이 모드에서 현재 입력 스택을 표시합니다.", MessageType.Info);

                return;
            }

            IReadOnlyList<IInputReceiver> snapshot = InputStack.GetSnapshot();

            EditorGUILayout.LabelField($"수신자 수: {snapshot.Count}");
            EditorGUILayout.Space();

            if (snapshot.Count == 0)
            {
                EditorGUILayout.LabelField("(스택이 비어 있음)");

                return;
            }

            // 최상단(활성)부터 위에서 아래로 표시.
            for (int i = snapshot.Count - 1; i >= 0; i--)
            {
                IInputReceiver receiver = snapshot[i];

                bool isTop = i == snapshot.Count - 1;

                string name = receiver != null ? receiver.GetType().Name : "(null)";
                string label = isTop ? $"▶ {name}  (활성)" : $"   {name}";

                GUIStyle style = new GUIStyle(EditorStyles.label)
                {
                    fontStyle = isTop ? FontStyle.Bold : FontStyle.Normal,
                };

                EditorGUILayout.LabelField(label, style);
            }
        }
    }
}