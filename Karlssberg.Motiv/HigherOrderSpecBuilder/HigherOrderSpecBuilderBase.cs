namespace Karlssberg.Motiv.HigherOrderSpecBuilder;

internal abstract class HigherOrderSpecBuilderBase<TModel, TMetadata, TUnderlyingMetadata> : IHigherOrderSpecBuilder<TModel, TMetadata, TUnderlyingMetadata>
{
    private Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>>? _yieldWhenAllTrue;
    private Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>>? _yieldWhenAllFalse;
    private Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>>? _yieldWhenAnything;
    private Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>>? _yieldWhenAnyFalse;
    private Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>>? _yieldWhenAnyTrue;
    public IYieldAnyMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAllTrue(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> metadata)
    {
        _yieldWhenAllTrue = metadata;
        return this;
    }

    public IYieldFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAnything(
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> metadata)
    {
        _yieldWhenAnything = metadata;
        return this;
    }

    public IYieldFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAnyTrue(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> metadata)
    {
        _yieldWhenAnyTrue = results => metadata(results.Where(result => result.IsSatisfied));
        return this;
    }

    public IHigherOrderSpecFactory<TModel, TMetadata> YieldWhenAllFalse(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> metadata)
    {
        _yieldWhenAllFalse = metadata;
        return this;
    }
    public IYieldAllFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAnyFalse(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> metadata)
    {
        _yieldWhenAnyFalse = results => metadata(results.Where(result => !result.IsSatisfied));
        return this;
    }

    public abstract SpecBase<IEnumerable<TModel>, TMetadata> CreateSpec();

    public abstract SpecBase<IEnumerable<TModel>, TMetadata> CreateSpec(string description);
    
    protected IEnumerable<TMetadata> YieldMetadata(bool isSatisfied, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>> results)
    {
        var resultArray = results.ToArray();

        return isSatisfied switch
        {
            true when _yieldWhenAllTrue is not null && AllSatisfied() =>
                _yieldWhenAllTrue(resultArray),
            false when _yieldWhenAllFalse is not null && NoneSatisfied() =>
                _yieldWhenAllFalse(resultArray),

            true when _yieldWhenAnyTrue is not null =>
                _yieldWhenAnyTrue(resultArray),
            false when _yieldWhenAnyFalse is not null =>
                _yieldWhenAnyFalse(resultArray),

            _ when _yieldWhenAnything is not null =>
                _yieldWhenAnything(isSatisfied, resultArray),

            _ => []
        };

        bool AllSatisfied() => resultArray.All(result => result.IsSatisfied);

        bool NoneSatisfied() => resultArray.All(result => !result.IsSatisfied);
    }
    public abstract IYieldFalseMetadata<TModel, TAltMetadata, TMetadata> YieldWhenAnyTrue<TAltMetadata>(
        Func<IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TAltMetadata>> metadata);
    public abstract IYieldFalseMetadata<TModel, TAltMetadata, TMetadata> YieldWhenAnything<TAltMetadata>(
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TAltMetadata>> metadata);
}