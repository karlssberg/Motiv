
using Microsoft.CodeAnalysis.Testing;
using Motiv.Generator.FluentFactory;
using static Microsoft.CodeAnalysis.DiagnosticSeverity;
using VerifyCS =
    Motiv.Generator.Tests.CSharpSourceGeneratorVerifier<Motiv.Generator.FluentFactory.FluentFactoryGenerator>;

namespace Motiv.Generator.Tests;


public class FluentFactoryDiagnosticsTests
{
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
                Sources = { ("Source.cs", code) },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult("MOTIV001", Error)
                        .WithSpan("Source.cs",12, 30, 12, 34)
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
                Sources = { ("Source.cs", code) },

                ExpectedDiagnostics =
                {
                    new DiagnosticResult("MOTIV001", Error)
                        .WithSpan("Source.cs", 23, 69, 23, 71)
                },
                GeneratedSources =
                {
                    (typeof(FluentFactoryGenerator), "MyNamespace.Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }



    [Fact]
    public async Task Given_a_regular_and_multi_methods_collide_when_they_service_different_different_source_parameter_types_Should_generate_a_step_method_collisions_error2()
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

            public readonly partial struct MyTypeA<TModel, TPredicateResult>
            {

                [FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
                public MyTypeA(
                    [FluentMethod("From")]Expression<Func<TModel, TPredicateResult>> expression,
                    [MultipleFluentMethods(typeof(WhenTrueYieldOverloads))]Func<TModel, BooleanResultBase<string>, IEnumerable<string>> trueBecause,
                    [MultipleFluentMethods(typeof(WhenFalseYieldOverloads))]Func<TModel, BooleanResultBase<string>, IEnumerable<string>> falseBecause)
                {
                }

                [FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
                public MyTypeA(
                    [FluentMethod("From")]Expression<Func<TModel, TPredicateResult>> expression,
                    [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<TModel, BooleanResultBase<string>, string> trueBecause,
                    [FluentMethod("WhenFalseYield")]Func<TModel, BooleanResultBase<string>, IEnumerable<string>> falseBecause)
                {
                }
            }

            [FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
            public readonly partial struct MyTypeB<TModel, TPredicateResult>(
                [FluentMethod("From")]Expression<Func<TModel, TPredicateResult>> expression,
                [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<TModel, BooleanResultBase<string>, string> trueBecause,
                [MultipleFluentMethods(typeof(WhenFalseOverloads))]Func<TModel, BooleanResultBase<string>, string> falseBecause)
            {
            }

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
                internal static Func<TModel, TBooleanResult, TNewMetadata> WhenTrue<TModel, TBooleanResult, TNewMetadata>(TNewMetadata whenTrue)
                {
                    return (_, _) => whenTrue;
                }
            }

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

            internal class WhenTrueYieldOverloads
            {
                [FluentMethodTemplate]
                internal static Func<TModel, TResult, IEnumerable<TMetadata>> WhenTrueYield<TModel, TMetadata, TResult>(Func<TModel, TResult, IEnumerable<TMetadata>> whenTrue)
                {
                    return whenTrue;
                }

                [FluentMethodTemplate]
                internal static Func<TModel, TResult, IEnumerable<string>> WhenTrueYield<TModel, TResult>(Func<TModel, TResult, IEnumerable<string>> whenTrue)
                {
                    return whenTrue;
                }
            }

            internal class WhenFalseYieldOverloads
            {
                [FluentMethodTemplate]
                internal static Func<TEvaluation, IEnumerable<TNewMetadata>> WhenFalseYield<TEvaluation, TNewMetadata>(Func<TEvaluation, IEnumerable<TNewMetadata>> function)
                {
                    return function;
                }

                [FluentMethodTemplate]
                internal static Func<TEvaluation, IEnumerable<TNewMetadata>> WhenFalse<TEvaluation, TNewMetadata>(Func<TEvaluation, TNewMetadata> whenFalse)
                {
                    return (model) => [whenFalse(model)];
                }

                [FluentMethodTemplate]
                internal static Func<TEvaluation, IEnumerable<TNewMetadata>> WhenFalse<TEvaluation, TNewMetadata>(TNewMetadata whenFalse)
                {
                    return _ => [whenFalse];
                }
            }
            """;

        const string expected =
            """
            using System;
            using System.Linq.Expressions;

            namespace MyNamespace
            {
                public static partial class Spec
                {
                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="MyNamespace.MyTypeA{TModel, TPredicateResult}"/>
                    ///     <seealso cref="MyNamespace.MyTypeB{TModel, TPredicateResult}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__MyNamespace_Spec<TModel, TPredicateResult> From<TModel, TPredicateResult>(in System.Linq.Expressions.Expression<System.Func<TModel, TPredicateResult>> expression)
                    {
                        return new Step_0__MyNamespace_Spec<TModel, TPredicateResult>(expression);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyNamespace.MyTypeA{TModel, TPredicateResult}"/>
                ///     <seealso cref="MyNamespace.MyTypeB{TModel, TPredicateResult}"/>
                /// </summary>
                public struct Step_0__MyNamespace_Spec<TModel, TPredicateResult>
                {
                    private readonly System.Linq.Expressions.Expression<System.Func<TModel, TPredicateResult>> _expression__parameter;
                    public Step_0__MyNamespace_Spec(in System.Linq.Expressions.Expression<System.Func<TModel, TPredicateResult>> expression)
                    {
                        this._expression__parameter = expression;
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="MyNamespace.MyTypeA{TModel, TPredicateResult}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyTypeA<TModel, TPredicateResult> WhenTrueYield(in System.Func<TModel, BooleanResultBase<string>, System.Collections.Generic.IEnumerable<string>> whenTrue)
                    {
                        return new MyTypeA<TModel, TPredicateResult>(this._expression__parameter, WhenTrueYieldOverloads.WhenTrueYield<TModel, string, BooleanResultBase<string>>(whenTrue));
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="MyNamespace.MyTypeA{TModel, TPredicateResult}"/>
                    ///     <seealso cref="MyNamespace.MyTypeB{TModel, TPredicateResult}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__MyNamespace_Spec<TModel, TPredicateResult> WhenTrue(in System.Func<TModel, BooleanResultBase<string>, string> whenTrue)
                    {
                        return new Step_1__MyNamespace_Spec<TModel, TPredicateResult>(this._expression__parameter, WhenTrueOverloads.WhenTrue<TModel, BooleanResultBase<string>, string>(whenTrue));
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="MyNamespace.MyTypeA{TModel, TPredicateResult}"/>
                    ///     <seealso cref="MyNamespace.MyTypeB{TModel, TPredicateResult}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__MyNamespace_Spec<TModel, TPredicateResult> WhenTrue(in System.Func<TModel, string> whenTrue)
                    {
                        return new Step_1__MyNamespace_Spec<TModel, TPredicateResult>(this._expression__parameter, WhenTrueOverloads.WhenTrue<TModel, BooleanResultBase<string>, string>(whenTrue));
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="MyNamespace.MyTypeA{TModel, TPredicateResult}"/>
                    ///     <seealso cref="MyNamespace.MyTypeB{TModel, TPredicateResult}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__MyNamespace_Spec<TModel, TPredicateResult> WhenTrue(in string whenTrue)
                    {
                        return new Step_1__MyNamespace_Spec<TModel, TPredicateResult>(this._expression__parameter, WhenTrueOverloads.WhenTrue<TModel, BooleanResultBase<string>, string>(whenTrue));
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyNamespace.MyTypeA{TModel, TPredicateResult}"/>
                ///     <seealso cref="MyNamespace.MyTypeB{TModel, TPredicateResult}"/>
                /// </summary>
                public struct Step_1__MyNamespace_Spec<TModel, TPredicateResult>
                {
                    private readonly System.Linq.Expressions.Expression<System.Func<TModel, TPredicateResult>> _expression__parameter;
                    private readonly System.Func<TModel, BooleanResultBase<string>, string> _trueBecause__parameter;
                    public Step_1__MyNamespace_Spec(in System.Linq.Expressions.Expression<System.Func<TModel, TPredicateResult>> expression, in System.Func<TModel, BooleanResultBase<string>, string> trueBecause)
                    {
                        this._expression__parameter = expression;
                        this._trueBecause__parameter = trueBecause;
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="MyNamespace.MyTypeA{TModel, TPredicateResult}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyTypeA<TModel, TPredicateResult> WhenFalseYield(in System.Func<TModel, BooleanResultBase<string>, System.Collections.Generic.IEnumerable<string>> falseBecause)
                    {
                        return new MyTypeA<TModel, TPredicateResult>(this._expression__parameter, this._trueBecause__parameter, falseBecause);
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="MyNamespace.MyTypeB{TModel, TPredicateResult}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyTypeB<TModel, TPredicateResult> WhenFalse(in System.Func<TModel, BooleanResultBase<string>, string> whenFalse)
                    {
                        return new MyTypeB<TModel, TPredicateResult>(this._expression__parameter, this._trueBecause__parameter, WhenFalseOverloads.WhenFalse<TModel, BooleanResultBase<string>, string>(whenFalse));
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="MyNamespace.MyTypeB{TModel, TPredicateResult}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyTypeB<TModel, TPredicateResult> WhenFalse(in System.Func<TModel, string> whenFalse)
                    {
                        return new MyTypeB<TModel, TPredicateResult>(this._expression__parameter, this._trueBecause__parameter, WhenFalseOverloads.WhenFalse<TModel, BooleanResultBase<string>, string>(whenFalse));
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="MyNamespace.MyTypeB{TModel, TPredicateResult}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyTypeB<TModel, TPredicateResult> WhenFalse(in string whenFalse)
                    {
                        return new MyTypeB<TModel, TPredicateResult>(this._expression__parameter, this._trueBecause__parameter, WhenFalseOverloads.WhenFalse<TModel, BooleanResultBase<string>, string>(whenFalse));
                    }
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { ("Source.cs", code) },

                GeneratedSources =
                {
                    (typeof(FluentFactoryGenerator), "MyNamespace.Spec.g.cs", expected)
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
                [MultipleFluentMethods(typeof(MyTypeB<,>.WhenTrueOverloads))]Func<TModel, BooleanResultBase<string>, string> trueBecause)
            {
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
                    internal static Func<TModel, TBooleanResult, TNewMetadata> WhenTrue<TModel, TBooleanResult, TNewMetadata>(TNewMetadata whenTrue)
                    {
                        return (_, _) => whenTrue;
                    }
                }
            }
            """;

        const string expected =
            """
            namespace MyNamespace
            {
                public static partial class Spec
                {
                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="MyNamespace.MyTypeB{TModel, TPredicateResult}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static MyTypeB<TModel, TPredicateResult> From<TModel, TPredicateResult>(in Func<TModel, TBooleanResult, TNewMetadata> whenTrue)
                    {
                        return new MyTypeB<TModel, TPredicateResult>(whenTrue);
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="MyNamespace.MyTypeB{TModel, TPredicateResult}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static MyTypeB<TModel, TPredicateResult> From<TModel, TPredicateResult>(in Func<TModel, TBooleanResult, TNewMetadata> whenTrue)
                    {
                        return new MyTypeB<TModel, TPredicateResult>(whenTrue);
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="MyNamespace.MyTypeB{TModel, TPredicateResult}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static MyTypeB<TModel, TPredicateResult> From<TModel, TPredicateResult>(in Func<TModel, TBooleanResult, TNewMetadata> whenTrue)
                    {
                        return new MyTypeB<TModel, TPredicateResult>(whenTrue);
                    }
                }
            }
            """;
        return new VerifyCS.Test
        {
            TestState =
            {
                Sources = { ("Source.cs", code) },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult("MOTIV003", Error)
                        .WithSpan("Source.cs", 23, 69, 23, 71)
                        .WithArguments("Convert.WithName")
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
                [MultipleFluentMethods(typeof(MyTypeB<,>.WhenTrueOverloads))]Func<TModel, BooleanResultBase<string>, string> trueBecause)
            {
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
                    internal static Func<TModel, TBooleanResult, TNewMetadata> WhenTrue<TModel, TBooleanResult, TNewMetadata>(TNewMetadata whenTrue)
                    {
                        return (_, _) => whenTrue;
                    }
                }
            }
            """;

        return new VerifyCS.Test
        {
            TestState =
            {
                Sources = { ("Source.cs", code) },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult("MOTIV004", Error)
                        .WithSpan("Source.cs", 23, 69, 23, 71)
                        .WithArguments("Convert.WithName")
                }
            }
        }.RunAsync();
    }
}
