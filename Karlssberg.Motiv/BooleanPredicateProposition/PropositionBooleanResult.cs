namespace Karlssberg.Motiv.BooleanPredicateProposition;

public sealed class PropositionBooleanResult<TMetadata>(
    bool value,
    MetadataNode<TMetadata> metadataTier,
    Explanation explanation,
    string reason)
    : BooleanResultBase<TMetadata>, IPropositionResult
{
    public override MetadataNode<TMetadata> MetadataTier => metadataTier;

    public override IEnumerable<BooleanResultBase> Underlying =>
        Enumerable.Empty<BooleanResultBase>();

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata => 
        Enumerable.Empty<BooleanResultBase<TMetadata>>();

    public override IEnumerable<BooleanResultBase> Causes =>
        Enumerable.Empty<BooleanResultBase>();

    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata =>
        Enumerable.Empty<BooleanResultBase<TMetadata>>();

    /// <summary>Gets the reasons for the result.</summary>
    public override Explanation Explanation => explanation;

    /// <summary>Gets a value indicating whether the result is satisfied.</summary>
    public override bool Satisfied { get; } = value;

    /// <summary>Gets the description of the result.</summary>
    public override ResultDescriptionBase Description => new PropositionResultDescription(reason);
}

public interface IPropositionResult
{
}