using Motiv.Generator.Attributes;

namespace Motiv.BooleanPredicateProposition.PropositionBuilders;

internal class WhenTrueYieldOverloads
{
    [FluentMethodTemplate]
    internal static Func<TModel, IEnumerable<TMetadata>> WhenTrueYield<TModel, TMetadata>(Func<TModel, IEnumerable<TMetadata>> whenTrue)
    {
        return whenTrue;
    }

    [FluentMethodTemplate]
    internal static Func<TModel, IEnumerable<string>> WhenTrueYield<TModel>(Func<TModel, IEnumerable<string>> whenTrue)
    {
        return whenTrue;
    }
}
