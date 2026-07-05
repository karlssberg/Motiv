using Motiv.Shared;

namespace Motiv.BooleanResultPredicateProposition;

/// <summary>
///     Represents the result of a <see cref="MinimalBooleanResultPredicateProposition{TModel,TMetadata}" /> evaluation.
///     The metadata resolver is only invoked when the <see cref="MetadataTier" /> (or a property derived from it) is
///     read, and all derived state is cached in fields to avoid per-evaluation lazy-wrapper and closure allocations.
/// </summary>
/// <param name="model">The model that was evaluated.</param>
/// <param name="underlyingResult">The underlying boolean result.</param>
/// <param name="metadataResolver">Resolves the metadata for the outcome from the model and underlying result.</param>
/// <param name="specDescription">The description of the proposition.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata, which is also the type of the underlying metadata.</typeparam>
internal sealed class MinimalBooleanResultPredicateBooleanResult<TModel, TMetadata>(
    TModel model,
    BooleanResultBase<TMetadata> underlyingResult,
    Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<TMetadata>> metadataResolver,
    ISpecDescription specDescription)
    : BooleanResultBase<TMetadata>
{
    private readonly BooleanResultBase<TMetadata>[] _underlyingResults = [underlyingResult];

    private TMetadata[] Metadata => field ??= metadataResolver(model, underlyingResult).ToArray();

    private string[] ResolvedAssertions =>
        field ??= Metadata switch
        {
            IEnumerable<string> assertions => assertions.ToArray(),
            _ => [specDescription.ToReason(Satisfied)]
        };

    /// <summary>Gets the metadata tier of the result.</summary>
    public override MetadataNode<TMetadata> MetadataTier =>
        field ??= new MetadataNode<TMetadata>(Metadata,
            _underlyingResults as IEnumerable<BooleanResultBase<TMetadata>> ?? []);

    /// <summary>Gets the underlying results of the result.</summary>
    public override IEnumerable<BooleanResultBase> Underlying => _underlyingResults;

    /// <summary>Gets the underlying results that share the same metadata type.</summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithValues =>
        _underlyingResults as IEnumerable<BooleanResultBase<TMetadata>> ?? [];

    /// <summary>Gets the causes of the result.</summary>
    public override IEnumerable<BooleanResultBase> Causes => _underlyingResults;

    /// <summary>Gets the results that share the same metadata type that also helped determine the final result.</summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithValues =>
        _underlyingResults as IEnumerable<BooleanResultBase<TMetadata>> ?? [];

    /// <summary>Gets the reasons for the result.</summary>
    public override Explanation Explanation =>
        field ??= new Explanation(ResolvedAssertions, _underlyingResults, _underlyingResults);

    /// <summary>Gets a value indicating whether the result is satisfied.</summary>
    public override bool Satisfied { get; } = underlyingResult.Satisfied;

    /// <summary>Gets the description of the result.</summary>
    public override ResultDescriptionBase Description =>
        field ??= new BooleanResultDescriptionWithUnderlying(
            underlyingResult, specDescription.ToReason(Satisfied), specDescription.Statement);
}
