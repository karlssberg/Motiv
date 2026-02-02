namespace Motiv.CodeFix;

/// <summary>
/// Represents the result of decomposing a logical expression into individual clauses.
/// </summary>
public readonly struct ExpressionDecomposition
{
    /// <summary>The individual clauses with their original and model-transformed text.</summary>
    public IReadOnlyList<(string OriginalText, string TransformedText)> Clauses { get; }

    /// <summary>The Motiv composition expression (e.g. "Clause1.AndAlso(Clause2)").</summary>
    public string CompositionExpression { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionDecomposition"/> struct.
    /// </summary>
    /// <param name="clauses">The individual clauses.</param>
    /// <param name="compositionExpression">The composition expression.</param>
    public ExpressionDecomposition(
        IReadOnlyList<(string OriginalText, string TransformedText)> clauses,
        string compositionExpression)
    {
        Clauses = clauses;
        CompositionExpression = compositionExpression;
    }
}
