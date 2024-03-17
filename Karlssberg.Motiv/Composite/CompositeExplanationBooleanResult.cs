namespace Karlssberg.Motiv.Composite;

/// <summary>Represents a boolean result of changing the metadata type.</summary>
/// <typeparam name="TMetadata">The type of the new metadata.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the original metadata.</typeparam>
internal sealed class CompositeExplanationBooleanResult<TUnderlyingMetadata>(
    BooleanResultBase<TUnderlyingMetadata> booleanResult,
    MetadataTree<string> metadata,
    string because)
    : BooleanResultBase<string>
{
    /// <summary>Gets a value indicating whether the boolean result is satisfied.</summary>
    public override bool Satisfied => booleanResult.Satisfied;

    /// <summary>Gets the description of the boolean result.</summary>
    public override ResultDescriptionBase Description =>
        new CompositeExplanationBooleanResultDescription<TUnderlyingMetadata>(because, booleanResult);
        

    /// <summary>Gets the reasons for the boolean result.</summary>
    public override Explanation Explanation =>
        new(Description)
        {
            Underlying = booleanResult.Explanation.ToEnumerable()
        };

    public override MetadataTree<string> MetadataTree => metadata;
    public override IEnumerable<BooleanResultBase> Underlying => booleanResult.ToEnumerable();
    public override IEnumerable<BooleanResultBase<string>> UnderlyingWithMetadata =>
        booleanResult.ResolveUnderlyingWithMetadata<string, TUnderlyingMetadata>();
    
    public override IEnumerable<BooleanResultBase> Causes => booleanResult.Causes.ToEnumerable();
    public override IEnumerable<BooleanResultBase<string>> CausesWithMetadata =>
        booleanResult.ResolveCausesWithMetadata<string, TUnderlyingMetadata>();
}