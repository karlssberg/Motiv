namespace Karlssberg.Motiv.Between;

internal class BetweenSpec<TModel, TMetadata>(
    int minimum,
    int maximum,
    SpecBase<TModel, TMetadata> underlyingSpec,
    string? description = null)
    : SpecBase<IEnumerable<TModel>, TMetadata>
{
    public override string Description => description switch
    {
        null => $"BETWEEN_{minimum}_AND_{maximum}({underlyingSpec})",
        not null => $"<{description}>({underlyingSpec})"
    };

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(IEnumerable<TModel> model)
    {
        var underlyingResults = model
            .Select(model =>
            {
                var underlyingResult = underlyingSpec.IsSatisfiedByOrWrapException(model);
                return new BooleanResultWithModel<TModel, TMetadata>(model, underlyingResult);
            })
            .ToArray();

        var count = underlyingResults.Count(result => result.Value);
        var isSatisfied = count >= minimum && count <= maximum;

        return new BetweenBooleanResult<TModel, TMetadata>(isSatisfied, minimum, maximum, underlyingResults);
    }
}