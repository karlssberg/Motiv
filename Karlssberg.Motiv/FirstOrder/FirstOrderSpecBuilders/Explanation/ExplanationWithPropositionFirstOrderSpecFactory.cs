namespace Karlssberg.Motiv.FirstOrder.FirstOrderSpecBuilders.Explanation;

public readonly ref struct ExplanationWithPropositionFirstOrderSpecFactory<TModel>(
    Func<TModel, bool> predicate,
    Func<TModel, string> whenTrue,
    Func<TModel, string> whenFalse)
{
    /// <summary>Provide a human readable explanation for when the condition is false.</summary>
    /// <param name="proposition">The name of the specification. Preferably this would be in predicate form eg "is even number".</param>
    /// <returns>A specification base.</returns>
    public SpecBase<TModel, string> CreateSpec(string proposition) =>
        new MetadataSpec<TModel, string>(
            predicate,
            whenTrue,
            whenFalse, 
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)));
}