using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Reflection;
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

            var metadataLoadContext = new MetadataLoadContext(context.Compilation);
            var wellKnownTypes = new WellKnownTypes(metadataLoadContext);
            if (wellKnownTypes.IConfigurationType is null)
            {
                // No configuration type, bail
                return;
            }

            var configTypes = new HashSet<Type>();

            ProcessBindCalls(context, receiver, metadataLoadContext, wellKnownTypes, configTypes);
            ProcessGetCalls(context, receiver, metadataLoadContext, wellKnownTypes, configTypes);

            if (wellKnownTypes.IServiceCollectionType is not null)
            {
                ProcessConfigureCalls(context, receiver, metadataLoadContext, wellKnownTypes, configTypes);
            }

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

            writer.WriteLine($"internal static class GeneratedConfigurationBinder");
            writer.StartBlock();

            var i = 0;
            // Only generate configure calls if these assemblies are referenced and if we found configuration types
            if (wellKnownTypes.IServiceCollectionType is not null && configTypes.Count > 0)
            {
                writer.WriteLine(@$"public static {wellKnownTypes.IServiceCollectionType} Configure<T>(this {wellKnownTypes.IServiceCollectionType} services, {wellKnownTypes.IConfigurationType} configuration)");
                writer.StartBlock();

                foreach (var c in configTypes)
                {
                    // Configure method
                    writer.WriteLine(@$"{(i > 0 ? "else " : "")}if (typeof(T) == typeof({c}))");
                    writer.StartBlock();
                    writer.WriteLine(@$"return services.Configure<{c}>(o => BindCore(configuration, o));");
                    writer.EndBlock();
                    i++;
                }

                writer.WriteLine(@$"throw new {typeof(InvalidOperationException)}($""Unable to bind {{typeof(T)}}"");");
                writer.EndBlock();
                writer.WriteLineNoIndent("");
            }

            // Get methods
            writer.WriteLine(@$"public static T Get<T>(this {wellKnownTypes.IConfigurationType} configuration)");
            writer.StartBlock();

            i = 0;
            foreach (var c in configTypes)
            {
                // Configure method
                writer.WriteLine(@$"{(i > 0 ? "else " : "")}if (typeof(T) == typeof({c}))");
                writer.StartBlock();
                // Which constructor?
                writer.WriteLine($"var obj = ({c})System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof({c}));");
                writer.WriteLine(@$"BindCore(configuration, obj);");
                writer.WriteLine("return (T)(object)obj;");
                writer.EndBlock();
                i++;
            }

            writer.WriteLine(@$"throw new {typeof(InvalidOperationException)}($""Unable to bind {{typeof(T)}}"");");
            writer.EndBlock();
            writer.WriteLineNoIndent("");

            // Bind methods
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
                writer.StartBlock();
                GenerateConfigurationBind(wellKnownTypes, writer, type, q);
                writer.EndBlock();
                writer.WriteLineNoIndent("");
            }

            writer.EndBlock();

            if (sb.Length > 0)
            {
                var text = writer.ToString();

                context.AddSource($"Configuration.g", SourceText.From(text, Encoding.UTF8));
            }
        }

        private static void ProcessBindCalls(GeneratorExecutionContext context, SyntaxReceiver receiver, MetadataLoadContext metadataLoadContext, WellKnownTypes wellKnownTypes, HashSet<Type> configTypes)
        {
            foreach (var invocation in receiver.BindCalls)
            {
                var semanticModel = context.Compilation.GetSemanticModel(invocation.SyntaxTree);

                var operation = semanticModel.GetOperation(invocation) as IInvocationOperation;

                if (operation is IInvocationOperation { Arguments: { Length: 2 } arguments } invocationOperation &&
                    invocationOperation.TargetMethod.IsExtensionMethod &&
                    wellKnownTypes.IConfigurationType.Equals(arguments[0].Parameter.Type) &&
                    arguments[1].Parameter.Type.SpecialType == SpecialType.System_Object)
                {
                    // We're looking for IConfiguration.Bind(object)
                }
                else
                {
                    continue;
                }

                var argument = arguments[1].Value as IConversionOperation;

                static ITypeSymbol ResolveType(IOperation argument)
                {
                    return argument switch
                    {
                        IConversionOperation c => ResolveType(c.Operand),
                        IInstanceReferenceOperation i => i.Type,
                        ILocalReferenceOperation l => l.Local.Type,
                        IFieldReferenceOperation f => f.Field.Type,
                        IMethodReferenceOperation m when m.Method.MethodKind == MethodKind.Constructor => m.Method.ContainingType,
                        IMethodReferenceOperation m => m.Method.ReturnType,
                        IAnonymousFunctionOperation f => f.Symbol.ReturnType,
                        _ => null
                    };
                }

                var configurationType = ResolveType(argument)?.WithNullableAnnotation(NullableAnnotation.None);

                if (configurationType is null || configurationType.SpecialType == SpecialType.System_Object || configurationType.SpecialType == SpecialType.System_Void)
                {
                    continue;
                }

                configTypes.Add(configurationType.AsType(metadataLoadContext));
            }
        }

        private static void ProcessGetCalls(GeneratorExecutionContext context, SyntaxReceiver receiver, MetadataLoadContext metadataLoadContext, WellKnownTypes wellKnownTypes, HashSet<Type> configTypes)
        {
            foreach (var invocation in receiver.GetCalls)
            {
                var semanticModel = context.Compilation.GetSemanticModel(invocation.SyntaxTree);

                var operation = semanticModel.GetOperation(invocation);

                if (operation is IInvocationOperation { Arguments: { Length: 1 } } invocationOperation &&
                    invocationOperation.TargetMethod.IsExtensionMethod &&
                    invocationOperation.TargetMethod.IsGenericMethod &&
                    wellKnownTypes.IConfigurationType.Equals(invocationOperation.TargetMethod.Parameters[0].Type))
                {
                    // We're looking for IConfiguration.Get<T>()
                }
                else
                {
                    continue;
                }

                var configurationType = invocationOperation.TargetMethod.TypeArguments[0].WithNullableAnnotation(NullableAnnotation.None);

                if (configurationType is null || configurationType.SpecialType == SpecialType.System_Object || configurationType.SpecialType == SpecialType.System_Void)
                {
                    continue;
                }

                configTypes.Add(configurationType.AsType(metadataLoadContext));
            }
        }

        private static void ProcessConfigureCalls(GeneratorExecutionContext context, SyntaxReceiver receiver, MetadataLoadContext metadataLoadContext, WellKnownTypes wellKnownTypes, HashSet<Type> configTypes)
        {
            foreach (var invocation in receiver.ConfigureCalls)
            {
                var semanticModel = context.Compilation.GetSemanticModel(invocation.SyntaxTree);

                var operation = semanticModel.GetOperation(invocation);

                if (operation is IInvocationOperation { Arguments: { Length: 2 } } invocationOperation &&
                    invocationOperation.TargetMethod.IsExtensionMethod &&
                    invocationOperation.TargetMethod.IsGenericMethod &&
                    wellKnownTypes.IServiceCollectionType.Equals(invocationOperation.TargetMethod.Parameters[0].Type) &&
                    wellKnownTypes.IConfigurationType.Equals(invocationOperation.TargetMethod.Parameters[1].Type))
                {
                    // We're looking for IServiceCollection.Configure<T>(IConfiguration)
                }
                else
                {
                    continue;
                }

                var configurationType = invocationOperation.TargetMethod.TypeArguments[0].WithNullableAnnotation(NullableAnnotation.None);

                if (configurationType is null || configurationType.SpecialType == SpecialType.System_Object)
                {
                    continue;
                }

                configTypes.Add(configurationType.AsType(metadataLoadContext));
            }
        }

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
                writer.WriteLineNoIndent($@"{configurationExpr}[{index}] is {{ }} {tempName} ? System.Convert.FromBase64String({tempName}) : default;");
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
                writer.StartBlock();
                writer.WriteLine($"var items = new System.Collections.Generic.List<{elementType}>();");
                writer.WriteLine($@"foreach (var item in {configurationExpr}.GetSection({index}).GetChildren())");
                writer.StartBlock();
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
                writer.EndBlock();
                writer.Write(lhs);
                writer.WriteNoIndent(" = ");
                writer.WriteLineNoIndent("items.ToArray();");

                writer.EndBlock();
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

                writer.StartBlock();
                writer.WriteLine($"var dict = new System.Collections.Generic.Dictionary<{keyType}, {valueType}>();");
                writer.WriteLine($@"foreach (var item in {configurationExpr}.GetSection({index}).GetChildren())");
                writer.StartBlock();

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

                writer.EndBlock();
                writer.Write(lhs);
                writer.WriteNoIndent(" = ");
                writer.WriteLineNoIndent("dict;");

                writer.EndBlock();
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
            return type.GetMethod("TryParse",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(string), type.MakeByRefType() },
                modifiers: default) is not null;
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

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        private class SyntaxReceiver : ISyntaxReceiver
        {
            public List<InvocationExpressionSyntax> BindCalls { get; } = new();
            public List<InvocationExpressionSyntax> GetCalls { get; } = new();
            public List<InvocationExpressionSyntax> ConfigureCalls { get; } = new();

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
                    BindCalls.Add(bindCall);
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
                    ConfigureCalls.Add(configureCall);
                }

                if (syntaxNode is InvocationExpressionSyntax
                    {
                        Expression: MemberAccessExpressionSyntax
                        {
                            Name: GenericNameSyntax
                            {
                                Identifier: { ValueText: "Get" }
                            }
                        },
                        ArgumentList: { Arguments: { Count: 0 } getArgs }
                    } getCall)
                {
                    GetCalls.Add(getCall);
                }
            }
        }
    }
}
