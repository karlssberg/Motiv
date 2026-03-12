using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Motiv.CodeFix;

/// <summary>
/// Represents the result of decomposing a logical expression into individual clauses.
/// </summary>
public readonly struct ExpressionDecomposition
{
    /// <summary>The individual clauses with their original text, model-transformed expression, and original expression syntax.</summary>
    public IReadOnlyList<(string OriginalText, ExpressionSyntax TransformedExpression, ExpressionSyntax OriginalExpression)> Clauses { get; }

    /// <summary>The Motiv composition expression tree (e.g. Clause1.AndAlso(Clause2)).</summary>
    public ExpressionSyntax CompositionExpression { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionDecomposition"/> struct.
    /// </summary>
    /// <param name="clauses">The individual clauses.</param>
    /// <param name="compositionExpression">The composition expression.</param>
    public ExpressionDecomposition(
        IReadOnlyList<(string OriginalText, ExpressionSyntax TransformedExpression, ExpressionSyntax OriginalExpression)> clauses,
        ExpressionSyntax compositionExpression)
    {
        Clauses = clauses;
        CompositionExpression = compositionExpression;
    }
}
