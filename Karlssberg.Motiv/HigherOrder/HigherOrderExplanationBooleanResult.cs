namespace Karlssberg.Motiv.HigherOrder;

internal sealed class HigherOrderExplanationBooleanResult<TModel, TUnderlyingMetadata>(
    bool isSatisfied,
    IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> underlyingResults,
    IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> causes,
    string because)
    : BooleanResultBase<string>
{
    public override MetadataTree<string> MetadataTree => new (because.ToEnumerable());
    public override IEnumerable<BooleanResultBase> Underlying => underlyingResults;

    public override IEnumerable<BooleanResultBase<string>> UnderlyingWithMetadata =>
        underlyingResults.ResolveUnderlyingWithMetadata<string, TUnderlyingMetadata>();
    
    public override IEnumerable<BooleanResultBase> Causes => causes;

    public override IEnumerable<BooleanResultBase<string>> CausesWithMetadata =>
        causes.ResolveCausesWithMetadata<string, TUnderlyingMetadata>();
    
    public override bool Satisfied => isSatisfied;

    public override ResultDescriptionBase Description =>
        new HigherOrderResultDescription<TModel, TUnderlyingMetadata>(
            because,
            causes);

    public override Explanation Explanation => 
        new (Description)
        {
            Underlying = causes
                .Select(cause => cause.Explanation)
                .ElseIfEmpty(underlyingResults.Select(result => result.Explanation))
        };
}