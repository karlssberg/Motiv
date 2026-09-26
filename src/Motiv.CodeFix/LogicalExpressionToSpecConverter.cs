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
    private SpecTypeParameters? _typeParameters;
    private MemberDeclarationSyntax? _instanceField;

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
        // The spec is declared beside the outermost type, so each type is named as the source would name it there
        var specPosition = logicalExpressionSyntax.Ancestors().OfType<BaseTypeDeclarationSyntax>().Last().SpanStart;
        var modelValues = variables
            .Select(variable => (variable.Symbol, TypeName: variable.Type?.ToMinimalDisplayString(semanticModel, specPosition) ?? "object"))
            .ToImmutableArray();
        _typeParameters = SpecTypeParameters.Find(logicalExpressionSyntax, semanticModel, variables.Select(variable => variable.Type));
        var specTypeName = _typeParameters.Qualify(propositionName);

        var containingTypeSymbol = await syntaxContext.ContainingTypeSymbol(cancellationToken).ConfigureAwait(false);
        var detectionResult = DetectInstanceMethods(logicalExpressionSyntax, semanticModel, containingTypeSymbol);

        var hasInstanceMethods = detectionResult.HasInstanceMethods;
        var instanceMethodNames = detectionResult.AllMethodNames;
        var staticMethodNames = detectionResult.StaticMethodNames;

        var groupedExpression = hasInstanceMethods
            ? LogicalChainGrouper.Group(logicalExpressionSyntax)
            : logicalExpressionSyntax;

        var modelTypeName = modelValues.Length == 1
            ? modelValues[0].TypeName
            : $"{specTypeName}.{defaultModelName}";

        _instanceField = _typeParameters.IsEmpty
            ? null
            : BuildInstanceField(syntaxContext, specTypeName, modelTypeName);

        var newRoot = _invocationReplacer.Replace(
            syntaxContext, variableSymbols, logicalExpressionSyntax,
            root, hasInstanceMethods, groupedExpression, specTypeName, modelTypeName);

        // The namespace that encloses the expression, found again by its start, which the replacement comes after
        var enclosingNamespaceStart = logicalExpressionSyntax.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.SpanStart;
        var baseNamespace = newRoot.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault(ns => ns.SpanStart == enclosingNamespaceStart);
        var isBlockNamespace = baseNamespace is NamespaceDeclarationSyntax;
        var containingTypeName = ResolveContainingTypeName(containingTypeSymbol, isBlockNamespace, semanticModel, logicalExpressionSyntax);

        var rootMembers = BuildSpecClassMembers(
            syntaxContext, modelValues, groupedExpression,
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
    ///     a struct would hand the spec a stale copy of itself, and an interface cannot hold an instance field. A spec
    ///     over the surrounding type parameters holds its instance statically, once per closed type, so it has no
    ///     <c>this</c> at all. The spec is a class of its own, so every method of the containing type it calls must be
    ///     one it can reach. A property the model would read must read like a field (see <see cref="IsReadLikeAField" />).
    /// </summary>
    public static bool CanConvert(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        INamedTypeSymbol? containingTypeSymbol)
    {
        var variables = GetVariablesInExpression(expression, semanticModel);
        if (variables.Any(variable => variable.Symbol is IPropertySymbol property && !IsReadLikeAField(property)))
            return false;

        var detectionResult = DetectInstanceMethods(expression, semanticModel, containingTypeSymbol);
        if (!detectionResult.ResolvedMethods.Concat(detectionResult.StaticMethods)
                .All(call => IsAccessibleToSpec(call.Method, semanticModel.Compilation)))
            return false;

        if (!detectionResult.HasInstanceMethods)
            return true;

        return expression.FirstAncestorOrSelf<TypeDeclarationSyntax>() is ClassDeclarationSyntax
               && SpecTypeParameters.Find(expression, semanticModel, variables.Select(variable => variable.Type)).IsEmpty;
    }

    /// <summary>
    ///     Whether a type of the same assembly, declared outside the method's own type and deriving from nothing of
    ///     it, can call the method — which is what the spec is.
    /// </summary>
    private static bool IsAccessibleToSpec(IMethodSymbol method, Compilation compilation) =>
        compilation.IsSymbolAccessibleWithin(method, compilation.Assembly);

    /// <summary>
    ///     <c>public static readonly XProposition&lt;T&gt; Instance = new();</c>, declared and formatted the way the
    ///     field customizer declares a spec field.
    /// </summary>
    private MemberDeclarationSyntax BuildInstanceField(SyntaxContext syntaxContext, string specTypeName, string modelTypeName)
    {
        var field = FieldDeclaration(
                VariableDeclaration(fieldCustomizer.GetFieldType(specTypeName, modelTypeName))
                    .WithVariables(SingletonSeparatedList(
                        VariableDeclarator(Identifier(SpecTypeParameters.InstanceFieldName))
                            .WithInitializer(EqualsValueClause(fieldCustomizer.GetFieldInitializer(specTypeName))))))
            .WithModifiers(TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.StaticKeyword),
                Token(SyntaxKind.ReadOnlyKeyword)));

        var lineFeed = syntaxContext.LineFeed;
        var formatted = fieldCustomizer.FormatMember(field.NormalizeWhitespace(eol: lineFeed.ToString()), lineFeed);
        return SyntaxIndentHelper.ReindentMember(formatted, syntaxContext.GetIndent(1).ToString());
    }

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
        ImmutableArray<(ISymbol Symbol, string TypeName)> modelValues,
        ExpressionSyntax logicalExpressionSyntax,
        HashSet<string> instanceMethodNames,
        HashSet<string> staticMethodNames,
        string? containingTypeName)
    {
        var hasInstanceMethods = instanceMethodNames.Count > 0;
        var hasStaticMethods = staticMethodNames.Count > 0;

        if (modelValues.Length == 1 && !hasInstanceMethods && !hasStaticMethods)
            return BuildSimpleSpec(syntaxContext, modelValues[0], logicalExpressionSyntax);

        if (modelValues.Length == 1)
            return BuildSingleVarComposedSpec(syntaxContext, modelValues[0], logicalExpressionSyntax, instanceMethodNames, staticMethodNames, containingTypeName);

        return BuildMultiVarComposedSpec(syntaxContext, modelValues, logicalExpressionSyntax, instanceMethodNames, staticMethodNames, containingTypeName);
    }

    private IEnumerable<MemberDeclarationSyntax> BuildSimpleSpec(
        SyntaxContext syntaxContext,
        (ISymbol Symbol, string TypeName) modelValue,
        ExpressionSyntax logicalExpressionSyntax)
    {
        var originalExpressionText = logicalExpressionSyntax.ToString().Trim();

        var specChain = SpecFluentChainBuilder.Build(
            modelValue.TypeName, modelValue.Symbol.Name, logicalExpressionSyntax, originalExpressionText);

        yield return new SimpleSpecClassDeclaration(
            syntaxContext, propositionName, modelValue.TypeName, specChain, _typeParameters, _instanceField).Build();
    }

    private IEnumerable<MemberDeclarationSyntax> BuildSingleVarComposedSpec(
        SyntaxContext syntaxContext,
        (ISymbol Symbol, string TypeName) modelValue,
        ExpressionSyntax logicalExpressionSyntax,
        HashSet<string> instanceMethodNames,
        HashSet<string> staticMethodNames,
        string? containingTypeName)
    {
        var hasInstanceMethods = instanceMethodNames.Count > 0;
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
            innerLambdaModelType: modelValue.TypeName,
            innerLambdaParameterName: modelValue.Symbol.Name,
            decomposition,
            containingTypeName: hasInstanceMethods ? containingTypeName : null,
            typeParameters: _typeParameters,
            instanceField: _instanceField).Build();
    }

    private IEnumerable<MemberDeclarationSyntax> BuildMultiVarComposedSpec(
        SyntaxContext syntaxContext,
        ImmutableArray<(ISymbol Symbol, string TypeName)> modelValues,
        ExpressionSyntax logicalExpressionSyntax,
        HashSet<string> instanceMethodNames,
        HashSet<string> staticMethodNames,
        string? containingTypeName)
    {
        var hasInstanceMethods = instanceMethodNames.Count > 0;
        var memberNames = ExpressionTransformer.ModelMemberNames(modelValues.Select(value => value.Symbol));
        var decomposition = ExpressionDecomposer.Decompose(
            logicalExpressionSyntax,
            expr => ExpressionTransformer.ConvertVariablesToModelMemberAccess(expr, memberNames, instanceMethodNames, staticMethodNames, containingTypeName));

        var recordParameterList = ParameterList(
            SeparatedList(
                modelValues.Select(value =>
                    Parameter(Identifier(memberNames[value.Symbol.Name]))
                        .WithType(ParseTypeName(value.TypeName)))));

        var resolvedContainingTypeName = hasInstanceMethods ? containingTypeName : null;

        yield return new ComposedSpecClassDeclaration(
            syntaxContext, propositionName,
            innerLambdaModelType: defaultModelName,
            innerLambdaParameterName: "m",
            decomposition,
            resolvedContainingTypeName,
            nestedRecordName: defaultModelName,
            nestedRecordParameterList: recordParameterList,
            typeParameters: _typeParameters,
            instanceField: _instanceField).Build();
    }

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
            .Where(candidate => IsModelValue(candidate.Identifier, candidate.Symbol, expression))
            .GroupBy(candidate => candidate.Symbol!, SymbolEqualityComparer.Default)
            .Select(group =>
            {
                var symbol = group.Key;
                // A range variable carries no type of its own; the expression that reads it does
                var type = symbol.GetTypeSymbol() ?? semanticModel.GetTypeInfo(group.First().Identifier).Type;
                return (symbol, type);
            })
    ];

    private static bool IsModelValue(IdentifierNameSyntax identifier, ISymbol? symbol, ExpressionSyntax expression) =>
        IsReadFromScope(identifier)
        && symbol switch
        {
            IFieldSymbol => true,
            IPropertySymbol property => !property.IsIndexer,
            IRangeVariableSymbol or IParameterSymbol or ILocalSymbol => !IsDeclaredWithin(symbol, expression),
            _ => false
        };

    /// <summary>
    ///     Whether a bare name is read from the expression's scope, rather than naming a member of another object:
    ///     <c>?.Name</c>, <c>{ Name: … }</c>, <c>new T { Name = … }</c>, <c>x with { Name = … }</c>, <c>new { Name = … }</c>,
    ///     or a named argument's parameter, <c>M(name: …)</c>.
    /// </summary>
    private static bool IsReadFromScope(IdentifierNameSyntax identifier) =>
        identifier.Parent switch
        {
            MemberBindingExpressionSyntax or NameColonSyntax or ExpressionColonSyntax or NameEqualsSyntax => false,
            AssignmentExpressionSyntax { Parent: InitializerExpressionSyntax } assignment => assignment.Left != identifier,
            _ => true
        };

    /// <summary>
    ///     Whether a property can be read eagerly, when the model is built, without changing what the expression does:
    ///     an auto-property or a positional record member reads like a field, but a computed getter may throw or have
    ///     effects that a <c>&amp;&amp;</c> or <c>||</c> before it would have skipped, and so may an override of an
    ///     abstract or virtual one.
    /// </summary>
    private static bool IsReadLikeAField(IPropertySymbol property) =>
        !(property.IsAbstract || property.IsVirtual || property.IsOverride && !property.IsSealed)
        && property.DeclaringSyntaxReferences.Length > 0
        && property.DeclaringSyntaxReferences.All(reference => reference.GetSyntax() switch
        {
            ParameterSyntax => true,
            PropertyDeclarationSyntax { ExpressionBody: null, AccessorList: { } accessors } =>
                accessors.Accessors.All(accessor => accessor.Body is null && accessor.ExpressionBody is null),
            _ => false
        });

    private static bool IsDeclaredWithin(ISymbol symbol, ExpressionSyntax expression) =>
        symbol.DeclaringSyntaxReferences
            .Any(syntaxRef => syntaxRef.SyntaxTree == expression.SyntaxTree && expression.Span.Contains(syntaxRef.Span));
}
