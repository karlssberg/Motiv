
using Microsoft.CodeAnalysis.Testing;
using Motiv.Generator.FluentFactory;
using static Microsoft.CodeAnalysis.DiagnosticSeverity;
using static Motiv.Generator.FluentFactory.MotivDiagnosticDescriptor;
using VerifyCS =
    Motiv.Generator.Tests.CSharpSourceGeneratorVerifier<Motiv.Generator.FluentFactory.FluentFactoryGenerator>;

namespace Motiv.Generator.Tests;


public class FluentFactoryDiagnosticsTests
{
    private const string SourceFile = "Source.cs";

    [Fact]
    public async Task Given_two_regular_methods_collide_when_and_there_are_no_more_steps_Should_generate_an_ambiguous_constructor_selection_error()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            namespace MyNamespace
            {
                [FluentFactory]
                public static partial class Factory;

                public partial class Person
                {
                    [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                    public Person(string name)
                    {
                        Name = name;
                    }

                    public string Name { get; }
                }

                public partial class Company
                {
                    [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                    public Company(string name)
                    {
                        Name = name;
                    }

                    public string Name { get; }
                }
            }
            """;

        const string expected =
            """
            using System;

            namespace MyNamespace
            {
                public static partial class Factory
                {
                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="MyNamespace.Company"/>
                    ///     <seealso cref="MyNamespace.Person"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Company WithName(in string name)
                    {
                        return new Company(name);
                    }
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { (SourceFile, code) },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(UnreachableConstructor.Id, Error)
                        .WithSpan(SourceFile,12, 30, 12, 34)
                        .WithSpan(SourceFile, 12, 30, 12, 34)
                        .WithArguments(
                            "MyNamespace.Person.Person(string name)",
                            "WithName(string name)",
                            "This involves the constructor parameter 'string name'. Try changing the fluent method name 'WithName', or constructor parameter type 'string name'")
                },
                GeneratedSources =
                {
                    (typeof(FluentFactoryGenerator), "MyNamespace.Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Given_a_regular_and_multi_methods_collide_when_they_service_different_different_source_parameter_types_Should_generate_a_step_method_collisions_error()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            namespace MyNamespace
            {
                [FluentFactory]
                public static partial class Factory;

                public partial class Person
                {
                    [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                    public Person(string name)
                    {
                        Name = name;
                    }

                    public string Name { get; }
                }

                public partial class Company
                {
                    [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                    public Company([MultipleFluentMethods(typeof(Convert))]int? id)
                    {
                        Id = id;
                    }

                    public int? Id { get; }
                }

                public class Convert
                {
                    [FluentMethodTemplate]
                    public static int? WithName(string name)
                    {
                        return int.TryParse(name, out var id) ? id : null;
                    }
                }
            }
            """;

        const string expected =
            """
            using System;

            namespace MyNamespace
            {
                public static partial class Factory
                {
                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="MyNamespace.Person"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Person WithName(in string name)
                    {
                        return new Person(name);
                    }
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { (SourceFile, code) },

                ExpectedDiagnostics =
                {
                    new DiagnosticResult(UnreachableConstructor.Id, Error)
                        .WithSpan(SourceFile, 23, 69, 23, 71)
                        .WithSpan(SourceFile, 12, 30, 12, 34),
                },
                GeneratedSources =
                {
                    (typeof(FluentFactoryGenerator), "MyNamespace.Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public Task Given_that_there_are_no_compatible_parameter_converters_Should_raise_error()
    {
        const string code =
            """
            using System;
            using System.Collections.Generic;
            using Motiv.Generator.Attributes;
            using System.Linq.Expressions;

            namespace MyNamespace;

            public abstract class BooleanResultBase<T> {}

            [FluentFactory]
            public static partial class Spec;

            [FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
            public readonly partial struct MyTypeB<TModel, TPredicateResult>(
                [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<TModel, BooleanResultBase<string>, string> trueBecause);

            internal class WhenTrueOverloads
            {
                [FluentMethodTemplate]
                internal static Func<TModel, TNewMetadata> WhenTrue<TModel, TNewMetadata>(Func<TModel, TNewMetadata> whenTrue)
                {
                    return whenTrue;
                }

                [FluentMethodTemplate]
                internal static Func<TModel, TNewMetadata> WhenTrue<TModel, TNewMetadata>(TNewMetadata whenTrue)
                {
                    return _ => whenTrue;
                }
            }
            """;

        const string expected =
            """
            using System;

            namespace MyNamespace
            {
                public static partial class Spec
                {
                }
            }
            """;

        return new VerifyCS.Test
        {
            TestState =
            {
                Sources = { (SourceFile, code) },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(AllFluentMethodTemplatesIncompatible.Id, Error)
                        .WithSpan(SourceFile, 15, 28, 15, 53)
                        .WithArguments("System.Func<TModel, MyNamespace.BooleanResultBase<string>, string> trueBecause")
                },
                GeneratedSources =
                {
                (typeof(FluentFactoryGenerator), "MyNamespace.Spec.g.cs", expected)
            }
            }
        }.RunAsync();
    }

    [Fact]
    public Task Given_that_there_are_ignored_parameter_converters_Should_raise_warning()
    {
        const string code =
            """
            using System;
            using System.Collections.Generic;
            using Motiv.Generator.Attributes;
            using System.Linq.Expressions;

            namespace MyNamespace;

            public abstract class BooleanResultBase<T> {}

            [FluentFactory]
            public static partial class Spec;

            [FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
            public readonly partial struct MyTypeB<TModel, TPredicateResult>(
                [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<TModel, string> trueBecause);

            internal class WhenTrueOverloads
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

                [FluentMethodTemplate]
                internal static Func<TModel, TNewMetadata> WhenTrue<TModel, TNewMetadata>(TNewMetadata whenTrue)
                {
                    return _ => whenTrue;
                }
            }
            """;

        const string expected =
            """
            using System;

            namespace MyNamespace
            {
                public static partial class Spec
                {
                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="MyNamespace.MyTypeB{TModel, TPredicateResult}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static MyTypeB<TModel, TPredicateResult> WhenTrue<TModel, TPredicateResult>(in string whenTrue)
                    {
                        return new MyTypeB<TModel, TPredicateResult>(WhenTrueOverloads.WhenTrue<TModel, string>(whenTrue));
                    }
                }
            }
            """;

        return new VerifyCS.Test
        {
            TestState =
            {
                Sources = { (SourceFile, code) },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(IncompatibleFluentMethodTemplate.Id, Warning)
                        .WithSpan(SourceFile, 15, 28, 15, 53)
                        .WithSpan(SourceFile, 20, 64, 20, 72)
                        .WithArguments(
                            "System.Func<TModel, TBooleanResult, TNewMetadata> MyNamespace.WhenTrueOverloads.WhenTrue<TModel, TBooleanResult, TNewMetadata>(System.Func<TModel, TBooleanResult, TNewMetadata> whenTrue)",
                            "System.Func<TModel, string> trueBecause"),
                    new DiagnosticResult(IncompatibleFluentMethodTemplate.Id, Warning)
                        .WithSpan(SourceFile, 15, 28, 15, 53)
                        .WithSpan(SourceFile, 26, 64, 26, 72)
                        .WithArguments(
                            "System.Func<TModel, TBooleanResult, TNewMetadata> MyNamespace.WhenTrueOverloads.WhenTrue<TModel, TBooleanResult, TNewMetadata>(System.Func<TModel, TNewMetadata> whenTrue)",
                            "System.Func<TModel, string> trueBecause"),
                },
                GeneratedSources =
                {
                (typeof(FluentFactoryGenerator), "MyNamespace.Spec.g.cs", expected)
            }
            }
        }.RunAsync();
    }

    [Fact]
    public Task Examine_unexpected_unreachable_constructor_error()
    {
        const string code =
            """
            using System;
            using System.Collections.Generic;
            using System.Linq.Expressions;
            using Motiv.Generator.Attributes;

            namespace Test;

            public class BooleanResultBase<T>;

            [FluentFactory]
            public static partial class Spec;

            [FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
            public readonly partial struct ExplanationExpressionTreePropositionFactory<TModel, TPredicateResult>(
                Expression<Func<TModel, TPredicateResult>> expression,
                [MultipleFluentMethods(typeof(WhenTrueOverloads))] Func<TModel, BooleanResultBase<string>, string> trueBecause);

            [FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
            public readonly partial struct ExplanationWithNameExpressionTreePropositionFactory<TModel, TPredicateResult>(
                Expression<Func<TModel, TPredicateResult>> expression,
                [FluentMethod("WhenTrue")]string trueBecause);

            public readonly partial struct MultiAssertionExplanationExpressionTreePropositionFactory<TModel, TPredicateResult>
            {
                [FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
                public MultiAssertionExplanationExpressionTreePropositionFactory(
                    Expression<Func<TModel, TPredicateResult>> expression,
                    [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<TModel, BooleanResultBase<string>, string> trueBecause)
                {
                }
            }

            internal class WhenTrueOverloads
            {

                [FluentMethodTemplate]
                internal static Func<TModel, TBooleanResult, TNewMetadata> WhenTrue<TModel, TBooleanResult, TNewMetadata>(Func<TModel, TNewMetadata> whenTrue)
                {
                    return (model, _) => whenTrue(model);
                }

                [FluentMethodTemplate]
                internal static Func<TModel, TBooleanResult, TNewMetadata> WhenTrue<TModel, TBooleanResult, TNewMetadata>(TNewMetadata whenTrue)
                {
                    return (_, _) => whenTrue;
                }
            }
            """;

        const string expected =
            """
            using System;
            using System.Linq.Expressions;

            namespace Test
            {
                public static partial class Spec
                {
                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.ExplanationExpressionTreePropositionFactory{TModel, TPredicateResult}"/>
                    ///     <seealso cref="Test.ExplanationWithNameExpressionTreePropositionFactory{TModel, TPredicateResult}"/>
                    ///     <seealso cref="Test.MultiAssertionExplanationExpressionTreePropositionFactory{TModel, TPredicateResult}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Spec<TModel, TPredicateResult> WithExpression<TModel, TPredicateResult>(in System.Linq.Expressions.Expression<System.Func<TModel, TPredicateResult>> expression)
                    {
                        return new Step_0__Test_Spec<TModel, TPredicateResult>(expression);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="Test.ExplanationExpressionTreePropositionFactory{TModel, TPredicateResult}"/>
                ///     <seealso cref="Test.ExplanationWithNameExpressionTreePropositionFactory{TModel, TPredicateResult}"/>
                ///     <seealso cref="Test.MultiAssertionExplanationExpressionTreePropositionFactory{TModel, TPredicateResult}"/>
                /// </summary>
                public struct Step_0__Test_Spec<TModel, TPredicateResult>
                {
                    private readonly System.Linq.Expressions.Expression<System.Func<TModel, TPredicateResult>> _expression__parameter;
                    public Step_0__Test_Spec(in System.Linq.Expressions.Expression<System.Func<TModel, TPredicateResult>> expression)
                    {
                        this._expression__parameter = expression;
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.ExplanationExpressionTreePropositionFactory{TModel, TPredicateResult}"/>
                    ///     <seealso cref="Test.MultiAssertionExplanationExpressionTreePropositionFactory{TModel, TPredicateResult}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MultiAssertionExplanationExpressionTreePropositionFactory<TModel, TPredicateResult> WhenTrue(in System.Func<TModel, string> whenTrue)
                    {
                        return new MultiAssertionExplanationExpressionTreePropositionFactory<TModel, TPredicateResult>(this._expression__parameter, WhenTrueOverloads.WhenTrue<TModel, BooleanResultBase<string>, string>(whenTrue));
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.ExplanationWithNameExpressionTreePropositionFactory{TModel, TPredicateResult}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public ExplanationWithNameExpressionTreePropositionFactory<TModel, TPredicateResult> WhenTrue(in string trueBecause)
                    {
                        return new ExplanationWithNameExpressionTreePropositionFactory<TModel, TPredicateResult>(this._expression__parameter, trueBecause);
                    }
                }
            }
            """;

        return new VerifyCS.Test
        {
            TestState =
            {
                Sources = { (SourceFile, code) },
                ExpectedDiagnostics =
                {
                },
                GeneratedSources =
                {
                (typeof(FluentFactoryGenerator), "Test.Spec.g.cs", expected)
            }
            }
        }.RunAsync();
    }
}
