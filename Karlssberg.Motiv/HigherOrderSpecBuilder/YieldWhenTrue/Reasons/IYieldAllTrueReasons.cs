namespace Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Reasons;

public interface IYieldAllTrueReasons<TModel, TUnderlyingMetadata> :
    IYieldAnyTrueReasons<TModel, TUnderlyingMetadata>
{
    IYieldAnyTrueReasonsOrFalseReasons<TModel, TUnderlyingMetadata> YieldWhenAllTrue(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<string>> trueBecause);
    
}