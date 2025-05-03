using Motiv.Generator.Attributes;

public class HigherOrderSpecPredicateOperation<TModel, TUnderlyingMetadata>;

[FluentFactory]
public static partial class Spec;

public class SpecBase<TModel, TMetadata>;

[FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
public readonly partial struct MinimalHigherOrderFromSpecPropositionFactory<TModel, TMetadata>(
    [MultipleFluentMethods(typeof(SpecBuildOverloads))]SpecBase<TModel, TMetadata> spec,
    [MultipleFluentMethods(typeof(HigherOrderPredicateSpecMethods))]HigherOrderSpecPredicateOperation<TModel, TMetadata> higherOrderOperation);

[FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
public readonly partial struct ExplanationFromSpecWithNameHigherOrderPropositionFactory<TModel, TMetadata>(
    [MultipleFluentMethods(typeof(SpecBuildOverloads))] SpecBase<TModel, TMetadata> spec,
    [MultipleFluentMethods(typeof(HigherOrderPredicateSpecMethods))]
    HigherOrderSpecPredicateOperation<TModel, TMetadata> higherOrderOperation,
    [FluentMethod("WhenTrue")] string trueBecause);

internal static class SpecBuildOverloads
{
    [FluentMethodTemplate]
    internal static SpecBase<TModel, TMetadata> Build<TModel, TMetadata>(SpecBase<TModel, TMetadata> spec) =>
        spec;

    [FluentMethodTemplate]
    internal static SpecBase<TModel, TMetadata> Build<TModel, TMetadata>(Func<SpecBase<TModel, TMetadata>> specFactory) =>
        specFactory.Invoke();

    [FluentMethodTemplate]
    internal static SpecBase<TModel, string> Build<TModel>(SpecBase<TModel, string> spec) =>
        spec;

    [FluentMethodTemplate]
    internal static SpecBase<TModel, string> Build<TModel>(Func<SpecBase<TModel, string>> specFactory) =>
        specFactory.Invoke();
}

internal static class HigherOrderPredicateSpecMethods
{
    [FluentMethodTemplate]
    internal static HigherOrderSpecPredicateOperation<TModel, TUnderlyingMetadata> AsNSatisfied<TModel, TUnderlyingMetadata>(int n)
    {
        return new HigherOrderSpecPredicateOperation<TModel, TUnderlyingMetadata>();
    }
}

