using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    /// <summary>Schema-driven editor for asset-independent dialogue commands.</summary>
    public class CommandNode : BaseNode
    {
        private sealed class Schema
        {
            public string id;
            public string label;
            public Type type;
            public FieldInfo[] fields;
            public object defaults;
        }

        private sealed class ArgumentBinding
        {
            public VisualElement commandElement;
            public string key;
        }

        private const string CUSTOM_LABEL = "Custom / Missing Schema";

        private List<Schema> schemas;
        private Dictionary<string, Schema> schemasById;

        protected virtual Type SchemaAttributeType => typeof(DialogueCommandAttribute);

        public CommandNode(DialogueEditorWindow window, string guid) : base(window, guid)
        {
        }

        public override void SetPorts()
        {
            EnsureSchemas();
            EnsureDefaultData();

            CreateInputPort();
            CreateOutputPort();

            for (int i = 0; i < optionDatas.Count; i++) AddCommandElement();
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (evt.target != this) return;

            evt.menu.AppendAction("Add Command", _ => CreateCommand());
            evt.menu.AppendSeparator();
        }

        private void EnsureSchemas()
        {
            if (schemas != null) return;

            schemas = new List<Schema>();
            schemasById = new Dictionary<string, Schema>();

            IEnumerable<Type> types = SchemaAttributeType == typeof(DialogueEventAttribute)
                ? TypeCache.GetTypesWithAttribute<DialogueEventAttribute>()
                : TypeCache.GetTypesWithAttribute<DialogueCommandAttribute>();

            foreach (Type type in types)
            {
                DialogueSchemaAttribute command = type.GetCustomAttribute(SchemaAttributeType) as DialogueSchemaAttribute;

                if (command == null || string.IsNullOrWhiteSpace(command.ID) || !type.IsClass || type.IsAbstract) continue;

                if (schemasById.ContainsKey(command.ID))
                {
                    Debug.LogWarning($"Duplicate dialogue command id '{command.ID}' on {type.FullName}; the later schema was ignored.");
                    continue;
                }

                List<FieldInfo> supportedFields = new();

                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (field.IsStatic || field.IsNotSerialized || !IsSupported(field.FieldType)) continue;

                    supportedFields.Add(field);
                }

                supportedFields.Sort((a, b) =>
                {
                    int aOrder = a.GetCustomAttribute<DialogueArgumentAttribute>()?.Order ?? 0;
                    int bOrder = b.GetCustomAttribute<DialogueArgumentAttribute>()?.Order ?? 0;
                    int order = aOrder.CompareTo(bOrder);

                    return order != 0 ? order : a.MetadataToken.CompareTo(b.MetadataToken);
                });

                object defaults = null;

                try
                {
                    defaults = Activator.CreateInstance(type);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Dialogue command schema '{type.FullName}' needs a parameterless constructor. Defaults were ignored.\n{exception.Message}");
                }

                Schema schema = new()
                {
                    id = command.ID,
                    label = string.IsNullOrWhiteSpace(command.Menu) ? command.ID : command.Menu,
                    type = type,
                    fields = supportedFields.ToArray(),
                    defaults = defaults,
                };

                schemas.Add(schema);
                schemasById.Add(schema.id, schema);
            }

            schemas.Sort((a, b) => string.Compare(a.label, b.label, StringComparison.Ordinal));

            Dictionary<string, int> labelCounts = new();

            foreach (Schema schema in schemas)
                labelCounts[schema.label] = labelCounts.TryGetValue(schema.label, out int count) ? count + 1 : 1;

            foreach (Schema schema in schemas)
            {
                if (labelCounts[schema.label] > 1)
                    schema.label = $"{schema.label} ({schema.id})";
            }
        }

        private static bool IsSupported(Type type)
            => type == typeof(string) || type == typeof(int) || type == typeof(float) ||
               type == typeof(bool) || type.IsEnum;

        private void EnsureDefaultData()
        {
            if (portDatas.Count == 0) portDatas.Add(new DataWrapper());

            if (optionDatas.Count == 0)
            {
                optionDatas.Add(schemas.Count > 0
                    ? CreateSchemaData(schemas[0], null)
                    : new DataWrapper(new GenericData(GenericData.DataType.String)));
            }

            for (int i = 0; i < optionDatas.Count; i++)
            {
                List<GenericData> data = optionDatas[i].data;
                string id = data != null && data.Count > 0 ? data[0].ToString() ?? "" : "";

                if (schemasById.TryGetValue(id, out Schema schema))
                    optionDatas[i] = CreateSchemaData(schema, data);
            }
        }

        protected void CreateCommand()
        {
            DialogueSO so = window.SO;

            Undo.RecordObject(so, "Create Command");

            optionDatas.Add(schemas.Count > 0
                ? CreateSchemaData(schemas[0], null)
                : new DataWrapper(new GenericData(GenericData.DataType.String)));

            AddCommandElement();
            MarkChanged(so);
        }

        private void AddCommandElement()
        {
            VisualElement commandElement = new() { name = "Command Element" };
            commandElement.AddToClassList("dlg-command");
            extensionContainer.Add(commandElement);

            RebuildCommandElement(commandElement);
        }

        private void RebuildCommandElement(VisualElement commandElement)
        {
            commandElement.Clear();

            int commandIndex = extensionContainer.IndexOf(commandElement);
            List<GenericData> data = optionDatas[commandIndex].data;
            string id = data.Count > 0 ? data[0].ToString() ?? "" : "";

            schemasById.TryGetValue(id, out Schema schema);

            VisualElement header = new();
            header.AddToClassList("dlg-row");

            List<string> choices = new();
            foreach (Schema item in schemas) choices.Add(item.label);

            if (schema == null) choices.Add(CUSTOM_LABEL);

            int selected = schema == null ? choices.Count - 1 : schemas.IndexOf(schema);
            PopupField<string> selector = new("Command", choices, selected);
            selector.userData = commandElement;
            selector.AddToClassList("dlg-grow");
            selector.RegisterValueChangedCallback(OnSchemaChanged);

            Button remove = new(() => RemoveCommand(commandElement)) { text = "X" };

            header.Add(RowDrag.CreateHandle(commandElement, 0, SwapCommand));
            header.Add(selector);
            header.Add(remove);
            commandElement.Add(header);

            if (schema == null)
            {
                TextField customId = new("Command ID") { value = id };
                customId.userData = commandElement;
                customId.RegisterValueChangedCallback(OnCustomIdChanged);
                commandElement.Add(customId);

                Label warning = new("No matching dialogue schema. Existing arguments are preserved but cannot be edited here.");
                warning.AddToClassList("dlg-warning");
                commandElement.Add(warning);
                return;
            }

            for (int i = 0; i < schema.fields.Length; i++)
            {
                FieldInfo field = schema.fields[i];
                GenericData value = FindArgument(data, field.Name, out GenericData found)
                    ? found
                    : DefaultValue(schema, field);

                commandElement.Add(CreateArgumentField(commandElement, field, value));
            }
        }

        private void OnSchemaChanged(ChangeEvent<string> evt)
        {
            VisualElement commandElement = (VisualElement)((VisualElement)evt.target).userData;
            Schema schema = schemas.Find(item => item.label == evt.newValue);

            if (schema == null) return;

            int commandIndex = extensionContainer.IndexOf(commandElement);
            DialogueSO so = window.SO;

            Undo.RecordObject(so, "Change Command Schema");

            optionDatas[commandIndex] = CreateSchemaData(schema, optionDatas[commandIndex].data);

            RebuildCommandElement(commandElement);
            MarkChanged(so);
        }

        private void OnCustomIdChanged(ChangeEvent<string> evt)
        {
            VisualElement commandElement = (VisualElement)((VisualElement)evt.target).userData;
            int commandIndex = extensionContainer.IndexOf(commandElement);
            DialogueSO so = window.SO;

            Undo.RecordObject(so, "Change Command ID");

            optionDatas[commandIndex].data[0] = new GenericData(evt.newValue);
            MarkChanged(so);
        }

        private VisualElement CreateArgumentField(VisualElement commandElement, FieldInfo field, GenericData value)
        {
            string label = field.GetCustomAttribute<DialogueArgumentAttribute>()?.DisplayName;
            label = string.IsNullOrWhiteSpace(label) ? ObjectNames.NicifyVariableName(field.Name) : label;

            ArgumentBinding binding = new() { commandElement = commandElement, key = field.Name };
            Type type = field.FieldType;

            if (type == typeof(string))
            {
                List<string> options = GetOptions(field);

                if (options != null)
                {
                    string current = value.TryGetString(out string selectedValue) ? selectedValue : "";

                    if (!options.Contains(current)) options.Insert(0, current);
                    if (options.Count == 0) options.Add("");

                    PopupField<string> popup = new(label, options, Math.Max(0, options.IndexOf(current)));
                    popup.userData = binding;
                    popup.RegisterValueChangedCallback(evt => SetArgument((ArgumentBinding)popup.userData, new GenericData(evt.newValue)));
                    return popup;
                }

                TextField text = new(label) { value = value.TryGetString(out string stringValue) ? stringValue : "" };
                text.userData = binding;
                text.RegisterValueChangedCallback(evt => SetArgument((ArgumentBinding)text.userData, new GenericData(evt.newValue)));
                return text;
            }

            if (type == typeof(int))
            {
                IntegerField integer = new(label) { value = value.TryGetInt(out int intValue) ? intValue : 0 };
                integer.userData = binding;
                integer.RegisterValueChangedCallback(evt => SetArgument((ArgumentBinding)integer.userData, new GenericData(evt.newValue)));
                return integer;
            }

            if (type == typeof(float))
            {
                FloatField number = new(label) { value = value.TryGetFloat(out float floatValue) ? floatValue : 0f };
                number.userData = binding;
                number.RegisterValueChangedCallback(evt => SetArgument((ArgumentBinding)number.userData, new GenericData(evt.newValue)));
                return number;
            }

            if (type == typeof(bool))
            {
                Toggle toggle = new(label) { value = value.TryGetBool(out bool boolValue) && boolValue };
                toggle.userData = binding;
                toggle.RegisterValueChangedCallback(evt => SetArgument((ArgumentBinding)toggle.userData, new GenericData(evt.newValue)));
                return toggle;
            }

            int enumValue = 0;
            int.TryParse(value.ToString(), out enumValue);
            EnumField enumField = new(label, (Enum)Enum.ToObject(type, enumValue));
            enumField.userData = binding;
            enumField.RegisterValueChangedCallback(evt => SetArgument((ArgumentBinding)enumField.userData, new GenericData(evt.newValue)));
            return enumField;
        }

        private static List<string> GetOptions(FieldInfo field)
        {
            DialogueOptionsAttribute attribute = field.GetCustomAttribute<DialogueOptionsAttribute>();

            if (attribute == null) return null;

            try
            {
                MethodInfo method = attribute.ProviderType?.GetMethod(attribute.MethodName,
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                if (method == null || method.GetParameters().Length != 0) return new List<string>();

                IEnumerable enumerable = method.Invoke(null, null) as IEnumerable;
                List<string> values = new();

                if (enumerable != null)
                {
                    foreach (object item in enumerable)
                    {
                        if (item is string value && !values.Contains(value)) values.Add(value);
                    }
                }

                return values;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to load dialogue options for '{field.DeclaringType?.FullName}.{field.Name}'.\n{exception.Message}");
                return new List<string>();
            }
        }

        private void SetArgument(ArgumentBinding binding, GenericData value)
        {
            int commandIndex = extensionContainer.IndexOf(binding.commandElement);
            List<GenericData> data = optionDatas[commandIndex].data;
            DialogueSO so = window.SO;

            Undo.RecordObject(so, "Change Command Argument");

            for (int i = 1; i + 1 < data.Count; i += 2)
            {
                if (data[i].ToString() != binding.key) continue;

                data[i + 1] = value;
                MarkChanged(so);
                return;
            }

            data.Add(new GenericData(binding.key));
            data.Add(value);
            MarkChanged(so);
        }

        private static DataWrapper CreateSchemaData(Schema schema, IReadOnlyList<GenericData> previous)
        {
            List<GenericData> data = new() { new GenericData(schema.id) };

            for (int i = 0; i < schema.fields.Length; i++)
            {
                FieldInfo field = schema.fields[i];
                GenericData value = previous != null && FindArgument(previous, field.Name, out GenericData found) &&
                                    MatchesType(found, field.FieldType)
                    ? found
                    : DefaultValue(schema, field);

                data.Add(new GenericData(field.Name));
                data.Add(value);
            }

            return new DataWrapper(data);
        }

        private static bool FindArgument(IReadOnlyList<GenericData> data, string key, out GenericData value)
        {
            for (int i = 1; i + 1 < data.Count; i += 2)
            {
                if (data[i].ToString() != key) continue;

                value = data[i + 1];
                return true;
            }

            value = default;
            return false;
        }

        private static bool MatchesType(GenericData value, Type type)
            => type == typeof(string) && value.Type == GenericData.DataType.String ||
               type == typeof(int) && value.Type == GenericData.DataType.Int ||
               type == typeof(float) && value.Type == GenericData.DataType.Float ||
               type == typeof(bool) && value.Type == GenericData.DataType.Bool ||
               type.IsEnum && value.Type == GenericData.DataType.Enum;

        private static GenericData DefaultValue(Schema schema, FieldInfo field)
        {
            object value = schema.defaults == null ? null : field.GetValue(schema.defaults);

            if (field.FieldType == typeof(string)) return new GenericData((string)value ?? "");
            if (field.FieldType == typeof(int)) return new GenericData(value == null ? 0 : (int)value);
            if (field.FieldType == typeof(float)) return new GenericData(value == null ? 0f : (float)value);
            if (field.FieldType == typeof(bool)) return new GenericData(value != null && (bool)value);
            if (field.FieldType.IsEnum) return new GenericData((Enum)(value ?? Enum.ToObject(field.FieldType, 0)));

            return default;
        }

        internal static string ArgumentsToString(IReadOnlyList<GenericData> data, bool escape = false)
        {
            List<string> values = new();

            for (int i = 1; i + 1 < data.Count; i += 2)
            {
                string key = data[i].ToString();
                GenericData value = data[i + 1];

                string type = value.Type switch
                {
                    GenericData.DataType.Int => "int",
                    GenericData.DataType.Float => "float",
                    GenericData.DataType.Bool => "bool",
                    GenericData.DataType.Enum => "enum",
                    _ => "string",
                };

                string serializedKey = escape ? Uri.EscapeDataString(key) : key;
                string serializedValue = escape ? Uri.EscapeDataString(value.ToString()) : value.ToString();

                values.Add($"{serializedKey}:{type}={serializedValue}");
            }

            return string.Join("; ", values);
        }

        private void SwapCommand(int a, int b)
        {
            DialogueSO so = window.SO;

            Undo.RecordObject(so, "Reorder Command");

            (optionDatas[a], optionDatas[b]) = (optionDatas[b], optionDatas[a]);
            extensionContainer.Insert(a, extensionContainer[b]);

            MarkChanged(so);
        }

        private void RemoveCommand(VisualElement commandElement)
        {
            if (extensionContainer.childCount <= 1) return;

            DialogueSO so = window.SO;
            int commandIndex = extensionContainer.IndexOf(commandElement);

            Undo.RecordObject(so, "Remove Command");

            optionDatas.RemoveAt(commandIndex);
            extensionContainer.Remove(commandElement);

            MarkChanged(so);
        }

        private void MarkChanged(DialogueSO so)
        {
            EditorUtility.SetDirty(so);
            window.SetUnsaved();
        }
    }
}
