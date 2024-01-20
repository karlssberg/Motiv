using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Reasons;

namespace Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Reasons;

public interface IYieldAnyTrueReasonsOrFalseReasons<TModel, TUnderlyingMetadata> :
    IYieldAnyTrueReasons<TModel, TUnderlyingMetadata>,
    IYieldAnyFalseReasons<TModel, TUnderlyingMetadata>
{
}