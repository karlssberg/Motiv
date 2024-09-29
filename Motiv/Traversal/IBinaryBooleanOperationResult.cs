namespace Motiv.Traversal;

internal interface IBinaryBooleanOperationResult : IBooleanOperationResult
{
    BooleanResultBase Left { get; }

    BooleanResultBase? Right { get; }

    string Operation { get; }

    bool IsCollapsable { get; }
}

internal interface IBinaryBooleanOperationResult<TMetadata> : IBinaryBooleanOperationResult, IBooleanOperationResult<TMetadata>
{
    new BooleanResultBase<TMetadata> Left { get; }

    new BooleanResultBase<TMetadata>? Right { get; }
}
