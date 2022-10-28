using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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

            var configTypes = new HashSet<Type>();

            ProcessBindCalls(context, receiver, metadataLoadContext, wellKnownTypes, configTypes);

            // ProcessConfigureCalls(context, receiver, metadataLoadContext, wellKnownTypes, configTypes);

            if (wellKnownTypes.GenerateBinderAttributeType is { } attribute)
            {
                foreach (var t in metadataLoadContext.Assembly.GetTypes())
                {
                    // Looks for types with an attribute
                    if (t.CustomAttributes.Any(a => attribute.IsAssignableFrom(a.AttributeType)))
                    {
                        configTypes.Add(t);
                    }
                }
            }

            var sb = new StringBuilder();
            var writer = new CodeWriter(sb);
            writer.Indent();
            writer.Indent();

            writer.WriteLine(@$"public static {wellKnownTypes.IServiceCollectionType} ConfigureAot<T>(this {wellKnownTypes.IServiceCollectionType} services, {wellKnownTypes.IConfigurationSectionType} configuration)");
            writer.WriteLine("{");
            writer.Indent();

            var i = 0;
            foreach (var c in configTypes)
            {
                // Configure method
                writer.WriteLine(@$"{(i > 0 ? "else " : "")}if (typeof(T) == typeof({c}))");
                writer.WriteLine("{");
                writer.Indent();
                writer.WriteLine(@$"services.Configure<{c}>(o => BindCore(configuration, o));");
                writer.Unindent();
                writer.WriteLine("}");
                i++;
            }

            writer.WriteLine(@$"return services;");
            writer.Unindent();
            writer.WriteLine("}");
            writer.WriteLineNoIndent("");

            // Bind method
            foreach (var c in configTypes)
            {
                writer.WriteLine(@$"internal static void Bind(this {wellKnownTypes.IConfigurationType} configuration, {c} value) => BindCore(configuration, value);");
                writer.WriteLineNoIndent("");
            }

            var generatedTypes = new HashSet<Type>();

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

                writer.WriteLine($@"static void BindCore({wellKnownTypes.IConfigurationType} configuration, {type} value)");
                writer.WriteLine("{");
                writer.Indent();
                GenerateConfigurationBind(wellKnownTypes, writer, type, q);
                writer.Unindent();
                writer.WriteLine("}");
                writer.WriteLineNoIndent("");
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

        private static void ProcessBindCalls(GeneratorExecutionContext context, SyntaxReceiver receiver, MetadataLoadContext metadataLoadContext, WellKnownTypes wellKnownTypes, HashSet<Type> configTypes)
        {
            foreach (var (invocation, argument) in receiver.BindCalls)
            {
                var semanticModel = context.Compilation.GetSemanticModel(invocation.SyntaxTree);

                var method = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;

                if (method is { Parameters: { Length: 1 } parameters } &&
                    parameters[0].Type.SpecialType == SpecialType.System_Object &&
                    wellKnownTypes.IConfigurationType.Equals(method.ReceiverType))
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
                        IMethodSymbol m when m.MethodKind == MethodKind.Constructor => m.ContainingType,
                        IMethodSymbol m => m.ReturnType,
                        _ => null
                    };
                }

                var configurationType = ResolveType(argumentSymbolInfo.Symbol)?.WithNullableAnnotation(NullableAnnotation.None);

                if (configurationType is null || configurationType.SpecialType == SpecialType.System_Object || configurationType.SpecialType == SpecialType.System_Void)
                {
                    continue;
                }

                configTypes.Add(configurationType.AsType(metadataLoadContext));
            }
        }

        //private static void ProcessConfigureCalls(GeneratorExecutionContext context, SyntaxReceiver receiver, MetadataLoadContext metadataLoadContext, WellKnownTypes wellKnownTypes, HashSet<Type> configTypes)
        //{
        //    foreach (var (invocation, argument) in receiver.ConfigureCalls)
        //    {
        //        var semanticModel = context.Compilation.GetSemanticModel(invocation.SyntaxTree);

        //        var method = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;

        //        if (method is { IsGenericMethod: true, Parameters: { Length: 1 } parameters } &&
        //            wellKnownTypes.IConfigurationType.Equals(parameters[0].Type) &&
        //            wellKnownTypes.IServiceCollectionType.Equals(method.ReceiverType))
        //        {
        //            // We're looking for IServiceCollection.Configure<T>(IConfiguration)
        //        }
        //        else
        //        {
        //            continue;
        //        }

        //        var configurationType = method.TypeArguments[0].WithNullableAnnotation(NullableAnnotation.None);

        //        if (configurationType is null || configurationType.SpecialType == SpecialType.System_Object)
        //        {
        //            continue;
        //        }

        //        configTypes.Add(configurationType.AsType(metadataLoadContext));
        //    }
        //}

        private void GenerateConfigurationBind(WellKnownTypes wellKnownTypes, CodeWriter writer, Type type, Queue<Type> dependentTypes)
        {
            foreach (var p in type.GetProperties())
            {
                if (p.CanWrite && p.SetMethod.IsPublic)
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
            else if (IsArrayCompatibleInterface(type, out var elementType))
            {
                writer.WriteLine("{");
                writer.Indent();
                writer.WriteLine($"var items = new System.Collections.Generic.List<{elementType}>();");
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
                    WriteValue("current", elementType, "item.Key", "indexTemp", "item", wellKnownTypes, writer, dependentTypes);
                    writer.WriteLine("items.Add(current);");
                }
                writer.Unindent();
                writer.WriteLine("}");
                writer.Write(lhs);
                writer.WriteNoIndent(" = ");
                writer.WriteLineNoIndent("items.ToArray();");

                writer.Unindent();
                writer.WriteLine("}");
            }
            else if (TypeIsADictionaryInterface(type, out var keyType, out var valueType))
            {
                var keyExpression = "item.Key";

                if (keyType.IsEnum)
                {
                    keyExpression = $@"Enum.TryParse<{keyType}>({keyExpression}, true, out var key) ? key : throw new {typeof(InvalidDataException)}($""Unable to parse {{item.Key}}."")";
                }
                else if (IsTryParseable(keyType))
                {
                    keyExpression = $@"{keyType}.TryParse({keyExpression}, out var key) ? key : throw new {typeof(InvalidDataException)}($""Unable to parse {{item.Key}}."")";
                }
                else if (!keyType.Equals(typeof(string)))
                {
                    writer.WriteLine($"// {type} is not supported");
                    writer.WriteLine($"{lhs} = default;");
                    return;
                }

                writer.WriteLine("{");
                writer.Indent();
                writer.WriteLine($"var dict = new System.Collections.Generic.Dictionary<{keyType}, {valueType}>();");
                writer.WriteLine($@"foreach (var item in {configurationExpr}.GetSection({index}).GetChildren())");
                writer.WriteLine("{");
                writer.Indent();

                if (valueType.Equals(typeof(string)))
                {
                    writer.WriteLine($"dict.Add({keyExpression}, item.Value);");
                }
                else
                {
                    writer.WriteLine($"{valueType} current = default;");
                    WriteValue("current", valueType, "item.Key", "indexTemp", "item", wellKnownTypes, writer, dependentTypes);
                    writer.WriteLine($"dict.Add({keyExpression}, current);");
                }
                writer.Unindent();
                writer.WriteLine("}");
                writer.Write(lhs);
                writer.WriteNoIndent(" = ");
                writer.WriteLineNoIndent("dict;");

                writer.Unindent();
                writer.WriteLine("}");
            }
            else if (IsTryParseable(type))
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
                    writer.WriteLine($"// {type} is not supported");
                    writer.WriteLine($"{lhs} = default;");
                }
                else
                {
                    writer.WriteLine($"{lhs} ??= new();");

                    writer.WriteLine($@"BindCore({configurationExpr}.GetSection({index}), {lhs});");

                    // We need to generate a method for this type
                    dependentTypes.Enqueue(type);
                }
            }
        }

        private bool IsTryParseable(Type type)
        {
            return GetStaticMethodFromHierarchy(type, "TryParse", new[] { typeof(string), type.MakeByRefType() }) != null;
        }

        private static bool IsArrayCompatibleInterface(Type type, out Type elementType)
        {
            if (type.IsArray)
            {
                elementType = type.GetElementType();
                return true;
            }

            if (!type.IsInterface || !type.IsConstructedGenericType)
            {
                elementType = null;
                return false;
            }

            Type genericTypeDefinition = type.GetGenericTypeDefinition();
            if (genericTypeDefinition.Equals(typeof(IEnumerable<>))
                || genericTypeDefinition.Equals(typeof(ICollection<>))
                || genericTypeDefinition.Equals(typeof(IList<>))
                || genericTypeDefinition.Equals(typeof(IReadOnlyCollection<>))
                || genericTypeDefinition.Equals(typeof(IReadOnlyList<>)))
            {
                elementType = type.GetGenericArguments()[0];
                return true;
            }

            elementType = null;
            return false;
        }

        private static bool TypeIsADictionaryInterface(Type type, out Type keyType, out Type elementType)
        {
            if (!type.IsInterface || !type.IsConstructedGenericType)
            {
                keyType = null;
                elementType = null;
                return false;
            }

            Type genericTypeDefinition = type.GetGenericTypeDefinition();
            if (genericTypeDefinition.Equals(typeof(IDictionary<,>)) ||
                genericTypeDefinition.Equals(typeof(IReadOnlyDictionary<,>)))
            {
                var genericArgs = type.GetGenericArguments();
                keyType = genericArgs[0];
                elementType = genericArgs[1];
                return true;
            }
            keyType = null;
            elementType = null;
            return false;
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
            public List<(InvocationExpressionSyntax, ExpressionSyntax)> BindCalls { get; } = new();
            public List<(InvocationExpressionSyntax, ExpressionSyntax)> ConfigureCalls { get; } = new();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is InvocationExpressionSyntax
                    {
                        Expression: MemberAccessExpressionSyntax
                        {
                            Name: IdentifierNameSyntax
                            {
                                Identifier: { ValueText: "Bind" }
                            }
                        },
                        ArgumentList: { Arguments: { Count: 1 } bindArgs }
                    } bindCall)
                {
                    BindCalls.Add((bindCall, bindArgs[0].Expression));
                }

                if (syntaxNode is InvocationExpressionSyntax
                    {
                        Expression: MemberAccessExpressionSyntax
                        {
                            Name: GenericNameSyntax
                            {
                                Identifier: { ValueText: "Configure" }
                            }
                        },
                        ArgumentList: { Arguments: { Count: 1 } configureArgs }
                    } configureCall)
                {
                    ConfigureCalls.Add((configureCall, configureArgs[0].Expression));
                }
            }
        }
    }
}
