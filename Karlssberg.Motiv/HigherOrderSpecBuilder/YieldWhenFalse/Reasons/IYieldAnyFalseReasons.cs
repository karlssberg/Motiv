namespace Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Reasons;

public interface IYieldAnyFalseReasons<TModel, TUnderlyingMetadata> : IHigherOrderSpecFactory<TModel, string>
{
    IYieldAllFalseReasons<TModel, TUnderlyingMetadata> YieldWhenAnyFalse(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<string>> metadata);
}