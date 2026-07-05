using Motiv.Shared;

namespace Motiv.DecoratorProposition;

/// <summary>
///     Represents the result of a minimal spec decorator evaluation. The underlying result supplies the values and
///     explanation directly, and all derived state is cached in fields to avoid per-evaluation closure allocations.
/// </summary>
/// <param name="predicateResult">The underlying boolean result being decorated.</param>
/// <param name="specDescription">The description of the proposition.</param>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
internal sealed class MinimalSpecDecoratorBooleanResult<TMetadata>(
    BooleanResultBase<TMetadata> predicateResult,
    ISpecDescription specDescription)
    : BooleanResultBase<TMetadata>
{
    private BooleanResultBase<TMetadata>[] UnderlyingResults => field ??= [predicateResult];

    /// <summary>
    ///     Gets the metadata tier of the result.
    /// </summary>
    public override MetadataNode<TMetadata> MetadataTier =>
        field ??= new MetadataNode<TMetadata>(predicateResult.Values, UnderlyingResults);

    /// <summary>
    ///     Gets the underlying results of the result.
    /// </summary>
    public override IEnumerable<BooleanResultBase> Underlying => UnderlyingResults;

    /// <summary>
    ///     Gets the underlying results that share the same metadata type.
    /// </summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithValues => UnderlyingResults;

    /// <summary>
    ///     Gets the causes of the result.
    /// </summary>
    public override IEnumerable<BooleanResultBase> Causes => UnderlyingResults;

    /// <summary>
    ///     Gets the results that share the same metadata type that also helped determine the final result.
    /// </summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithValues => UnderlyingResults;

    /// <summary>Gets the reasons for the result.</summary>
    public override Explanation Explanation => predicateResult.Explanation;

    /// <summary>Gets a value indicating whether the result is satisfied.</summary>
    public override bool Satisfied => predicateResult.Satisfied;

    /// <summary>Gets the description of the result.</summary>
    public override ResultDescriptionBase Description =>
        field ??= new BooleanResultDescriptionWithUnderlying(
            predicateResult,
            specDescription.ToReason(Satisfied),
            specDescription.Statement);
}
