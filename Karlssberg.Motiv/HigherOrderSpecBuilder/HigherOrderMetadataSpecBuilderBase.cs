using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Metadata;
using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Metadata;

namespace Karlssberg.Motiv.HigherOrderSpecBuilder;

internal abstract class HigherOrderMetadataSpecBuilderBase<TModel, TMetadata, TUnderlyingMetadata> :
    IHigherOrderMetadataSpecBuilder<TModel, TMetadata, TUnderlyingMetadata>
{
    private Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>>? _yield;
    private Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>>? _yieldWhenAllFalse;
    private Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>>? _yieldWhenAllTrue;
    private Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>>? _yieldWhenAnyFalse;
    private Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>>? _yieldWhenAnyTrue;

    public IYieldAnyTrueMetadataOrFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAllTrue(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> metadata)
    {
        _yieldWhenAllTrue = metadata;
        return this;
    }
    
    public abstract IYieldFalseMetadata<TModel, TAltMetadata, TMetadata> YieldWhenAllTrue<TAltMetadata>(
        Func<IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TAltMetadata>> metadata);

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

    public abstract SpecBase<IEnumerable<TModel>, TMetadata> CreateSpec(string description);
    public abstract SpecBase<IEnumerable<TModel>, TMetadata> CreateSpec();

    public abstract IYieldFalseMetadata<TModel, TAltMetadata, TMetadata> YieldWhenAnyTrue<TAltMetadata>(
        Func<IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TAltMetadata>> metadata);

    public abstract IHigherOrderSpecFactory<TModel, TAltMetadata> Yield<TAltMetadata>(
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TAltMetadata>> metadata);


    public IHigherOrderSpecFactory<TModel, TMetadata> Yield(
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> metadata)
    {
        _yield = metadata;
        return this;
    }


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
                _yieldWhenAnyTrue(resultArray.Where(result => result.IsSatisfied)),
            false when _yieldWhenAnyFalse is not null =>
                _yieldWhenAnyFalse(resultArray.Where(result => !result.IsSatisfied)),

            _ when _yield is not null =>
                _yield(isSatisfied, resultArray),

            _ => []
        };

        bool AllSatisfied()
        {
            return resultArray.All(result => result.IsSatisfied);
        }

        bool NoneSatisfied()
        {
            return resultArray.All(result => !result.IsSatisfied);
        }
    }
}