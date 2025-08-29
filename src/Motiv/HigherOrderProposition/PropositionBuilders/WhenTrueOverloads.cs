using Motiv.Generator.Attributes;

namespace Motiv.HigherOrderProposition.PropositionBuilders;

internal class WhenTrueOverloads
{
    [FluentMethodTemplate]
    internal static Func<TEvaluation, TNewMetadata> WhenTrue<TEvaluation, TNewMetadata>(Func<TEvaluation, TNewMetadata> whenTrue)
    {
        return whenTrue;
    }

    [FluentMethodTemplate]
    internal static Func<TEvaluation, TNewMetadata> WhenTrue<TEvaluation, TNewMetadata>(TNewMetadata whenTrue)
    {
        return _ => whenTrue;
    }
}
