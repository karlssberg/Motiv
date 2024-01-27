using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Metadata;
using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Metadata;

namespace Karlssberg.Motiv.HigherOrderSpecBuilder;

internal abstract class HigherOrderSpecBuilderBase<TModel, TMetadata, TUnderlyingMetadata> :
    IHigherOrderSpecBuilder<TModel, TMetadata, TUnderlyingMetadata>
{
    private MetadataFactory<TModel, TMetadata, TUnderlyingMetadata>? _yield;
    private Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>>? _yieldWhenAllFalse;
    private Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>>? _yieldWhenAllTrue;
    private Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>>? _yieldWhenAnyFalse;
    private Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>>? _yieldWhenAnyTrue;

    /// <inheritdoc />
    public IYieldMetadataWhenAnyTrueOrFalse<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAllTrue(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> metadata)
    {
        _yieldWhenAllTrue = metadata;
        return this;
    }


    /// <inheritdoc />
    public abstract IYieldMetadataWhenFalse<TModel, TAltMetadata, TUnderlyingMetadata> YieldWhenAllTrue<TAltMetadata>(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TAltMetadata>> metadata);

    /// <inheritdoc />
    public IYieldMetadataWhenFalse<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAnyTrue(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> metadata)
    {
        _yieldWhenAnyTrue = results => metadata(results.Where(result => result.IsSatisfied));
        return this;
    }
    
    /// <inheritdoc />
    public abstract IYieldMetadataWhenFalse<TModel, TAltMetadata, TUnderlyingMetadata> YieldWhenAnyTrue<TAltMetadata>(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TAltMetadata>> metadata);

    /// <inheritdoc />
    public IHigherOrderSpecFactory<TModel, TMetadata> YieldWhenAllFalse(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> metadata)
    {
        _yieldWhenAllFalse = metadata;
        return this;
    }

    /// <inheritdoc />
    public IYieldMetadataWhenAllFalse<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAnyFalse(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> metadata)
    {
        _yieldWhenAnyFalse = results => metadata(results.Where(result => !result.IsSatisfied));
        return this;
    }

    /// <inheritdoc />
    public abstract SpecBase<IEnumerable<TModel>, TMetadata> CreateSpec(string description);
    
    /// <inheritdoc />
    public abstract SpecBase<IEnumerable<TModel>, TMetadata> CreateSpec();
    
    /// <inheritdoc />
    public abstract IHigherOrderSpecFactory<TModel, TAltMetadata> Yield<TAltMetadata>(
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TAltMetadata>> metadata);
    
    /// <inheritdoc />
    public IHigherOrderSpecFactory<TModel, TMetadata> Yield(
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> metadata)
    {
        _yield = new MetadataFactory<TModel, TMetadata, TUnderlyingMetadata>(metadata);
        return this;
    }

    protected IEnumerable<TMetadata> YieldMetadata(bool isSatisfied, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>> results)
    {
        var resultArray = results.ToArray();

        return isSatisfied switch
        {
            _ when _yield is not null =>
                _yield.Value.Create(isSatisfied, resultArray),
            
            true when _yieldWhenAllTrue is not null && AllSatisfied() =>
                _yieldWhenAllTrue(resultArray),
            false when _yieldWhenAllFalse is not null && NoneSatisfied() =>
                _yieldWhenAllFalse(resultArray),

            true when _yieldWhenAnyTrue is not null =>
                _yieldWhenAnyTrue(resultArray.Where(result => result.IsSatisfied)),
            false when _yieldWhenAnyFalse is not null =>
                _yieldWhenAnyFalse(resultArray.Where(result => !result.IsSatisfied)),

            _ => []
        };

        bool AllSatisfied() => resultArray.All(result => result.IsSatisfied);

        bool NoneSatisfied() => resultArray.All(result => !result.IsSatisfied);
    }

    public IHigherOrderSpecFactory<TModel, TMetadata1> Yield<TMetadata1>(Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, TMetadata1> metadata) => throw new NotImplementedException();
}