namespace Karlssberg.Motiv;

/// <summary>
/// Represents a boolean result with associated metadata and description.
/// </summary>
/// <typeparam name="TMetadata">The type of the metadata associated with the result.</typeparam>
public class BooleanResult<TMetadata> : BooleanResultBase<TMetadata>
{
    private readonly string _description;

    /// <summary>
    /// Initializes a new instance of the <see cref="BooleanResult{TMetadata}"/> class.
    /// </summary>
    /// <param name="isSatisfied">A value indicating whether the result is satisfied.</param>
    /// <param name="metadata">The metadata associated with the result.</param>
    /// <param name="description">The description of the result.</param>
    public BooleanResult(bool isSatisfied, TMetadata metadata, string description)
    {
        description.ThrowIfNull(nameof(description));

        _description = description;
        Metadata = metadata;
        IsSatisfied = isSatisfied;
    }

    /// <summary>
    /// Gets the metadata associated with the result.
    /// </summary>
    public TMetadata Metadata { get; }

    /// <summary>
    /// Gets a value indicating whether the result is satisfied.
    /// </summary>
    public override bool IsSatisfied { get; }

    /// <summary>
    /// Gets the description of the result.
    /// </summary>
    public override string Description => Metadata switch
    {
        string reason => reason,
        _ => $"{_description}:{(IsSatisfied ? True : False)}"
    };

    /// <summary>
    /// Gets the reasons for the result.
    /// </summary>
    public override IEnumerable<string> GatherReasons()
    {
        yield return Description;
    }
}