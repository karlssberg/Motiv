using Motiv.Generator.Attributes;

namespace Motiv.HigherOrderProposition.PropositionBuilders;

internal class WhenFalseYieldOverloads
{
    [FluentMethodTemplate]
    internal static Func<TEvaluation, IEnumerable<TNewMetadata>> WhenFalseYield<TEvaluation, TNewMetadata>(Func<TEvaluation, IEnumerable<TNewMetadata>> function)
    {
        return function;
    }
}
