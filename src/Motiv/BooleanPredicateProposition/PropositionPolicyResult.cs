using Motiv.Shared;

namespace Motiv.BooleanPredicateProposition;

/// <summary>
///     Represents a proposition that yields custom metadata based on the result of a boolean predicate.
/// </summary>
/// <param name="satisfied">The value of the proposition.</param>
/// <param name="valueFactory">The factory for the policy result value.</param>
/// <param name="metadataTierFactory">The factory for the metadata tier.</param>
/// <param name="explanationFactory">The factory for the explanation of the proposition.</param>
/// <param name="descriptionFactory">The factory for the description of the proposition result.</param>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
internal sealed class PropositionPolicyResult<TMetadata>(
    bool satisfied,
    Func<TMetadata> valueFactory,
    Func<MetadataNode<TMetadata>> metadataTierFactory,
    Func<Explanation> explanationFactory,
    Func<ResultDescriptionBase> descriptionFactory)
    : PolicyResultBase<TMetadata>
{
    private bool _hasValue;
    private TMetadata _value = default!;
    private MetadataNode<TMetadata>? _metadataTier;
    private Explanation? _explanation;
    private ResultDescriptionBase? _description;

    /// <inheritdoc />
    public override TMetadata Value
    {
        get
        {
            if (!_hasValue) { _value = valueFactory(); _hasValue = true; }
            return _value;
        }
    }

    /// <summary>
    ///     Gets the metadata tier of the result.
    /// </summary>
    public override MetadataNode<TMetadata> MetadataTier => _metadataTier ??= metadataTierFactory();

    /// <summary>
    ///     Gets the underlying results of the result.
    /// </summary>
    public override IEnumerable<BooleanResultBase> Underlying => [];

    /// <summary>
    ///     Gets the underlying results that share the same metadata type.
    /// </summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithValues => [];

    /// <summary>
    ///     Gets the causes of the result.
    /// </summary>
    public override IEnumerable<BooleanResultBase> Causes => [];

    /// <summary>
    ///     Gets the results that share the same metadata type that also helped determine the final result.
    /// </summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithValues => [];

    /// <summary>Gets the reasons for the result.</summary>
    public override Explanation Explanation => _explanation ??= explanationFactory();

    /// <summary>Gets a value indicating whether the result is satisfied.</summary>
    public override bool Satisfied { get; } = satisfied;

    /// <summary>Gets the description of the result.</summary>
    public override ResultDescriptionBase Description => _description ??= descriptionFactory();
}
