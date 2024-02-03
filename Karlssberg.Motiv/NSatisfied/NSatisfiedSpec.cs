namespace Karlssberg.Motiv.NSatisfied;

internal class NSatisfiedSpec<TModel, TMetadata>(int n, SpecBase<TModel, TMetadata> underlyingSpec, string? description) :
    SpecBase<IEnumerable<TModel>, TMetadata>
{
    public SpecBase<TModel, TMetadata> UnderlyingSpec { get; } = underlyingSpec;
    public override string Description => $"{n}_SATISFIED({UnderlyingSpec})";

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var results = models
            .Select(model =>
            {
                var underlyingResult = UnderlyingSpec.IsSatisfiedByOrWrapException(model);
                return new BooleanResultWithModel<TModel, TMetadata>(model, underlyingResult);
            })
            .ToArray();

        var isSatisfied = results.Count(result => result.IsSatisfied) == n;

        return new NSatisfiedBooleanResult<TModel, TMetadata>(n, isSatisfied, results);
    }
}