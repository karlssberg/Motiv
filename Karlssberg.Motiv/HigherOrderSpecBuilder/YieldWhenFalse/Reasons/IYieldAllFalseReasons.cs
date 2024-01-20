namespace Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Reasons;

public interface IYieldAllFalseReasons<TModel, TUnderlyingMetadata> : IHigherOrderSpecFactory<TModel, string>
{
    IHigherOrderSpecFactory<TModel, string> YieldWhenAllFalse(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<string>> falseBecause);
}