namespace Karlssberg.Motiv;

internal interface ICompositeBooleanResult
{
    /// <summary>Gets the description of the XOR operation.</summary>
    ResultDescriptionBase Description { get; }
    
    IEnumerable<BooleanResultBase> Underlying { get; }
}