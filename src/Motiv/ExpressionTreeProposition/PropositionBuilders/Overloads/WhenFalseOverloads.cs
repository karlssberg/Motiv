using Motiv.Generator.Attributes;

namespace Motiv.ExpressionTreeProposition.PropositionBuilders.Overloads;

internal class WhenFalseOverloads
{
    [FluentMethodTemplate]
    internal static Func<TModel, TBooleanResult, TNewMetadata> WhenFalse<TModel, TBooleanResult, TNewMetadata>(Func<TModel, TBooleanResult, TNewMetadata> whenFalse)
    {
        return whenFalse;
    }

    [FluentMethodTemplate]
    internal static Func<TModel, TBooleanResult, TNewMetadata> WhenFalse<TModel, TBooleanResult, TNewMetadata>(Func<TModel, TNewMetadata> whenFalse)
    {
        return (model, _) => whenFalse(model);
    }

    [FluentMethodTemplate]
    internal static Func<TModel, TBooleanResult, TNewMetadata> WhenFalse<TModel, TBooleanResult, TNewMetadata>(TNewMetadata whenFalse)
    {
        return (_, _) => whenFalse;
    }
}
