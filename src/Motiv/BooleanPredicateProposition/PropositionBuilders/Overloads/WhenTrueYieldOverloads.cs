using Motiv.FluentFactory.Attributes;

namespace Motiv.BooleanPredicateProposition.PropositionBuilders.Overloads;

internal class WhenTrueYieldOverloads
{
    [FluentMethodTemplate]
    internal static Func<TModel, IEnumerable<TMetadata>> WhenTrueYield<TModel, TMetadata>(Func<TModel, IEnumerable<TMetadata>> whenTrue)
    {
        return whenTrue;
    }
}
