using Motiv.Shared;

namespace Motiv.MetadataToExplanationAdapter;

/// <summary>
///     Represents the result of adapting a metadata proposition to an explanation proposition. The underlying
///     result's assertions become the metadata, and all derived state is cached in fields to avoid per-evaluation
///     closure allocations.
/// </summary>
/// <param name="result">The underlying boolean result being adapted.</param>
/// <param name="specDescription">The description of the proposition.</param>
/// <typeparam name="TUnderlyingMetadata">The type of the original metadata.</typeparam>
internal sealed class MetadataToExplanationAdapterBooleanResult<TUnderlyingMetadata>(
    BooleanResultBase<TUnderlyingMetadata> result,
    ISpecDescription specDescription)
    : BooleanResultBase<string>
{
    private BooleanResultBase<TUnderlyingMetadata>[] UnderlyingResults => field ??= [result];

    /// <summary>
    ///     Gets the metadata tier of the result.
    /// </summary>
    public override MetadataNode<string> MetadataTier =>
        field ??= new MetadataNode<string>(
            result.Assertions,
            UnderlyingResults as IEnumerable<BooleanResultBase<string>> ?? []);

    /// <summary>
    ///     Gets the underlying results of the result.
    /// </summary>
    public override IEnumerable<BooleanResultBase> Underlying => UnderlyingResults;

    /// <summary>
    ///     Gets the underlying results that share the same metadata type.
    /// </summary>
    public override IEnumerable<BooleanResultBase<string>> UnderlyingWithValues =>
        UnderlyingResults as IEnumerable<BooleanResultBase<string>> ?? [];

    /// <summary>
    ///     Gets the causes of the result.
    /// </summary>
    public override IEnumerable<BooleanResultBase> Causes => UnderlyingResults;

    /// <summary>
    ///     Gets the results that share the same metadata type that also helped determine the final result.
    /// </summary>
    public override IEnumerable<BooleanResultBase<string>> CausesWithValues =>
        UnderlyingResults as IEnumerable<BooleanResultBase<string>> ?? [];

    /// <summary>Gets the reasons for the result.</summary>
    public override Explanation Explanation => result.Explanation;

    /// <summary>Gets a value indicating whether the result is satisfied.</summary>
    public override bool Satisfied => result.Satisfied;

    /// <summary>Gets the description of the result.</summary>
    public override ResultDescriptionBase Description =>
        field ??= new BooleanResultDescriptionWithUnderlying(
            result,
            specDescription.ToReason(Satisfied),
            specDescription.Statement);
}
