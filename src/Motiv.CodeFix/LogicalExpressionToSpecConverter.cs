using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.CodeFix.Syntax;

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
        var variableSymbols = GetVariablesInExpression(logicalExpressionSyntax, semanticModel).ToImmutableArray();

        var containingTypeSymbol = await syntaxContext.ContainingTypeSymbol(cancellationToken).ConfigureAwait(false);
        var detectionResult = DetectInstanceMethods(logicalExpressionSyntax, semanticModel, containingTypeSymbol);

        var hasInstanceMethods = detectionResult.HasInstanceMethods;
        var instanceMethodNames = detectionResult.AllMethodNames;

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
        var containingTypeName = ResolveContainingTypeName(containingTypeSymbol, isBlockNamespace);

        var rootMembers = BuildSpecClassMembers(
            syntaxContext, variableSymbols, groupedExpression,
            instanceMethodNames, containingTypeName).ToArray();

        newRoot = SpecClassPlacer.AddNearContainingClass(syntaxContext, newRoot, baseNamespace, rootMembers);
        newRoot = SpecClassPlacer.AddUsingStatementsIfNeeded(newRoot, fieldCustomizer);

        var resultDoc = document.WithSyntaxRoot(newRoot);

        if (!isBlockNamespace)
            resultDoc = await SpecClassPlacer.MoveSpecClassesBeforeOrphanBrace(resultDoc, cancellationToken).ConfigureAwait(false);

        return resultDoc;
    }

    private static InstanceMethodResult DetectInstanceMethods(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        INamedTypeSymbol? containingTypeSymbol)
    {
        var detector = new InstanceMethodDetector(semanticModel);
        return containingTypeSymbol is not null
            ? detector.Detect(expression, containingTypeSymbol)
            : new InstanceMethodResult([], []);
    }

    private static string? ResolveContainingTypeName(
        INamedTypeSymbol? containingTypeSymbol,
        bool isBlockNamespace) =>
        containingTypeSymbol is not null
            ? (isBlockNamespace ? containingTypeSymbol.Name : containingTypeSymbol.ToDisplayString())
            : null;

    private IEnumerable<MemberDeclarationSyntax> BuildSpecClassMembers(
        SyntaxContext syntaxContext,
        ImmutableArray<ISymbol> variableSymbols,
        ExpressionSyntax logicalExpressionSyntax,
        HashSet<string> instanceMethodNames,
        string? containingTypeName)
    {
        var hasInstanceMethods = instanceMethodNames.Count > 0;

        if (variableSymbols.Length == 1 && !hasInstanceMethods)
            return BuildSimpleSpec(syntaxContext, variableSymbols.First(), logicalExpressionSyntax);

        if (variableSymbols.Length == 1 && hasInstanceMethods)
            return BuildSingleVarComposedSpec(syntaxContext, variableSymbols.First(), logicalExpressionSyntax, instanceMethodNames, containingTypeName);

        return BuildMultiVarComposedSpec(syntaxContext, variableSymbols, logicalExpressionSyntax, instanceMethodNames, containingTypeName);
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
        string? containingTypeName)
    {
        var variableTypeName = GetSymbolTypeName(variable);
        var decomposition = ExpressionDecomposer.Decompose(
            logicalExpressionSyntax,
            expr => ExpressionTransformer.PrefixInstanceMethods(expr, instanceMethodNames));

        yield return new ComposedSpecClassDeclaration(
            syntaxContext, propositionName,
            innerLambdaModelType: variableTypeName,
            innerLambdaParameterName: variable.Name,
            decomposition,
            containingTypeName: containingTypeName).Build();
    }

    private IEnumerable<MemberDeclarationSyntax> BuildMultiVarComposedSpec(
        SyntaxContext syntaxContext,
        ImmutableArray<ISymbol> variableSymbols,
        ExpressionSyntax logicalExpressionSyntax,
        HashSet<string> instanceMethodNames,
        string? containingTypeName)
    {
        var hasInstanceMethods = instanceMethodNames.Count > 0;
        var decomposition = ExpressionDecomposer.Decompose(
            logicalExpressionSyntax,
            expr => ExpressionTransformer.ConvertVariablesToModelMemberAccess(expr, variableSymbols, instanceMethodNames));

        var recordParameters = string.Join(", ", variableSymbols.Select(s =>
        {
            var typeName = GetSymbolTypeName(s);
            return $"{typeName} {s.Name.Capitalize()}";
        }));

        var resolvedContainingTypeName = hasInstanceMethods ? containingTypeName : null;

        yield return new ComposedSpecClassDeclaration(
            syntaxContext, propositionName,
            innerLambdaModelType: defaultModelName,
            innerLambdaParameterName: "m",
            decomposition,
            resolvedContainingTypeName,
            nestedRecordName: defaultModelName,
            nestedRecordParameters: recordParameters).Build();
    }

    private static string GetSymbolTypeName(ISymbol symbol) =>
        symbol.GetTypeSymbol()?.GetCSharpTypeName() ?? "object";

    private static IEnumerable<ISymbol> GetVariablesInExpression(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        return expression
            .DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Where(identifier =>
            {
                if (identifier.Parent is MemberAccessExpressionSyntax memberAccess &&
                    memberAccess.Name == identifier)
                {
                    return false;
                }
                return true;
            })
            .Select(identifier => ModelExtensions.GetSymbolInfo(semanticModel, identifier).Symbol)
            .Where(symbol => symbol is IFieldSymbol or ILocalSymbol or IParameterSymbol)
            .Distinct(SymbolEqualityComparer.Default)
            .Cast<ISymbol>();
    }
}
