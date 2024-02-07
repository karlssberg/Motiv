
namespace Karlssberg.Motiv.AtMost;

internal sealed class AtMostSpec<TModel, TMetadata>(
    int maximum,
    SpecBase<TModel, TMetadata> underlyingSpec,
    string? description = null)
    : SpecBase<IEnumerable<TModel>, TMetadata>
{
    public override string Description => description switch
    {
        null => $"AT_MOST_{maximum}({underlyingSpec})",
        not null => $"<{description}>({underlyingSpec})"
    };

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var results = models
            .Select(model =>
            {
                var underlyingResult = underlyingSpec.IsSatisfiedByOrWrapException(model);
                return new BooleanResultWithModel<TModel, TMetadata>(model, underlyingResult);
            })
            .ToArray();
        
        var isSatisfied = results.Count(result => result.Value) <= maximum;
        return new AtMostBooleanResult<TMetadata>(
            isSatisfied,
            maximum,
            results.Select(result => result.UnderlyingResult)); ;
    }
}