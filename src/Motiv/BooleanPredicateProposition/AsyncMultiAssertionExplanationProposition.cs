namespace Motiv.BooleanPredicateProposition;

/// <summary>
/// Represents an unnamed asynchronous multi-assertion explanation proposition whose statement derives from the
/// WhenTrue assertion. The because-strings double as the assertions; degenerate (null/empty/whitespace) strings
/// fall back to the statement-derived reason.
/// </summary>
/// <param name="predicate">The async predicate that determines the boolean result.</param>
/// <param name="whenTrue">The assertions to yield when the predicate is true.</param>
/// <param name="whenFalse">The assertions to yield when the predicate is false.</param>
/// <param name="specDescription">The description of the proposition.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
internal sealed class AsyncMultiAssertionExplanationProposition<TModel>(
    Func<TModel, CancellationToken, Task<bool>> predicate,
    Func<TModel, IEnumerable<string>> whenTrue,
    Func<TModel, IEnumerable<string>> whenFalse,
    ISpecDescription specDescription)
    : AsyncSpecBase<TModel, string>
{
    public override IEnumerable<SpecBase> Underlying => [];

    /// <summary>Gets or sets the description of the proposition.</summary>
    public override ISpecDescription Description => specDescription;

    public override async Task<bool> MatchesAsync(TModel model, CancellationToken cancellationToken = default) =>
        await predicate(model, cancellationToken).ConfigureAwait(false);

    /// <summary>Determines if the specified model satisfies the proposition.</summary>
    /// <param name="model">The model to be evaluated.</param>
    /// <param name="cancellationToken">A token that can cancel the evaluation.</param>
    /// <returns>A BooleanResultBase object containing the result of the evaluation.</returns>
    protected override async Task<BooleanResultBase<string>> EvaluateSpecAsync(
        TModel model,
        CancellationToken cancellationToken)
    {
        var isSatisfied = await predicate(model, cancellationToken).ConfigureAwait(false);

        var metadataResolver =
            isSatisfied switch
            {
                true => whenTrue,
                false => whenFalse
            };

        return new MultiAssertionExplanationPropositionBooleanResult<TModel>(
            isSatisfied,
            model,
            metadataResolver,
            specDescription);
    }
}
