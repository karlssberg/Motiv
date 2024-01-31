namespace Karlssberg.Motiv.NSatisfied;
internal class NSatisfiedSpec<TModel, TMetadata>(
    string name,
    SpecBase<TModel, TMetadata> underlyingSpec,
    Func<IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, bool> higherOrderPredicateFn)
    : 
    SpecBase<IEnumerable<TModel>, TMetadata>
{
    public SpecBase<TModel, TMetadata> UnderlyingSpec { get; } = underlyingSpec;
    public override string Description => $"{name}({UnderlyingSpec})";

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var results = models 
            .Select(model =>
            {  
                var underlyingResult = UnderlyingSpec.IsSatisfiedByOrWrapException(model);
                return new BooleanResultWithModel<TModel, TMetadata>(model, underlyingResult);
            })
            .ToArray();

        var isSatisfied = higherOrderPredicateFn(results);
        return new NSatisfiedBooleanResult<TModel, TMetadata>(
            name,
            isSatisfied,
            results);
    }
}