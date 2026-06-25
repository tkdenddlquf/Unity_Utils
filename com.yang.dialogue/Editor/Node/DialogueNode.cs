using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    /// <summary>
    /// Port Data (Unused)
    /// 
    /// Option Data (0 : Speaker Table)
    /// 0 : Name - string
    /// 1 : Guid - guid
    /// 
    /// Option Data (1 : Speaker Entry)
    /// 0 : Key - string
    /// 1 : ID - long
    /// 
    /// Option Data (2 : Text Table)
    /// 0 : Name - string
    /// 1 : Guid - guis
    /// 
    /// Option Data (3 : Text Entry)
    /// 0 : Key - string
    /// 1 : ID - long
    /// 
    /// Option Data (4 : Message)
    /// 0 : Text - string
    /// </summary>
    public class DialogueNode : BaseNode
    {
        private readonly List<string> tables;
        private readonly List<EntryData> speakerEntries = new();
        private readonly List<EntryData> textEntries = new();

        private readonly IReadOnlyList<LocalizationTableCollection> collections;

        private readonly VisualElement textsElement = new();

        /// <summary>Creates the dialogue node, caching the window's tables and collections.</summary>
        public DialogueNode(DialogueEditorWindow window, string guid) : base(window, guid)
        {
            tables = window.Tables;
            collections = window.collections;
        }

        /// <summary>Builds default data, input/output ports, preview text fields, and refreshes the preview.</summary>
        public override void SetPorts()
        {
            SetDefault();

            CreateInputPort();
            CreateOutputPort();

            contentContainer[0].Insert(2, textsElement);

            AddTextField("Speaker", "Speaker Text");
            AddTextField("Text", "Text Text");

            SetPreview();
        }

        /// <summary>Populates the extension container with the speaker/text table, entry, and message fields.</summary>
        protected override void BuildExtension() => SetOptions();

        /// <summary>Seeds default option and port data, honoring any speaker/text table overrides on the asset.</summary>
        private void SetDefault()
        {
            if (portDatas.Count == 0)
            {
                DialogueSO so = window.SO;

                LocalizedStringTable speakerOverride = so.SpeakerTable;
                LocalizedStringTable textOverride = so.TextTable;

                DataWrapper speakerTable;
                DataWrapper textTable;

                if (speakerOverride == null || speakerOverride.IsEmpty)
                {
                    speakerTable = new(
                        new(GenericData.DataType.String),
                        new(GenericData.DataType.Guid)
                    );
                }
                else
                {
                    TableReference reference = speakerOverride.TableReference;

                    speakerTable = new(
                        new(reference.TableCollectionName),
                        new(reference.TableCollectionNameGuid)
                    );
                }

                if (textOverride == null || textOverride.IsEmpty)
                {
                    textTable = new(
                        new(GenericData.DataType.String),
                        new(GenericData.DataType.Guid)
                    );
                }
                else
                {
                    TableReference reference = textOverride.TableReference;

                    textTable = new(
                        new(reference.TableCollectionName),
                        new(reference.TableCollectionNameGuid)
                    );
                }

                DataWrapper speakerEntry = new(
                    new(GenericData.DataType.String),
                    new(GenericData.DataType.Long)
                );

                DataWrapper textEntry = new(
                    new(GenericData.DataType.String),
                    new(GenericData.DataType.Long)
                );

                DataWrapper message = new(new GenericData(GenericData.DataType.String));

                optionDatas.Add(speakerTable);
                optionDatas.Add(speakerEntry);

                optionDatas.Add(textTable);
                optionDatas.Add(textEntry);

                optionDatas.Add(message);

                portDatas.Add(new());
            }
        }

        /// <summary>Adds the speaker and text table/entry fields plus the message field to the extension.</summary>
        private void SetOptions()
        {
            AddTableField(true);
            AddEntryField(true);

            AddTableField(false);
            AddEntryField(false);

            AddMessageField();
        }

        /// <summary>Creates a read-only preview text field with the given title and name.</summary>
        private void AddTextField(string title, string name)
        {
            TextField field = new(title)
            {
                name = name,
                isReadOnly = true,
            };

            field.AddToClassList("dlg-preview");

            textsElement.Add(field);
        }

        /// <summary>Refreshes both speaker and text preview fields from current option data.</summary>
        private void SetPreview()
        {
            SetPreviewField("Speaker Text", optionDatas[0].data, optionDatas[1].data);
            SetPreviewField("Text Text", optionDatas[2].data, optionDatas[3].data);
        }

        /// <summary>Sets the named preview field to the localized text resolved from the given table and entry.</summary>
        private void SetPreviewField(string fieldName, List<GenericData> table, List<GenericData> entry)
        {
            TextField field = textsElement.Q<TextField>(fieldName);

            if (field == null) return;

            field.value = ResolvePreview(table[0].ToString(), entry[0].ToString(), entry[1].GetLong());
        }

        /// <summary>Looks up the localized text for a table name, key, and id; returns empty when unresolved.</summary>
        private string ResolvePreview(string tableName, string key, long id)
        {
            if (string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(key)) return "";

            for (int i = 0; i < collections.Count; i++)
            {
                if (collections[i].TableCollectionName == tableName)
                    return new EntryData(id, key, collections[i].Tables).GetText(window.Language);
            }

            return "";
        }

        /// <summary>Adds a table popup field for the speaker or text table and syncs its option data.</summary>
        private void AddTableField(bool speaker)
        {
            List<EntryData> entries = speaker ? speakerEntries : textEntries;
            List<GenericData> optionData = optionDatas[speaker ? 0 : 2].data;

            int index = GetTableIndex(optionData[0].ToString(), optionData[1].GetGuid());

            string name = speaker ? "Speaker Table" : "Text Table";
            PopupField<string> field = new(name, tables, index) { name = name };

            field.AddToClassList("dlg-field");

            field.RegisterValueChangedCallback(ChangedCallback);
            field.RegisterCallback<KeyDownEvent>(OnTableKeyDownEvent);

            extensionContainer.Add(field);

            if (index != -1)
            {
                window.GetEntriesInto(collections[index], entries);

                optionData[0] = new(collections[index].TableCollectionName);
                optionData[1] = new(collections[index].TableCollectionNameReference.TableCollectionNameGuid);
            }
        }

        /// <summary>Finds a collection index by table guid, falling back to name; returns -1 when not found.</summary>
        private int GetTableIndex(string value, System.Guid guid)
        {
            for (int i = 0; i < collections.Count; i++)
            {
                LocalizationTableCollection collection = collections[i];

                if (collection.TableCollectionNameReference.TableCollectionNameGuid == guid) return i;
            }

            for (int i = 0; i < collections.Count; i++)
            {
                LocalizationTableCollection collection = collections[i];

                if (collection.TableCollectionName == value) return i;
            }

            return -1;
        }

        /// <summary>On Delete, clears the table field along with its paired entry, preview, and option data.</summary>
        private void OnTableKeyDownEvent(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Delete)
            {
                PopupField<string> tableField = evt.target.FindParentInCurrent<PopupField<string>>();

                if (tableField == null) return;

                DialogueSO so = window.SO;

                bool speaker = tableField.name == "Speaker Table";

                PopupField<EntryData> entryField = extensionContainer.Q<PopupField<EntryData>>(speaker ? "Speaker Entry" : "Text Entry");

                if (entryField == null) return;

                List<GenericData> tableOptionData = optionDatas[speaker ? 0 : 2].data;
                List<GenericData> entryOptionData = optionDatas[speaker ? 1 : 3].data;

                Undo.RecordObject(so, "Delete Table Option");

                tableField.value = "";
                entryField.value = default;

                tableOptionData[0] = new(GenericData.DataType.String);
                tableOptionData[1] = new(GenericData.DataType.Guid);

                entryOptionData[0] = new(GenericData.DataType.String);
                entryOptionData[1] = new(GenericData.DataType.Long);

                if (speaker)
                {
                    TextField textField = textsElement.Q<TextField>("Speaker Text");

                    if (textField != null) textField.value = "";

                    speakerEntries.Clear();
                }
                else
                {
                    TextField textField = textsElement.Q<TextField>("Text Text");

                    if (textField != null) textField.value = "";

                    textEntries.Clear();
                }

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }

        /// <summary>Handles a table selection change, resetting the paired entry/preview and updating option data.</summary>
        private void ChangedCallback(ChangeEvent<string> evt)
        {
            int index = tables.IndexOf(evt.newValue);

            if (index != -1)
            {
                DialogueSO so = window.SO;

                VisualElement target = evt.target as VisualElement;
                bool speaker = target.name == "Speaker Table";

                LocalizationTableCollection collection = collections[index];

                Undo.RecordObject(so, "Change Table");

                List<GenericData> optionData = optionDatas[speaker ? 0 : 2].data;

                if (optionData[0].ToString() != collection.TableCollectionName)
                {
                    PopupField<EntryData> entryField = extensionContainer.Q<PopupField<EntryData>>(speaker ? "Speaker Entry" : "Text Entry");

                    if (entryField != null)
                    {
                        List<GenericData> entryOptionData = optionDatas[speaker ? 1 : 3].data;

                        entryField.value = default;

                        entryOptionData[0] = new(GenericData.DataType.String);
                        entryOptionData[1] = new(GenericData.DataType.Long);

                        if (speaker)
                        {
                            TextField textField = textsElement.Q<TextField>("Speaker Text");

                            if (textField != null) textField.value = "";

                            speakerEntries.Clear();
                        }
                        else
                        {
                            TextField textField = textsElement.Q<TextField>("Text Text");

                            if (textField != null) textField.value = "";

                            textEntries.Clear();
                        }
                    }

                    window.GetEntriesInto(collection, speaker ? speakerEntries : textEntries);
                }

                optionData[0] = new(collection.TableCollectionName);
                optionData[1] = new(collection.TableCollectionNameReference.TableCollectionNameGuid);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }
        /// <summary>Adds an entry popup field for the speaker or text entry and syncs its option data and preview.</summary>
        private void AddEntryField(bool speaker)
        {
            List<EntryData> entries = speaker ? speakerEntries : textEntries;
            List<GenericData> optionData = optionDatas[speaker ? 1 : 3].data;

            int index = entries.IndexOf(new EntryData(optionData[1].GetLong(), optionData[0].ToString()));

            string name = speaker ? "Speaker Entry" : "Text Entry";
            PopupField<EntryData> field = new(name, entries, index) { name = name };

            field.AddToClassList("dlg-field");

            field.RegisterValueChangedCallback(ChangedCallback);
            field.RegisterCallback<KeyDownEvent>(OnEntryKeyDownEvent);

            extensionContainer.Add(field);

            if (index != -1)
            {
                EntryData entryData = entries[index];

                optionData[0] = new(entryData.key);
                optionData[1] = new(entryData.id);

                if (speaker)
                {
                    TextField textField = textsElement.Q<TextField>("Speaker Text");

                    if (textField != null) textField.value = entryData.GetText(window.Language);
                }
                else
                {
                    TextField textField = textsElement.Q<TextField>("Text Text");

                    if (textField != null) textField.value = entryData.GetText(window.Language);
                }
            }
        }

        /// <summary>On Delete, clears the entry field, its option data, and the matching preview text.</summary>
        private void OnEntryKeyDownEvent(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Delete)
            {
                PopupField<EntryData> field = evt.target.FindParentInCurrent<PopupField<EntryData>>();

                if (field == null) return;

                DialogueSO so = window.SO;

                bool speaker = field.name == "Speaker Entry";
                List<GenericData> optionData = optionDatas[speaker ? 1 : 3].data;

                Undo.RecordObject(so, "Delete Entry Option");

                field.value = default;

                optionData[0] = new(GenericData.DataType.String);
                optionData[1] = new(GenericData.DataType.Long);

                if (speaker)
                {
                    TextField textField = textsElement.Q<TextField>("Speaker Text");

                    if (textField != null) textField.value = "";
                }
                else
                {
                    TextField textField = textsElement.Q<TextField>("Text Text");

                    if (textField != null) textField.value = "";
                }

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }

        /// <summary>Handles an entry selection change, updating option data and the matching preview text.</summary>
        private void ChangedCallback(ChangeEvent<EntryData> evt)
        {
            if (evt.target is PopupField<EntryData> target)
            {
                bool speaker = target.name == "Speaker Entry";

                List<EntryData> entries = speaker ? speakerEntries : textEntries;
                List<GenericData> optionData = optionDatas[speaker ? 1 : 3].data;

                int index = entries.IndexOf(evt.newValue);

                if (index != -1)
                {
                    DialogueSO so = window.SO;

                    Undo.RecordObject(so, "Change Entry");

                    EntryData entryData = entries[index];

                    optionData[0] = new(entryData.key);
                    optionData[1] = new(entryData.id);

                    if (speaker)
                    {
                        TextField textField = textsElement.Q<TextField>("Speaker Text");

                        if (textField != null) textField.value = entryData.GetText(window.Language);
                    }
                    else
                    {
                        TextField textField = textsElement.Q<TextField>("Text Text");

                        if (textField != null) textField.value = entryData.GetText(window.Language);
                    }

                    EditorUtility.SetDirty(so);

                    window.SetUnsaved();
                }
            }
        }
        /// <summary>Writes the edited choice message into option data and marks the asset unsaved.</summary>
        private void MessageChangedCallback(ChangeEvent<string> evt)
        {
            DialogueSO so = window.SO;

            Undo.RecordObject(so, "Change Choice Message");

            optionDatas[4].data[0] = new(evt.newValue);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        /// <summary>Adds the message text field bound to the current message option data.</summary>
        private void AddMessageField()
        {
            TextField field = new("Message") { value = optionDatas[4].data[0].ToString() };

            field.AddToClassList("dlg-field");

            field.RegisterValueChangedCallback(MessageChangedCallback);

            extensionContainer.Add(field);
        }
    }
}