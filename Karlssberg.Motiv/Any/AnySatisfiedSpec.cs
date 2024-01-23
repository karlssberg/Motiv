namespace Karlssberg.Motiv.Any;

public class AnySatisfiedSpec<TModel, TMetadata, TUnderlyingMetadata> :
    SpecBase<IEnumerable<TModel>, TMetadata>,
    IHaveUnderlyingSpec<TModel, TUnderlyingMetadata>
{
    private readonly string? _description;

    private readonly
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>>
        _metadataFactoryFn;

    internal AnySatisfiedSpec(
        SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>>
            metadataFactoryFn,
        string? description = null)
    {
        UnderlyingSpec = underlyingSpec;
        _metadataFactoryFn = metadataFactoryFn;
        _description = description;
    }

    public override string Description => _description is null
        ? $"ANY({UnderlyingSpec})"
        : $"ANY<{_description}>({UnderlyingSpec})";

    public SpecBase<TModel, TUnderlyingMetadata> UnderlyingSpec { get; }

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var resultsWithModel = models
            .Select(model =>
            {
                var underlyingResult = UnderlyingSpec.IsSatisfiedByOrWrapException(model);
                return new BooleanResultWithModel<TModel, TUnderlyingMetadata>(model, underlyingResult);
            })
            .ToArray();
        
        var isSatisfied = resultsWithModel.Any(result => result.IsSatisfied);
        var metadata = _metadataFactoryFn(isSatisfied, resultsWithModel);
        return new AnySatisfiedBooleanResult<TModel, TMetadata, TUnderlyingMetadata>(
            isSatisfied,
            metadata,
            resultsWithModel);
    }
}

public class AnySatisfiedSpec<TModel, TMetadata> : AnySatisfiedSpec<TModel, TMetadata, TMetadata>
{
    internal AnySatisfiedSpec(
        SpecBase<TModel, TMetadata> underlyingSpec,
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> metadataFactoryFn)
        : base(underlyingSpec, metadataFactoryFn)
    {
    }
}