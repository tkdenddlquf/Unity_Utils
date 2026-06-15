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

        public DialogueNode(DialogueEditorWindow window, string guid) : base(window, guid)
        {
            tables = window.Tables;
            collections = window.collections;
        }

        public override void SetPorts()
        {
            SetDefault();

            CreateInputPort();
            CreateOutputPort();

            contentContainer[0].Insert(2, textsElement);

            AddTextField("Speaker", "Speaker Text");
            AddTextField("Text", "Text Text");

            SetOptions();
        }

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

        private void SetOptions()
        {
            AddTableField(true);
            AddEntryField(true);

            AddTableField(false);
            AddEntryField(false);

            AddMessageField();
        }

        private void AddTextField(string title, string name)
        {
            TextField field = new(title)
            {
                name = name,
                isReadOnly = true,
            };

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field.style.whiteSpace = WhiteSpace.Normal;
            field.style.minWidth = StyleKeyword.Auto;

            textsElement.Add(field);
        }

        #region Table
        private void AddTableField(bool speaker)
        {
            List<EntryData> entries = speaker ? speakerEntries : textEntries;
            List<GenericData> optionData = optionDatas[speaker ? 0 : 2].data;

            int index = GetTableIndex(optionData[0].ToString(), optionData[1].GetGuid());

            string name = speaker ? "Speaker Table" : "Text Table";
            PopupField<string> field = new(name, tables, index) { name = name };

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field[1].style.minWidth = ITEM_MIN_WIDTH;

            field.RegisterValueChangedCallback(ChangedCallback);
            field.RegisterCallback<KeyDownEvent>(OnTableKeyDownEvent);

            extensionContainer.Add(field);

            if (index != -1)
            {
                collections[index].SetEntries(entries);

                optionData[0] = new(collections[index].TableCollectionName);
                optionData[1] = new(collections[index].TableCollectionNameReference.TableCollectionNameGuid);
            }
        }

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

                    collection.SetEntries(speaker ? speakerEntries : textEntries);
                }

                optionData[0] = new(collection.TableCollectionName);
                optionData[1] = new(collection.TableCollectionNameReference.TableCollectionNameGuid);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }
        #endregion

        #region Entry
        private void AddEntryField(bool speaker)
        {
            List<EntryData> entries = speaker ? speakerEntries : textEntries;
            List<GenericData> optionData = optionDatas[speaker ? 1 : 3].data;

            int index = entries.IndexOf(new EntryData(optionData[1].GetLong(), optionData[0].ToString()));

            string name = speaker ? "Speaker Entry" : "Text Entry";
            PopupField<EntryData> field = new(name, entries, index) { name = name };

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field[1].style.minWidth = ITEM_MIN_WIDTH;

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
        #endregion

        #region Message
        private void MessageChangedCallback(ChangeEvent<string> evt)
        {
            DialogueSO so = window.SO;

            Undo.RecordObject(so, "Change Choice Message");

            optionDatas[4].data[0] = new(evt.newValue);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        private void AddMessageField()
        {
            TextField field = new("Message") { value = optionDatas[4].data[0].ToString() };

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field[1].style.minWidth = ITEM_MIN_WIDTH;

            field.RegisterValueChangedCallback(MessageChangedCallback);

            extensionContainer.Add(field);
        }
        #endregion
    }
}