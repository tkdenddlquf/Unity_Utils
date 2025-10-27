using UnityEditor;
using UnityEngine;

namespace Yang.Layer
{
    [CustomPropertyDrawer(typeof(SingleLayer))]
    public class SingleLayerDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty layerProp = property.FindPropertyRelative("layer");

            layerProp.intValue = EditorGUI.LayerField(position, label, layerProp.intValue);
        }
    }
}