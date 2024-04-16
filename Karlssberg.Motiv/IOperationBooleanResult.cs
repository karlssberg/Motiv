namespace Karlssberg.Motiv;

internal interface IOperationBooleanResult
{
    /// <summary>Gets the description of the XOR operation.</summary>
    ResultDescriptionBase Description { get; }
    
    IEnumerable<BooleanResultBase> Underlying { get; }
    
    IEnumerable<BooleanResultBase> Causes { get; }
    
    IEnumerable<string> Assertions { get; }
}