using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Yang.Dialogue.Generator
{
    [Generator]
    public sealed class DialogueInstructionGenerator : ISourceGenerator
    {
        private static readonly DiagnosticDescriptor InvalidInstruction = new(
            "YDG001", "Invalid dialogue instruction", "{0}", "Dialogue", DiagnosticSeverity.Error, true);

        private sealed class Instruction
        {
            public INamedTypeSymbol Type = null!;
            public string Id = "";
            public bool IsEvent;
            public List<IFieldSymbol> Fields = new();
        }

        private sealed class Receiver : ISyntaxReceiver
        {
            public List<ClassDeclarationSyntax> Candidates { get; } = new();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is ClassDeclarationSyntax declaration && declaration.AttributeLists.Count > 0)
                    Candidates.Add(declaration);
            }
        }

        public void Initialize(GeneratorInitializationContext context) => context.RegisterForSyntaxNotifications(() => new Receiver());

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxReceiver is not Receiver receiver) return;

            List<Instruction> instructions = new();
            HashSet<INamedTypeSymbol> seen = new(SymbolEqualityComparer.Default);
            HashSet<string> commandIds = new(StringComparer.Ordinal);
            HashSet<string> eventIds = new(StringComparer.Ordinal);

            foreach (ClassDeclarationSyntax declaration in receiver.Candidates)
            {
                SemanticModel model = context.Compilation.GetSemanticModel(declaration.SyntaxTree);

                if (model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol || !seen.Add(symbol)) continue;

                AttributeData? attribute = null;
                bool isEvent = false;

                foreach (AttributeData current in symbol.GetAttributes())
                {
                    string name = current.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "";

                    if (name != "global::Yang.Dialogue.DialogueCommandAttribute" && name != "global::Yang.Dialogue.DialogueEventAttribute") continue;

                    attribute = current;
                    isEvent = name == "global::Yang.Dialogue.DialogueEventAttribute";
                    break;
                }

                if (attribute == null) continue;

                string id = attribute.ConstructorArguments.Length > 0 ? attribute.ConstructorArguments[0].Value as string ?? "" : "";
                HashSet<string> ids = isEvent ? eventIds : commandIds;

                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                {
                    Report(context, declaration.Identifier.GetLocation(), $"'{symbol.Name}' has an empty or duplicate instruction ID '{id}'.");
                    continue;
                }

                if (symbol.IsAbstract || symbol.TypeParameters.Length != 0 || symbol.ContainingType != null)
                {
                    Report(context, declaration.Identifier.GetLocation(), $"'{symbol.Name}' must be a non-generic, non-nested, concrete class.");
                    continue;
                }

                bool hasConstructor = false;

                foreach (IMethodSymbol constructor in symbol.InstanceConstructors)
                {
                    if (constructor.Parameters.Length != 0 || constructor.DeclaredAccessibility != Accessibility.Public && constructor.DeclaredAccessibility != Accessibility.Internal) continue;

                    hasConstructor = true;
                    break;
                }

                if (!hasConstructor)
                {
                    Report(context, declaration.Identifier.GetLocation(), $"'{symbol.Name}' needs an accessible parameterless constructor.");
                    continue;
                }

                Instruction instruction = new() { Type = symbol, Id = id, IsEvent = isEvent };
                bool valid = true;

                foreach (ISymbol member in symbol.GetMembers())
                {
                    if (member is not IFieldSymbol field || field.IsStatic || field.IsConst || field.DeclaredAccessibility != Accessibility.Public) continue;

                    bool ignored = false;

                    foreach (AttributeData fieldAttribute in field.GetAttributes())
                    {
                        if (fieldAttribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.NonSerializedAttribute") ignored = true;
                    }

                    if (ignored) continue;

                    if (field.IsReadOnly || GetFieldKind(field.Type) == null)
                    {
                        Report(context, field.Locations[0], $"'{symbol.Name}.{field.Name}' must be a writable public field of type string, int, long, float, bool, Color32, Guid or an int-backed enum.");
                        valid = false;
                        continue;
                    }

                    instruction.Fields.Add(field);
                }

                if (!valid) continue;

                HashSet<int> idsByName = new();

                foreach (IFieldSymbol field in instruction.Fields)
                {
                    if (idsByName.Add(GetFieldId(field.Name))) continue;

                    Report(context, field.Locations[0], $"'{symbol.Name}' has a field-name hash collision.");
                    valid = false;
                    break;
                }

                if (!valid) continue;

                instruction.Fields.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
                instructions.Add(instruction);
            }

            if (instructions.Count == 0) return;

            StringBuilder source = new();
            source.AppendLine("// <auto-generated />");
            source.AppendLine("namespace Yang.Dialogue.Generated");
            source.AppendLine("{");
            source.AppendLine("    internal static class DialogueInstructionFactories");
            source.AppendLine("    {");
            source.AppendLine("        [global::UnityEngine.RuntimeInitializeOnLoadMethod(global::UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]");
            source.AppendLine("        private static void Register()");
            source.AppendLine("        {");

            for (int i = 0; i < instructions.Count; i++)
            {
                Instruction instruction = instructions[i];
                string register = instruction.IsEvent ? "RegisterEvent" : "RegisterCommand";
                source.Append("            global::Yang.Dialogue.DialogueInstructionRegistry.").Append(register)
                    .Append('(').Append(Quote(instruction.Id)).Append(", Create_").Append(i).AppendLine(");");
            }

            source.AppendLine("        }");

            for (int i = 0; i < instructions.Count; i++)
            {
                Instruction instruction = instructions[i];
                source.Append("        private static object Create_").Append(i)
                    .AppendLine("(global::Yang.Dialogue.RunnerCommand command)");
                source.AppendLine("        {");
                source.Append("            ").Append(instruction.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                    .Append(" instance = new ").Append(instruction.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).AppendLine("();");

                for (int j = 0; j < instruction.Fields.Count; j++)
                {
                    IFieldSymbol field = instruction.Fields[j];
                    string member = "instance." + (SyntaxFacts.GetKeywordKind(field.Name) == SyntaxKind.None ? field.Name : "@" + field.Name);
                    string kind = GetFieldKind(field.Type)!;
                    int fieldId = GetFieldId(field.Name);
                    string value = kind == "Enum" ? "(" + field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ")command.GetEnumInt(" + fieldId + ", (int)" + member + ")"
                        : "command.Get" + kind + "(" + fieldId + ", " + member + ")";
                    source.Append("            ").Append(member).Append(" = ").Append(value).AppendLine(";");
                }

                source.AppendLine("            return instance;");
                source.AppendLine("        }");
            }

            source.AppendLine("    }");
            source.AppendLine("}");
            context.AddSource("DialogueInstructionFactories.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
        }

        private static string? GetFieldKind(ITypeSymbol type)
        {
            if (type.TypeKind == TypeKind.Enum)
            {
                INamedTypeSymbol enumeration = (INamedTypeSymbol)type;
                return enumeration.EnumUnderlyingType?.SpecialType == SpecialType.System_Int32 ? "Enum" : null;
            }

            switch (type.SpecialType)
            {
                case SpecialType.System_String: return "String";
                case SpecialType.System_Int32: return "Int";
                case SpecialType.System_Int64: return "Long";
                case SpecialType.System_Single: return "Float";
                case SpecialType.System_Boolean: return "Bool";
            }

            string name = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            if (name == "global::UnityEngine.Color32") return "Color";

            if (name == "global::System.Guid") return "Guid";

            return null;
        }

        private static int GetFieldId(string name)
        {
            uint hash = 2166136261u;

            for (int i = 0; i < name.Length; i++) hash = unchecked((hash ^ name[i]) * 16777619u);

            int result = (int)(hash & 0x7fffffff);
            return result == 0 ? 1 : result;
        }

        private static void Report(GeneratorExecutionContext context, Location location, string message)
            => context.ReportDiagnostic(Diagnostic.Create(InvalidInstruction, location, message));

        private static string Quote(string value)
        {
            StringBuilder result = new("\"");

            foreach (char character in value)
            {
                switch (character)
                {
                    case '\\': result.Append("\\\\"); break;
                    case '"': result.Append("\\\""); break;
                    case '\n': result.Append("\\n"); break;
                    case '\r': result.Append("\\r"); break;
                    case '\t': result.Append("\\t"); break;
                    default: result.Append(character); break;
                }
            }

            return result.Append('"').ToString();
        }
    }
}
