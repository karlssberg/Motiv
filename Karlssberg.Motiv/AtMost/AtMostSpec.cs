
namespace Karlssberg.Motiv.AtMost;

internal sealed class AtMostSpec<TModel, TMetadata>(
    int maximum,
    SpecBase<TModel, TMetadata> underlyingSpec,
    string? description = null)
    : SpecBase<IEnumerable<TModel>, TMetadata>
{
    public override string Description =>
        description switch
        {
            null => $"AT_MOST_{maximum}({underlyingSpec})",
            not null => $"<{description}>({underlyingSpec})"
        };

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var results = models
            .Select(underlyingSpec.IsSatisfiedByOrWrapException)
            .ToArray();
        
        var isSatisfied = results.Count(result => result.Satisfied) <= maximum;
        return new AtMostBooleanResult<TMetadata>(
            isSatisfied,
            maximum,
            results);
    }
}