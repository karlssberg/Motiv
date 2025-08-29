using Motiv.Generator.Attributes;

namespace Motiv.BooleanPredicateProposition.PropositionBuilders.Overloads;

internal class WhenFalseOverloads
{
    [FluentMethodTemplate]
    internal static Func<TModel, TMetadata> WhenFalse<TModel, TMetadata>(Func<TModel, TMetadata> whenFalse)
    {
        return whenFalse;
    }


    [FluentMethodTemplate]
    internal static Func<TModel, TMetadata> WhenFalse<TModel, TMetadata>(TMetadata whenFalse)
    {
        return _ => whenFalse;
    }
}
