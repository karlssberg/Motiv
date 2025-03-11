using Motiv.Generator.Attributes;

namespace Motiv.BooleanResultPredicateProposition.PropositionBuilders;

internal class WhenTrueYieldOverloads
{

    [FluentMethodTemplate]
    internal static Func<TModel, TResult, IEnumerable<TMetadata>> WhenTrueYield<TModel, TMetadata, TResult>(Func<TModel, TResult, IEnumerable<TMetadata>> whenTrue)
    {
        return whenTrue;
    }

    [FluentMethodTemplate]
    internal static Func<TModel, TResult, IEnumerable<TMetadata>> WhenTrueYield<TModel, TMetadata, TResult>(Func<TModel, IEnumerable<TMetadata>> whenTrue)
    {
        return (model, _) => whenTrue(model);
    }
}
