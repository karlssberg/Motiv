using Motiv.Generator.Attributes;

namespace Motiv.BooleanPredicateProposition.PropositionBuilders;

internal class WhenTrueYieldOverloads
{
    [FluentMethodTemplate]
    internal static Func<TModel, IEnumerable<TMetadata>> WhenTrueYield<TModel, TMetadata>(Func<TModel, IEnumerable<TMetadata>> whenTrue)
    {
        return whenTrue;
    }

    [FluentMethodTemplate]
    internal static Func<TModel, IEnumerable<TMetadata>> WhenTrue<TModel, TMetadata>(Func<TModel, TMetadata> whenTrue)
    {
        return model => [whenTrue(model)];
    }

    [FluentMethodTemplate]
    internal static Func<TModel, IEnumerable<TMetadata>> WhenTrue<TModel, TMetadata>(TMetadata whenTrue)
    {
        return _ => [whenTrue];
    }
}
