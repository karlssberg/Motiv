using Motiv.Shared;

namespace Motiv.DecoratorProposition;

/// <summary>
///     Represents the result of a policy-decorator policy evaluation. The metadata resolver is only invoked when the
///     <see cref="Value" /> (or a property derived from it) is read, and all derived state is cached in fields to
///     avoid per-evaluation lazy-wrapper and closure allocations.
/// </summary>
/// <param name="policyResult">The underlying policy result that was decorated.</param>
/// <param name="model">The model that was evaluated.</param>
/// <param name="metadataResolver">Resolves the metadata for the outcome from the model and underlying result.</param>
/// <param name="description">The description of the proposition.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
internal sealed class PolicyDecoratorPolicyResult<TModel, TMetadata, TUnderlyingMetadata>(
    PolicyResultBase<TUnderlyingMetadata> policyResult,
    TModel model,
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>, TMetadata> metadataResolver,
    ISpecDescription description)
    : PolicyResultBase<TMetadata>
{
    private readonly PolicyResultBase<TUnderlyingMetadata>[] _underlyingResults = [policyResult];

    private bool _hasValue;

    /// <inheritdoc />
    public override TMetadata Value
    {
        get
        {
            if (!_hasValue) { field = metadataResolver(model, policyResult); _hasValue = true; }
            return field;
        }
    } = default!;

    private string Assertion => field ??= description.ToReason(Satisfied);

    /// <summary>Gets a value indicating whether the result is satisfied.</summary>
    public override bool Satisfied { get; } = policyResult.Satisfied;

    /// <summary>
    ///     Gets the metadata tier of the result.
    /// </summary>
    public override MetadataNode<TMetadata> MetadataTier =>
        field ??= new MetadataNode<TMetadata>(Value,
            _underlyingResults as IEnumerable<PolicyResultBase<TMetadata>> ?? []);

    /// <summary>
    ///     Gets the underlying results of the result.
    /// </summary>
    public override IEnumerable<BooleanResultBase> Underlying => _underlyingResults;

    /// <summary>
    ///     Gets the underlying results that share the same metadata type.
    /// </summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithValues =>
        _underlyingResults as IEnumerable<BooleanResultBase<TMetadata>> ?? [];

    /// <summary>
    ///     Gets the causes of the result.
    /// </summary>
    public override IEnumerable<BooleanResultBase> Causes => _underlyingResults;

    /// <summary>
    ///     Gets the results that share the same metadata type that also helped determine the final result.
    /// </summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithValues =>
        _underlyingResults as IEnumerable<BooleanResultBase<TMetadata>> ?? [];

    /// <summary>Gets the reasons for the result.</summary>
    public override Explanation Explanation =>
        field ??= new Explanation(Assertion, _underlyingResults, _underlyingResults);

    /// <summary>Gets the description of the result.</summary>
    public override ResultDescriptionBase Description =>
        field ??= new BooleanResultDescriptionWithUnderlying(policyResult, Assertion, description.Statement);
}
