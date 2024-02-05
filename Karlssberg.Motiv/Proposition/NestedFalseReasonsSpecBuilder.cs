using Karlssberg.Motiv.Proposition.Factories;
using Karlssberg.Motiv.Proposition.YieldWhenFalse;

namespace Karlssberg.Motiv.Proposition;

internal class NestedFalseReasonsSpecBuilder<TModel>(
    Func<TModel, SpecBase<TModel, string>> specPredicate,
    Func<TModel, string> trueBecause,
    string candidateDescription = null!) : 
    ISpecFactory<TModel>, 
    IYieldReasonWhenFalse<TModel>
{
    private Func<TModel, string> _falseBecause = null!;

    public ISpecFactory<TModel> YieldWhenFalse(string falseBecause)
    {
        _falseBecause = _ => falseBecause;
        return this;
    }

    public ISpecFactory<TModel> YieldWhenFalse(Func<TModel, string> falseBecause)
    {
        _falseBecause = falseBecause;
        return this;
    }
    
    public SpecBase<TModel, string> CreateSpec(string description) =>
        CreateSpecInternal(description.ThrowIfNullOrWhitespace(nameof(description)));

    public SpecBase<TModel, string> CreateSpec() =>
        CreateSpecInternal(candidateDescription);
    
    private SpecBase<TModel, string> CreateSpecInternal(string description) =>
        new Spec<TModel, string>(description, specPredicate)
            .YieldWhenTrue(trueBecause)
            .YieldWhenFalse(_falseBecause);

}