﻿using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Reasons;
using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Reasons;

namespace Karlssberg.Motiv.HigherOrderSpecBuilder;

internal abstract class HigherOrderSpecBuilderBase<TModel, TUnderlyingMetadata> :
    IHigherOrderSpecBuilder<TModel, TUnderlyingMetadata>,
    IYieldReasonsWhenFalse<TModel, TUnderlyingMetadata>
    
{
    private Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<string>>? _yieldWhenAllFalse;
    private Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<string>>? _yieldWhenAllTrue;
    private Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<string>>? _yieldWhenAnyFalse;
    private Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<string>>? _yieldWhenAnyTrue;
    
    /// <inheritdoc />
    public IYieldReasonsWhenAnyTrueOrFalse<TModel, TUnderlyingMetadata> YieldWhenAllTrue(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<string>> trueBecause)
    {
        _yieldWhenAllTrue = trueBecause;
        return this;
    }
    
    /// <inheritdoc />
    IYieldReasonsWhenFalse<TModel, TUnderlyingMetadata> IYieldReasonsWhenAnyTrue<TModel, TUnderlyingMetadata>.YieldWhenAnyTrue(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<string>> trueBecause)
    {
        _yieldWhenAnyTrue = trueBecause;
        return this;
    }
    
    /// <inheritdoc />
    public abstract SpecBase<IEnumerable<TModel>, string> CreateSpec(string description);
    
    /// <inheritdoc />
    public abstract SpecBase<IEnumerable<TModel>, string> CreateSpec();
    
    /// <inheritdoc />
    IYieldReasonsWhenAllFalse<TModel, TUnderlyingMetadata> IYieldReasonsWhenAnyFalse<TModel, TUnderlyingMetadata>.YieldWhenAnyFalse(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<string>> falseBecause)
    {
        _yieldWhenAnyFalse = falseBecause;
        return this;
    }
    
    /// <inheritdoc />
    IHigherOrderSpecFactory<TModel, string> IYieldReasonsWhenAllFalse<TModel, TUnderlyingMetadata>.YieldWhenAllFalse(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<string>> falseBecause)
    {
        _yieldWhenAllFalse = falseBecause;
        return this;
    }
    
    protected IEnumerable<string> YieldReasons(bool isSatisfied, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>> results)
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

            _ => []
        };

        bool AllSatisfied() => resultArray.All(result => result.IsSatisfied);

        bool NoneSatisfied() => resultArray.All(result => !result.IsSatisfied);
    }
}