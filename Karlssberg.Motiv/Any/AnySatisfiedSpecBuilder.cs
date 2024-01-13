using Karlssberg.Motiv.CollectionBuilder;

namespace Karlssberg.Motiv.Any;

internal class AnySatisfiedSpecBuilder<TModel, TMetadata>(SpecBase<TModel, TMetadata> underlyingSpec) : 
    ICollectionSpecBuilder<TModel, TMetadata>
{
    private Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>>? _yieldWhenAny;
    private Func<IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>>? _yieldWhenAllTrue;
    private Func<IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>>? _yieldWhenAllFalse;

    IYieldMetadata<TModel, TMetadata> IYieldTrueMetadata<TModel, TMetadata>.YieldWhenAllTrue(
        Func<IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> yieldWhenAllTrue)
    {
        _yieldWhenAllTrue = yieldWhenAllTrue;
        return this;
    }

    IYieldFalseMetadata<TModel, TMetadata> IYieldMetadata<TModel, TMetadata>.YieldWhenAny(
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> yieldWhenAny)
    {
        _yieldWhenAny = yieldWhenAny;
        return this;
    }

    public ISpecFactory<TModel, TMetadata> YieldWhenAllFalse(
        Func<IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> yieldWhenAllFalse)
    {
        _yieldWhenAllFalse = yieldWhenAllFalse;
        return this;
    }
    
    SpecBase<IEnumerable<TModel>, TMetadata> ISpecFactory<TModel, TMetadata>.CreateSpec()
    {
        return new AnySatisfiedSpec<TModel, TMetadata>(underlyingSpec,
            (anySatisfied, results) =>
            {
                var resultList = results.ToList();

                return anySatisfied switch
                {
                    true when _yieldWhenAllTrue is not null && AllSatisfied() => 
                        _yieldWhenAllTrue(resultList),
                    
                    false when _yieldWhenAllFalse is not null => 
                        _yieldWhenAllFalse(resultList),
                    
                    _ when _yieldWhenAny is not null => 
                        _yieldWhenAny.Invoke(anySatisfied, resultList),
                    
                    _ => []
                };

                bool AllSatisfied() => resultList.All(result => result.IsSatisfied);
            });
    }
}
