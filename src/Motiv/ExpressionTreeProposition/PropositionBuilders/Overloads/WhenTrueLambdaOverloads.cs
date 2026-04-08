using Converj.Attributes;

namespace Motiv.ExpressionTreeProposition.PropositionBuilders.Overloads;

internal class WhenTrueLambdaOverloads
{
    [FluentMethodTemplate]
    internal static Func<TModel, TBooleanResult, TNewMetadata> WhenTrue<TModel, TBooleanResult, TNewMetadata>(Func<TModel, TBooleanResult, TNewMetadata> whenTrue)
    {
        return whenTrue;
    }

    [FluentMethodTemplate]
    internal static Func<TModel, TBooleanResult, TNewMetadata> WhenTrue<TModel, TBooleanResult, TNewMetadata>(Func<TModel, TNewMetadata> whenTrue)
    {
        return (model, _) => whenTrue(model);
    }
}
