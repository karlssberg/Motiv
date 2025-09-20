using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.CodeFix.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.CodeFix;

public class LogicalExpressionToSpecConverter(
    string propositionName,
    string defaultModelName,
    Document document)
{

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
        newRoot = AddMotivUsingStatementsIfNeeded(newRoot);

        return document.WithSyntaxRoot(newRoot);
    }

    private IEnumerable<MemberDeclarationSyntax> GetRootMembers(
        ImmutableArray<ISymbol> variableSymbols,
        ExpressionSyntax logicalExpressionSyntax)
    {
        var isSingleVariable = variableSymbols.Length == 1;
        var predicateExpression = GetPredicateExpressionSyntax();

        if (isSingleVariable)
        {
            yield return CustomSpecDeclarationSyntax.Create(
                IdentifierName(propositionName),
                IdentifierName(GetModelVariableName(variableSymbols)),
                predicateExpression,
                GetModelTypeName(variableSymbols));
        }
        else
        {
            yield return CustomSpecDeclarationSyntax.Create(
                IdentifierName(propositionName),
                IdentifierName(GetModelVariableName(variableSymbols)),
                predicateExpression,
                logicalExpressionSyntax,
                GetModelTypeName(variableSymbols));
        }

        if (isSingleVariable) yield break;

        yield return PropositionModelSyntax.Create(defaultModelName, variableSymbols);

        yield break;

        ExpressionSyntax GetPredicateExpressionSyntax()
        {
            return isSingleVariable
                ? logicalExpressionSyntax
                : ConvertLogicVariablesToModelMemberAccess(logicalExpressionSyntax, variableSymbols);
        }
    }

    private ExpressionSyntax ConvertLogicVariablesToModelMemberAccess(ExpressionSyntax expression, ImmutableArray<ISymbol> variableSymbols)
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
                        IdentifierName(defaultModelName.ToLower()),
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
        ExpressionSyntax createSpecInvocation;

        if (variableSymbols.Length == 1)
        {
            // For single variables, pass the variable directly
            createSpecInvocation = SpecInvocationExpressionSyntax.Create(
                IdentifierName(propositionName),
                IdentifierName(variableSymbols.First().Name));
        }
        else
        {
            // For multiple variables, create a model object
            createSpecInvocation = SpecInvocationExpressionSyntax.Create(
                IdentifierName(propositionName),
                ObjectCreationExpression(ParseTypeName(GetModelTypeName(variableSymbols)))
                    .WithNewKeyword(Token(SyntaxKind.NewKeyword).WithTrailingTrivia(Space))
                    .WithArgumentList(ArgumentList([..variableSymbols.Select(CreateArgument)])));
        }

        return root.ReplaceNode(logicalExpressionSyntax, createSpecInvocation);

        ArgumentSyntax CreateArgument(ISymbol symbol)
        {
            return Argument(IdentifierName(symbol.Name));
        }
    }

    private static SyntaxNode AddMotivUsingStatementsIfNeeded(SyntaxNode newRoot)
    {
        // Add using directive if not already present
        var compilationUnitWithUsings = (CompilationUnitSyntax)newRoot;
        var hasMotivUsing = compilationUnitWithUsings
            .Usings.Any(u => u.Name?.ToString() == nameof(Motiv));

        if (hasMotivUsing) return newRoot;

        var usingDirective = UsingDirective(IdentifierName(nameof(Motiv))).NormalizeWhitespace();
        return compilationUnitWithUsings.AddUsings(usingDirective.WithTrailingTrivia(CarriageReturnLineFeed, CarriageReturnLineFeed));
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
