using Motiv.Generator.Attributes;

namespace Motiv.HigherOrderProposition.PropositionBuilders;

internal class WhenTrueYieldOverloads
{
    [FluentMethodTemplate]
    internal static Func<TEvaluation, IEnumerable<TNewMetadata>> WhenTrueYield<TEvaluation, TNewMetadata>(Func<TEvaluation, IEnumerable<TNewMetadata>> whenTrue)
    {
        return whenTrue;
    }

    [FluentMethodTemplate]
    internal static Func<TEvaluation, IEnumerable<string>> WhenTrueYield<TEvaluation>(Func<TEvaluation, IEnumerable<string>> whenTrue)
    {
        return whenTrue;
    }
}
