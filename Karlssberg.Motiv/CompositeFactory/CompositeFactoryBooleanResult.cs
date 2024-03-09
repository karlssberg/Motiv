using Karlssberg.Motiv.ChangeMetadataType;

namespace Karlssberg.Motiv.CompositeFactory;

/// <summary>Represents a boolean result of changing the metadata type.</summary>
/// <typeparam name="TMetadata">The type of the new metadata.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the original metadata.</typeparam>
internal sealed class CompositeFactoryBooleanResult<TMetadata, TUnderlyingMetadata>(
    BooleanResultBase<TUnderlyingMetadata> booleanResult,
    MetadataTree<TMetadata> metadataTree,
    IProposition proposition)
    : BooleanResultBase<TMetadata>
{
    /// <summary>Gets a value indicating whether the boolean result is satisfied.</summary>
    public override bool Satisfied => booleanResult.Satisfied;

    /// <summary>Gets the description of the boolean result.</summary>
    public override ResultDescriptionBase Description =>
        metadataTree.Count switch
        {
            1 => new ChangeMetadataBooleanResultDescription<TMetadata, TUnderlyingMetadata>(
                booleanResult,
                metadataTree.Single(),
                proposition),
            _ => new MultiMetadataCompositeFactoryBooleanResultDescription<TUnderlyingMetadata>(
                booleanResult,
                proposition)
        };

    /// <summary>Gets the reasons for the boolean result.</summary>
    public override Explanation Explanation =>
        new(Description)
        {
            Underlying = booleanResult.Explanation.ToEnumerable()
        };
    
    public override MetadataTree<TMetadata> MetadataTree => metadataTree;
    public override IEnumerable<BooleanResultBase> Underlying => booleanResult.ToEnumerable();
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata =>
        booleanResult switch
        {
            BooleanResultBase<TMetadata> result=> result.ToEnumerable(),
            _ => Enumerable.Empty<BooleanResultBase<TMetadata>>()
        };
    public override IEnumerable<BooleanResultBase> Causes => booleanResult.Causes.ToEnumerable();
    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata =>
        booleanResult.Causes switch
        {
            IEnumerable<BooleanResultBase<TMetadata>> results => results,
            _ => Enumerable.Empty<BooleanResultBase<TMetadata>>()
        };
}