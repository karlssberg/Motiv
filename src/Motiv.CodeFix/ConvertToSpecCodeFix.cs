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
        var root = await document
            .GetSyntaxRootAsync(cancellationToken)
            .ConfigureAwait(false);

        if (root is null) return document;

        var semanticModel = await document
                                .GetSemanticModelAsync(cancellationToken)
                                .ConfigureAwait(false)
                            ?? throw new InvalidOperationException("Could not get semantic model for document");

        var variableSymbols = GetVariablesInExpression(logicalExpressionSyntax, semanticModel).ToImmutableArray();
        var newRoot = ReplaceLogicalExpressionWithSpecInvocation(variableSymbols, logicalExpressionSyntax, root);

        var rootMembers = GetRootMembers(variableSymbols, logicalExpressionSyntax).ToArray();
        var baseNamespace = newRoot.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        newRoot = baseNamespace is { } namespaceDeclaration
            ? AddToNamespace(newRoot, namespaceDeclaration, rootMembers)
            : AddToCompilationUnit(newRoot, rootMembers);
        newRoot = AddUsingStatementsIfNeeded(newRoot, variableSymbols.Length > 1);

        return document.WithSyntaxRoot(newRoot);
    }

    private IEnumerable<MemberDeclarationSyntax> GetRootMembers(
        ImmutableArray<ISymbol> variableSymbols,
        ExpressionSyntax logicalExpressionSyntax)
    {
        if (variableSymbols.Length == 1)
        {
            yield return CustomSpecDeclarationSyntax.Create(
                IdentifierName(propositionName),
                IdentifierName(GetModelVariableName(variableSymbols)),
                logicalExpressionSyntax,
                GetModelTypeName(variableSymbols));
            yield break;
        }

        var decomposition = DecomposeExpression(logicalExpressionSyntax, variableSymbols);

        var recordParams = string.Join(", ", variableSymbols.Select(s =>
        {
            var typeName = GetSymbolTypeName(s);
            return $"{typeName} {s.Name.Capitalize()}";
        }));

        yield return CustomSpecDeclarationSyntax.CreateComposed(
            propositionName,
            defaultModelName,
            recordParams,
            decomposition.Clauses,
            decomposition.CompositionExpression);
    }

    private static ExpressionDecomposition DecomposeExpression(ExpressionSyntax expression,
        ImmutableArray<ISymbol> variableSymbols)
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
            var clauseName = $"Clause{counter}";
            var transformed = ConvertLogicVariablesToModelMemberAccess(expr, variableSymbols);
            return new ExpressionDecomposition(
                new List<(string, string)> { (expr.ToString().Trim(), transformed.ToString()) },
                clauseName);
        }
    }

    private static ExpressionSyntax ConvertLogicVariablesToModelMemberAccess(
        ExpressionSyntax expression,
        ImmutableArray<ISymbol> variableSymbols)
    {
        var variableNames = variableSymbols.Select(s => s.Name).ToArray();

        return expression.ReplaceNodes(
            expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
                .Where(identifier => variableNames.Contains(identifier.Identifier.ValueText)),
            (original, _) =>
            {
                var propertyName = original.Identifier.ValueText.Capitalize();
                return MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("m"),
                        IdentifierName(propertyName))
                    .WithTriviaFrom(original);
            });
    }

    private string GetModelVariableName(ImmutableArray<ISymbol> variableSymbols)
    {
        return variableSymbols.Length == 1
            ? variableSymbols.First().Name
            : defaultModelName;
    }

    private string GetModelTypeName(ImmutableArray<ISymbol> variableSymbols)
    {
        return variableSymbols.Length == 1
            ? GetSymbolTypeName(variableSymbols.First())
            : defaultModelName;
    }

    private static string GetSymbolTypeName(ISymbol symbol)
    {
        var typeSymbol = symbol switch
        {
            IParameterSymbol parameter => parameter.Type,
            ILocalSymbol local => local.Type,
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            _ => null
        };

        return typeSymbol switch
        {
            null => "object",
            _ => typeSymbol.ToDisplayString()
        };
    }

    private SyntaxNode ReplaceLogicalExpressionWithSpecInvocation(
        ImmutableArray<ISymbol> variableSymbols,
        ExpressionSyntax logicalExpressionSyntax,
        SyntaxNode root)
    {
        if (variableSymbols.Length != 1)
            return ReplaceMultiVariableExpression(variableSymbols, logicalExpressionSyntax, root);

        var createSpecInvocation = SpecInvocationExpressionSyntax.Create(
            IdentifierName(propositionName),
            IdentifierName(variableSymbols.First().Name));
        return root.ReplaceNode(logicalExpressionSyntax, createSpecInvocation);
    }

    private SyntaxNode ReplaceMultiVariableExpression(
        ImmutableArray<ISymbol> variableSymbols,
        ExpressionSyntax logicalExpressionSyntax,
        SyntaxNode root)
    {
        var method = logicalExpressionSyntax.Ancestors().OfType<MethodDeclarationSyntax>().First();
        var containingClass = method.Ancestors().OfType<ClassDeclarationSyntax>().First();
        var statement = logicalExpressionSyntax.Ancestors().OfType<StatementSyntax>().FirstOrDefault();

        var fieldName = $"_{propositionName.ToCamelCase()}";
        var modelArgs = string.Join(", ", variableSymbols.Select(s => s.Name));
        var originalExprText = logicalExpressionSyntax.ToString();
        var specInvocation = $"{fieldName}.IsSatisfiedBy(new {propositionName}.{defaultModelName}({modelArgs}))";

        var assignmentLine = statement switch
        {
            ReturnStatementSyntax => "return result.Satisfied;",
            LocalDeclarationStatementSyntax local =>
                $"var {local.Declaration.Variables.First().Identifier.Text} = result.Satisfied;",
            ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment } =>
                $"{assignment.Left} = result.Satisfied;",
            null when method.ExpressionBody != null && method.ReturnType.ToString() == "bool" => "return result.Satisfied;",
            _ => "var isSatisfied = result.Satisfied;"
        };

        var newMethodSource = $$"""
                                class Temp
                                {
                                    private readonly {{propositionName}} {{fieldName}} = new {{propositionName}}();
                                    public {{method.ReturnType}} {{method.Identifier}}{{method.ParameterList}}
                                    {
                                        // {{originalExprText}}
                                        var result = {{specInvocation}};
                                        Debug.WriteLine(result.Reason);
                                        {{assignmentLine}}
                                    }
                                }
                                """;

        var tempUnit = ParseCompilationUnit(newMethodSource.Replace("\r\n", "\n").Replace("\n", "\r\n"));
        var tempClass = tempUnit.DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        var newField = tempClass.Members.OfType<FieldDeclarationSyntax>().First();
        var newMethod = tempClass.Members.OfType<MethodDeclarationSyntax>().First();

        var fieldAdded = containingClass.Members.OfType<FieldDeclarationSyntax>()
            .Any(f => f.Declaration.Variables.Any(v => v.Identifier.Text == fieldName));

        var newMembers = new List<MemberDeclarationSyntax>();
        if (!fieldAdded) newMembers.Add(newField);

        foreach (var member in containingClass.Members) newMembers.Add(member == method ? newMethod : member);

        var newClass = containingClass.WithMembers(List(newMembers));

        return root.ReplaceNode(containingClass, newClass);
    }

    private MethodDeclarationSyntax CreateMethodWithPropositionLogic(
        MethodDeclarationSyntax method,
        StatementSyntax? statement,
        string originalExprText,
        string specInvocation)
    {
        var assignmentLine = statement switch
        {
            ReturnStatementSyntax => "return result.Satisfied;",
            LocalDeclarationStatementSyntax local =>
                $"var {local.Declaration.Variables.First().Identifier.Text} = result.Satisfied;",
            ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment } =>
                $"{assignment.Left} = result.Satisfied;",
            _ => "var isSatisfied = result.Satisfied;"
        };

        var newCode = $$"""
                        {
                                // {{originalExprText}}
                                var result = {{specInvocation}};
                                Debug.WriteLine(result.Reason);
                                {{assignmentLine}}
                            }
                        """;

        var newBlock = (BlockSyntax)ParseStatement(newCode);

        return method
            .WithExpressionBody(null)
            .WithSemicolonToken(default)
            .WithBody(newBlock);
    }

    private ClassDeclarationSyntax EnsurePropositionField(
        ClassDeclarationSyntax containingClass,
        string fieldName,
        MemberDeclarationSyntax fieldDeclaration)
    {
        var hasField = containingClass.Members
            .OfType<FieldDeclarationSyntax>()
            .Any(f => f.Declaration.Variables.Any(v => v.Identifier.Text == fieldName));

        return hasField
            ? containingClass
            : containingClass.WithMembers(containingClass.Members.Insert(0, fieldDeclaration));
    }

    private static SyntaxNode AddUsingStatementsIfNeeded(SyntaxNode newRoot, bool includeSystemDiagnostics)
    {
        var compilationUnit = (CompilationUnitSyntax)newRoot;
        var usingsToAdd = new List<UsingDirectiveSyntax>();

        if (includeSystemDiagnostics && compilationUnit.Usings.All(u => u.Name?.ToString() != "System.Diagnostics"))
            usingsToAdd.Add(UsingDirective(
                QualifiedName(IdentifierName("System"), IdentifierName("Diagnostics"))));

        if (compilationUnit.Usings.All(u => u.Name?.ToString() != nameof(Motiv)))
            usingsToAdd.Add(UsingDirective(IdentifierName(nameof(Motiv))));

        return usingsToAdd.Count == 0
            ? newRoot
            : compilationUnit.AddUsings(usingsToAdd.ToArray());
    }

    private static SyntaxNode AddToCompilationUnit(
        SyntaxNode newRoot,
        params MemberDeclarationSyntax[] customSpecClassDeclaration)
    {
        var compilationUnit = (CompilationUnitSyntax)newRoot;
        var membersWithTrivia = customSpecClassDeclaration.Select(member =>
            member.WithLeadingTrivia(CarriageReturnLineFeed, CarriageReturnLineFeed));

        newRoot = compilationUnit.AddMembers(membersWithTrivia.ToArray());
        return newRoot;
    }

    private static SyntaxNode AddToNamespace(
        SyntaxNode newRoot,
        BaseNamespaceDeclarationSyntax namespaceDeclaration,
        params MemberDeclarationSyntax[] customSpecClassDeclaration)
    {
        var membersWithTrivia = customSpecClassDeclaration.Select(member =>
            member.WithLeadingTrivia(CarriageReturnLineFeed, CarriageReturnLineFeed));

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
            .Select(identifier => ModelExtensions.GetSymbolInfo(semanticModel, identifier).Symbol)
            .Where(symbol => symbol is IFieldSymbol or IPropertySymbol or ILocalSymbol or IParameterSymbol)
            .Distinct(SymbolEqualityComparer.Default)
            .Cast<ISymbol>();
    }
}
