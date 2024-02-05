using Karlssberg.Motiv.Proposition.Factories;
using Karlssberg.Motiv.Proposition.YieldWhenFalse;

namespace Karlssberg.Motiv.Proposition;

internal class NestedTrueReasonsSpecBuilder<TModel>(Func<TModel, SpecBase<TModel, string>> specPredicate) : 
    INestedReasonsSpecBuilderStart<TModel>,
    IDescriptiveSpecFactory<TModel, string>
{
    public IYieldReasonWhenFalse<TModel> YieldWhenTrue(string trueBecause) => 
        new NestedFalseReasonsSpecBuilder<TModel>(specPredicate, _ => trueBecause);

    public IYieldReasonWhenFalse<TModel> YieldWhenTrue(Func<TModel, string> trueBecause) => 
        new NestedFalseReasonsSpecBuilder<TModel>(specPredicate, trueBecause);

    public SpecBase<TModel, string> CreateSpec(string description) =>
        new Spec<TModel, string>(
            description.ThrowIfNullOrWhitespace(nameof(description)),
            specPredicate);
}