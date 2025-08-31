using Motiv.FluentFactory.Generator;

namespace Motiv.BooleanPredicateProposition.PropositionBuilders.Overloads;

internal class WhenFalseYieldOverloads
{
    [FluentMethodTemplate]
    internal static Func<TModel, IEnumerable<TMetadata>> WhenFalseYield<TModel, TMetadata>(Func<TModel, IEnumerable<TMetadata>> whenFalse)
    {
        return whenFalse;
    }

    [FluentMethodTemplate]
    internal static Func<TModel, IEnumerable<TMetadata>> WhenFalse<TModel, TMetadata>(Func<TModel, TMetadata> whenFalse)
    {
        return whenFalse.ToEnumerableReturn();
    }

    [FluentMethodTemplate]
    internal static Func<TModel, IEnumerable<TMetadata>> WhenFalse<TModel, TMetadata>(TMetadata whenFalse)
    {
        return _ => whenFalse.ToEnumerable();
    }
}
