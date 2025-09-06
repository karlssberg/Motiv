using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.FluentFactory.Generator.Analysis;
using Motiv.FluentFactory.Generator.Generation.SyntaxElements;
using Motiv.FluentFactory.Generator.Model;

namespace Motiv.FluentFactory.Generator;

[Generator(LanguageNames.CSharp)]
public class FluentFactoryGenerator : IIncrementalGenerator
{
    private const string Category = "FluentFactory";

    public static readonly DiagnosticDescriptor UnreachableConstructor = new(
        id: "MOTIV001",
        title: "Unreachable fluent constructor",
        messageFormat:
        "Unreachable fluent constructor '{0}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.Unnecessary]);

    public static readonly DiagnosticDescriptor ContainsSupersededFluentMethodTemplate = new(
        id: "MOTIV002",
        title: "Multiple fluent method contains superseded method",
        messageFormat: "Ignoring fluent-method-template '{0}', used by the parameter '{1}' in the constructor '{2}'. Instead, {3}.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.Unnecessary]);

    public static readonly DiagnosticDescriptor IncompatibleFluentMethodTemplate = new(
        id: "MOTIV003",
        title: "Fluent method template not compatible",
        category: Category,
        messageFormat: "Incompatible return type to the method '{0}'. It is not assignable to the fluent constructor parameter '{1}'.",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.Unnecessary]);

    public static readonly DiagnosticDescriptor AllFluentMethodTemplatesIncompatible = new(
        id: "MOTIV004",
        title: "All fluent method template incompatible",
        category: Category,
        messageFormat: "None of the fluent-method-templates have return types that are assignable to the fluent constructor parameter '{0}'",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.Unnecessary]);

    public static readonly DiagnosticDescriptor FluentMethodTemplateAttributeNotStatic = new(
        id: "MOTIV005",
        title: "Fluent method template not static",
        category: Category,
        messageFormat: "Static method required '{0}'",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor FluentMethodTemplateSuperseded = new(
        id: "MOTIV006",
        title: "Fluent method template superseded",
        category: Category,
        messageFormat: "Fluent method template '{0}' is not being applied for the fluent constructor parameter '{1}' in constructor '{2}'. " +
            "This is because of the higher precedence afforded to fluent constructor parameter '{3}' in constructor '{4}'.",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidCreateMethodName = new(
        id: "MOTIV007",
        title: "Invalid CreateMethodName",
        category: Category,
        messageFormat: "CreateMethodName must be a valid identifier",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateCreateMethodName = new(
        id: "MOTIV008",
        title: "Duplicate CreateMethodName",
        category: Category,
        messageFormat: "CreateMethodName must be unique",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var compilationProvider = context.CompilationProvider;

        // Step 1: Match FluentConstructorAttribute usages
        var typeOrConstructorDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(TypeName.FluentConstructorAttribute,
                (node, _) => node switch
                {
                    // Capture primary constructors
                    TypeDeclarationSyntax { ParameterList: not null, AttributeLists.Count: > 0 } => true,
                    // Capture explicit constructors
                    ConstructorDeclarationSyntax { AttributeLists.Count: > 0 } => true,
                    _ => false
                },
                (ctx, _) =>
                {
                    var syntax = ctx.TargetNode;
                    var filePath = syntax.SyntaxTree.FilePath;
                    return (syntax, filePath);
                }
            );

        // Step 2: Gather all discovered candidate constructors and capture metadata
        var constructorModels = typeOrConstructorDeclarations
            .Combine(compilationProvider)
            .SelectMany((data, ct) =>
            {
                var compilation = data.Right;
                var syntax = data.Left.syntax;
                return CreateConstructorContexts(compilation, syntax, ct);
            })
            .WithTrackingName("ConstructorModelCreation");

        // Step 3: Transform constructor contexts into compiled file contents
        var consolidated = constructorModels
            .Collect()
            .Combine(compilationProvider)
            .WithTrackingName("ConstructorModelsConsolidation")
            .SelectMany((tuple, _) =>
            {
                var (builderContextsCollection, compilation) = tuple;
                return builderContextsCollection
                    .SelectMany(builderContexts => builderContexts)
                    .GroupBy(builderContext => builderContext.RootType, SymbolEqualityComparer.Default)
                    .Select(group =>
                        new FluentModelFactory(compilation).CreateFluentFactoryCompilationUnit((INamedTypeSymbol)group.Key!, [..group]));
            })
            .WithTrackingName("ConstructorModelsToFluentBuilderFiles");

        // Step 4: Write the generated files.
        context.RegisterSourceOutput(consolidated, Execute);
    }

    private static void Execute(
        SourceProductionContext context,
        FluentFactoryCompilationUnit builder)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        foreach (var diagnostic in builder.Diagnostics)
        {
            context.ReportDiagnostic(diagnostic);
        }
        if (builder.IsEmpty)
            return;

        var source = CompilationUnit.CreateCompilationUnit(builder).NormalizeWhitespace().ToString();
        context.AddSource($"{builder.RootType.ToFileName()}.g.cs", source);
    }

    private static ImmutableArray<IEnumerable<FluentConstructorContext>> CreateConstructorContexts(
        Compilation compilation,
        SyntaxNode syntaxTree,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var semanticModel = compilation.GetSemanticModel(syntaxTree.SyntaxTree);

        var symbol = semanticModel.GetDeclaredSymbol(syntaxTree);
        if (symbol is null)
            return [];

        return
        [
            ..GetFluentFactoryMetadata(symbol)
                .Select(metadata =>
                {
                    var attributePresent = metadata.AttributePresent;
                    var rootTypeFullName = metadata.RootTypeFullName;
                    if (!attributePresent || string.IsNullOrWhiteSpace(rootTypeFullName)
                                          || !IsRootTypeDecoratedWithAttribute(metadata.RootTypeSymbol))
                        return [];

                    return symbol switch
                    {
                        IMethodSymbol constructor =>
                        [
                            new FluentConstructorContext(
                                constructor,
                                metadata.RootTypeSymbol,
                                metadata,
                                semanticModel)
                        ],
                        INamedTypeSymbol type => CreateFluentConstructorContexts(
                            type,
                            metadata.RootTypeSymbol,
                            metadata),
                        _ => []
                    };
                })
        ];

        ImmutableArray<FluentConstructorContext> CreateFluentConstructorContexts(
            INamedTypeSymbol type,
            INamedTypeSymbol alreadyDeclaredRootType,
            FluentFactoryMetadata metadata)
        {
            var primaryCtor = type.Constructors.FirstOrDefault(c => c.Parameters.Length > 0);
            if (primaryCtor != null)
                return
                [
                    new FluentConstructorContext(
                        primaryCtor,
                        alreadyDeclaredRootType,
                        metadata,
                        semanticModel)
                ];

            return
            [
                ..type.Constructors
                    .Select(ctor =>
                        new FluentConstructorContext(
                            ctor,
                            alreadyDeclaredRootType,
                            metadata,
                            semanticModel))
            ];
        }
    }

    private static bool IsRootTypeDecoratedWithAttribute(INamedTypeSymbol? alreadyDeclaredRootType)
    {
        if (alreadyDeclaredRootType is null)
            return false;

        // First, try direct matching
        if (alreadyDeclaredRootType.GetAttributes()
            .Any(attr => attr.AttributeClass?.ToDisplayString() == TypeName.FluentFactoryAttribute))
        {
            return true;
        }

        // If the alreadyDeclaredRootType is generic (e.g., Factory<>), also check its original definition
        // This handles cases where the attribute references typeof(Factory<>) but the actual type is Factory
        if (alreadyDeclaredRootType.IsGenericType)
        {
            var originalDefinition = alreadyDeclaredRootType.OriginalDefinition;
            if (originalDefinition.GetAttributes()
                .Any(attr => attr.AttributeClass?.ToDisplayString() == TypeName.FluentFactoryAttribute))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<FluentFactoryMetadata> GetFluentFactoryMetadata(ISymbol symbol)
    {
        return symbol.GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == TypeName.FluentConstructorAttribute)
            .Select(attribute =>
            {
                // ensure an attribute is present and has an argument
                if (attribute is null || attribute.ConstructorArguments.Length == 0)
                    return FluentFactoryMetadata.Invalid;

                var typeArg = attribute.ConstructorArguments.FirstOrDefault();
                if (typeArg.IsNull || typeArg.Value is not INamedTypeSymbol typeSymbol)
                    return FluentFactoryMetadata.Invalid;

                // Grab the options flags symbol
                var optionsArgument = attribute.NamedArguments
                    .FirstOrDefault(namedArg => namedArg.Key == nameof(FluentConstructorAttribute.Options))
                    .Value;
                var options = ConvertToFluentFactoryGeneratorOptions(optionsArgument);

                // Grab the create method name
                var createMethodNameArgument = attribute.NamedArguments
                    .FirstOrDefault(namedArg => namedArg.Key == nameof(FluentConstructorAttribute.CreateMethodName))
                    .Value;
                var createMethodName = createMethodNameArgument.Value as string;

                return new FluentFactoryMetadata(typeSymbol)
                {
                    Options = options,
                    RootTypeFullName = typeSymbol.ToDisplayString(),
                    CreateMethodName = createMethodName
                };
            });
    }

    private static FluentFactoryGeneratorOptions ConvertToFluentFactoryGeneratorOptions(
        TypedConstant namedAttributeArgument)
    {
        if (namedAttributeArgument.Kind != TypedConstantKind.Enum)
            return FluentFactoryGeneratorOptions.None;

        // Get the underlying int value
        var value = (int?)namedAttributeArgument.Value ?? (int)FluentOptions.None;

        // Get the type symbol for the enum
        if (namedAttributeArgument.Type is not INamedTypeSymbol enumType)
            return FluentFactoryGeneratorOptions.None;

        // Get all the declared members of the enum
        var flagMembers = enumType.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => f.HasConstantValue && f.ConstantValue is int)
            .ToList();

        // Check which flags are set
        var setFlags = flagMembers
            .Where(member =>
            {
                var memberValue = (int?)member.ConstantValue ?? (int)FluentOptions.None;
                return memberValue != 0 && (value & memberValue) == memberValue;
            })
            .ToList();

        if (setFlags.Count == 0)
            return FluentFactoryGeneratorOptions.None;

        return setFlags
            .Select(flag => Enum.TryParse<FluentFactoryGeneratorOptions>(flag.Name, true, out var option)
                ? option
                : FluentFactoryGeneratorOptions.None)
            .Aggregate((prev, next) => prev | next);
    }
}
