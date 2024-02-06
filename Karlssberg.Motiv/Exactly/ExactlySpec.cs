namespace Karlssberg.Motiv.Exactly;

internal class ExactlySpec<TModel, TMetadata>(int n, SpecBase<TModel, TMetadata> underlyingSpec, string? description) :
    SpecBase<IEnumerable<TModel>, TMetadata>
{
    public SpecBase<TModel, TMetadata> UnderlyingSpec { get; } = underlyingSpec;

    public override string Description => description switch
    {
        null => $"{n}_SATISFIED({UnderlyingSpec})",
        not null => $"<{description}>({UnderlyingSpec})"
    };

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var results = models
            .Select(model =>
            {
                var underlyingResult = UnderlyingSpec.IsSatisfiedByOrWrapException(model);
                return new BooleanResultWithModel<TModel, TMetadata>(model, underlyingResult);
            })
            .ToArray();

        var isSatisfied = results.Count(result => result.Value) == n;

        return new ExactlyBooleanResult<TModel, TMetadata>(n, isSatisfied, results);
    }
}