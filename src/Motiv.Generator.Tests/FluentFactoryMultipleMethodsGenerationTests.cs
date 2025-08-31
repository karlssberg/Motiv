using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using static Motiv.Generator.FluentFactoryGenerator;

namespace Motiv.Generator.Tests;
using VerifyCS = CSharpSourceGeneratorVerifier<FluentFactoryGenerator>;

public class FluentFactoryMultipleMethodsGenerationTests
{
    private const string SourceFile = "Source.cs";

    [Fact]
    public async Task Should_build_multiple_root_constructor_methods_for_single_parameter()
    {
        const string code =
            """
            using System;
            using Motiv.Generator;

            namespace Test.Namespace
            {
                [FluentFactory]
                public static partial class Factory;

                public class MyBuildTarget<T>
                {
                    [FluentConstructor(typeof(Factory))]
                    public MyBuildTarget(
                        [MultipleFluentMethods(typeof(MethodVariants))]T data)
                    {
                        Data = data;
                    }

                    public T Data { get; set; }
                }

                public class MethodVariants
                {
                    [FluentMethodTemplate]
                    public static T WithValue<T>(in T value)
                    {
                        return value;
                    }

                    [FluentMethodTemplate]
                    public static T WithFunction<T>(in Func<T> function)
                    {
                        return function();
                    }
                }
            }
            """;

        const string expected =
            """
            namespace Test.Namespace
            {
                public static partial class Factory
                {
                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Namespace_Factory<T> WithValue<T>(in T value)
                    {
                        return new Step_0__Test_Namespace_Factory<T>(MethodVariants.WithValue<T>(value));
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Namespace_Factory<T> WithFunction<T>(in System.Func<T> function)
                    {
                        return new Step_0__Test_Namespace_Factory<T>(MethodVariants.WithFunction<T>(function));
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.Namespace.MyBuildTarget{T}"/>
                /// </summary>
                public struct Step_0__Test_Namespace_Factory<T>
                {
                    private readonly T _data__parameter;
                    internal Step_0__Test_Namespace_Factory(in T data)
                    {
                        this._data__parameter = data;
                    }

                    /// <summary>
                    /// Creates a new instance using constructor Test.Namespace.MyBuildTarget<T>.MyBuildTarget(T data).
                    ///
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T> Create()
                    {
                        return new MyBuildTarget<T>(this._data__parameter);
                    }
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { code },
                GeneratedSources =
                {
                    (typeof(FluentFactoryGenerator), "Test.Namespace.Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_ensure_no_duplicate_signatures_are_generated_when_using_multiple_constructors()
    {
        const string code =
            """
            using System;
            using Motiv.Generator;

            namespace Test.Namespace
            {
                [FluentFactory]
                public static partial class Factory;

                public class MyBuildTargetA<T>
                {
                    [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                    public MyBuildTargetA(
                        [MultipleFluentMethods(typeof(MethodVariants))]T data,
                        int value)
                    {
                        Data = data;
                        Value = value;
                    }

                    public T Data { get; set; }
                    public int Value { get; set; }
                }

                public class MyBuildTargetB<T>
                {
                    [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                    public MyBuildTargetB(
                        [MultipleFluentMethods(typeof(MethodVariants))]T data,
                        string value)
                    {
                        Data = data;
                        Value = value;
                    }

                    public T Data { get; set; }
                    public string Value { get; set; }
                }

                public class MethodVariants
                {
                    [FluentMethodTemplate]
                    public static T WithDefaultValue<T>()
                    {
                        return default(T);
                    }

                    [FluentMethodTemplate]
                    public static T WithFunction<T>(in Func<T> function)
                    {
                        return function();
                    }
                }
            }
            """;

        const string expected =
            """
            using System;

            namespace Test.Namespace
            {
                public static partial class Factory
                {
                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTargetA{T}"/>
                    ///     <seealso cref="Test.Namespace.MyBuildTargetB{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Namespace_Factory<T> WithDefaultValue<T>()
                    {
                        return new Step_0__Test_Namespace_Factory<T>(MethodVariants.WithDefaultValue<T>());
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTargetA{T}"/>
                    ///     <seealso cref="Test.Namespace.MyBuildTargetB{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Namespace_Factory<T> WithFunction<T>(in System.Func<T> function)
                    {
                        return new Step_0__Test_Namespace_Factory<T>(MethodVariants.WithFunction<T>(function));
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.Namespace.MyBuildTargetA{T}"/>
                ///     <seealso cref="Test.Namespace.MyBuildTargetB{T}"/>
                /// </summary>
                public struct Step_0__Test_Namespace_Factory<T>
                {
                    private readonly T _data__parameter;
                    internal Step_0__Test_Namespace_Factory(in T data)
                    {
                        this._data__parameter = data;
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTargetA{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTargetA<T> WithValue(in int value)
                    {
                        return new MyBuildTargetA<T>(this._data__parameter, value);
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTargetB{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTargetB<T> WithValue(in string value)
                    {
                        return new MyBuildTargetB<T>(this._data__parameter, value);
                    }
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { code },
                GeneratedSources =
                {
                    (typeof(FluentFactoryGenerator), "Test.Namespace.Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_ensure_no_duplicate_signatures_are_generated_when_using_multiple_constructors_on_second_parameter()
    {
        const string code =
            """
            using System;
            using Motiv.Generator;

            namespace Test.Namespace
            {
                [FluentFactory]
                public static partial class Factory;

                public class MyBuildTargetA<T>
                {
                    [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                    public MyBuildTargetA(
                        int number,
                        [MultipleFluentMethods(typeof(MethodVariants))]T data,
                        int value)
                    {
                        Number = number;
                        Data = data;
                        Value = value;
                    }

                    public int Number { get; set; }
                    public T Data { get; set; }
                    public int Value { get; set; }
                }

                public class MyBuildTargetB<T>
                {
                    [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                    public MyBuildTargetB(
                        int number,
                        [MultipleFluentMethods(typeof(MethodVariants))]T data,
                        string value)
                    {
                        Data = data;
                        Value = value;
                    }

                    public int Number { get; set; }
                    public T Data { get; set; }
                    public string Value { get; set; }
                }

                public class MethodVariants
                {
                    [FluentMethodTemplate]
                    public static T WithDefaultValue<T>()
                    {
                        return default(T);
                    }

                    [FluentMethodTemplate]
                    public static T WithFunction<T>(in Func<T> function)
                    {
                        return function();
                    }
                }
            }
            """;

        const string expected =
            """
            using System;

            namespace Test.Namespace
            {
                public static partial class Factory
                {
                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTargetA{T}"/>
                    ///     <seealso cref="Test.Namespace.MyBuildTargetB{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Namespace_Factory WithNumber(in int number)
                    {
                        return new Step_0__Test_Namespace_Factory(number);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.Namespace.MyBuildTargetA{T}"/>
                ///     <seealso cref="Test.Namespace.MyBuildTargetB{T}"/>
                /// </summary>
                public struct Step_0__Test_Namespace_Factory
                {
                    private readonly int _number__parameter;
                    internal Step_0__Test_Namespace_Factory(in int number)
                    {
                        this._number__parameter = number;
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTargetA{T}"/>
                    ///     <seealso cref="Test.Namespace.MyBuildTargetB{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__Test_Namespace_Factory<T> WithDefaultValue<T>()
                    {
                        return new Step_1__Test_Namespace_Factory<T>(this._number__parameter, MethodVariants.WithDefaultValue<T>());
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTargetA{T}"/>
                    ///     <seealso cref="Test.Namespace.MyBuildTargetB{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__Test_Namespace_Factory<T> WithFunction<T>(in System.Func<T> function)
                    {
                        return new Step_1__Test_Namespace_Factory<T>(this._number__parameter, MethodVariants.WithFunction<T>(function));
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.Namespace.MyBuildTargetA{T}"/>
                ///     <seealso cref="Test.Namespace.MyBuildTargetB{T}"/>
                /// </summary>
                public struct Step_1__Test_Namespace_Factory<T>
                {
                    private readonly int _number__parameter;
                    private readonly T _data__parameter;
                    internal Step_1__Test_Namespace_Factory(in int number, in T data)
                    {
                        this._number__parameter = number;
                        this._data__parameter = data;
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTargetA{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTargetA<T> WithValue(in int value)
                    {
                        return new MyBuildTargetA<T>(this._number__parameter, this._data__parameter, value);
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTargetB{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTargetB<T> WithValue(in string value)
                    {
                        return new MyBuildTargetB<T>(this._number__parameter, this._data__parameter, value);
                    }
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { code },
                GeneratedSources =
                {
                    (typeof(FluentFactoryGenerator), "Test.Namespace.Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_ensure_no_duplicate_signatures_are_generated_when_using_multiple_constructors_on_second_parameter_against_a_regular_method()
    {
        const string code =
            """
            using System;
            using Motiv.Generator;

            namespace Test.Namespace
            {
                [FluentFactory]
                public static partial class Factory;

                public class MyBuildTargetA<T>
                {
                    [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                    public MyBuildTargetA(
                        int number,
                        [MultipleFluentMethods(typeof(MethodVariants))]T data,
                        int value)
                    {
                        Number = number;
                        Data = data;
                        Value = value;
                    }

                    public int Number { get; set; }
                    public T Data { get; set; }
                    public int Value { get; set; }
                }

                public class MyBuildTargetB<T>
                {
                    [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                    public MyBuildTargetB(
                        int number,
                        [FluentMethod("WithFunction")]Func<T> nativeFunction,
                        string value)
                    {
                        NativeFunction = nativeFunction;
                        Value = value;
                    }

                    public int Number { get; set; }
                    public Func<T> NativeFunction { get; set; }
                    public string Value { get; set; }
                }

                public class MethodVariants
                {
                    [FluentMethodTemplate]
                    public static T WithDefaultValue<T>()
                    {
                        return default;
                    }

                    [FluentMethodTemplate]
                    public static T WithFunction<T>(in Func<T> function)
                    {
                        return function();
                    }
                }
            }
            """;

        const string expected =
            """
            using System;

            namespace Test.Namespace
            {
                public static partial class Factory
                {
                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTargetA{T}"/>
                    ///     <seealso cref="Test.Namespace.MyBuildTargetB{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Namespace_Factory WithNumber(in int number)
                    {
                        return new Step_0__Test_Namespace_Factory(number);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.Namespace.MyBuildTargetA{T}"/>
                ///     <seealso cref="Test.Namespace.MyBuildTargetB{T}"/>
                /// </summary>
                public struct Step_0__Test_Namespace_Factory
                {
                    private readonly int _number__parameter;
                    internal Step_0__Test_Namespace_Factory(in int number)
                    {
                        this._number__parameter = number;
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTargetA{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__Test_Namespace_Factory<T> WithDefaultValue<T>()
                    {
                        return new Step_1__Test_Namespace_Factory<T>(this._number__parameter, MethodVariants.WithDefaultValue<T>());
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTargetB{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_2__Test_Namespace_Factory<T> WithFunction<T>(in System.Func<T> nativeFunction)
                    {
                        return new Step_2__Test_Namespace_Factory<T>(this._number__parameter, nativeFunction);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.Namespace.MyBuildTargetA{T}"/>
                /// </summary>
                public struct Step_1__Test_Namespace_Factory<T>
                {
                    private readonly int _number__parameter;
                    private readonly T _data__parameter;
                    internal Step_1__Test_Namespace_Factory(in int number, in T data)
                    {
                        this._number__parameter = number;
                        this._data__parameter = data;
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTargetA{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTargetA<T> WithValue(in int value)
                    {
                        return new MyBuildTargetA<T>(this._number__parameter, this._data__parameter, value);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.Namespace.MyBuildTargetB{T}"/>
                /// </summary>
                public struct Step_2__Test_Namespace_Factory<T>
                {
                    private readonly int _number__parameter;
                    private readonly System.Func<T> _nativeFunction__parameter;
                    internal Step_2__Test_Namespace_Factory(in int number, in System.Func<T> nativeFunction)
                    {
                        this._number__parameter = number;
                        this._nativeFunction__parameter = nativeFunction;
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTargetB{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTargetB<T> WithValue(in string value)
                    {
                        return new MyBuildTargetB<T>(this._number__parameter, this._nativeFunction__parameter, value);
                    }
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { (SourceFile, code) },
                GeneratedSources =
                {
                    (typeof(FluentFactoryGenerator), "Test.Namespace.Factory.g.cs", expected)
                },
                ExpectedDiagnostics =
                {
                    DiagnosticResult.CompilerWarning(ContainsSupersededFluentMethodTemplate.Id)
                        .WithSpan(SourceFile, 14, 36, 14, 58)
                        .WithSpan(SourceFile, 32, 51, 32, 65)
                        .WithArguments(
                            "Test.Namespace.MethodVariants.WithFunction<T>(System.Func<T> function)",
                            "T data",
                            "Test.Namespace.MyBuildTargetA<T>.MyBuildTargetA(int number, T data, int value)",
                            "the parameter 'System.Func<T> nativeFunction' in the constructor 'Test.Namespace.MyBuildTargetB<T>.MyBuildTargetB(int number, System.Func<T> nativeFunction, string value)' was used as the basis for the fluent method. Perhaps the ignored method-template can be removed or modified."),
                    new DiagnosticResult(FluentMethodTemplateSuperseded.Id, DiagnosticSeverity.Info)
                        .WithSpan("Source.cs", 53, 25, 53, 37)
                        .WithArguments(
                            "T Test.Namespace.MethodVariants.WithFunction<T>(System.Func<T> function)",
                            "T data",
                            "Test.Namespace.MyBuildTargetA<T>.MyBuildTargetA(int number, T data, int value)",
                            "System.Func<T> nativeFunction",
                            "Test.Namespace.MyBuildTargetB<T>.MyBuildTargetB(int number, System.Func<T> nativeFunction, string value)")
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_support_multiple_methods_containing_overload_methods()
    {
        const string code =
            """
            using System;
            using Motiv.Generator;

            namespace Test.Namespace
            {
                [FluentFactory]
                public static partial class Factory;

                public class MyBuildTargetA<T>
                {
                    [FluentConstructor(typeof(Factory))]
                    public MyBuildTargetA(
                        [MultipleFluentMethods(typeof(NumberMethods))]int number,
                        [MultipleFluentMethods(typeof(AsMethods))]T data)
                    {
                        Number = number;
                        Data = data;
                    }

                    public int Number { get; set; }
                    public T Data { get; set; }
                }

                public class NumberMethods
                {
                    [FluentMethodTemplate]
                    public static int Number()
                    {
                        return default(int);
                    }

                    [FluentMethodTemplate]
                    public static int Number(in Func<int> function)
                    {
                        return function();
                    }

                    [FluentMethodTemplate]
                    public static int Number(in Func<string, int> function, in string value)
                    {
                        return function(value);
                    }
                }

                public class AsMethods
                {
                    [FluentMethodTemplate]
                    public static T As<T>()
                    {
                        return default(T);
                    }

                    [FluentMethodTemplate]
                    public static T As<T>(in Func<T> function)
                    {
                        return function();
                    }

                    [FluentMethodTemplate]
                    public static T As<T>(in Func<string, T> function, in string value)
                    {
                        return function(value);
                    }
                }
            }
            """;

        const string expected =
            """
            using System;

            namespace Test.Namespace
            {
                public static partial class Factory
                {
                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTargetA{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Namespace_Factory Number()
                    {
                        return new Step_0__Test_Namespace_Factory(NumberMethods.Number());
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTargetA{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Namespace_Factory Number(in System.Func<int> function)
                    {
                        return new Step_0__Test_Namespace_Factory(NumberMethods.Number(function));
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTargetA{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Namespace_Factory Number(in System.Func<string, int> function, in string value)
                    {
                        return new Step_0__Test_Namespace_Factory(NumberMethods.Number(function, value));
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.Namespace.MyBuildTargetA{T}"/>
                /// </summary>
                public struct Step_0__Test_Namespace_Factory
                {
                    private readonly int _number__parameter;
                    internal Step_0__Test_Namespace_Factory(in int number)
                    {
                        this._number__parameter = number;
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTargetA{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__Test_Namespace_Factory<T> As<T>()
                    {
                        return new Step_1__Test_Namespace_Factory<T>(this._number__parameter, AsMethods.As<T>());
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTargetA{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__Test_Namespace_Factory<T> As<T>(in System.Func<T> function)
                    {
                        return new Step_1__Test_Namespace_Factory<T>(this._number__parameter, AsMethods.As<T>(function));
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTargetA{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__Test_Namespace_Factory<T> As<T>(in System.Func<string, T> function, in string value)
                    {
                        return new Step_1__Test_Namespace_Factory<T>(this._number__parameter, AsMethods.As<T>(function, value));
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.Namespace.MyBuildTargetA{T}"/>
                /// </summary>
                public struct Step_1__Test_Namespace_Factory<T>
                {
                    private readonly int _number__parameter;
                    private readonly T _data__parameter;
                    internal Step_1__Test_Namespace_Factory(in int number, in T data)
                    {
                        this._number__parameter = number;
                        this._data__parameter = data;
                    }

                    /// <summary>
                    /// Creates a new instance using constructor Test.Namespace.MyBuildTargetA<T>.MyBuildTargetA(int number, T data).
                    ///
                    ///     <seealso cref="Test.Namespace.MyBuildTargetA{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTargetA<T> Create()
                    {
                        return new MyBuildTargetA<T>(this._number__parameter, this._data__parameter);
                    }
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { code },
                GeneratedSources =
                {
                    (typeof(FluentFactoryGenerator), "Test.Namespace.Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_ensure_that_generic_parameters_used_inside_multiple_fluent_methods_are_converted_to_those_used_in_the_fluent_constructor()
    {
        const string code =
            """
            using System;
            using System.Threading.Tasks;
            using Motiv.Generator;

            namespace Test.Namespace
            {
                [FluentFactory]
                public static partial class Factory;

                public class MyBuildTarget<T>
                {
                    [FluentConstructor(typeof(Factory))]
                    public MyBuildTarget(
                        [MultipleFluentMethods(typeof(MethodVariants))]Task<T> data)
                    {
                        Data = data;
                    }

                    public Task<T> Data { get; set; }
                }

                public class MethodVariants
                {
                    [FluentMethodTemplate]
                    public static Task<TAlternative> WithValue<TAlternative>(in Task<TAlternative> value)
                    {
                        return value;
                    }

                    [FluentMethodTemplate]
                    public static Task<TAlternative> WithFunction<TAlternative>(in Func<Task<TAlternative>, Task<TAlternative>> function, Task<TAlternative> defaultTask)
                    {
                        return function(defaultTask);
                    }
                }
            }
            """;

        const string expected =
            """
            using System.Threading.Tasks;

            namespace Test.Namespace
            {
                public static partial class Factory
                {
                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Namespace_Factory<T> WithValue<T>(in System.Threading.Tasks.Task<T> value)
                    {
                        return new Step_0__Test_Namespace_Factory<T>(MethodVariants.WithValue<T>(value));
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Namespace_Factory<T> WithFunction<T>(in System.Func<System.Threading.Tasks.Task<T>, System.Threading.Tasks.Task<T>> function, in System.Threading.Tasks.Task<T> defaultTask)
                    {
                        return new Step_0__Test_Namespace_Factory<T>(MethodVariants.WithFunction<T>(function, defaultTask));
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.Namespace.MyBuildTarget{T}"/>
                /// </summary>
                public struct Step_0__Test_Namespace_Factory<T>
                {
                    private readonly System.Threading.Tasks.Task<T> _data__parameter;
                    internal Step_0__Test_Namespace_Factory(in System.Threading.Tasks.Task<T> data)
                    {
                        this._data__parameter = data;
                    }

                    /// <summary>
                    /// Creates a new instance using constructor Test.Namespace.MyBuildTarget<T>.MyBuildTarget(System.Threading.Tasks.Task<T> data).
                    ///
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T> Create()
                    {
                        return new MyBuildTarget<T>(this._data__parameter);
                    }
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { code },
                GeneratedSources =
                {
                    (typeof(FluentFactoryGenerator), "Test.Namespace.Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task
        Should_support_multiple_methods_containing_overload_methods_where_a_concrete_generic_argument_type_is_used()
    {
        const string code =
            """
            using System;
            using Motiv.Generator;

            namespace Test.Namespace
            {
                [FluentFactory]
                public static partial class Factory;

                public class MyBuildTarget<T1, T2>
                {
                    [FluentConstructor(typeof(Factory))]
                    public MyBuildTarget(
                        [MultipleFluentMethods(typeof(FirstMethods))]T1 first,
                        [MultipleFluentMethods(typeof(SecondMethods))]T2 second)
                    {
                        First = first;
                        Second = second;
                    }

                    public T1 First { get; set; }
                    public T2 Second { get; set; }
                }

                public class FirstMethods
                {
                    [FluentMethodTemplate]
                    public static TX SetFirst<TX>(in Func<TX> function)
                    {
                        return function();
                    }

                    [FluentMethodTemplate]
                    public static int SetFirst(in Func<int> function)
                    {
                        return function();
                    }

                    [FluentMethodTemplate]
                    public static string SetFirst(in Func<string> function)
                    {
                        return function();
                    }
                }

                public class SecondMethods
                {
                    [FluentMethodTemplate]
                    public static TX SetSecond<TX>(in Func<TX> function)
                    {
                        return function();
                    }

                    [FluentMethodTemplate]
                    public static int SetSecond(in Func<int> function)
                    {
                        return function();
                    }

                    [FluentMethodTemplate]
                    public static string SetSecond(in Func<string> function)
                    {
                        return function();
                    }
                }
            }
            """;

        const string expected =
            """
            namespace Test.Namespace
            {
                public static partial class Factory
                {
                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Namespace_Factory<T1> SetFirst<T1>(in System.Func<T1> function)
                    {
                        return new Step_0__Test_Namespace_Factory<T1>(FirstMethods.SetFirst<T1>(function));
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Namespace_Factory<int> SetFirst(in System.Func<int> function)
                    {
                        return new Step_0__Test_Namespace_Factory<int>(FirstMethods.SetFirst(function));
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Namespace_Factory<string> SetFirst(in System.Func<string> function)
                    {
                        return new Step_0__Test_Namespace_Factory<string>(FirstMethods.SetFirst(function));
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.Namespace.MyBuildTarget{T1, T2}"/>
                /// </summary>
                public struct Step_0__Test_Namespace_Factory<T1>
                {
                    private readonly T1 _first__parameter;
                    internal Step_0__Test_Namespace_Factory(in T1 first)
                    {
                        this._first__parameter = first;
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__Test_Namespace_Factory<T1, T2> SetSecond<T2>(in System.Func<T2> function)
                    {
                        return new Step_1__Test_Namespace_Factory<T1, T2>(this._first__parameter, SecondMethods.SetSecond<T2>(function));
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__Test_Namespace_Factory<T1, int> SetSecond(in System.Func<int> function)
                    {
                        return new Step_1__Test_Namespace_Factory<T1, int>(this._first__parameter, SecondMethods.SetSecond(function));
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__Test_Namespace_Factory<T1, string> SetSecond(in System.Func<string> function)
                    {
                        return new Step_1__Test_Namespace_Factory<T1, string>(this._first__parameter, SecondMethods.SetSecond(function));
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.Namespace.MyBuildTarget{T1, T2}"/>
                /// </summary>
                public struct Step_1__Test_Namespace_Factory<T1, T2>
                {
                    private readonly T1 _first__parameter;
                    private readonly T2 _second__parameter;
                    internal Step_1__Test_Namespace_Factory(in T1 first, in T2 second)
                    {
                        this._first__parameter = first;
                        this._second__parameter = second;
                    }

                    /// <summary>
                    /// Creates a new instance using constructor Test.Namespace.MyBuildTarget<T1, T2>.MyBuildTarget(T1 first, T2 second).
                    ///
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T1, T2> Create()
                    {
                        return new MyBuildTarget<T1, T2>(this._first__parameter, this._second__parameter);
                    }
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { code },
                GeneratedSources =
                {
                    (typeof(FluentFactoryGenerator), "Test.Namespace.Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Given_no_create_method_Should_support_multiple_methods_containing_overload_methods_where_a_concrete_generic_argument_type_is_used()
    {
        const string code =
            """
            using System;
            using System.Collections.Generic;
            using Motiv.Generator;

            namespace Test.Namespace
            {
                [FluentFactory]
                public static partial class Factory;

                public class MyBuildTarget<T1, T2>
                {
                    [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                    public MyBuildTarget(
                        [MultipleFluentMethods(typeof(FirstMethods))]T1 first,
                        [MultipleFluentMethods(typeof(SecondMethods))]T2 second)
                    {
                        First = first;
                        Second = second;
                    }

                    public T1 First { get; set; }
                    public T2 Second { get; set; }
                }

                public class FirstMethods
                {
                    [FluentMethodTemplate]
                    public static TX SetFirst<TX>(in Func<TX> function)
                    {
                        return function();
                    }

                    [FluentMethodTemplate]
                    public static int SetFirst(in Func<int> function)
                    {
                        return function();
                    }

                    [FluentMethodTemplate]
                    public static Func<TX2, TX1> SetFirst<TX1, TX2>()
                    {
                        return _ => default(TX1);
                    }
                }

                public class SecondMethods
                {
                    [FluentMethodTemplate]
                    public static TY SetSecond<TY>(in Func<TY> function)
                    {
                        return function();
                    }

                    [FluentMethodTemplate]
                    public static int SetSecond(in Func<int> function)
                    {
                        return function();
                    }

                    [FluentMethodTemplate]
                    public static Func<TY2, TY1> SetSecond<TY1, TY2>()
                    {
                        return _ => default(TY1);
                    }
                }
            }
            """;

        const string expected =
            """
            namespace Test.Namespace
            {
                public static partial class Factory
                {
                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Namespace_Factory<T1> SetFirst<T1>(in System.Func<T1> function)
                    {
                        return new Step_0__Test_Namespace_Factory<T1>(FirstMethods.SetFirst<T1>(function));
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Namespace_Factory<int> SetFirst(in System.Func<int> function)
                    {
                        return new Step_0__Test_Namespace_Factory<int>(FirstMethods.SetFirst(function));
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Namespace_Factory<System.Func<TX2, TX1>> SetFirst<TX1, TX2>()
                    {
                        return new Step_0__Test_Namespace_Factory<System.Func<TX2, TX1>>(FirstMethods.SetFirst<TX1, TX2>());
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.Namespace.MyBuildTarget{T1, T2}"/>
                /// </summary>
                public struct Step_0__Test_Namespace_Factory<T1>
                {
                    private readonly T1 _first__parameter;
                    internal Step_0__Test_Namespace_Factory(in T1 first)
                    {
                        this._first__parameter = first;
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T1, T2> SetSecond<T2>(in System.Func<T2> function)
                    {
                        return new MyBuildTarget<T1, T2>(this._first__parameter, SecondMethods.SetSecond<T2>(function));
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T1, int> SetSecond(in System.Func<int> function)
                    {
                        return new MyBuildTarget<T1, int>(this._first__parameter, SecondMethods.SetSecond(function));
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T1, System.Func<TY2, TY1>> SetSecond<TY1, TY2>()
                    {
                        return new MyBuildTarget<T1, System.Func<TY2, TY1>>(this._first__parameter, SecondMethods.SetSecond<TY1, TY2>());
                    }
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { code },
                GeneratedSources =
                {
                    (typeof(FluentFactoryGenerator), "Test.Namespace.Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Given_an_multimethod_class_with_varying_number_of_deeply_nested_generic_type_parameters_Should_match_deeply_nested_genric_expressions()
    {
        const string code =
            """
            using System;
            using System.Collections.Generic;
            using Motiv.Generator;

            namespace Test.Namespace
            {
                [FluentFactory]
                public static partial class Factory;

                public class MyBuildTarget<T1, T2>
                {
                    [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                    public MyBuildTarget(
                        [MultipleFluentMethods(typeof(Overloads))]Func<KeyValuePair<T1, T2>> first,
                        string second)
                    {
                        First = first;
                        Second = second;
                    }

                    public Func<KeyValuePair<T1, T2>> First { get; set; }
                    public string Second { get; set; }
                }

                internal static class Overloads
                {

                    [FluentMethodTemplate]
                    internal static Func<KeyValuePair<T1, T2>> Build<T1, T2>(Func<KeyValuePair<T1, T2>> resultFactory) => resultFactory;


                    [FluentMethodTemplate]
                    internal static Func<KeyValuePair<T1, string>> Build<T1>(Func<KeyValuePair<T1, string>> resultFactory) => resultFactory;
                }
            }
            """;

        const string expected =
            """
            using System;

            namespace Test.Namespace
            {
                public static partial class Factory
                {
                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Namespace_Factory<T1, T2> Build<T1, T2>(in System.Func<System.Collections.Generic.KeyValuePair<T1, T2>> resultFactory)
                    {
                        return new Step_0__Test_Namespace_Factory<T1, T2>(Overloads.Build<T1, T2>(resultFactory));
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Namespace_Factory<T1, string> Build<T1>(in System.Func<System.Collections.Generic.KeyValuePair<T1, string>> resultFactory)
                    {
                        return new Step_0__Test_Namespace_Factory<T1, string>(Overloads.Build<T1>(resultFactory));
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.Namespace.MyBuildTarget{T1, T2}"/>
                /// </summary>
                public struct Step_0__Test_Namespace_Factory<T1, T2>
                {
                    private readonly System.Func<System.Collections.Generic.KeyValuePair<T1, T2>> _first__parameter;
                    internal Step_0__Test_Namespace_Factory(in System.Func<System.Collections.Generic.KeyValuePair<T1, T2>> first)
                    {
                        this._first__parameter = first;
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T1, T2> WithSecond(in string second)
                    {
                        return new MyBuildTarget<T1, T2>(this._first__parameter, second);
                    }
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { code },
                GeneratedSources =
                {
                    (typeof(FluentFactoryGenerator), "Test.Namespace.Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Given_an_existing_fluent_step_Should_handle_compatible_yet_different_generic_parameters()
    {
        const string code =
            """
            using System;
            using System.Collections.Generic;
            using Motiv.Generator;

            namespace Test.Namespace
            {
                [FluentFactory]
                public static partial class Factory;

                public class MyBuildTarget<T1, T2>
                {
                    [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                    public MyBuildTarget(
                        [MultipleFluentMethods(typeof(Overloads))]Func<T1, IEnumerable<int>, T2> first,
                        string second)
                    {
                        First = first;
                        Second = second;
                    }

                    public Func<T1, IEnumerable<int>, T2> First { get; set; }
                    public string Second { get; set; }
                }

                internal static class Overloads
                {
                    [FluentMethodTemplate]
                    internal static Func<T1, TAbstraction, T2> Build<T1, T2, TAbstraction>(Func<T1, TAbstraction, T2> firstFactory) => firstFactory;

                    [FluentMethodTemplate]
                    internal static Func<T1, TAbstraction, string> Build<T1, TAbstraction>(Func<T1, TAbstraction, string> secondFactory) => secondFactory;
                }
            }
            """;

        const string expected =
            """
            using System;

            namespace Test.Namespace
            {
                public static partial class Factory
                {
                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Namespace_Factory<T1, T2> Build<T1, T2>(in System.Func<T1, System.Collections.Generic.IEnumerable<int>, T2> firstFactory)
                    {
                        return new Step_0__Test_Namespace_Factory<T1, T2>(Overloads.Build<T1, T2, System.Collections.Generic.IEnumerable<int>>(firstFactory));
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Namespace_Factory<T1, string> Build<T1>(in System.Func<T1, System.Collections.Generic.IEnumerable<int>, string> secondFactory)
                    {
                        return new Step_0__Test_Namespace_Factory<T1, string>(Overloads.Build<T1, System.Collections.Generic.IEnumerable<int>>(secondFactory));
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.Namespace.MyBuildTarget{T1, T2}"/>
                /// </summary>
                public struct Step_0__Test_Namespace_Factory<T1, T2>
                {
                    private readonly System.Func<T1, System.Collections.Generic.IEnumerable<int>, T2> _first__parameter;
                    internal Step_0__Test_Namespace_Factory(in System.Func<T1, System.Collections.Generic.IEnumerable<int>, T2> first)
                    {
                        this._first__parameter = first;
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T1, T2> WithSecond(in string second)
                    {
                        return new MyBuildTarget<T1, T2>(this._first__parameter, second);
                    }
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { code },
                GeneratedSources =
                {
                    (typeof(FluentFactoryGenerator), "Test.Namespace.Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Given_a_method_conversion_class_Should_handle_compatible_yet_different_generic_parameters()
    {
        const string code =
            """
            using System;
            using System.Collections.Generic;
            using Motiv.Generator;

            namespace Test.Namespace
            {
                [FluentFactory]
                public static partial class Factory;

                public partial class MyBuildTarget
                {
                    [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                    public MyBuildTarget(
                        string first)
                    {
                        First = first;
                    }

                    public string First { get; set; }
                }

                public class MyBuildTarget<T1, T2>
                {
                    [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                    public MyBuildTarget(
                        string first,
                        [MultipleFluentMethods(typeof(Overloads))]Func<T1, IEnumerable<int>, T2> second,
                        string third)
                    {
                        First = first;
                        Second = second;
                        Third = third;
                    }

                    public string First { get; set; }
                    public Func<T1, IEnumerable<int>, T2> Second { get; set; }
                    public string Third { get; set; }
                }

                internal static class Overloads
                {
                    // Should be ignored
                    [FluentMethodTemplate]
                    internal static string Build<T>(string ignoreThis) => ignoreThis;

                    [FluentMethodTemplate]
                    internal static Func<T1, TAbstraction, T2> Build<T1, T2, TAbstraction>(Func<T1, TAbstraction, T2> firstFactory) => firstFactory;

                    [FluentMethodTemplate]
                    internal static Func<T1, TAbstraction, string> Build<T1, TAbstraction>(Func<T1, TAbstraction, string> secondFactory) => secondFactory;
                }
            }
            """;

        const string expected =
            """
            using System;

            namespace Test.Namespace
            {
                public static partial class Factory
                {
                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget"/>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static MyBuildTarget WithFirst(in string first)
                    {
                        return new MyBuildTarget(first);
                    }
                }

                public partial class MyBuildTarget
                {
                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__Test_Namespace_Factory<T1, T2> Build<T1, T2>(in System.Func<T1, System.Collections.Generic.IEnumerable<int>, T2> firstFactory)
                    {
                        return new Step_1__Test_Namespace_Factory<T1, T2>(this.First, Overloads.Build<T1, T2, System.Collections.Generic.IEnumerable<int>>(firstFactory));
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__Test_Namespace_Factory<T1, string> Build<T1>(in System.Func<T1, System.Collections.Generic.IEnumerable<int>, string> secondFactory)
                    {
                        return new Step_1__Test_Namespace_Factory<T1, string>(this.First, Overloads.Build<T1, System.Collections.Generic.IEnumerable<int>>(secondFactory));
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.Namespace.MyBuildTarget{T1, T2}"/>
                /// </summary>
                public struct Step_1__Test_Namespace_Factory<T1, T2>
                {
                    private readonly string _first__parameter;
                    private readonly System.Func<T1, System.Collections.Generic.IEnumerable<int>, T2> _second__parameter;
                    internal Step_1__Test_Namespace_Factory(in string first, in System.Func<T1, System.Collections.Generic.IEnumerable<int>, T2> second)
                    {
                        this._first__parameter = first;
                        this._second__parameter = second;
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T1, T2> WithThird(in string third)
                    {
                        return new MyBuildTarget<T1, T2>(this._first__parameter, this._second__parameter, third);
                    }
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { (SourceFile, code) },
                GeneratedSources =
                {
                    (typeof(FluentFactoryGenerator), "Test.Namespace.Factory.g.cs", expected)
                },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(IncompatibleFluentMethodTemplate.Id, DiagnosticSeverity.Warning)
                        .WithSpan(SourceFile, 27, 36, 27, 53)
                        .WithSpan(SourceFile, 44, 32, 44, 37)
                        .WithArguments(
                            "string Test.Namespace.Overloads.Build<T>(string ignoreThis)",
                            "System.Func<T1, System.Collections.Generic.IEnumerable<int>, T2> second")
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Given_overloaded_multiple_methods_that_differ_by_a_open_and_closed_generic_types_When_transforming_template_method_causes_signature_clashes_Should_ignore_open_generic()
    {
        const string code =
            """
            using System;
            using System.Collections.Generic;
            using Motiv.Generator;
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
                internal static Func<TModel, TResult, IEnumerable<TNewMetadata>> WhenFalseYield<TModel, TResult, TNewMetadata>(Func<TModel, TResult, IEnumerable<TNewMetadata>> function)
                {
                    return function;
                }

                [FluentMethodTemplate]
                internal static Func<TModel, TResult, IEnumerable<string>> WhenFalseYield<TModel, TResult>(Func<TModel, TResult, IEnumerable<string>> function)
                {
                    return function;
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
                    ///     <seealso cref="MyNamespace.MyTypeA{TModel, TPredicateResult}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__MyNamespace_Spec<TModel, TPredicateResult> From<TModel, TPredicateResult>(in System.Linq.Expressions.Expression<System.Func<TModel, TPredicateResult>> expression)
                    {
                        return new Step_0__MyNamespace_Spec<TModel, TPredicateResult>(expression);
                    }
                }

                /// <summary>
                ///     <seealso cref="MyNamespace.MyTypeA{TModel, TPredicateResult}"/>
                /// </summary>
                public struct Step_0__MyNamespace_Spec<TModel, TPredicateResult>
                {
                    private readonly System.Linq.Expressions.Expression<System.Func<TModel, TPredicateResult>> _expression__parameter;
                    internal Step_0__MyNamespace_Spec(in System.Linq.Expressions.Expression<System.Func<TModel, TPredicateResult>> expression)
                    {
                        this._expression__parameter = expression;
                    }

                    /// <summary>
                    ///     <seealso cref="MyNamespace.MyTypeA{TModel, TPredicateResult}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__MyNamespace_Spec<TModel, TPredicateResult> WhenTrueYield(in System.Func<TModel, BooleanResultBase<string>, System.Collections.Generic.IEnumerable<string>> whenTrue)
                    {
                        return new Step_1__MyNamespace_Spec<TModel, TPredicateResult>(this._expression__parameter, WhenTrueYieldOverloads.WhenTrueYield<TModel, string, BooleanResultBase<string>>(whenTrue));
                    }
                }

                /// <summary>
                ///     <seealso cref="MyNamespace.MyTypeA{TModel, TPredicateResult}"/>
                /// </summary>
                public struct Step_1__MyNamespace_Spec<TModel, TPredicateResult>
                {
                    private readonly System.Linq.Expressions.Expression<System.Func<TModel, TPredicateResult>> _expression__parameter;
                    private readonly System.Func<TModel, BooleanResultBase<string>, System.Collections.Generic.IEnumerable<string>> _trueBecause__parameter;
                    internal Step_1__MyNamespace_Spec(in System.Linq.Expressions.Expression<System.Func<TModel, TPredicateResult>> expression, in System.Func<TModel, BooleanResultBase<string>, System.Collections.Generic.IEnumerable<string>> trueBecause)
                    {
                        this._expression__parameter = expression;
                        this._trueBecause__parameter = trueBecause;
                    }

                    /// <summary>
                    ///     <seealso cref="MyNamespace.MyTypeA{TModel, TPredicateResult}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyTypeA<TModel, TPredicateResult> WhenFalseYield(in System.Func<TModel, BooleanResultBase<string>, System.Collections.Generic.IEnumerable<string>> function)
                    {
                        return new MyTypeA<TModel, TPredicateResult>(this._expression__parameter, this._trueBecause__parameter, WhenFalseYieldOverloads.WhenFalseYield<TModel, BooleanResultBase<string>, string>(function));
                    }
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { code },
                GeneratedSources =
                {
                    (typeof(FluentFactoryGenerator), "MyNamespace.Spec.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public Task Given_that_the_multiple_method_type_is_nested_within_the_fluent_constructors_generic_containing_type_Should_use_type_parameters_of_parent()
    {
        const string code =
            """
            using System;
            using System.Collections.Generic;
            using Motiv.Generator;
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
                    internal static Func<TModel, TResult, TNewMetadata> WhenTrue<TModel, TResult, TNewMetadata>(Func<TModel, TResult, TNewMetadata> whenTrue)
                    {
                        return whenTrue;
                    }
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
                    ///     <seealso cref="MyNamespace.MyTypeB{TModel, TPredicateResult}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static MyTypeB<TModel, TPredicateResult> WhenTrue<TModel, TPredicateResult>(in System.Func<TModel, BooleanResultBase<string>, string> whenTrue)
                    {
                        return new MyTypeB<TModel, TPredicateResult>(MyTypeB<TModel, TPredicateResult>.WhenTrueOverloads.WhenTrue<TModel, BooleanResultBase<string>, string>(whenTrue));
                    }
                }
            }
            """;

        return new VerifyCS.Test
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
}
