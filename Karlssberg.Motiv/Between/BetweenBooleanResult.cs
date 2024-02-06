namespace Karlssberg.Motiv.Between;

internal class BetweenBooleanResult<TModel, TMetadata>(
    bool isSatisfied,
    int minimum,
    int maximum,
    IEnumerable<BooleanResultWithModel<TModel, TMetadata>> underlyingResults)
    : BooleanResultBase<TMetadata>
{
    public override bool Value => isSatisfied;
    
    public override string Description => $"BETWEEN_{minimum}_AND_{maximum}({UnderlyingResults})";
    
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults => underlyingResults;
    
    public override IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperands => Value switch
    {
        true => UnderlyingResults.Where(result => result.Value == Value),
        false => UnderlyingResults
    };
    
    public override IEnumerable<string> GatherReasons() => DeterminativeOperands
        .SelectMany(result => result.GatherReasons());
}