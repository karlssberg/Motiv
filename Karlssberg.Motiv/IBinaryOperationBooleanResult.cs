namespace Karlssberg.Motiv;

internal interface IBinaryOperationBooleanResult : IOperationBooleanResult
{
    BooleanResultBase Left { get; }
    
    BooleanResultBase? Right { get; }
}