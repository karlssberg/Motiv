namespace Karlssberg.Motiv;

internal interface IBinaryBooleanOperationResult : IBooleanOperationResult
{
    BooleanResultBase Left { get; }
    
    BooleanResultBase? Right { get; }
}

internal interface IBinaryBooleanOperationResult<TMetadata> : IBinaryBooleanOperationResult, IBooleanOperationResult<TMetadata>
{
    new BooleanResultBase<TMetadata> Left { get; }

    new BooleanResultBase<TMetadata>? Right { get; }
}