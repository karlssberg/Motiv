using Karlssberg.Motiv.NSatisfied;

namespace Karlssberg.Motiv.Any;

public class AnySatisfiedSpec<TModel, TMetadata, TUnderlyingMetadata> :
    SpecBase<IEnumerable<TModel>, TMetadata>,
    IHaveUnderlyingSpec<TModel, TUnderlyingMetadata>
{
    private readonly string? _description;

    private readonly
        MetadataFactory<TModel, TMetadata, TUnderlyingMetadata>
        _metadataFactory;

    internal AnySatisfiedSpec(
        SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
        MetadataFactory<TModel, TMetadata, TUnderlyingMetadata>
            metadataFactory,
        string? description = null)
    {
        UnderlyingSpec = underlyingSpec;
        _metadataFactory = metadataFactory;
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
        var metadata = _metadataFactory.Create(isSatisfied, resultsWithModel);
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
        MetadataFactory<TModel, TMetadata, TMetadata> metadataFactory)
        : base(underlyingSpec, metadataFactory)
    {
    }
}