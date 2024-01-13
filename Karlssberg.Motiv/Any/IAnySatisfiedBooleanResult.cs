namespace Karlssberg.Motiv.Any;

public interface IAnySatisfiedBooleanResult<TMetadata> : ICompositeBooleanResult<TMetadata>
{
    /// <summary>
    /// Gets the substitute metadata associated with the boolean result.
    /// </summary>
    IEnumerable<TMetadata> SubstituteMetadata { get; }
}