using Motiv.Generator.Attributes;

namespace Motiv.HigherOrderProposition.PropositionBuilders;

internal class WhenTrueYieldOverloads
{
    [FluentMethodTemplate]
    internal static Func<TEvaluation, IEnumerable<TNewMetadata>> WhenTrueYield<TEvaluation, TNewMetadata>(Func<TEvaluation, IEnumerable<TNewMetadata>> function)
    {
        return function;
    }

    [FluentMethodTemplate]
    internal static Func<TEvaluation, IEnumerable<string>> WhenTrueYield<TEvaluation>(Func<TEvaluation, IEnumerable<string>> function)
    {
        return function;
    }
}
