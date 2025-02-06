using Motiv.Generator.Attributes;

namespace Motiv.SpecDecoratorProposition.PropositionBuilders;

internal class WhenTrueYieldOverloads
{
    [FluentMethodTemplate]
    internal static Func<TModel, IEnumerable<TMetadata>> WhenTrueYield<TModel, TMetadata>(Func<TModel, IEnumerable<TMetadata>> function)
    {
        return function;
    }

    [FluentMethodTemplate]
    internal static Func<TModel, IEnumerable<TMetadata>> WhenTrue<TModel, TMetadata>(Func<TModel, TMetadata> function)
    {
        return model => [function(model)];
    }

    [FluentMethodTemplate]
    internal static Func<TModel, IEnumerable<TMetadata>> WhenTrue<TModel, TMetadata>(TMetadata value)
    {
        return _ => [value];
    }
}
