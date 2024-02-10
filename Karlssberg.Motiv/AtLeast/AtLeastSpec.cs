namespace Karlssberg.Motiv.AtLeast;

internal sealed class AtLeastSpec<TModel, TMetadata>(
    int minimum,
    SpecBase<TModel, TMetadata> underlyingSpec,
    string? description = null)
    : SpecBase<IEnumerable<TModel>, TMetadata>
{
    public override string Description => 
        description switch
        {
            null => $"AT_LEAST_{minimum}({underlyingSpec})",
            _ => $"<{description}>({underlyingSpec})"
        };

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var results = models
            .Select(underlyingSpec.IsSatisfiedByOrWrapException)
            .ToArray();

        var isSatisfied = results.Count(result => result.Satisfied) >= minimum;
        return new AtLeastBooleanResult<TMetadata>(
            isSatisfied,
            minimum, 
            results);
    }
}