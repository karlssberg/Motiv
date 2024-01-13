namespace Karlssberg.Motiv;

public interface IBinaryBooleanResult<TMetadata> : ICompositeBooleanResult<TMetadata>
{
    BooleanResultBase<TMetadata> LeftOperandResult { get; }
    
    BooleanResultBase<TMetadata> RightOperandResult { get; }
}