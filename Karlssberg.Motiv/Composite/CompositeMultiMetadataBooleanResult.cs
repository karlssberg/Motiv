namespace Karlssberg.Motiv.Composite;

/// <summary>Represents a boolean result of changing the metadata type.</summary>
/// <typeparam name="TMetadata">The type of the new metadata.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the original metadata.</typeparam>
internal sealed class CompositeMultiMetadataBooleanResult<TMetadata, TUnderlyingMetadata>(
    BooleanResultBase<TUnderlyingMetadata> booleanResult,
    MetadataTree<TMetadata> metadata,
    IProposition proposition)
    : BooleanResultBase<TMetadata>
{
    /// <summary>Gets a value indicating whether the boolean result is satisfied.</summary>
    public override bool Satisfied => booleanResult.Satisfied;

    /// <summary>Gets the description of the boolean result.</summary>
    public override ResultDescriptionBase Description =>
        new CompositeMetadataBooleanResultDescription<TMetadata, TUnderlyingMetadata>(
            booleanResult,
            proposition.ToReason(booleanResult.Satisfied),
            proposition);


    /// <summary>Gets the reasons for the boolean result.</summary>
    public override Explanation Explanation =>
        metadata switch
        {
            MetadataTree<string> metadataTree =>
                new Explanation(metadataTree)
                {
                    Underlying = booleanResult.Explanation.ToEnumerable()
                },
            _ =>
                new(Description)
                {
                    Underlying = booleanResult.Explanation.ToEnumerable()
                }
        };

    public override MetadataTree<TMetadata> MetadataTree => metadata;
    public override IEnumerable<BooleanResultBase> Underlying => booleanResult.ToEnumerable();
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata =>
        booleanResult.ResolveUnderlyingWithMetadata<TMetadata, TUnderlyingMetadata>();
    
    public override IEnumerable<BooleanResultBase> Causes => booleanResult.Causes.ToEnumerable();
    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata =>
        booleanResult.ResolveCausesWithMetadata<TMetadata, TUnderlyingMetadata>();
}