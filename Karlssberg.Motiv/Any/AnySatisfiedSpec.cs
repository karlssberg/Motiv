namespace Karlssberg.Motiv.Any;

public class AnySatisfiedSpec<TModel, TMetadata, TUnderlyingMetadata> : SpecBase<IEnumerable<TModel>, TMetadata>
{
    private readonly SpecBase<TModel, TUnderlyingMetadata> _underlyingSpec;
    private readonly Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> _metadataFactory;
    private readonly string? _description;

    internal AnySatisfiedSpec(
        SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> metadataFactory,
        string? description = null)
    {
        _underlyingSpec = underlyingSpec;
        _metadataFactory = metadataFactory;
        _description = description;
    }

    public override string Description => _description ?? $"ANY({_underlyingSpec})";

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var resultsWithModel = models
            .Select(model =>
                WrapException.IfIsSatisfiedByInvocationFails(this, _underlyingSpec,
                    () =>
                    {
                        var underlyingResult = _underlyingSpec.IsSatisfiedBy(model);
                        return new BooleanResultWithModel<TModel, TUnderlyingMetadata>(model, underlyingResult);
                    }))
            .ToList();

        return new AnySatisfiedBooleanResult<TModel, TMetadata, TUnderlyingMetadata>(
            isSatisfied => _metadataFactory(isSatisfied, resultsWithModel), 
            resultsWithModel);
    }
}

public class AnySatisfiedSpec<TModel, TMetadata> : AnySatisfiedSpec<TModel, TMetadata, TMetadata>
{
    internal AnySatisfiedSpec(
        SpecBase<TModel, TMetadata> underlyingSpec, 
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> metadataFactory) 
        : base(underlyingSpec, metadataFactory)
    {
    }
}