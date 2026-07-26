using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    /// <summary>Custom inspector for DialogueSO with localization, edit, and CSV controls.</summary>
    [CustomEditor(typeof(DialogueSO))]
    public class DialogueSOEditor : UnityEditor.Editor
    {
        /// <summary>Builds the inspector UI with table overrides, edit, and CSV buttons.</summary>
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new();

            serializedObject.Update();

            Button button = new(Open) { text = "Edit" };

            Button exportButton = new(ExportCsv) { text = "Export CSV" };

            Button importButton = new(ImportCsv) { text = "Import CSV" };

            root.Bind(serializedObject);

            root.Add(GetHeader("Override Settings"));
            root.Add(GetField("speakerTable"));
            root.Add(GetField("textTable"));

            root.Add(button);

            root.Add(GetHeader("CSV"));
            root.Add(exportButton);
            root.Add(importButton);

            serializedObject.ApplyModifiedProperties();

            return root;
        }

        /// <summary>Exports the dialogue to a user-chosen CSV file.</summary>
        private void ExportCsv()
        {
            DialogueSO so = target as DialogueSO;

            if (so == null) return;

            string path = EditorUtility.SaveFilePanel("Export Dialogue CSV", "", so.name + ".csv", "csv");

            if (string.IsNullOrEmpty(path)) return;

            string csv = DialogueCsvExporter.Export(so);

            System.IO.File.WriteAllText(path, csv, new System.Text.UTF8Encoding(true));

            EditorUtility.RevealInFinder(path);
        }

        /// <summary>Imports a CSV after confirmation, replacing the dialogue and opening the editor.</summary>
        private void ImportCsv()
        {
            DialogueSO so = target as DialogueSO;

            if (so == null) return;

            string path = EditorUtility.OpenFilePanel("Import Dialogue CSV", "", "csv");

            if (string.IsNullOrEmpty(path)) return;

            if (!EditorUtility.DisplayDialog(
                "Import Dialogue CSV",
                "This replaces all nodes and links in this dialogue, and writes text into the assigned Speaker/Text tables. Continue?",
                "Import",
                "Cancel")) return;

            string csv = System.IO.File.ReadAllText(path);

            if (DialogueCsvImporter.Import(so, csv, out string message))
            {
                DialogueEditorWindow window = DialogueEditorWindow.Open();

                window.SO = so;

                if (!string.IsNullOrEmpty(message)) EditorUtility.DisplayDialog("Import Complete", message, "OK");
            }
            else EditorUtility.DisplayDialog("Import Failed", message, "OK");
        }

        /// <summary>Opens the dialogue editor window for this asset.</summary>
        private void Open()
        {
            DialogueEditorWindow window = DialogueEditorWindow.Open();

            window.SO = target as DialogueSO;
        }

        /// <summary>Creates a bold section header label.</summary>
        private Label GetHeader(string text)
        {
            Label header = new(text);

            header.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            header.style.fontSize = 14;
            header.style.marginTop = 6;
            header.style.marginBottom = 4;

            return header;
        }

        /// <summary>Creates a bound property field for the named serialized property.</summary>
        private PropertyField GetField(string propName)
        {
            SerializedProperty prop = serializedObject.FindProperty(propName);

            return new(prop);
        }
    }
}
