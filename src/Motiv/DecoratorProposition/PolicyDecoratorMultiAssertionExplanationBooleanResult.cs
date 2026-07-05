using Motiv.Shared;

namespace Motiv.DecoratorProposition;

/// <summary>
///     Represents the result of a policy-decorator multi-assertion explanation evaluation. The assertions resolver
///     is only invoked when the <see cref="MetadataTier" /> (or a property derived from it) is read, and all derived
///     state is cached in fields to avoid per-evaluation lazy-wrapper and closure allocations. Degenerate
///     (null/empty/whitespace) assertions fall back to the statement-derived reason.
/// </summary>
/// <param name="policyResult">The underlying policy result that was decorated.</param>
/// <param name="model">The model that was evaluated.</param>
/// <param name="assertionsResolver">Resolves the assertions for the outcome from the model and underlying result.</param>
/// <param name="description">The description of the proposition.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
internal sealed class PolicyDecoratorMultiAssertionExplanationBooleanResult<TModel, TUnderlyingMetadata>(
    PolicyResultBase<TUnderlyingMetadata> policyResult,
    TModel model,
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>, IEnumerable<string>> assertionsResolver,
    ISpecDescription description)
    : BooleanResultBase<string>
{
    private readonly PolicyResultBase<TUnderlyingMetadata>[] _underlyingResults = [policyResult];

    private bool _hasMetadata;

    private IEnumerable<string> Metadata
    {
        get
        {
            if (!_hasMetadata) { field = assertionsResolver(model, policyResult); _hasMetadata = true; }
            return field;
        }
    } = default!;

    private string Assertion => field ??= description.ToReason(Satisfied);

    /// <summary>Gets a value indicating whether the result is satisfied.</summary>
    public override bool Satisfied { get; } = policyResult.Satisfied;

    /// <summary>
    ///     Gets the metadata tier of the result.
    /// </summary>
    public override MetadataNode<string> MetadataTier =>
        field ??= new MetadataNode<string>(Metadata,
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
        field ??= new Explanation(
            Metadata.ElseFallback(() => Assertion).ToArray(),
            _underlyingResults,
            _underlyingResults);

    /// <summary>Gets the description of the result.</summary>
    public override ResultDescriptionBase Description =>
        field ??= new BooleanResultDescriptionWithUnderlying(policyResult, Assertion, description.Statement);
}
