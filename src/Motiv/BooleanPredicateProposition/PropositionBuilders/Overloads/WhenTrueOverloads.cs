using Motiv.Generator;

namespace Motiv.BooleanPredicateProposition.PropositionBuilders.Overloads;

internal class WhenTrueOverloads
{
    [FluentMethodTemplate]
    internal static Func<TModel, TMetadata> WhenTrue<TModel, TMetadata>(Func<TModel, TMetadata> whenTrue)
    {
        return whenTrue;
    }

    [FluentMethodTemplate]
    internal static Func<TModel, TMetadata> WhenTrue<TModel, TMetadata>(TMetadata whenTrue)
    {
        return _ => whenTrue;
    }
}
