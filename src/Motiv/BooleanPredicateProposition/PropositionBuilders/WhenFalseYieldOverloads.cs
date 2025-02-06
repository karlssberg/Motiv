using Motiv.Generator.Attributes;

namespace Motiv.BooleanPredicateProposition.PropositionBuilders;

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
        return model => [whenFalse(model)];
    }

    [FluentMethodTemplate]
    internal static Func<TModel, IEnumerable<TMetadata>> WhenFalse<TModel, TMetadata>(TMetadata whenFalse)
    {
        return _ => [whenFalse];
    }
}
