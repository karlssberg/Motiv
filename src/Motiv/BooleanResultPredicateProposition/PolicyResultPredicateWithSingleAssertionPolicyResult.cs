using Motiv.Shared;

namespace Motiv.BooleanResultPredicateProposition;

/// <summary>
///     Represents the result of a
///     <see cref="PolicyResultPredicateWithSingleAssertionProposition{TModel,TUnderlyingMetadata}" /> evaluation. The
///     because-resolver is only invoked when the <see cref="Value" /> (or a property derived from it) is read, and all
///     derived state is cached in fields to avoid per-evaluation lazy-wrapper and closure allocations. Degenerate
///     (null/empty/whitespace) because-strings fall back to the statement-derived reason for explanation purposes.
/// </summary>
/// <param name="model">The model that was evaluated.</param>
/// <param name="underlyingResult">The underlying policy result.</param>
/// <param name="becauseResolver">Resolves the because-string for the outcome from the model and underlying result.</param>
/// <param name="specDescription">The description of the proposition.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the policy result.</typeparam>
internal sealed class PolicyResultPredicateWithSingleAssertionPolicyResult<TModel, TUnderlyingMetadata>(
    TModel model,
    PolicyResultBase<TUnderlyingMetadata> underlyingResult,
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>, string> becauseResolver,
    ISpecDescription specDescription)
    : PolicyResultBase<string>
{
    private readonly PolicyResultBase<TUnderlyingMetadata>[] _underlyingResults = [underlyingResult];

    private bool _hasValue;

    /// <inheritdoc />
    public override string Value
    {
        get
        {
            if (!_hasValue) { field = becauseResolver(model, underlyingResult); _hasValue = true; }
            return field;
        }
    } = default!;

    private string Assertion => field ??= Value.ElseFallback(() => specDescription.ToReason(Satisfied));

    /// <summary>Gets the metadata tier of the result.</summary>
    public override MetadataNode<string> MetadataTier =>
        field ??= new MetadataNode<string>(Value.ToEnumerable(),
            _underlyingResults as IEnumerable<PolicyResultBase<string>> ?? []);

    /// <summary>Gets the underlying results of the result.</summary>
    public override IEnumerable<BooleanResultBase> Underlying => _underlyingResults;

    /// <summary>Gets the underlying results that share the same metadata type.</summary>
    public override IEnumerable<BooleanResultBase<string>> UnderlyingWithValues =>
        _underlyingResults as IEnumerable<BooleanResultBase<string>> ?? [];

    /// <summary>Gets the causes of the result.</summary>
    public override IEnumerable<BooleanResultBase> Causes => _underlyingResults;

    /// <summary>Gets the results that share the same metadata type that also helped determine the final result.</summary>
    public override IEnumerable<BooleanResultBase<string>> CausesWithValues =>
        _underlyingResults as IEnumerable<BooleanResultBase<string>> ?? [];

    /// <summary>Gets the reasons for the result.</summary>
    public override Explanation Explanation =>
        field ??= new Explanation(Assertion, _underlyingResults, _underlyingResults);

    /// <summary>Gets a value indicating whether the result is satisfied.</summary>
    public override bool Satisfied { get; } = underlyingResult.Satisfied;

    /// <summary>Gets the description of the result.</summary>
    public override ResultDescriptionBase Description =>
        field ??= new BooleanResultDescriptionWithUnderlying(underlyingResult, Assertion, specDescription.Statement);
}
