using Karlssberg.Motiv.Proposition.YieldWhenFalse;

namespace Karlssberg.Motiv.Proposition;

public interface INestedReasonsSpecBuilderStart<TModel>
{
    SpecBase<TModel, string> CreateSpec(string description);
    
    IYieldReasonWhenFalse<TModel> YieldWhenTrue(string trueBecause);
    IYieldReasonWhenFalse<TModel> YieldWhenTrue(Func<TModel, string> trueBecause);
}