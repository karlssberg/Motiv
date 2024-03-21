namespace Karlssberg.Motiv.Composite;

internal sealed class CompositeMetadataBooleanResult<TMetadata, TUnderlyingMetadata>(
    BooleanResultBase<TUnderlyingMetadata> booleanResult,
    TMetadata metadata,
    IProposition proposition)
    : BooleanResultBase<TMetadata>
{
    /// <summary>Gets a value indicating whether the boolean result is satisfied.</summary>
    public override bool Satisfied => booleanResult.Satisfied;

    /// <summary>Gets the description of the boolean result.</summary>
    public override ResultDescriptionBase Description =>
        new BooleanResultDescriptionWithUnderlying<TUnderlyingMetadata>(
            booleanResult,
            Assertion);

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

    public override MetadataTree<TMetadata> MetadataTree => new(
        metadata, 
        booleanResult.ResolveMetadataSets<TMetadata, TUnderlyingMetadata>());
    
    public override IEnumerable<BooleanResultBase> Underlying => booleanResult.ToEnumerable();
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata =>
        booleanResult.ResolveUnderlyingWithMetadata<TMetadata, TUnderlyingMetadata>();
    
    public override IEnumerable<BooleanResultBase> Causes => booleanResult.Causes;
    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata =>
        booleanResult.ResolveCausesWithMetadata<TMetadata, TUnderlyingMetadata>();
    
    private string Assertion => 
        metadata switch {
            string reason => reason,
            _ => proposition.ToReason(booleanResult.Satisfied)
        };
}