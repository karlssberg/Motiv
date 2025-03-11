using Motiv.Generator.Attributes;

namespace Motiv.BooleanPredicateProposition.PropositionBuilders;

internal class WhenFalseYieldOverloads
{
    [FluentMethodTemplate]
    internal static Func<TModel, IEnumerable<TMetadata>> WhenFalseYield<TModel, TMetadata>(Func<TModel, IEnumerable<TMetadata>> whenFalse)
    {
        return whenFalse;
    }
}
