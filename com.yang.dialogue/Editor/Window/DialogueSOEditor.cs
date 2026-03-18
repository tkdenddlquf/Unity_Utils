using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    [CustomEditor(typeof(DialogueSO))]
    public class DialogueSOEditor : UnityEditor.Editor
    {
        PopupField<string> eventPopup;
        PopupField<string> conditionPopup;

        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new();

            serializedObject.Update();

            eventPopup = GetMarkerPopup<IEventMarker>("events");
            conditionPopup = GetMarkerPopup<IConditionMarker>("conditions");

            Button button = new(Open) { text = "Edit" };

            root.Add(eventPopup);
            root.Add(conditionPopup);
            root.Add(button);

            serializedObject.ApplyModifiedProperties();

            return root;
        }

        private void Open()
        {
            DialogueEditorWindow window = DialogueEditorWindow.Open();

            window.SO = target as DialogueSO;
        }

        private PopupField<string> GetMarkerPopup<T>(string propName)
        {
            List<Type> types = new();

            SerializedProperty containerProp = serializedObject.FindProperty(propName);

            TypeCache.TypeCollection rawTypes = TypeCache.GetTypesDerivedFrom<T>();

            for (int i = 0; i < rawTypes.Count; i++)
            {
                Type t = rawTypes[i];

                if (t.IsAbstract) continue;
                if (t.IsGenericType) continue;
                if (!t.IsClass) continue;

                types.Add(t);
            }

            List<string> typeNames = new(types.Count);

            for (int i = 0; i < types.Count; i++) typeNames.Add(types[i].Name);

            Type currentType = GetManagedReferenceType(containerProp);

            int currentIndex = -1;

            for (int i = 0; i < types.Count; i++)
            {
                if (types[i] == currentType)
                {
                    currentIndex = i;

                    break;
                }
            }

            PopupField<string> typePopup = new(containerProp.displayName, typeNames, currentIndex);

            typePopup.TrackPropertyValue(containerProp, _ =>
            {
                typePopup.SetValueWithoutNotify(_.managedReferenceValue.ToString());
            });
            typePopup.RegisterValueChangedCallback(evt =>
            {
                int newIndex = typeNames.IndexOf(evt.newValue);

                if (newIndex < 0) return;

                Undo.RecordObject(target, $"Changed {propName}");

                containerProp.managedReferenceValue = Activator.CreateInstance(types[newIndex]);

                serializedObject.ApplyModifiedProperties();
            });

            return typePopup;
        }

        private Type GetManagedReferenceType(SerializedProperty prop)
        {
            if (prop == null) return null;
            if (string.IsNullOrEmpty(prop.managedReferenceFullTypename)) return null;

            string[] split = prop.managedReferenceFullTypename.Split(' ');

            return Type.GetType($"{split[1]}, {split[0]}");
        }
    }
}