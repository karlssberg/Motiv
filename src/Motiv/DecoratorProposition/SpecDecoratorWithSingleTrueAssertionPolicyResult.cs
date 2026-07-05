using Motiv.Shared;

namespace Motiv.DecoratorProposition;

/// <summary>
///     Represents the result of a spec-decorator policy evaluation with a single (constant) true assertion. The
///     false because-resolver is only invoked when the <see cref="Value" /> (or a property derived from it) is read,
///     and all derived state is cached in fields to avoid per-evaluation lazy-wrapper and closure allocations.
///     Degenerate (null/empty/whitespace) because-strings fall back to the statement-derived reason for explanation
///     purposes.
/// </summary>
/// <param name="underlyingResult">The underlying result that was decorated.</param>
/// <param name="model">The model that was evaluated.</param>
/// <param name="trueBecause">The assertion to use when the result is satisfied.</param>
/// <param name="whenFalse">Resolves the assertion for the unsatisfied outcome from the model and underlying result.</param>
/// <param name="description">The description of the proposition.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
internal sealed class SpecDecoratorWithSingleTrueAssertionPolicyResult<TModel, TUnderlyingMetadata>(
    BooleanResultBase<TUnderlyingMetadata> underlyingResult,
    TModel model,
    string trueBecause,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> whenFalse,
    ISpecDescription description)
    : PolicyResultBase<string>
{
    private readonly BooleanResultBase<TUnderlyingMetadata>[] _underlyingResults = [underlyingResult];

    private bool _hasValue;

    /// <inheritdoc />
    public override string Value
    {
        get
        {
            if (!_hasValue)
            {
                field = Satisfied ? trueBecause : whenFalse(model, underlyingResult);
                _hasValue = true;
            }
            return field;
        }
    } = default!;

    private string Assertion => field ??= Value.ElseFallback(() => description.ToReason(Satisfied));

    /// <summary>Gets a value indicating whether the result is satisfied.</summary>
    public override bool Satisfied { get; } = underlyingResult.Satisfied;

    /// <summary>
    ///     Gets the metadata tier of the result.
    /// </summary>
    public override MetadataNode<string> MetadataTier =>
        field ??= new MetadataNode<string>(Value,
            _underlyingResults as IEnumerable<BooleanResultBase<string>> ?? []);

    /// <summary>
    ///     Gets the underlying results of the result.
    /// </summary>
    public override IEnumerable<BooleanResultBase> Underlying => _underlyingResults;

    /// <summary>
    ///     Gets the underlying results that share the same metadata type.
    /// </summary>
    public override IEnumerable<BooleanResultBase<string>> UnderlyingWithValues =>
        _underlyingResults as IEnumerable<BooleanResultBase<string>> ?? [];

    /// <summary>
    ///     Gets the causes of the result.
    /// </summary>
    public override IEnumerable<BooleanResultBase> Causes => _underlyingResults;

    /// <summary>
    ///     Gets the results that share the same metadata type that also helped determine the final result.
    /// </summary>
    public override IEnumerable<BooleanResultBase<string>> CausesWithValues =>
        _underlyingResults as IEnumerable<BooleanResultBase<string>> ?? [];

    /// <summary>Gets the reasons for the result.</summary>
    public override Explanation Explanation =>
        field ??= new Explanation(Assertion, _underlyingResults, _underlyingResults);

    /// <summary>Gets the description of the result.</summary>
    public override ResultDescriptionBase Description =>
        field ??= new BooleanResultDescriptionWithUnderlying(underlyingResult, Assertion, description.Statement);
}
