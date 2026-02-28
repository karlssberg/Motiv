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
public class LogicalExpressionToSpecConverter(
    string propositionName,
    string defaultModelName,
    Document document)
{
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

        // Detect instance method calls
        var containingTypeSymbol = await syntaxContext.ContainingTypeSymbol(cancellationToken).ConfigureAwait(false);
        var instanceMethodDetector = new InstanceMethodDetector(semanticModel);
        var instanceMethods = containingTypeSymbol is not null
            ? instanceMethodDetector.GetInstanceMethods(logicalExpressionSyntax, containingTypeSymbol)
            : [];

        var hasInstanceMethods = instanceMethods.Count > 0;
        var instanceMethodNames = new HashSet<string>(instanceMethods.Select(m => m.Method.Name));

        var newRoot = ReplaceLogicalExpressionWithSpecInvocation(syntaxContext, variableSymbols, logicalExpressionSyntax, root, hasInstanceMethods);

        var rootMembers = GetRootMembers(syntaxContext, variableSymbols, logicalExpressionSyntax, instanceMethodNames, containingTypeSymbol).ToArray();
        var baseNamespace = newRoot.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        newRoot = baseNamespace != null
            ? AddToNamespace(syntaxContext, newRoot, baseNamespace, rootMembers)
            : AddToCompilationUnit(syntaxContext, newRoot, rootMembers);
        newRoot = AddUsingStatementsIfNeeded(newRoot);

        return document.WithSyntaxRoot(newRoot);
    }

    private IEnumerable<MemberDeclarationSyntax> GetRootMembers(
        SyntaxContext syntaxContext,
        ImmutableArray<ISymbol> variableSymbols,
        ExpressionSyntax logicalExpressionSyntax,
        HashSet<string> instanceMethodNames,
        INamedTypeSymbol? containingTypeSymbol)
    {
        var hasInstanceMethods = instanceMethodNames.Count > 0;

        if (variableSymbols.Length == 1 && !hasInstanceMethods)
        {
            var variable = variableSymbols.First();
            var variableTypeName = GetSymbolTypeName(variable);
            var originalExpressionText = logicalExpressionSyntax.ToString().Trim();

            var specChain = SpecFluentChainBuilder.Build(
                variableTypeName,
                variable.Name,
                logicalExpressionSyntax,
                originalExpressionText);

            yield return new SimpleSpecClassDeclaration(
                syntaxContext, propositionName, variableTypeName, specChain).Build();
            yield break;
        }

        if (variableSymbols.Length == 1 && hasInstanceMethods)
        {
            var variable = variableSymbols.First();
            var variableTypeName = GetSymbolTypeName(variable);
            var decomposition = DecomposeExpression(
                logicalExpressionSyntax,
                expr => PrefixInstanceMethods(expr, instanceMethodNames));

            yield return new ComposedSpecClassDeclaration(
                syntaxContext,
                propositionName,
                innerLambdaModelType: variableTypeName,
                innerLambdaParameterName: variable.Name,
                decomposition,
                containingTypeName: containingTypeSymbol?.ToDisplayString()).Build();
            yield break;
        }

        var multiVarDecomposition = DecomposeExpression(
            logicalExpressionSyntax,
            expr => ConvertLogicVariablesToModelMemberAccess(expr, variableSymbols, instanceMethodNames));

        var recordParameters = string.Join(", ", variableSymbols.Select(s =>
        {
            var typeName = GetSymbolTypeName(s);
            return $"{typeName} {s.Name.Capitalize()}";
        }));

        var containingTypeName = hasInstanceMethods ? containingTypeSymbol?.ToDisplayString() : null;

        yield return new ComposedSpecClassDeclaration(
            syntaxContext,
            propositionName,
            innerLambdaModelType: defaultModelName,
            innerLambdaParameterName: "m",
            multiVarDecomposition,
            containingTypeName,
            nestedRecordName: defaultModelName,
            nestedRecordParameters: recordParameters).Build();
    }

    private static ExpressionDecomposition DecomposeExpression(
        ExpressionSyntax expression,
        Func<ExpressionSyntax, ExpressionSyntax> transformClause)
    {
        var counter = 0;
        return Decompose(expression);

        ExpressionDecomposition Decompose(ExpressionSyntax expr) => expr switch
        {
            // Unwrap parentheses - recursively handle inner expression
            ParenthesizedExpressionSyntax paren => DecomposeParenthesized(paren),

            // Handle NOT (!) prefix unary expression
            PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.ExclamationToken } unary
                => DecomposeNot(unary),

            // Handle binary logical operators (&&, ||, ^)
            BinaryExpressionSyntax binary when GetLogicalOperator(binary) is { } op
                => DecomposeBinary(binary, op),

            // Leaf node: create a clause
            _ => CreateLeafClause(expr)
        };

        ExpressionDecomposition DecomposeParenthesized(ParenthesizedExpressionSyntax paren)
        {
            var inner = Decompose(paren.Expression);
            return new ExpressionDecomposition(
                inner.Clauses,
                $"({inner.CompositionExpression})");
        }

        ExpressionDecomposition DecomposeNot(PrefixUnaryExpressionSyntax unary)
        {
            var inner = Decompose(unary.Operand);
            return new ExpressionDecomposition(
                inner.Clauses,
                $"!{inner.CompositionExpression}");
        }

        (string Op, bool IsInfix)? GetLogicalOperator(BinaryExpressionSyntax binary) =>
            binary.OperatorToken.Kind() switch
            {
                SyntaxKind.AmpersandAmpersandToken => (".AndAlso", false),
                SyntaxKind.BarBarToken => (".OrElse", false),
                SyntaxKind.CaretToken => (" ^ ", true),
                _ => null
            };

        ExpressionDecomposition DecomposeBinary(BinaryExpressionSyntax binary, (string Op, bool IsInfix) op)
        {
            var left = Decompose(binary.Left);
            var right = Decompose(binary.Right);
            var allClauses = left.Clauses.Concat(right.Clauses).ToList();

            var composition = op.IsInfix
                ? $"{left.CompositionExpression}{op.Op}{right.CompositionExpression}"
                : $"{left.CompositionExpression}{op.Op}({right.CompositionExpression})";

            return new ExpressionDecomposition(allClauses, composition);
        }

        ExpressionDecomposition CreateLeafClause(ExpressionSyntax expr)
        {
            counter++;
            var transformed = transformClause(expr);
            var clauseName = ClauseNameDeriver.DeriveName(transformed, counter);
            return new ExpressionDecomposition(
                [(expr.ToString().Trim(), transformed.ToString(), expr)],
                clauseName);
        }
    }

    private static ExpressionSyntax PrefixInstanceMethods(
        ExpressionSyntax expression,
        HashSet<string> instanceMethodNames)
    {
        if (instanceMethodNames.Count == 0) return expression;

        var instanceInvocations = expression.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.Expression is IdentifierNameSyntax id
                          && instanceMethodNames.Contains(id.Identifier.ValueText))
            .ToList();

        if (instanceInvocations.Count == 0) return expression;

        return expression.ReplaceNodes(
            instanceInvocations,
            (original, _) =>
            {
                var methodName = (IdentifierNameSyntax)original.Expression;
                var qualifiedAccess = MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("instance"),
                    methodName);
                return original.WithExpression(qualifiedAccess);
            });
    }

    private static ExpressionSyntax ConvertLogicVariablesToModelMemberAccess(
        ExpressionSyntax expression,
        ImmutableArray<ISymbol> variableSymbols,
        HashSet<string> instanceMethodNames)
    {
        var variableNames = variableSymbols.Select(s => s.Name).ToArray();

        // Three-pass approach:
        // 1. Replace member access expressions that start with a variable (e.g., order.Total -> m.Order.Total)
        // 2. Replace standalone identifiers (e.g., x -> m.X)
        // 3. Prefix instance method invocations with "instance." (e.g., IsGreen(x) -> instance.IsGreen(x))

        // First pass: Find member access expressions where the root is a variable
        var memberAccessToReplace = expression.DescendantNodesAndSelf()
            .OfType<MemberAccessExpressionSyntax>()
            .Where(ma =>
            {
                // Find the leftmost identifier in the chain
                var expr = ma.Expression;
                while (expr is MemberAccessExpressionSyntax innerMemberAccess)
                    expr = innerMemberAccess.Expression;

                return expr is IdentifierNameSyntax id && variableNames.Contains(id.Identifier.ValueText);
            })
            .ToList();

        var result = expression.ReplaceNodes(
            memberAccessToReplace,
            (original, _) =>
            {
                // Find the root variable identifier
                var expr = original.Expression;
                while (expr is MemberAccessExpressionSyntax innerMa)
                    expr = innerMa.Expression;

                if (expr is not IdentifierNameSyntax rootId)
                    return original;

                var propertyName = rootId.Identifier.ValueText.Capitalize();
                var newBase = MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("m"),
                    IdentifierName(propertyName));

                // Rebuild the member access chain: m.Order + .Total -> m.Order.Total
                return RebuildMemberAccessChain(original, newBase);

            });

        // Second pass: Replace standalone identifiers that weren't part of member access
        var standaloneIdentifiers = result.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Where(id => variableNames.Contains(id.Identifier.ValueText))
            .Where(id => id.Parent is not MemberAccessExpressionSyntax)
            .ToList();

        result = result.ReplaceNodes(
            standaloneIdentifiers,
            (original, _) =>
            {
                var propertyName = original.Identifier.ValueText.Capitalize();
                return MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("m"),
                        IdentifierName(propertyName))
                    .WithTriviaFrom(original);
            });

        // Third pass: Prefix instance method invocations with "instance."
        return PrefixInstanceMethods(result, instanceMethodNames);
    }

    private static ExpressionSyntax RebuildMemberAccessChain(
        MemberAccessExpressionSyntax original,
        ExpressionSyntax newBase)
    {
        // If the original expression part is just an identifier, we're done
        if (original.Expression is IdentifierNameSyntax)
        {
            return MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    newBase,
                    original.Name)
                .WithTriviaFrom(original);
        }

        // If it's a nested member access, rebuild recursively
        if (original.Expression is MemberAccessExpressionSyntax innerMa)
        {
            var rebuiltInner = RebuildMemberAccessChain(innerMa, newBase);
            return MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    rebuiltInner,
                    original.Name)
                .WithTriviaFrom(original);
        }

        return original;
    }

    private static string GetSymbolTypeName(ISymbol symbol) =>
        symbol.GetTypeSymbol()?.GetCSharpTypeName() ?? "object";

    private SyntaxNode ReplaceLogicalExpressionWithSpecInvocation(
        SyntaxContext syntaxContext,
        ImmutableArray<ISymbol> variableSymbols,
        ExpressionSyntax logicalExpressionSyntax,
        SyntaxNode root,
        bool hasInstanceMethods)
    {
        if (variableSymbols.Length != 1 || hasInstanceMethods)
            return ReplaceMultiVariableExpression(syntaxContext, variableSymbols, logicalExpressionSyntax, root, hasInstanceMethods);

        var createSpecInvocation = SpecInvocationExpressionSyntax.Create(
            IdentifierName(propositionName),
            IdentifierName(variableSymbols.First().Name));
        return root.ReplaceNode(logicalExpressionSyntax, createSpecInvocation);
    }

    private SyntaxNode ReplaceMultiVariableExpression(
        SyntaxContext syntaxContext,
        ImmutableArray<ISymbol> variableSymbols,
        ExpressionSyntax logicalExpressionSyntax,
        SyntaxNode root,
        bool hasInstanceMethods)
    {
        var method = logicalExpressionSyntax.Ancestors().OfType<MethodDeclarationSyntax>().First();
        var containingClass = method.Ancestors().OfType<ClassDeclarationSyntax>().First();
        var statement = logicalExpressionSyntax.Ancestors().OfType<StatementSyntax>().FirstOrDefault();

        var fieldName = $"_{propositionName.ToCamelCase()}";
        var originalExprText = logicalExpressionSyntax.ToString();

        string specInvocation;
        string fieldDeclaration;

        if (hasInstanceMethods)
        {
            // For instance methods with constructor parameter
            if (variableSymbols.Length == 1)
            {
                // Single variable: pass directly
                var varName = variableSymbols.First().Name;
                specInvocation = $"{fieldName}.IsSatisfiedBy({varName})";
            }
            else
            {
                // Multiple variables: create model instance
                var modelArgs = string.Join(", ", variableSymbols.Select(s => s.Name));
                specInvocation = $"{fieldName}.IsSatisfiedBy(new {propositionName}.{defaultModelName}({modelArgs}))";
            }
            fieldDeclaration = $"private readonly {propositionName} {fieldName};";
        }
        else
        {
            var modelArgs = string.Join(", ", variableSymbols.Select(s => s.Name));
            specInvocation = $"{fieldName}.IsSatisfiedBy(new {propositionName}.{defaultModelName}({modelArgs}))";
            fieldDeclaration = $"private readonly {propositionName} {fieldName} = new {propositionName}();";
        }

        var assignmentLine = statement switch
        {
            ReturnStatementSyntax => "return result.Satisfied;",
            LocalDeclarationStatementSyntax local =>
                $"var {local.Declaration.Variables.First().Identifier.Text} = result.Satisfied;",
            ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment } =>
                $"{assignment.Left} = result.Satisfied;",
            null when method.ExpressionBody is not null && method.ReturnType.ToString() == "bool" => "return result.Satisfied;",
            _ => "var isSatisfied = result.Satisfied;"
        };

        // Generate method source with optional constructor
        var newMethodSource = hasInstanceMethods
            ? $$"""
                class Temp
                {
                    {{fieldDeclaration}}
                    public {{containingClass.Identifier}}()
                    {
                        {{fieldName}} = new {{propositionName}}(this);
                    }
                    public {{method.ReturnType}} {{method.Identifier}}{{method.ParameterList}}
                    {
                        // {{originalExprText}}
                        var result = {{specInvocation}};
                        {{assignmentLine}}
                    }
                }
                """
            : $$"""
                class Temp
                {
                    {{fieldDeclaration}}
                    public {{method.ReturnType}} {{method.Identifier}}{{method.ParameterList}}
                    {
                        // {{originalExprText}}
                        var result = {{specInvocation}};
                        {{assignmentLine}}
                    }
                }
                """;

        var tempUnit = ParseCompilationUnit(newMethodSource);
        var tempClass = tempUnit.DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        var newField = tempClass.Members.OfType<FieldDeclarationSyntax>().First()
            .WithTrailingTrivia(syntaxContext.LineFeed);
        var newMethod = tempClass.Members.OfType<MethodDeclarationSyntax>().Last(); // Last method is the actual method, not constructor
        var newConstructor = hasInstanceMethods ? tempClass.Members.OfType<ConstructorDeclarationSyntax>().FirstOrDefault() : null;

        var fieldAdded = containingClass.Members.OfType<FieldDeclarationSyntax>()
            .Any(f => f.Declaration.Variables.Any(v => v.Identifier.Text == fieldName));

        var newMembers = new List<MemberDeclarationSyntax>();
        if (!fieldAdded) newMembers.Add(newField);
        if (newConstructor is not null) newMembers.Add(newConstructor);

        newMembers.AddRange(containingClass.Members
            .Select(member =>
                member == method
                    ? newMethod
                    : member));

        var newClass = containingClass.WithMembers(List(newMembers));

        return root.ReplaceNode(containingClass, newClass);
    }

    private static SyntaxNode AddUsingStatementsIfNeeded(SyntaxNode newRoot)
    {
        var compilationUnit = (CompilationUnitSyntax)newRoot;
        var usingsToAdd = new List<UsingDirectiveSyntax>();

        if (compilationUnit.Usings.All(u => u.Name?.ToString() != nameof(Motiv)))
            usingsToAdd.Add(UsingDirective(IdentifierName(nameof(Motiv))));

        return usingsToAdd.Count == 0
            ? newRoot
            : compilationUnit.AddUsings(usingsToAdd.ToArray());
    }

    private static SyntaxNode AddToCompilationUnit(
        SyntaxContext syntaxContext,
        SyntaxNode newRoot,
        params MemberDeclarationSyntax[] customSpecClassDeclaration)
    {
        var compilationUnit = (CompilationUnitSyntax)newRoot;
        var membersWithTrivia = customSpecClassDeclaration.Select(member =>
            member.WithLeadingTrivia(syntaxContext.LineFeed, syntaxContext.LineFeed));

        newRoot = compilationUnit.AddMembers(membersWithTrivia.ToArray());
        return newRoot;
    }

    private static SyntaxNode AddToNamespace(
        SyntaxContext syntaxContext,
        SyntaxNode newRoot,
        BaseNamespaceDeclarationSyntax namespaceDeclaration,
        params MemberDeclarationSyntax[] customSpecClassDeclaration)
    {
        var membersWithTrivia = customSpecClassDeclaration.Select(member =>
            member.WithLeadingTrivia(syntaxContext.LineFeed, syntaxContext.LineFeed));

        var updatedNamespace = namespaceDeclaration
            .AddMembers(membersWithTrivia.ToArray());
        newRoot = newRoot.ReplaceNode(namespaceDeclaration, updatedNamespace);
        return newRoot;
    }

    private static IEnumerable<ISymbol> GetVariablesInExpression(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        return expression
            .DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Where(identifier =>
            {
                // Only consider root identifiers (not the right side of member access)
                // For "order.Total", we only want "order", not "Total"
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
