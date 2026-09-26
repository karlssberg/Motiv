using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.CodeFix.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.CodeFix;

/// <summary>
///     Converts a logical expression into a Motiv specification.
/// </summary>
/// <param name="propositionName">The name of the proposition.</param>
/// <param name="defaultModelName">The default name for the model.</param>
/// <param name="document">The document containing the expression.</param>
/// <param name="fieldCustomizer">The customizer controlling field declaration and initialization.</param>
internal class LogicalExpressionToSpecConverter(
    string propositionName,
    string defaultModelName,
    Document document,
    ISpecFieldCustomizer fieldCustomizer)
{
    private Dictionary<ISymbol, string> _variableTypeNames = new(SymbolEqualityComparer.Default);

    private readonly SpecInvocationReplacer _invocationReplacer = new(propositionName, defaultModelName, fieldCustomizer);

    /// <summary>
    ///     Converts the specified logical expression into a specification.
    /// </summary>
    /// <param name="diagnostic">The diagnostic that triggered the fix.</param>
    /// <param name="logicalExpressionSyntax">The logical expression to convert.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The updated document.</returns>
    public async Task<Document> Convert(
        Diagnostic diagnostic,
        ExpressionSyntax logicalExpressionSyntax,
        CancellationToken cancellationToken = default)
    {
        var syntaxContext = new SyntaxContext(document, logicalExpressionSyntax);

        var root = await syntaxContext.RootNode(cancellationToken).ConfigureAwait(false);
        if (root is null) return document;

        var semanticModel = await syntaxContext.SemanticModel(cancellationToken).ConfigureAwait(false);
        var variables = GetVariablesInExpression(logicalExpressionSyntax, semanticModel);
        var variableSymbols = variables.Select(variable => variable.Symbol).ToImmutableArray();
        _variableTypeNames = variables.ToDictionary(
            variable => variable.Symbol,
            variable => variable.Type?.GetCSharpTypeName() ?? "object",
            SymbolEqualityComparer.Default);

        var containingTypeSymbol = await syntaxContext.ContainingTypeSymbol(cancellationToken).ConfigureAwait(false);
        var detectionResult = DetectInstanceMethods(logicalExpressionSyntax, semanticModel, containingTypeSymbol);

        var hasInstanceMethods = detectionResult.HasInstanceMethods;
        var instanceMethodNames = detectionResult.AllMethodNames;
        var staticMethodNames = detectionResult.StaticMethodNames;

        var groupedExpression = hasInstanceMethods
            ? LogicalChainGrouper.Group(logicalExpressionSyntax)
            : logicalExpressionSyntax;

        var modelTypeName = variableSymbols.Length == 1
            ? GetSymbolTypeName(variableSymbols.First())
            : $"{propositionName}.{defaultModelName}";

        var newRoot = _invocationReplacer.Replace(
            syntaxContext, variableSymbols, logicalExpressionSyntax,
            root, hasInstanceMethods, groupedExpression, modelTypeName);

        var baseNamespace = newRoot.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        var isBlockNamespace = baseNamespace is NamespaceDeclarationSyntax;
        var containingTypeName = ResolveContainingTypeName(containingTypeSymbol, isBlockNamespace, semanticModel, logicalExpressionSyntax);

        var rootMembers = BuildSpecClassMembers(
            syntaxContext, variableSymbols, groupedExpression,
            instanceMethodNames, staticMethodNames, containingTypeName).ToArray();

        newRoot = SpecClassPlacer.AddNearContainingClass(syntaxContext, newRoot, baseNamespace, rootMembers);
        newRoot = SpecClassPlacer.AddUsingStatementsIfNeeded(newRoot, fieldCustomizer, syntaxContext.LineFeed);

        var resultDoc = document.WithSyntaxRoot(newRoot);

        if (!isBlockNamespace)
            resultDoc = await SpecClassPlacer.MoveSpecClassesBeforeOrphanBrace(resultDoc, cancellationToken).ConfigureAwait(false);

        return resultDoc;
    }

    /// <summary>
    ///     Whether the fix can hold <paramref name="expression" />'s spec. One that calls an instance method captures
    ///     <c>this</c> through a constructor, which only a class can take: a record would lose its positional members,
    ///     a struct would hand the spec a stale copy of itself, and an interface cannot hold an instance field. The
    ///     spec is a class of its own, so every method of the containing type it calls must be one it can reach.
    /// </summary>
    public static bool CanConvert(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        INamedTypeSymbol? containingTypeSymbol)
    {
        var detectionResult = DetectInstanceMethods(expression, semanticModel, containingTypeSymbol);
        if (!detectionResult.ResolvedMethods.Concat(detectionResult.StaticMethods)
                .All(call => IsAccessibleToSpec(call.Method, semanticModel.Compilation)))
            return false;

        return expression.FirstAncestorOrSelf<TypeDeclarationSyntax>() is ClassDeclarationSyntax
               || !detectionResult.HasInstanceMethods;
    }

    /// <summary>
    ///     Whether a type of the same assembly, declared outside the method's own type and deriving from nothing of
    ///     it, can call the method — which is what the spec is.
    /// </summary>
    private static bool IsAccessibleToSpec(IMethodSymbol method, Compilation compilation) =>
        compilation.IsSymbolAccessibleWithin(method, compilation.Assembly);

    private static InstanceMethodResult DetectInstanceMethods(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        INamedTypeSymbol? containingTypeSymbol)
    {
        var detector = new InstanceMethodDetector(semanticModel);
        return containingTypeSymbol is not null
            ? detector.Detect(expression, containingTypeSymbol)
            : new InstanceMethodResult([], [], []);
    }

    /// <summary>
    ///     The containing type's name, qualified by its namespace in a file-scoped namespace, and otherwise as the
    ///     source would name it beside the outermost type, where the spec is declared — so a nested type keeps its
    ///     outer types.
    /// </summary>
    private static string? ResolveContainingTypeName(
        INamedTypeSymbol? containingTypeSymbol,
        bool isBlockNamespace,
        SemanticModel semanticModel,
        ExpressionSyntax expression)
    {
        if (containingTypeSymbol is null) return null;
        if (!isBlockNamespace) return containingTypeSymbol.ToDisplayString();

        var specPosition = expression.Ancestors().OfType<BaseTypeDeclarationSyntax>().Last().SpanStart;
        return containingTypeSymbol.ToMinimalDisplayString(semanticModel, specPosition);
    }

    private IEnumerable<MemberDeclarationSyntax> BuildSpecClassMembers(
        SyntaxContext syntaxContext,
        ImmutableArray<ISymbol> variableSymbols,
        ExpressionSyntax logicalExpressionSyntax,
        HashSet<string> instanceMethodNames,
        HashSet<string> staticMethodNames,
        string? containingTypeName)
    {
        var hasInstanceMethods = instanceMethodNames.Count > 0;
        var hasStaticMethods = staticMethodNames.Count > 0;

        if (variableSymbols.Length == 1 && !hasInstanceMethods && !hasStaticMethods)
            return BuildSimpleSpec(syntaxContext, variableSymbols.First(), logicalExpressionSyntax);

        if (variableSymbols.Length == 1)
            return BuildSingleVarComposedSpec(syntaxContext, variableSymbols.First(), logicalExpressionSyntax, instanceMethodNames, staticMethodNames, containingTypeName);

        return BuildMultiVarComposedSpec(syntaxContext, variableSymbols, logicalExpressionSyntax, instanceMethodNames, staticMethodNames, containingTypeName);
    }

    private IEnumerable<MemberDeclarationSyntax> BuildSimpleSpec(
        SyntaxContext syntaxContext,
        ISymbol variable,
        ExpressionSyntax logicalExpressionSyntax)
    {
        var variableTypeName = GetSymbolTypeName(variable);
        var originalExpressionText = logicalExpressionSyntax.ToString().Trim();

        var specChain = SpecFluentChainBuilder.Build(
            variableTypeName, variable.Name, logicalExpressionSyntax, originalExpressionText);

        yield return new SimpleSpecClassDeclaration(
            syntaxContext, propositionName, variableTypeName, specChain).Build();
    }

    private IEnumerable<MemberDeclarationSyntax> BuildSingleVarComposedSpec(
        SyntaxContext syntaxContext,
        ISymbol variable,
        ExpressionSyntax logicalExpressionSyntax,
        HashSet<string> instanceMethodNames,
        HashSet<string> staticMethodNames,
        string? containingTypeName)
    {
        var hasInstanceMethods = instanceMethodNames.Count > 0;
        var variableTypeName = GetSymbolTypeName(variable);
        var decomposition = ExpressionDecomposer.Decompose(
            logicalExpressionSyntax,
            expr =>
            {
                var result = ExpressionTransformer.PrefixInstanceMethods(expr, instanceMethodNames);
                if (staticMethodNames.Count > 0 && containingTypeName != null)
                    result = ExpressionTransformer.PrefixStaticMethods(result, staticMethodNames, containingTypeName);
                return result;
            });

        yield return new ComposedSpecClassDeclaration(
            syntaxContext, propositionName,
            innerLambdaModelType: variableTypeName,
            innerLambdaParameterName: variable.Name,
            decomposition,
            containingTypeName: hasInstanceMethods ? containingTypeName : null).Build();
    }

    private IEnumerable<MemberDeclarationSyntax> BuildMultiVarComposedSpec(
        SyntaxContext syntaxContext,
        ImmutableArray<ISymbol> variableSymbols,
        ExpressionSyntax logicalExpressionSyntax,
        HashSet<string> instanceMethodNames,
        HashSet<string> staticMethodNames,
        string? containingTypeName)
    {
        var hasInstanceMethods = instanceMethodNames.Count > 0;
        var decomposition = ExpressionDecomposer.Decompose(
            logicalExpressionSyntax,
            expr => ExpressionTransformer.ConvertVariablesToModelMemberAccess(expr, variableSymbols, instanceMethodNames, staticMethodNames, containingTypeName));

        var recordParameterList = ParameterList(
            SeparatedList(
                variableSymbols.Select(s =>
                    Parameter(Identifier(s.Name.Capitalize()))
                        .WithType(ParseTypeName(GetSymbolTypeName(s))))));

        var resolvedContainingTypeName = hasInstanceMethods ? containingTypeName : null;

        yield return new ComposedSpecClassDeclaration(
            syntaxContext, propositionName,
            innerLambdaModelType: defaultModelName,
            innerLambdaParameterName: "m",
            decomposition,
            resolvedContainingTypeName,
            nestedRecordName: defaultModelName,
            nestedRecordParameterList: recordParameterList).Build();
    }

    private string GetSymbolTypeName(ISymbol symbol) => _variableTypeNames[symbol];

    /// <summary>
    ///     The values the expression reads from its surroundings, each of which becomes a model value: fields,
    ///     properties, parameters, range variables and locals — except one the expression declares itself
    ///     (<c>o is string s</c>, <c>out var n</c>, a lambda's parameter), which the generated lambda declares too.
    /// </summary>
    private static ImmutableArray<(ISymbol Symbol, ITypeSymbol? Type)> GetVariablesInExpression(
        ExpressionSyntax expression,
        SemanticModel semanticModel) =>
    [
        ..expression
            .DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Where(identifier =>
                !(identifier.Parent is MemberAccessExpressionSyntax memberAccess &&
                  memberAccess.Name == identifier))
            .Select(identifier => (Identifier: identifier, Symbol: semanticModel.GetSymbolInfo(identifier).Symbol))
            .Where(candidate => IsModelValue(candidate.Symbol, expression))
            .GroupBy(candidate => candidate.Symbol!, SymbolEqualityComparer.Default)
            .Select(group =>
            {
                var symbol = group.Key;
                // A range variable carries no type of its own; the expression that reads it does
                var type = symbol.GetTypeSymbol() ?? semanticModel.GetTypeInfo(group.First().Identifier).Type;
                return (symbol, type);
            })
    ];

    private static bool IsModelValue(ISymbol? symbol, ExpressionSyntax expression) =>
        symbol switch
        {
            IFieldSymbol or IRangeVariableSymbol => true,
            IPropertySymbol property => !property.IsIndexer,
            IParameterSymbol or ILocalSymbol => !IsDeclaredWithin(symbol, expression),
            _ => false
        };

    private static bool IsDeclaredWithin(ISymbol symbol, ExpressionSyntax expression) =>
        symbol.DeclaringSyntaxReferences
            .Any(syntaxRef => syntaxRef.SyntaxTree == expression.SyntaxTree && expression.Span.Contains(syntaxRef.Span));
}
