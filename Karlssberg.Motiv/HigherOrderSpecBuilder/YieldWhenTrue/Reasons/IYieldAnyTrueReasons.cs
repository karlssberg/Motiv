using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Reasons;

namespace Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Reasons;

public interface IYieldAnyTrueReasons<TModel, TUnderlyingMetadata>
{
    IYieldFalseReasons<TModel, TUnderlyingMetadata> YieldWhenAnyTrue(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<string>> trueBecause);
}