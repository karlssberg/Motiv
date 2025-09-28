using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Motiv.CodeFix;

public class VariableExtractorWalker(SemanticModel semanticModel) : CSharpSyntaxWalker
{
    private HashSet<ISymbol> Symbols { get; } = new(SymbolEqualityComparer.Default);

    public IEnumerable<ISymbol> GetVariables(SyntaxNode node)
    {
        Symbols.Clear();
        Visit(node);
        return Symbols.ToList();
    }

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(node);
        if (symbolInfo.Symbol != null)
        {
            switch (symbolInfo.Symbol.Kind)
            {
                case SymbolKind.Local:
                case SymbolKind.Parameter:
                case SymbolKind.Field:
                case SymbolKind.Property:
                    Symbols.Add(symbolInfo.Symbol);
                    break;
            }
        }

        base.VisitIdentifierName(node);
    }
}
