namespace Karlssberg.Motiv;

internal interface IBinaryOperationBooleanResult
{
    /// <summary>Gets the description of the XOR operation.</summary>
    ResultDescriptionBase Description { get; }
    
    IEnumerable<BooleanResultBase> Underlying { get; }
}