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
    private SpecTypeParameters _typeParameters = SpecTypeParameters.None;
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
        var containingTypeSymbol = await syntaxContext.ContainingTypeSymbol(cancellationToken).ConfigureAwait(false);
        var helpers = HelperDelegates.Find(logicalExpressionSyntax, semanticModel, containingTypeSymbol, specPosition);

        _typeParameters = SpecTypeParameters.Find(
            logicalExpressionSyntax, semanticModel, variables.Select(variable => variable.Type).Concat(helpers.SignatureTypes));
        var specTypeName = _typeParameters.Qualify(propositionName);

        // Marked, so each call is still recognised in the clauses rebuilt from the expression
        var specExpression = helpers.Annotate(logicalExpressionSyntax);
        var groupedExpression = helpers.AnyInstance
            ? LogicalChainGrouper.Group(specExpression)
            : specExpression;

        var modelTypeName = modelValues.Length == 1
            ? modelValues[0].TypeName
            : $"{specTypeName}.{defaultModelName}";

        // A generic spec that takes no delegates holds its own instance, once per closed type
        var holdsOwnInstance = !_typeParameters.IsEmpty && helpers.IsEmpty;
        _instanceField = holdsOwnInstance
            ? BuildInstanceField(syntaxContext, specTypeName, modelTypeName)
            : null;

        var newRoot = _invocationReplacer.Replace(
            syntaxContext, variableSymbols, logicalExpressionSyntax,
            root, helpers, groupedExpression, specTypeName, holdsOwnInstance, modelTypeName);

        // The namespace that encloses the expression, found again by its start, which the replacement comes after
        var enclosingNamespaceStart = logicalExpressionSyntax.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.SpanStart;
        var baseNamespace = newRoot.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault(ns => ns.SpanStart == enclosingNamespaceStart);
        var isBlockNamespace = baseNamespace is NamespaceDeclarationSyntax;

        var rootMembers = BuildSpecClassMembers(syntaxContext, modelValues, groupedExpression, helpers).ToArray();

        newRoot = SpecClassPlacer.AddNearContainingClass(syntaxContext, newRoot, baseNamespace, rootMembers);
        newRoot = SpecClassPlacer.AddUsingStatementsIfNeeded(newRoot, fieldCustomizer, syntaxContext.LineFeed, helpers.NeedsSystemNamespace);

        var resultDoc = document.WithSyntaxRoot(newRoot);

        if (!isBlockNamespace)
            resultDoc = await SpecClassPlacer.MoveSpecClassesBeforeOrphanBrace(resultDoc, cancellationToken).ConfigureAwait(false);

        return resultDoc;
    }

    /// <summary>
    ///     Whether the fix can hold <paramref name="expression" />'s spec. Each method of the containing type it calls
    ///     must be one a delegate can carry (see <see cref="HelperDelegates.CanBePassed" />). A delegate bound to <c>this</c>
    ///     needs an instance to build the spec from, which only a class can give: a record would lose its positional
    ///     members, a struct would hand the spec a stale copy of itself, and an interface cannot hold an instance
    ///     field. A spec over the enclosing method's type parameters holds its own instance, once per closed type,
    ///     where nothing can hand it a delegate. A property the model would read must read like a field (see
    ///     <see cref="IsReadLikeAField" />).
    /// </summary>
    public static bool CanConvert(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        INamedTypeSymbol? containingTypeSymbol)
    {
        var variables = GetVariablesInExpression(expression, semanticModel);
        if (variables.Any(variable => variable.Symbol is IPropertySymbol property && !IsReadLikeAField(property)))
            return false;

        var specPosition = expression.Ancestors().OfType<BaseTypeDeclarationSyntax>().Last().SpanStart;
        var helpers = HelperDelegates.Find(expression, semanticModel, containingTypeSymbol, specPosition);
        if (!helpers.CanBePassed)
            return false;

        if (helpers.IsEmpty)
            return true;

        if (helpers.AnyInstance && expression.FirstAncestorOrSelf<TypeDeclarationSyntax>() is not ClassDeclarationSyntax)
            return false;

        // The syntactic check spares the semantic search wherever no generic declaration is in scope
        return !SpecTypeParameters.AnyInScope(expression)
               || !SpecTypeParameters.Find(expression, semanticModel, variables.Select(variable => variable.Type).Concat(helpers.SignatureTypes))
                   .DeclaresMethodTypeParameters;
    }

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
                            .WithInitializer(EqualsValueClause(fieldCustomizer.GetFieldInitializer(specTypeName, ArgumentList()))))))
            .WithModifiers(TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.StaticKeyword),
                Token(SyntaxKind.ReadOnlyKeyword)));

        return _invocationReplacer.FormatMember(field, syntaxContext.GetIndent(1), syntaxContext.LineFeed);
    }

    private IEnumerable<MemberDeclarationSyntax> BuildSpecClassMembers(
        SyntaxContext syntaxContext,
        ImmutableArray<(ISymbol Symbol, string TypeName)> modelValues,
        ExpressionSyntax logicalExpressionSyntax,
        HelperDelegates helpers)
    {
        if (modelValues.Length == 1 && helpers.IsEmpty)
            return CanDecompose(logicalExpressionSyntax)
                ? BuildSingleVarComposedSpec(syntaxContext, modelValues[0], logicalExpressionSyntax, helpers)
                : BuildSimpleSpec(syntaxContext, modelValues[0], logicalExpressionSyntax);

        if (modelValues.Length == 1)
            return BuildSingleVarComposedSpec(syntaxContext, modelValues[0], logicalExpressionSyntax, helpers);

        return BuildMultiVarComposedSpec(syntaxContext, modelValues, logicalExpressionSyntax, helpers);
    }

    // A pattern variable (`obj is string s && s.Length > 0`) is scoped to the expression that declares it,
    // so splitting its clauses into separate sub-specs would leave the later ones referring to nothing.
    private static bool CanDecompose(ExpressionSyntax expression) =>
        ExpressionDecomposer.IsComposite(expression)
        && !expression.DescendantNodes().OfType<SingleVariableDesignationSyntax>().Any();

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
        HelperDelegates helpers)
    {
        var decomposition = ExpressionDecomposer.Decompose(logicalExpressionSyntax, helpers.ReplaceCalls);

        yield return new ComposedSpecClassDeclaration(
            syntaxContext, propositionName,
            innerLambdaModelType: modelValue.TypeName,
            innerLambdaParameterName: modelValue.Symbol.Name,
            decomposition,
            _typeParameters,
            constructorParameters: [..helpers.Parameters],
            instanceField: _instanceField).Build();
    }

    private IEnumerable<MemberDeclarationSyntax> BuildMultiVarComposedSpec(
        SyntaxContext syntaxContext,
        ImmutableArray<(ISymbol Symbol, string TypeName)> modelValues,
        ExpressionSyntax logicalExpressionSyntax,
        HelperDelegates helpers)
    {
        var memberNames = ExpressionTransformer.ModelMemberNames(modelValues.Select(value => value.Symbol));
        var decomposition = ExpressionDecomposer.Decompose(
            logicalExpressionSyntax,
            expr => helpers.ReplaceCalls(ExpressionTransformer.ConvertVariablesToModelMemberAccess(expr, memberNames)));

        var recordParameterList = ParameterList(
            SeparatedList(
                modelValues.Select(value =>
                    Parameter(Identifier(memberNames[value.Symbol.Name]))
                        .WithType(ParseTypeName(value.TypeName)))));

        yield return new ComposedSpecClassDeclaration(
            syntaxContext, propositionName,
            innerLambdaModelType: defaultModelName,
            innerLambdaParameterName: "m",
            decomposition,
            _typeParameters,
            constructorParameters: [..helpers.Parameters],
            nestedRecordName: defaultModelName,
            nestedRecordParameterList: recordParameterList,
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
