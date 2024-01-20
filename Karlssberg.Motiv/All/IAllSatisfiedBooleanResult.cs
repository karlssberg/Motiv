namespace Karlssberg.Motiv.All;

public interface IAllSatisfiedBooleanResult<TMetadata> : ICompositeBooleanResult<TMetadata>
{
    /// <summary>Gets the substitute metadata associated with the boolean result.</summary>
    IEnumerable<TMetadata> SubstituteMetadata { get; }
}