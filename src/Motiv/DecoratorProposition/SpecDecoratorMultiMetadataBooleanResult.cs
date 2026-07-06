using Motiv.Shared;

namespace Motiv.DecoratorProposition;

/// <summary>
///     Represents the result of a spec-decorator multi-metadata evaluation. The metadata resolver is only invoked
///     when the <see cref="MetadataTier" /> is read, and all derived state is cached in fields to avoid
///     per-evaluation lazy-wrapper and closure allocations.
/// </summary>
/// <param name="booleanResult">The underlying result that was decorated.</param>
/// <param name="model">The model that was evaluated.</param>
/// <param name="metadataResolver">Resolves the metadata collection for the outcome from the model and underlying result.</param>
/// <param name="description">The description of the proposition.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
internal sealed class SpecDecoratorMultiMetadataBooleanResult<TModel, TMetadata, TUnderlyingMetadata>(
    BooleanResultBase<TUnderlyingMetadata> booleanResult,
    TModel model,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> metadataResolver,
    ISpecDescription description)
    : BooleanResultBase<TMetadata>
{
    private readonly BooleanResultBase<TUnderlyingMetadata>[] _underlyingResults = [booleanResult];

    private string Assertion => field ??= description.ToReason(Satisfied);

    /// <summary>Gets a value indicating whether the result is satisfied.</summary>
    public override bool Satisfied { get; } = booleanResult.Satisfied;

    /// <summary>
    ///     Gets the metadata tier of the result.
    /// </summary>
    public override MetadataNode<TMetadata> MetadataTier =>
        field ??= new MetadataNode<TMetadata>(metadataResolver(model, booleanResult).ToArray(),
            _underlyingResults as IEnumerable<BooleanResultBase<TMetadata>> ?? []);

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
        field ??= new Explanation([Assertion], _underlyingResults, _underlyingResults);

    /// <summary>Gets the description of the result.</summary>
    public override ResultDescriptionBase Description =>
        field ??= new BooleanResultDescriptionWithUnderlying(booleanResult, Assertion, description.Statement);
}
