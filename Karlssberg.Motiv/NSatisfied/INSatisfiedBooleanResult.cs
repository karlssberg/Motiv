namespace Karlssberg.Motiv.NSatisfied;

public interface INSatisfiedBooleanResult<TMetadata> : ILogicalOperatorResult<TMetadata>
{
    /// <summary>Gets the substitute metadata associated with the boolean result.</summary>
    IEnumerable<TMetadata> SubstituteMetadata { get; }
}