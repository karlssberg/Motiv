namespace Karlssberg.Motiv.NSatisfied;
internal class NSatisfiedSpec<TModel, TMetadata, TUnderlyingMetadata>(
    string name,
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
    Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicateFn,
    MetadataFactory<TModel, TMetadata, TUnderlyingMetadata> metadataFactoryFn)
    : 
    SpecBase<IEnumerable<TModel>, TMetadata>,
    IHaveUnderlyingSpec<TModel, TUnderlyingMetadata>
{
    public SpecBase<TModel, TUnderlyingMetadata> UnderlyingSpec { get; } = underlyingSpec;
    public override string Description => $"{name}({UnderlyingSpec})";

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var results = models 
            .Select(model =>
            {  
                var underlyingResult = UnderlyingSpec.IsSatisfiedByOrWrapException(model);
                return new BooleanResultWithModel<TModel, TUnderlyingMetadata>(model, underlyingResult);
            })
            .ToArray();

        var isSatisfied = higherOrderPredicateFn(results);
        var metadata = metadataFactoryFn.Create(isSatisfied, results);
        return new NSatisfiedBooleanResult<TModel, TMetadata, TUnderlyingMetadata>(
            name,
            isSatisfied,
            metadata,
            results);
    }
}