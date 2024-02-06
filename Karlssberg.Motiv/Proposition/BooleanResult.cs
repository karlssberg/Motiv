namespace Karlssberg.Motiv.Proposition;

/// <summary>Represents a boolean result with associated metadata and description.</summary>
/// <typeparam name="TMetadata">The type of the metadata associated with the result.</typeparam>
public class BooleanResult<TMetadata> : BooleanResultBase<TMetadata>, IPropositionResult<TMetadata>
{
    private readonly string _description;

    /// <summary>Initializes a new instance of the <see cref="BooleanResult{TMetadata}" /> class.</summary>
    /// <param name="valueisfied">A value indicating whether the result is satisfied.</param>
    /// <param name="metadata">The metadata associated with the result.</param>
    /// <param name="description">The description of the result.</param>
    public BooleanResult(bool value, TMetadata metadata, string description)
    {
        description.ThrowIfNull(nameof(description));

        _description = description;
        Metadata = metadata;
        Value = value;
    }

    /// <summary>Gets the metadata associated with the result.</summary>
    public TMetadata Metadata { get; }

    /// <summary>Gets a value indicating whether the result is satisfied.</summary>
    public override bool Value { get; }

    /// <summary>Gets the description of the result.</summary>
    public override string Description => Metadata switch
    {
        string reason => reason,
        _ => $"{_description} is {(Value ? True : False)}"
    };

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults { get; } = [];
    public override IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperands { get; } = [];

    /// <summary>Gets the reasons for the result.</summary>
    public override IEnumerable<string> GatherReasons()
    {
        yield return Description;
    }
}