using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.CodeFix.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.CodeFix;

public class LogicalExpressionToSpecConverter(
    string propositionName,
    string defaultModelName,
    CodeFixContext context)
{

    public async Task<Document> Convert(
        Diagnostic diagnostic,
        ExpressionSyntax logicalExpressionSyntax,
        CancellationToken cancellationToken = default)
    {
        var root = await context.Document
            .GetSyntaxRootAsync(cancellationToken)
            .ConfigureAwait(false);

        if (root is null) return context.Document;

        var semanticModel = await context.Document
                                .GetSemanticModelAsync(cancellationToken)
                                .ConfigureAwait(false)
                            ?? throw new InvalidOperationException("Could not get semantic model for document");

        var variableSymbols = GetVariablesInExpression(logicalExpressionSyntax, semanticModel).ToImmutableArray();
        var newRoot = ReplaceLogicalExpressionWithSpecInvocation(variableSymbols, logicalExpressionSyntax, root);

        var rootMembers = GetRootMembers(variableSymbols, logicalExpressionSyntax).ToArray();
        newRoot = logicalExpressionSyntax.FirstAncestorOrSelf<NamespaceDeclarationSyntax>() is {} namespaceDeclaration
                    ? AddToNamespace(newRoot, namespaceDeclaration, rootMembers)
                    : AddToCompilationUnit(newRoot, rootMembers);
        newRoot = AddMotivUsingStatementsIfNeeded(newRoot);
        newRoot = AddMotivUsingStatementsIfNeeded(newRoot);

        return context.Document.WithSyntaxRoot(newRoot);
    }

    private IEnumerable<MemberDeclarationSyntax> GetRootMembers(
        ImmutableArray<ISymbol> variableSymbols,
        ExpressionSyntax logicalExpressionSyntax)
    {
        yield return CustomSpecDeclarationSyntax.Create(
            IdentifierName(propositionName),
            IdentifierName(GetModelVariableName(variableSymbols)),
            logicalExpressionSyntax);

        if (variableSymbols.Length == 1) yield break;

        yield return PropositionModelSyntax.Create(defaultModelName, variableSymbols);
    }

    private string GetModelVariableName(ImmutableArray<ISymbol> variableSymbols)
    {
        return variableSymbols.Length == 1
            ? variableSymbols.First().Name.ToCamelCase()
            : defaultModelName;
    }

    private SyntaxNode ReplaceLogicalExpressionWithSpecInvocation(
        ImmutableArray<ISymbol> variableSymbols,
        ExpressionSyntax logicalExpressionSyntax,
        SyntaxNode root)
    {
        var createSpecInvocation = SpecInvocationExpressionSyntax.Create(
            IdentifierName(propositionName),
            ObjectCreationExpression(IdentifierName(GetModelVariableName(variableSymbols)))
                .WithArgumentList(ArgumentList([..variableSymbols.Select(CreateArgument)])));

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

        var usingDirective = UsingDirective(IdentifierName(nameof(Motiv)));
        return compilationUnitWithUsings.AddUsings(usingDirective);
    }

    private static SyntaxNode AddToCompilationUnit(
        SyntaxNode newRoot,
        params MemberDeclarationSyntax[] customSpecClassDeclaration)
    {
        var compilationUnit = (CompilationUnitSyntax)newRoot;
        newRoot = compilationUnit
            .WithTrailingTrivia(LineFeed, LineFeed)
            .AddMembers(customSpecClassDeclaration);
        return newRoot;
    }

    private static SyntaxNode AddToNamespace(
        SyntaxNode newRoot,
        BaseNamespaceDeclarationSyntax namespaceDeclaration,
        params MemberDeclarationSyntax[] customSpecClassDeclaration)
    {
        var updatedNamespace = namespaceDeclaration
            .WithTrailingTrivia(LineFeed, LineFeed)
            .AddMembers(customSpecClassDeclaration);
        newRoot = newRoot.ReplaceNode(namespaceDeclaration, updatedNamespace);
        return newRoot;
    }

    private IEnumerable<ISymbol> GetVariablesInExpression(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        return expression
            .DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Select(identifier => semanticModel.GetSymbolInfo(identifier).Symbol)
            .Where(symbol => symbol is IFieldSymbol or IPropertySymbol or ILocalSymbol or IParameterSymbol)
            .Distinct(SymbolEqualityComparer.Default)
            .Cast<ISymbol>();
    }
}
