namespace Motiv.Shared;

/// <summary>Represents a boolean result of changing the metadata type.</summary>
/// <typeparam name="TMetadata">The type of the new metadata.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the original metadata.</typeparam>
internal sealed class PolicyResultWithUnderlying<TMetadata, TUnderlyingMetadata>(
    BooleanResultBase<TUnderlyingMetadata> booleanResult,
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

    public override TMetadata Value
    {
        get
        {
            if (!_hasValue) { _value = valueFactory(); _hasValue = true; }
            return _value;
        }
    }

    public override bool Satisfied { get; } = booleanResult.Satisfied;

    public override ResultDescriptionBase Description => _description ??= descriptionFactory();

    public override Explanation Explanation => _explanation ??= explanationFactory();

    public override MetadataNode<TMetadata> MetadataTier => _metadataTier ??= metadataTierFactory();

    public override IEnumerable<BooleanResultBase> Underlying => booleanResult.ToEnumerable();


    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithValues =>
        booleanResult.ToEnumerable() as IEnumerable<BooleanResultBase<TMetadata>> ?? [];

    public override IEnumerable<BooleanResultBase> Causes => booleanResult.ToEnumerable();


    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithValues =>
        booleanResult.ToEnumerable() as IEnumerable<BooleanResultBase<TMetadata>> ?? [];
}
