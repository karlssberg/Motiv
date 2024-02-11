namespace Karlssberg.Motiv.Range;

internal class RangeSpec<TModel, TMetadata>(
    int minimum,
    int maximum,
    SpecBase<TModel, TMetadata> underlyingSpec,
    string? description = null)
    : SpecBase<IEnumerable<TModel>, TMetadata>
{
    public override string Description => 
        description switch
        {
            null => $"RANGE_{minimum}_TO_{maximum}({underlyingSpec})",
            not null => $"<{description}>({underlyingSpec})"
        };

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(IEnumerable<TModel> model)
    {
        var underlyingResults = model
            .Select(underlyingSpec.IsSatisfiedByOrWrapException)
            .ToArray();

        var count = underlyingResults.Count(result => result.Satisfied);
        var isSatisfied = count >= minimum && count <= maximum;

        return new RangeBooleanResult<TModel, TMetadata>(isSatisfied, minimum, maximum, underlyingResults);
    }
}