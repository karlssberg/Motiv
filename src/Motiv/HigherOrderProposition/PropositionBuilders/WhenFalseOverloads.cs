using Motiv.Generator.Attributes;

namespace Motiv.HigherOrderProposition.PropositionBuilders;

internal class WhenFalseOverloads
{
    [FluentMethodTemplate]
    internal static Func<TEvaluation, TNewMetadata> WhenFalse<TEvaluation, TNewMetadata>(Func<TEvaluation, TNewMetadata> whenFalse)
    {
        return whenFalse;
    }

    [FluentMethodTemplate]
    internal static Func<TEvaluation, TNewMetadata> WhenFalse<TEvaluation, TNewMetadata>(TNewMetadata whenFalse)
    {
        return _ => whenFalse;
    }
}
