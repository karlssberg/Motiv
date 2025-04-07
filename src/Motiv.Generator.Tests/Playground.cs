using System;
using System.Collections.Generic;
using System.Collections.Generic;
using Motiv.Generator.Attributes;

[FluentFactory]
public static partial class Spec;

[FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
public readonly partial struct ExplanationWithNamePropositionFactory<TModel>(
    [FluentMethod("Build")]Func<TModel, bool> predicate,
    [FluentMethod("WhenTrue")]string trueBecause,
    [MultipleFluentMethods(typeof(WhenFalseOverloads))]Func<TModel, string> falseBecause)
{
}

[FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
public readonly partial struct MultiAssertionExplanationWithNamePropositionFactory<TModel>(
    [FluentMethod("Build")]Func<TModel, bool> predicate,
    [FluentMethod("WhenTrue")]string trueBecause,
    [FluentMethod("WhenTrueYield")]Func<TModel, IEnumerable<string>> falseBecause)
{
}

[FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
public readonly partial struct ExplanationPropositionFactory<TModel>(
    [FluentMethod("Build")]Func<TModel, bool> predicate,
    [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<TModel, string> whenTrue,
    [MultipleFluentMethods(typeof(WhenFalseOverloads))]Func<TModel, string> whenFalse)
{
}

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
