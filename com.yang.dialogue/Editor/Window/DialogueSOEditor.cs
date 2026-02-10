using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    [CustomEditor(typeof(DialogueSO))]
    public class DialogueSOEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new();

            serializedObject.Update();

            PopupField<string> eventPopup = GetPopup<IEventMarker>(root, "events");
            PopupField<string> conditionPopup = GetPopup<IConditionMarker>(root, "conditions");

            root.Add(eventPopup);
            root.Add(conditionPopup);

            serializedObject.ApplyModifiedProperties();

            return root;
        }

        private PopupField<string> GetPopup<T>(VisualElement root, string propName)
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

            typePopup.RegisterValueChangedCallback(evt =>
            {
                int newIndex = -1;

                for (int i = 0; i < typeNames.Count; i++)
                {
                    if (typeNames[i] == evt.newValue)
                    {
                        newIndex = i;
                        break;
                    }
                }

                if (newIndex < 0) return;

                Type newType = types[newIndex];

                containerProp.managedReferenceValue = Activator.CreateInstance(newType);

                serializedObject.ApplyModifiedProperties();

                root.Clear();
                root.Add(CreateInspectorGUI());
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