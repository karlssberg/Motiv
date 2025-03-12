using Motiv.Generator.Attributes;

namespace Motiv.HigherOrderProposition.PropositionBuilders;

internal class WhenFalseYieldOverloads
{
    [FluentMethodTemplate]
    internal static Func<TEvaluation, IEnumerable<TNewMetadata>> WhenFalseYield<TEvaluation, TNewMetadata>(Func<TEvaluation, IEnumerable<TNewMetadata>> function)
    {
        return function;
    }

    [FluentMethodTemplate]
    internal static Func<TEvaluation, IEnumerable<TNewMetadata>> WhenFalse<TEvaluation, TNewMetadata>(Func<TEvaluation, TNewMetadata> whenFalse)
    {
        return whenFalse.ToEnumerableReturn();
    }

    [FluentMethodTemplate]
    internal static Func<TEvaluation, IEnumerable<TNewMetadata>> WhenFalse<TEvaluation, TNewMetadata>(TNewMetadata whenFalse)
    {
        return _ => whenFalse.ToEnumerable();
    }
}
