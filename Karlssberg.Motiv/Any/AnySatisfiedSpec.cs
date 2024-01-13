using static Karlssberg.Motiv.SpecException;

namespace Karlssberg.Motiv.Any;

public sealed class AnySatisfiedSpec<TModel, TMetadata> : SpecBase<IEnumerable<TModel>, TMetadata>
{
    private readonly SpecBase<TModel, TMetadata> _underlyingSpec;
    private readonly Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> _metadataFactory;
  
    internal AnySatisfiedSpec(SpecBase<TModel, TMetadata> underlyingSpec,
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>>? metadataFactory = null)
    {
        _underlyingSpec = underlyingSpec;
        _metadataFactory = metadataFactory ?? CreateDefaultMetadataFactory();
    }

    public override string Description => $"ANY({_underlyingSpec})";

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var resultsWithModel = models
            .Select(model =>
                WrapException.IfIsSatisfiedByInvocationFails(this, _underlyingSpec,
                    () =>
                    {
                        var underlyingResult = _underlyingSpec.IsSatisfiedBy(model);
                        return new BooleanResultWithModel<TModel, TMetadata>(model, underlyingResult);
                    }))
            .ToList();

        return new AnySatisfiedBooleanResult<TModel, TMetadata>(
            isSatisfied => _metadataFactory(isSatisfied, resultsWithModel), 
            resultsWithModel);
    }
    
    private static Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> CreateDefaultMetadataFactory()
    {
        return (isSatisfied, results) => isSatisfied switch
        {
            true => results
                .Where(result => result.IsSatisfied)
                .SelectMany(result => result.GetMetadata()),
            false => results.SelectMany(result => result.GetMetadata())
        };
    }
}