using System;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using SourceGenerator;

namespace Configuration.SourceGenerator
{
    [Generator]
    public class ConfigurationBindingGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxReceiver is not SyntaxReceiver receiver)
            {
                // nothing to do yet
                return;
            }

            if (Environment.GetEnvironmentVariable("CONFIGURATION_EXTENSIONS_DEBUG") == "1")
            {
                System.Diagnostics.Debugger.Launch();
            }

            //while (!System.Diagnostics.Debugger.IsAttached)
            //{
            //    System.Threading.Thread.Sleep(1000);
            //}
            // System.Diagnostics.Debugger.Launch();

            var metadataLoadContext = new MetadataLoadContext(context.Compilation);
            var wellKnownTypes = new WellKnownTypes(metadataLoadContext);

            var configTypes = new List<Type>();

            foreach (var (invocation, argument) in receiver.Calls)
            {
                var semanticModel = context.Compilation.GetSemanticModel(invocation.SyntaxTree);

                var mapMethodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;

                if (mapMethodSymbol is { Parameters: { Length: 1 } parameters } &&
                    wellKnownTypes.ObjectType.Equals(parameters[0].Type) &&
                    wellKnownTypes.IConfigurationType.Equals(mapMethodSymbol.ReceiverType))
                {
                    // We're looking for IConfiguration.Bind(object)
                }
                else
                {
                    continue;
                }

                var argumentSymbolInfo = semanticModel.GetSymbolInfo(argument);

                if (argumentSymbolInfo.Symbol is null)
                {
                    continue;
                }

                static ITypeSymbol ResolveType(ISymbol s)
                {
                    return s switch
                    {
                        ITypeSymbol t => t,
                        IFieldSymbol f => f.Type,
                        ILocalSymbol l => l.Type,
                        IMethodSymbol m => m.ReturnType,
                        _ => null
                    };
                }

                var configurationType = ResolveType(argumentSymbolInfo.Symbol)?.WithNullableAnnotation(NullableAnnotation.None);

                if (configurationType is null || wellKnownTypes.ObjectType.Equals(configurationType))
                {
                    continue;
                }
                configTypes.Add(configurationType.AsType(metadataLoadContext));
            }

            var sb = new StringBuilder();

            // Bind method
            sb.Append(@$"    public static void Bind<T>(this {wellKnownTypes.IConfigurationType} configuration, T value)
        {{
");

            var i = 0;
            foreach (var c in configTypes)
            {
                sb.Append(@$"            {(i > 0 ? "else " : "")}if (typeof(T) == typeof({c}))
            {{
                Bind(configuration, ({c})(object)value);
            }}
");
                i++;
            }

            sb.AppendLine("        }");
            sb.AppendLine();

            var generatedTypes = new HashSet<Type>();

            var writer = new CodeWriter(sb);
            writer.Indent();
            writer.Indent();
            var q = new Queue<Type>();

            foreach (var type in configTypes)
            {
                q.Enqueue(type);
            }

            while (q.Count > 0)
            {
                var type = q.Dequeue();

                if (!generatedTypes.Add(type))
                {
                    continue;
                }

                writer.WriteLine($@"static void Bind({wellKnownTypes.IConfigurationType} configuration, {type} value)");
                writer.WriteLine("{");
                writer.Indent();
                GenerateConfigurationBind(wellKnownTypes, writer, type, q);
                writer.Unindent();
                writer.WriteLine("}");
            }

            if (sb.Length > 0)
            {
                var text = $@"
namespace Microsoft.Extensions.Configuration
{{
    public static class GeneratedConfigurationBinder
    {{
    {writer.ToString().TrimEnd()}
    }}
}}";

                context.AddSource($"GeneratedConfigurationBinder.g", SourceText.From(text, Encoding.UTF8));
            }
        }

        private void GenerateConfigurationBind(WellKnownTypes wellKnownTypes, CodeWriter writer, Type type, Queue<Type> dependentTypes)
        {
            foreach (var p in type.GetProperties())
            {
                if (p.CanWrite)
                {
                    WriteValue($"value.{p.Name}", p.PropertyType, $@"""{p.Name}""", $"{p.Name}Temp", "configuration", wellKnownTypes, writer, dependentTypes);
                }
            }
        }

        private void WriteValue(string lhs, Type type, string index, string tempName, string configurationExpr, WellKnownTypes wellKnownTypes, CodeWriter writer, Queue<Type> dependentTypes)
        {
            if (type.Equals(typeof(string)))
            {
                writer.Write(lhs);
                writer.WriteNoIndent(" = ");
                writer.WriteLineNoIndent($@"{configurationExpr}[{index}];");
            }
            else if (type.Equals(typeof(byte[])))
            {
                writer.Write(lhs);
                writer.WriteNoIndent(" = ");
                writer.WriteLineNoIndent($@"System.Convert.FromBase64String({configurationExpr}[{index}]);");
            }
            else if (type.Equals(wellKnownTypes.IConfigurationSectionType))
            {
                writer.Write(lhs);
                writer.WriteNoIndent(" = ");
                writer.WriteLineNoIndent($"{configurationExpr} as {wellKnownTypes.IConfigurationSectionType};");
            }
            else if (type.IsEnum)
            {
                writer.Write(lhs);
                writer.WriteNoIndent(" = ");
                writer.WriteLineNoIndent($@"Enum.TryParse<{type}>({configurationExpr}[{index}], true, out var {tempName}) ? {tempName} : default;");
            }
            // TODO: Handle collection types (dictionaries, sets, lists)
            else if (type.IsArray)
            {
                var elementType = type.GetElementType();
                writer.WriteLine("{");
                writer.Indent();
                writer.WriteLine($"var items = new List<{elementType}>();");
                if (!elementType.Equals(typeof(string)))
                {
                    writer.WriteLine($"var index = 0;");
                }
                writer.WriteLine($@"foreach (var item in {configurationExpr}.GetSection({index}).GetChildren())");
                writer.WriteLine("{");
                writer.Indent();
                if (elementType.Equals(typeof(string)))
                {
                    writer.WriteLine($"items.Add(item.Value);");
                }
                else
                {
                    writer.WriteLine($"{elementType} current = default;");
                    WriteValue("current", elementType, "index.ToString()", "indexTemp", "item", wellKnownTypes, writer, dependentTypes);
                    writer.WriteLine("items.Add(current);");
                    writer.WriteLine("index++;");
                }
                writer.Unindent();
                writer.WriteLine("}");
                writer.Write(lhs);
                writer.WriteNoIndent(" = ");
                writer.WriteLineNoIndent("items.ToArray();");

                writer.Unindent();
                writer.WriteLine("}");
            }
            else if (GetStaticMethodFromHierarchy(type, "TryParse", new[] { typeof(string), type.MakeByRefType() }) != null)
            {
                writer.Write(lhs);
                writer.WriteNoIndent(" = ");
                writer.WriteLineNoIndent($@"{type}.TryParse({configurationExpr}[{index}], out var {tempName}) ? {tempName} : default;");
            }
            else
            {
                if (type.IsGenericType && 
                    (type.GetGenericTypeDefinition().Equals(typeof(Dictionary<,>)) || 
                     type.GetGenericTypeDefinition().Equals(typeof(List<>))))
                {
                    writer.WriteLine($"// {type} is currently unsupported");
                    writer.WriteLine($"{lhs} = default;");
                }
                else
                {
                    writer.WriteLine($"{lhs} ??= new();");

                    writer.WriteLine($@"Bind({configurationExpr}.GetSection({index}), {lhs});");

                    // We need to generate a method for this type
                    dependentTypes.Enqueue(type);
                }
            }
        }

        private MethodInfo GetStaticMethodFromHierarchy(Type type, string name, Type[] parameterTypes)
        {
            var methodInfo = type.GetMethods().FirstOrDefault(m => m.Name == name && m.IsPublic && m.IsStatic && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameterTypes));

            return methodInfo;
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        private class SyntaxReceiver : ISyntaxReceiver
        {
            private static readonly string[] KnownMethods = new[]
            {
                "Bind",
            };

            public List<(InvocationExpressionSyntax, ExpressionSyntax)> Calls { get; } = new();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is InvocationExpressionSyntax
                    {
                        Expression: MemberAccessExpressionSyntax
                        {
                            Name: IdentifierNameSyntax
                            {
                                Identifier: { ValueText: var method }
                            }
                        },
                        ArgumentList: { Arguments: { Count: 1 } args }
                    } mapActionCall && KnownMethods.Contains(method))
                {
                    Calls.Add((mapActionCall, args[0].Expression));
                }
            }
        }
    }
}
