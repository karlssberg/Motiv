namespace Karlssberg.Motiv.All;

public class AllSatisfiedSpec<TModel, TMetadata, TUnderlyingMetadata> :
    SpecBase<IEnumerable<TModel>, TMetadata>,
    IHaveUnderlyingSpec<TModel, TUnderlyingMetadata>
{
    private readonly string? _description;
    private readonly Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> _metadataFactory;

    internal AllSatisfiedSpec(
        SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> metadataFactory,
        string? description = null)
    {
        UnderlyingSpec = underlyingSpec;
        _metadataFactory = metadataFactory;
        _description = description;
    }

    public override string Description => _description is null 
        ? $"ALL({UnderlyingSpec})"
        : $"ALL<{_description}>({UnderlyingSpec})";

    public SpecBase<TModel, TUnderlyingMetadata> UnderlyingSpec { get; }

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var resultsWithModel = models
            .Select(model => WrapException.IfIsSatisfiedByInvocationFails(this,
                UnderlyingSpec,
                () =>
                {
                    var underlyingResult = UnderlyingSpec.IsSatisfiedBy(model);
                    return new BooleanResultWithModel<TModel, TUnderlyingMetadata>(model, underlyingResult);
                }))
            .ToList();

        return new AllSatisfiedBooleanResult<TModel, TMetadata, TUnderlyingMetadata>(
            isSatisfied => _metadataFactory(isSatisfied, resultsWithModel),
            resultsWithModel);
    }
}

public class AllSatisfiedSpec<TModel, TMetadata> : AllSatisfiedSpec<TModel, TMetadata, TMetadata>
{
    internal AllSatisfiedSpec(
        SpecBase<TModel, TMetadata> underlyingSpec,
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> metadataFactory)
        : base(underlyingSpec, metadataFactory)
    {
    }
}