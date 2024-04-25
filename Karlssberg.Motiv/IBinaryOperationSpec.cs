namespace Karlssberg.Motiv;

internal interface IBinaryOperationSpec
{
    ISpecDescription Description { get; }
    
    string Operation { get; }
    
    bool IsCollapsable { get; }
}

internal interface IBinaryOperationSpec<TModel, TMetadata> : IBinaryOperationSpec
{
    SpecBase<TModel, TMetadata> Left { get; }
    SpecBase<TModel, TMetadata> Right { get; }
}