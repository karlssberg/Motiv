namespace Karlssberg.Motiv.FirstOrder;

/// <summary>Represents a boolean result with associated metadata and description.</summary>
/// <typeparam name="TMetadata">The type of the metadata associated with the result.</typeparam>
public sealed class ExplanationBooleanResult(
    bool satisfied,
    string because)
    : BooleanResultBase<string>
{
    public override IEnumerable<BooleanResultBase> Underlying =>
        Enumerable.Empty<BooleanResultBase>();
    
    public override IEnumerable<BooleanResultBase> Causes { get;  } = 
        Enumerable.Empty<BooleanResultBase>();
    /// <summary>Gets the reasons for the result.</summary>
    public override ExplanationTree ExplanationTree => new(Description)
    {
        Underlying = Enumerable.Empty<ExplanationTree>()
    };

    /// <summary>Gets a value indicating whether the result is satisfied.</summary>
    public override bool Satisfied => satisfied;

    /// <summary>Gets the description of the result.</summary>
    public override ResultDescriptionBase Description { get;  } = 
        new ExplanationResultDescription(because);

    public override MetadataTree<string> MetadataTree { get;  } = new (because.ToEnumerable());
    public override IEnumerable<BooleanResultBase<string>> UnderlyingWithMetadata { get; } = 
        Enumerable.Empty<BooleanResultBase<string>>();
    public override IEnumerable<BooleanResultBase<string>> CausesWithMetadata{ get; } = 
        Enumerable.Empty<BooleanResultBase<string>>();
}