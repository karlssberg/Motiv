using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Motiv.CodeFix;

/// <summary>
/// Represents the result of decomposing a logical expression into individual clauses.
/// </summary>
public readonly struct ExpressionDecomposition
{
    /// <summary>The individual clauses with their original text, model-transformed text, and expression syntax.</summary>
    public IReadOnlyList<(string OriginalText, string TransformedText, ExpressionSyntax Expression)> Clauses { get; }

    /// <summary>The Motiv composition expression (e.g. "Clause1.AndAlso(Clause2)").</summary>
    public string CompositionExpression { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionDecomposition"/> struct.
    /// </summary>
    /// <param name="clauses">The individual clauses.</param>
    /// <param name="compositionExpression">The composition expression.</param>
    public ExpressionDecomposition(
        IReadOnlyList<(string OriginalText, string TransformedText, ExpressionSyntax Expression)> clauses,
        string compositionExpression)
    {
        Clauses = clauses;
        CompositionExpression = compositionExpression;
    }
}
