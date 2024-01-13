namespace Karlssberg.Motiv.AtMost;

public interface IAtMostNSatisfiedBooleanResult<TMetadata> : ICompositeBooleanResult<TMetadata>
{
    /// <summary>
    /// Gets the substitute metadata associated with the boolean result.
    /// </summary>
    IEnumerable<TMetadata> SubstituteMetadata { get; }
}