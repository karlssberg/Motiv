namespace Karlssberg.Motiv.AtLeast;

public interface IAtLeastNSatisfiedBooleanResult<TMetadata> : ICompositeBooleanResult<TMetadata>
{
    /// <summary>
    /// Gets the substitute metadata associated with the boolean result.
    /// </summary>
    IEnumerable<TMetadata> SubstituteMetadata { get; }
}