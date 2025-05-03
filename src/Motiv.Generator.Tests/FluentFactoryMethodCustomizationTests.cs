using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Motiv.Generator.FluentFactory;
using static Motiv.Generator.FluentFactory.MotivDiagnosticDescriptor;
using VerifyCS =
    Motiv.Generator.Tests.CSharpSourceGeneratorVerifier<Motiv.Generator.FluentFactory.FluentFactoryGenerator>;

namespace Motiv.Generator.Tests;

public class FluentFactoryMethodCustomizationTests
{
    private const string SourceFile = "Source.cs";

    [Fact]
    public async Task Should_generate_custom_step_when_applied_to_a_class_constructor_with_a_single_parameter()
    {
        const string code =
            """
            using Motiv.Generator.Attributes;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            public class MyBuildTarget<T>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyBuildTarget([FluentMethod("SetValue")]T value)
                {
                    Value = value;
                }

                public T Value { get; set; }
            }
            """;

        const string expected =
            """
            namespace Test
            {
                public static partial class Factory
                {
                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static MyBuildTarget<T> SetValue<T>(in T value)
                    {
                        return new MyBuildTarget<T>(value);
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
                    (typeof(FluentFactoryGenerator), "Test.Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_generate_custom_step_when_using_generic_nested_types_on_a_delegate()
    {
        const string code =
            """
            using Motiv.Generator.Attributes;
            using System;
            using System.Collections.Generic;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
            public class MyBuildTarget<T1, T2, T3>(
                int x,
                [MultipleFluentMethods(typeof(Methods))]Func<IEnumerable<T1>, T2, T3> function)
            {

                public Func<IEnumerable<T1>, T2, T3> Value { get; set; } = function;
            }

            public static class Methods
            {
                [FluentMethodTemplate]
                public static Func<IEnumerable<T1>, T2, T3> SetValue<T1, T2, T3>(Func<IEnumerable<T1>, T2, T3> function)
                {
                    return function;
                }

                [FluentMethodTemplate]
                public static Func<IEnumerable<T1>, T2, T3> SetValue<T1, T2, T3>(T3 value)
                {
                    return (_, _) => value;
                }
            }
            """;

        const string expected =
            """
            using System;

            namespace Test
            {
                public static partial class Factory
                {
                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static MyBuildTarget<T1, T2, T3> SetValue<T1, T2, T3>(in System.Func<System.Collections.Generic.IEnumerable<T1>, T2, T3> function)
                    {
                        return new MyBuildTarget<T1, T2, T3>(Methods.SetValue<T1, T2, T3>(function));
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static MyBuildTarget<T1, T2, T3> SetValue<T1, T2, T3>(in T3 value)
                    {
                        return new MyBuildTarget<T1, T2, T3>(Methods.SetValue<T1, T2, T3>(value));
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
                    (typeof(FluentFactoryGenerator), "Test.Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_generate_custom_step_when_using_generic_nested_types_on_an_interface()
    {
        const string code =
            """
            using Motiv.Generator.Attributes;
            using System;
            using System.Collections.Generic;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
            public class MyBuildTarget<T>([MultipleFluentMethods(typeof(Converter))]IEnumerable<IEnumerable<T>> function)
            {
                public IEnumerable<IEnumerable<T>> Value { get; set; } = function;
            }

            public static class Converter
            {
                [FluentMethodTemplate]
                public static IEnumerable<IEnumerable<T>> SetValue<T>(IEnumerable<IEnumerable<T>> value)
                {
                    return value;
                }

                [FluentMethodTemplate]
                public static IEnumerable<IEnumerable<T>> SetValue<T>(T value)
                {
                    return [[value]];
                }
            }
            """;

        const string expected =
            """
            using System.Collections.Generic;

            namespace Test
            {
                public static partial class Factory
                {
                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static MyBuildTarget<T> SetValue<T>(in System.Collections.Generic.IEnumerable<System.Collections.Generic.IEnumerable<T>> value)
                    {
                        return new MyBuildTarget<T>(Converter.SetValue<T>(value));
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static MyBuildTarget<T> SetValue<T>(in T value)
                    {
                        return new MyBuildTarget<T>(Converter.SetValue<T>(value));
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
                    (typeof(FluentFactoryGenerator), "Test.Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_generate_custom_step_when_applied_to_a_class_constructor_with_two_parameters()
    {
        const string code =
            """
            using Motiv.Generator.Attributes;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            public class MyBuildTarget<T1, T2>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyBuildTarget(
                    [FluentMethod("SetValue1")]T1 value1,
                    [FluentMethod("SetValue2")]T2 value2)
                {
                    Value1 = value1;
                    Value2 = value2;
                }

                public T1 Value1 { get; set; }

                public T2 Value2 { get; set; }
            }
            """;

        const string expected =
            """
            namespace Test
            {
                public static partial class Factory
                {
                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory<T1> SetValue1<T1>(in T1 value1)
                    {
                        return new Step_0__Test_Factory<T1>(value1);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="Test.MyBuildTarget{T1, T2}"/>
                /// </summary>
                public struct Step_0__Test_Factory<T1>
                {
                    private readonly T1 _value1__parameter;
                    public Step_0__Test_Factory(in T1 value1)
                    {
                        this._value1__parameter = value1;
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T1, T2> SetValue2<T2>(in T2 value2)
                    {
                        return new MyBuildTarget<T1, T2>(this._value1__parameter, value2);
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
                    (typeof(FluentFactoryGenerator), "Test.Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_generate_custom_step_when_applied_to_a_class_constructor_with_three_parameters()
    {
        const string code =
            """
            using Motiv.Generator.Attributes;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            public class MyBuildTarget<T1, T2, T3>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyBuildTarget(
                    [FluentMethod("SetValue1")]T1 value1,
                    [FluentMethod("SetValue2")]T2 value2,
                    [FluentMethod("SetValue3")]T3 value3)
                {
                    Value1 = value1;
                    Value2 = value2;
                    Value3 = value3;
                }

                public T1 Value1 { get; set; }

                public T2 Value2 { get; set; }

                public T3 Value3 { get; set; }
            }
            """;

        const string expected =
            """
            namespace Test
            {
                public static partial class Factory
                {
                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory<T1> SetValue1<T1>(in T1 value1)
                    {
                        return new Step_0__Test_Factory<T1>(value1);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3}"/>
                /// </summary>
                public struct Step_0__Test_Factory<T1>
                {
                    private readonly T1 _value1__parameter;
                    public Step_0__Test_Factory(in T1 value1)
                    {
                        this._value1__parameter = value1;
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__Test_Factory<T1, T2> SetValue2<T2>(in T2 value2)
                    {
                        return new Step_1__Test_Factory<T1, T2>(this._value1__parameter, value2);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3}"/>
                /// </summary>
                public struct Step_1__Test_Factory<T1, T2>
                {
                    private readonly T1 _value1__parameter;
                    private readonly T2 _value2__parameter;
                    public Step_1__Test_Factory(in T1 value1, in T2 value2)
                    {
                        this._value1__parameter = value1;
                        this._value2__parameter = value2;
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T1, T2, T3> SetValue3<T3>(in T3 value3)
                    {
                        return new MyBuildTarget<T1, T2, T3>(this._value1__parameter, this._value2__parameter, value3);
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
                    (typeof(FluentFactoryGenerator), "Test.Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_generate_custom_step_when_applied_to_a_class_constructor_with_four_parameters()
    {
        const string code =
            """
            using Motiv.Generator.Attributes;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            public class MyBuildTarget<T1, T2, T3, T4>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyBuildTarget(
                    [FluentMethod("SetValue1")]T1 value1,
                    [FluentMethod("SetValue2")]T2 value2,
                    [FluentMethod("SetValue3")]T3 value3,
                    [FluentMethod("SetValue4")]T4 value4)
                {
                    Value1 = value1;
                    Value2 = value2;
                    Value3 = value3;
                    Value4 = value4;
                }

                public T1 Value1 { get; set; }

                public T2 Value2 { get; set; }

                public T3 Value3 { get; set; }

                public T4 Value4 { get; set; }
            }
            """;

        const string expected =
            """
            namespace Test
            {
                public static partial class Factory
                {
                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3, T4}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory<T1> SetValue1<T1>(in T1 value1)
                    {
                        return new Step_0__Test_Factory<T1>(value1);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3, T4}"/>
                /// </summary>
                public struct Step_0__Test_Factory<T1>
                {
                    private readonly T1 _value1__parameter;
                    public Step_0__Test_Factory(in T1 value1)
                    {
                        this._value1__parameter = value1;
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3, T4}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__Test_Factory<T1, T2> SetValue2<T2>(in T2 value2)
                    {
                        return new Step_1__Test_Factory<T1, T2>(this._value1__parameter, value2);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3, T4}"/>
                /// </summary>
                public struct Step_1__Test_Factory<T1, T2>
                {
                    private readonly T1 _value1__parameter;
                    private readonly T2 _value2__parameter;
                    public Step_1__Test_Factory(in T1 value1, in T2 value2)
                    {
                        this._value1__parameter = value1;
                        this._value2__parameter = value2;
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3, T4}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_2__Test_Factory<T1, T2, T3> SetValue3<T3>(in T3 value3)
                    {
                        return new Step_2__Test_Factory<T1, T2, T3>(this._value1__parameter, this._value2__parameter, value3);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3, T4}"/>
                /// </summary>
                public struct Step_2__Test_Factory<T1, T2, T3>
                {
                    private readonly T1 _value1__parameter;
                    private readonly T2 _value2__parameter;
                    private readonly T3 _value3__parameter;
                    public Step_2__Test_Factory(in T1 value1, in T2 value2, in T3 value3)
                    {
                        this._value1__parameter = value1;
                        this._value2__parameter = value2;
                        this._value3__parameter = value3;
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3, T4}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T1, T2, T3, T4> SetValue4<T4>(in T4 value4)
                    {
                        return new MyBuildTarget<T1, T2, T3, T4>(this._value1__parameter, this._value2__parameter, this._value3__parameter, value4);
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
                    (typeof(FluentFactoryGenerator), "Test.Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_add_overloaded_methods()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            [FluentFactory]
            public static partial class Factory;

            public class MyClass<T>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyClass([MultipleFluentMethods(typeof(Overloads))]T value)
                {
                    Value = value;
                }

                public T Value { get; set; }
            }

            public static class Overloads
            {
                [FluentMethodTemplate]
                public static T Create<T>(T value)
                {
                    return value;
                }

                [FluentMethodTemplate]
                public static T Create<T>(Func<T> factory)
                {
                    return factory();
                }
            }

            """;

        const string expected =
            """
            public static partial class Factory
            {
                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass{T}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public static MyClass<T> Create<T>(in T value)
                {
                    return new MyClass<T>(Overloads.Create<T>(value));
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass{T}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public static MyClass<T> Create<T>(in System.Func<T> factory)
                {
                    return new MyClass<T>(Overloads.Create<T>(factory));
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
                    (typeof(FluentFactoryGenerator), "Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_add_overloaded_methods_containing_multiple_parameters()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            [FluentFactory]
            public static partial class Factory;

            public class MyClass<T>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyClass([MultipleFluentMethods(typeof(Overloads))]T value)
                {
                    Value = value;
                }

                public T Value { get; set; }
            }

            public static class Overloads
            {
                [FluentMethodTemplate]
                public static T Create<T>(T value)
                {
                    return value;
                }

                [FluentMethodTemplate]
                public static T Create<T>(Func<T> factory, string value)
                {
                    return factory();
                }
            }

            """;

        const string expected =
            """
            public static partial class Factory
            {
                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass{T}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public static MyClass<T> Create<T>(in T value)
                {
                    return new MyClass<T>(Overloads.Create<T>(value));
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass{T}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public static MyClass<T> Create<T>(in System.Func<T> factory, in string value)
                {
                    return new MyClass<T>(Overloads.Create<T>(factory, value));
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
                    (typeof(FluentFactoryGenerator), "Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_apply_a_generic_converter_to_concrete_types()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            [FluentFactory]
            public static partial class Factory;

            public class MyClass
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyClass(
                    [MultipleFluentMethods(typeof(Value1Methods))]string value1,
                    [MultipleFluentMethods(typeof(CreateMethods))]string value2)
                {
                    Value1 = value1;
                    Value2 = value2;
                }

                public string Value1 { get; set; }
                public string Value2 { get; set; }
            }

            public static class Value1Methods
            {
                [FluentMethodTemplate]
                public static T Value1<T>(T value)
                {
                    return value;
                }

                [FluentMethodTemplate]
                public static T Value1<T>(Func<T> factory)
                {
                    return factory();
                }
            }

            public static class CreateMethods
            {
                [FluentMethodTemplate]
                public static T Create<T>(T value)
                {
                    return value;
                }

                [FluentMethodTemplate]
                public static T Create<T>(Func<T> factory)
                {
                    return factory();
                }
            }

            """;

        const string expected =
            """
            using System;

            public static partial class Factory
            {
                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public static Step_0__Factory Value1(in string value)
                {
                    return new Step_0__Factory(Value1Methods.Value1<string>(value));
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public static Step_0__Factory Value1(in System.Func<string> factory)
                {
                    return new Step_0__Factory(Value1Methods.Value1<string>(factory));
                }
            }

            /// <summary>
            /// Candidate constructor types:
            ///     <seealso cref="MyClass"/>
            /// </summary>
            public struct Step_0__Factory
            {
                private readonly string _value1__parameter;
                public Step_0__Factory(in string value1)
                {
                    this._value1__parameter = value1;
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public MyClass Create(in string value)
                {
                    return new MyClass(this._value1__parameter, CreateMethods.Create<string>(value));
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public MyClass Create(in System.Func<string> factory)
                {
                    return new MyClass(this._value1__parameter, CreateMethods.Create<string>(factory));
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
                    (typeof(FluentFactoryGenerator), "Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_correctly_assign_generic_arguments_of_overloaded_methods_using_a_two_step_fluent_factory()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            [FluentFactory]
            public static partial class Factory;

            public class MyClass<T>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyClass(
                    [MultipleFluentMethods(typeof(Value1Methods))]T value1,
                    [MultipleFluentMethods(typeof(CreateMethods))]T value2)
                {
                    Value1 = value1;
                    Value2 = value2;
                }

                public T Value1 { get; set; }
                public T Value2 { get; set; }
            }

            public static class Value1Methods
            {
                [FluentMethodTemplate]
                public static T Value1<T>(T value)
                {
                    return value;
                }

                [FluentMethodTemplate]
                public static T Value1<T>(Func<T> factory)
                {
                    return factory();
                }
            }

            public static class CreateMethods
            {
                [FluentMethodTemplate]
                public static T Create<T>(T value)
                {
                    return value;
                }

                [FluentMethodTemplate]
                public static T Create<T>(Func<T> factory)
                {
                    return factory();
                }
            }

            """;

        const string expected =
            """
            public static partial class Factory
            {
                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass{T}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public static Step_0__Factory<T> Value1<T>(in T value)
                {
                    return new Step_0__Factory<T>(Value1Methods.Value1<T>(value));
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass{T}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public static Step_0__Factory<T> Value1<T>(in System.Func<T> factory)
                {
                    return new Step_0__Factory<T>(Value1Methods.Value1<T>(factory));
                }
            }

            /// <summary>
            /// Candidate constructor types:
            ///     <seealso cref="MyClass{T}"/>
            /// </summary>
            public struct Step_0__Factory<T>
            {
                private readonly T _value1__parameter;
                public Step_0__Factory(in T value1)
                {
                    this._value1__parameter = value1;
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass{T}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public MyClass<T> Create(in T value)
                {
                    return new MyClass<T>(this._value1__parameter, CreateMethods.Create<T>(value));
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass{T}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public MyClass<T> Create(in System.Func<T> factory)
                {
                    return new MyClass<T>(this._value1__parameter, CreateMethods.Create<T>(factory));
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
                    (typeof(FluentFactoryGenerator), "Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_correctly_assign_generic_arguments_of_overloaded_methods_using_a_three_step_fluent_factory()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            [FluentFactory]
            public static partial class Factory;

            public class MyClass<T1, T2, T3>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyClass(
                    [MultipleFluentMethods(typeof(Value1Methods))]T1 value1,
                    [MultipleFluentMethods(typeof(Value2Methods))]T2 value2,
                    [MultipleFluentMethods(typeof(CreateMethods))]T3 value3)
                {
                    Value1 = value1;
                    Value2 = value2;
                    Value3 = value3;
                }

                public T1 Value1 { get; set; }
                public T2 Value2 { get; set; }
                public T3 Value3 { get; set; }
            }

            public static class Value1Methods
            {
                [FluentMethodTemplate]
                public static T Value1<T>(T value)
                {
                    return value;
                }

                [FluentMethodTemplate]
                public static TResult Value1<TResult>(Func<string, string, TResult> function)
                {
                    // We are not interested in the string values - just that
                    // the handling of generic arguments is correct
                    return function("arbitrary-constant-string", "arbitrary-constant-string");
                }
            }

            public static class Value2Methods
            {
                [FluentMethodTemplate]
                public static T Value2<T>(T value)
                {
                    return value;
                }

                [FluentMethodTemplate]
                public static TResult Value2<TResult>(Func<string, string, TResult> function)
                {
                    // We are not interested in the string values - just that
                    // the handling of generic arguments is correct
                    return function("arbitrary-constant-string", "arbitrary-constant-string");
                }
            }

            public static class CreateMethods
            {
                [FluentMethodTemplate]
                public static T Create<T>(T value)
                {
                    return value;
                }

                [FluentMethodTemplate]
                public static TResult Create<TResult>(Func<string, string, TResult> function)
                {
                    // We are not interested in the string values - just that
                    // the handling of generic arguments is correct
                    return function("arbitrary-constant-string", "arbitrary-constant-string");
                }
            }
            """;

        const string expected =
            """
            public static partial class Factory
            {
                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass{T1, T2, T3}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public static Step_0__Factory<T1> Value1<T1>(in T1 value)
                {
                    return new Step_0__Factory<T1>(Value1Methods.Value1<T1>(value));
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass{T1, T2, T3}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public static Step_0__Factory<T1> Value1<T1>(in System.Func<string, string, T1> function)
                {
                    return new Step_0__Factory<T1>(Value1Methods.Value1<T1>(function));
                }
            }

            /// <summary>
            /// Candidate constructor types:
            ///     <seealso cref="MyClass{T1, T2, T3}"/>
            /// </summary>
            public struct Step_0__Factory<T1>
            {
                private readonly T1 _value1__parameter;
                public Step_0__Factory(in T1 value1)
                {
                    this._value1__parameter = value1;
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass{T1, T2, T3}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public Step_1__Factory<T1, T2> Value2<T2>(in T2 value)
                {
                    return new Step_1__Factory<T1, T2>(this._value1__parameter, Value2Methods.Value2<T2>(value));
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass{T1, T2, T3}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public Step_1__Factory<T1, T2> Value2<T2>(in System.Func<string, string, T2> function)
                {
                    return new Step_1__Factory<T1, T2>(this._value1__parameter, Value2Methods.Value2<T2>(function));
                }
            }

            /// <summary>
            /// Candidate constructor types:
            ///     <seealso cref="MyClass{T1, T2, T3}"/>
            /// </summary>
            public struct Step_1__Factory<T1, T2>
            {
                private readonly T1 _value1__parameter;
                private readonly T2 _value2__parameter;
                public Step_1__Factory(in T1 value1, in T2 value2)
                {
                    this._value1__parameter = value1;
                    this._value2__parameter = value2;
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass{T1, T2, T3}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public MyClass<T1, T2, T3> Create<T3>(in T3 value)
                {
                    return new MyClass<T1, T2, T3>(this._value1__parameter, this._value2__parameter, CreateMethods.Create<T3>(value));
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass{T1, T2, T3}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public MyClass<T1, T2, T3> Create<T3>(in System.Func<string, string, T3> function)
                {
                    return new MyClass<T1, T2, T3>(this._value1__parameter, this._value2__parameter, CreateMethods.Create<T3>(function));
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
                    (typeof(FluentFactoryGenerator), "Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_apply_a_generic_converter_to_paramters_that_are_themselves_generic_types()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            [FluentFactory]
            public static partial class Factory;

            public class MyClass<T1A, T1B>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyClass(
                    [MultipleFluentMethods(typeof(Value1Methods))]Func<T1A, T1B> factory)
                {
                    Factory = factory;
                }

                public Func<T1A, T1B> Factory { get; set; }
            }

            public static class Value1Methods
            {
                [FluentMethodTemplate]
                public static Func<T1, T2> Value1<T1, T2>(Func<T1, T2> value)
                {
                    return value;
                }

                [FluentMethodTemplate]
                public static Func<T1, T2> Value1<T1, T2>(T2 value)
                {
                    return _ => value;
                }
            }
            """;

        const string expected =
            """
            using System;

            public static partial class Factory
            {
                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass{T1A, T1B}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public static MyClass<T1A, T1B> Value1<T1A, T1B>(in System.Func<T1A, T1B> value)
                {
                    return new MyClass<T1A, T1B>(Value1Methods.Value1<T1A, T1B>(value));
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass{T1A, T1B}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public static MyClass<T1A, T1B> Value1<T1A, T1B>(in T1B value)
                {
                    return new MyClass<T1A, T1B>(Value1Methods.Value1<T1A, T1B>(value));
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
                    (typeof(FluentFactoryGenerator), "Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_apply_a_generic_converter_to_parameters_that_are_themselves_generic_types_when_there_are_two_constructor_parameters()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            [FluentFactory]
            public static partial class Factory;

            public class MyClass<T1A, T1B, T2A, T2B, T3A, T3B>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyClass(
                    [MultipleFluentMethods(typeof(Value1Methods))]Func<T1A, T1B> factory1,
                    [MultipleFluentMethods(typeof(Value2Methods))]Func<T2A, T2B> factory2,
                    [MultipleFluentMethods(typeof(Value3Methods))]Func<T3A, T3B> factory3)
                {
                    Factory1 = factory1;
                    Factory2 = factory2;
                    Factory3 = factory3;
                }

                public Func<T1A, T1B> Factory1 { get; set; }
                public Func<T2A, T2B> Factory2 { get; set; }
                public Func<T3A, T3B> Factory3 { get; set; }
            }

            public static class Value1Methods
            {
                [FluentMethodTemplate]
                public static Func<TX1, TX2> Value1<TX1, TX2>(Func<TX1, TX2> value)
                {
                    return value;
                }

                [FluentMethodTemplate]
                public static Func<TX1, TX2> Value1<TX1, TX2>(TX2 value)
                {
                    return _ => value;
                }
            }

            public static class Value2Methods
            {
                [FluentMethodTemplate]
                public static Func<TY1, TY2> Value2<TY1, TY2>(Func<TY1, TY2> value)
                {
                    return value;
                }

                [FluentMethodTemplate]
                public static Func<TY1, TY2> Value2<TY1, TY2>(TY2 value)
                {
                    return _ => value;
                }
            }

            public static class Value3Methods
            {
                [FluentMethodTemplate]
                public static Func<TY1, TY2> Value3<TY1, TY2>(Func<TY1, TY2> value)
                {
                    return value;
                }

                [FluentMethodTemplate]
                public static Func<TY1, TY2> Value3<TY1, TY2>(TY2 value)
                {
                    return _ => value;
                }
            }
            """;

        const string expected =
            """
            using System;

            public static partial class Factory
            {
                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass{T1A, T1B, T2A, T2B, T3A, T3B}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public static Step_0__Factory<T1A, T1B> Value1<T1A, T1B>(in System.Func<T1A, T1B> value)
                {
                    return new Step_0__Factory<T1A, T1B>(Value1Methods.Value1<T1A, T1B>(value));
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass{T1A, T1B, T2A, T2B, T3A, T3B}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public static Step_0__Factory<T1A, T1B> Value1<T1A, T1B>(in T1B value)
                {
                    return new Step_0__Factory<T1A, T1B>(Value1Methods.Value1<T1A, T1B>(value));
                }
            }

            /// <summary>
            /// Candidate constructor types:
            ///     <seealso cref="MyClass{T1A, T1B, T2A, T2B, T3A, T3B}"/>
            /// </summary>
            public struct Step_0__Factory<T1A, T1B>
            {
                private readonly System.Func<T1A, T1B> _factory1__parameter;
                public Step_0__Factory(in System.Func<T1A, T1B> factory1)
                {
                    this._factory1__parameter = factory1;
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass{T1A, T1B, T2A, T2B, T3A, T3B}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public Step_1__Factory<T1A, T1B, T2A, T2B> Value2<T2A, T2B>(in System.Func<T2A, T2B> value)
                {
                    return new Step_1__Factory<T1A, T1B, T2A, T2B>(this._factory1__parameter, Value2Methods.Value2<T2A, T2B>(value));
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass{T1A, T1B, T2A, T2B, T3A, T3B}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public Step_1__Factory<T1A, T1B, T2A, T2B> Value2<T2A, T2B>(in T2B value)
                {
                    return new Step_1__Factory<T1A, T1B, T2A, T2B>(this._factory1__parameter, Value2Methods.Value2<T2A, T2B>(value));
                }
            }

            /// <summary>
            /// Candidate constructor types:
            ///     <seealso cref="MyClass{T1A, T1B, T2A, T2B, T3A, T3B}"/>
            /// </summary>
            public struct Step_1__Factory<T1A, T1B, T2A, T2B>
            {
                private readonly System.Func<T1A, T1B> _factory1__parameter;
                private readonly System.Func<T2A, T2B> _factory2__parameter;
                public Step_1__Factory(in System.Func<T1A, T1B> factory1, in System.Func<T2A, T2B> factory2)
                {
                    this._factory1__parameter = factory1;
                    this._factory2__parameter = factory2;
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass{T1A, T1B, T2A, T2B, T3A, T3B}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public MyClass<T1A, T1B, T2A, T2B, T3A, T3B> Value3<T3A, T3B>(in System.Func<T3A, T3B> value)
                {
                    return new MyClass<T1A, T1B, T2A, T2B, T3A, T3B>(this._factory1__parameter, this._factory2__parameter, Value3Methods.Value3<T3A, T3B>(value));
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass{T1A, T1B, T2A, T2B, T3A, T3B}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public MyClass<T1A, T1B, T2A, T2B, T3A, T3B> Value3<T3A, T3B>(in T3B value)
                {
                    return new MyClass<T1A, T1B, T2A, T2B, T3A, T3B>(this._factory1__parameter, this._factory2__parameter, Value3Methods.Value3<T3A, T3B>(value));
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
                    (typeof(FluentFactoryGenerator), "Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_apply_a_generic_converter_to_parameters_that_are_themselves_generic_types_when_there_are_three_constructor_parameters_with_interleaved_generic_parameters()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            [FluentFactory]
            public static partial class Factory;

            public class MyClass<T1, T2, T3, T4>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyClass(
                    [MultipleFluentMethods(typeof(Value1Methods))]Func<T1, T2> factory1,
                    [MultipleFluentMethods(typeof(Value2Methods))]Func<T2, T3> factory2,
                    [MultipleFluentMethods(typeof(Value3Methods))]Func<T3, T4> factory3)
                {
                    Factory1 = factory1;
                    Factory2 = factory2;
                    Factory3 = factory3;
                }

                public Func<T1, T2> Factory1 { get; set; }
                public Func<T2, T3> Factory2 { get; set; }
                public Func<T3, T4> Factory3 { get; set; }
            }

            public static class Value1Methods
            {
                [FluentMethodTemplate]
                public static Func<TX1, TX2> Value1<TX1, TX2>(Func<TX1, TX2> value)
                {
                    return value;
                }

                [FluentMethodTemplate]
                public static Func<TX1, TX2> Value1<TX1, TX2>(TX2 value)
                {
                    return _ => value;
                }
            }

            public static class Value2Methods
            {
                [FluentMethodTemplate]
                public static Func<TY1, TY2> Value2<TY1, TY2>(Func<TY1, TY2> value)
                {
                    return value;
                }

                [FluentMethodTemplate]
                public static Func<TY1, TY2> Value2<TY1, TY2>(TY2 value)
                {
                    return _ => value;
                }
            }

            public static class Value3Methods
            {
                [FluentMethodTemplate]
                public static Func<TZ1, TZ2> Value3<TZ1, TZ2>(Func<TZ1, TZ2> value)
                {
                    return value;
                }

                [FluentMethodTemplate]
                public static Func<TZ1, TZ2> Value3<TZ1, TZ2>(TZ2 value)
                {
                    return _ => value;
                }
            }
            """;

        const string expected =
            """
            using System;

            public static partial class Factory
            {
                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass{T1, T2, T3, T4}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public static Step_0__Factory<T1, T2> Value1<T1, T2>(in System.Func<T1, T2> value)
                {
                    return new Step_0__Factory<T1, T2>(Value1Methods.Value1<T1, T2>(value));
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass{T1, T2, T3, T4}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public static Step_0__Factory<T1, T2> Value1<T1, T2>(in T2 value)
                {
                    return new Step_0__Factory<T1, T2>(Value1Methods.Value1<T1, T2>(value));
                }
            }

            /// <summary>
            /// Candidate constructor types:
            ///     <seealso cref="MyClass{T1, T2, T3, T4}"/>
            /// </summary>
            public struct Step_0__Factory<T1, T2>
            {
                private readonly System.Func<T1, T2> _factory1__parameter;
                public Step_0__Factory(in System.Func<T1, T2> factory1)
                {
                    this._factory1__parameter = factory1;
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass{T1, T2, T3, T4}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public Step_1__Factory<T1, T2, T3> Value2<T3>(in System.Func<T2, T3> value)
                {
                    return new Step_1__Factory<T1, T2, T3>(this._factory1__parameter, Value2Methods.Value2<T2, T3>(value));
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass{T1, T2, T3, T4}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public Step_1__Factory<T1, T2, T3> Value2<T3>(in T3 value)
                {
                    return new Step_1__Factory<T1, T2, T3>(this._factory1__parameter, Value2Methods.Value2<T2, T3>(value));
                }
            }

            /// <summary>
            /// Candidate constructor types:
            ///     <seealso cref="MyClass{T1, T2, T3, T4}"/>
            /// </summary>
            public struct Step_1__Factory<T1, T2, T3>
            {
                private readonly System.Func<T1, T2> _factory1__parameter;
                private readonly System.Func<T2, T3> _factory2__parameter;
                public Step_1__Factory(in System.Func<T1, T2> factory1, in System.Func<T2, T3> factory2)
                {
                    this._factory1__parameter = factory1;
                    this._factory2__parameter = factory2;
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass{T1, T2, T3, T4}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public MyClass<T1, T2, T3, T4> Value3<T4>(in System.Func<T3, T4> value)
                {
                    return new MyClass<T1, T2, T3, T4>(this._factory1__parameter, this._factory2__parameter, Value3Methods.Value3<T3, T4>(value));
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass{T1, T2, T3, T4}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public MyClass<T1, T2, T3, T4> Value3<T4>(in T4 value)
                {
                    return new MyClass<T1, T2, T3, T4>(this._factory1__parameter, this._factory2__parameter, Value3Methods.Value3<T3, T4>(value));
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
                    (typeof(FluentFactoryGenerator), "Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Given_multiple_constructors_Should_not_require_generic_methods_parameters_When_satisfied_by_earlier_steps()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            [FluentFactory]
            public static partial class Factory;

            public class MyClassA<T1, T2>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyClassA(
                    [MultipleFluentMethods(typeof(Value1Methods))]Func<T1, T2> factory1,
                    [MultipleFluentMethods(typeof(MyClassACreateMethods))]Func<T1, T2, string> factory2)
                {
                    Factory1 = factory1;
                    Factory2 = factory2;
                }

                public Func<T1, T2> Factory1 { get; set; }
                public Func<T1, T2, string> Factory2 { get; set; }
            }

            public class MyClassB<T1, T2>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyClassB(
                    [MultipleFluentMethods(typeof(Value1Methods))]Func<T1, T2> factory1,
                    [MultipleFluentMethods(typeof(MyClassBCreateMethods))]Func<T1, T2, int> factory2)
                {
                    Factory1 = factory1;
                    Factory2 = factory2;
                }

                public Func<T1, T2> Factory1 { get; set; }
                public Func<T1, T2, int> Factory2 { get; set; }
            }

            public static class Value1Methods
            {
                [FluentMethodTemplate]
                public static Func<T1, T2> Value1<T1, T2>(Func<T1, T2> value)
                {
                    return value;
                }

                [FluentMethodTemplate]
                public static Func<T1, T2> Value1<T1, T2>(T2 value)
                {
                    return _ => value;
                }
            }

            public static class MyClassACreateMethods
            {
                [FluentMethodTemplate]
                public static Func<T1, T2, string> CreateString<T1, T2>(Func<T1, T2, string> value)
                {
                    return value;
                }

                [FluentMethodTemplate]
                public static Func<T1, T2, string> CreateString<T1, T2>(string value)
                {
                    return (_, _) => value;
                }
            }

            public static class MyClassBCreateMethods
            {
                [FluentMethodTemplate]
                public static Func<T1, T2, int> CreateInt<T1, T2>(Func<T1, T2, int> value)
                {
                    return value;
                }

                [FluentMethodTemplate]
                public static Func<T1, T2, int> CreateInt<T1, T2>(int value)
                {
                    return (_, _) => value;
                }
            }
            """;

        const string expected =
            """
            using System;

            public static partial class Factory
            {
                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClassA{T1, T2}"/>
                ///     <seealso cref="MyClassB{T1, T2}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public static Step_0__Factory<T1, T2> Value1<T1, T2>(in System.Func<T1, T2> value)
                {
                    return new Step_0__Factory<T1, T2>(Value1Methods.Value1<T1, T2>(value));
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClassA{T1, T2}"/>
                ///     <seealso cref="MyClassB{T1, T2}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public static Step_0__Factory<T1, T2> Value1<T1, T2>(in T2 value)
                {
                    return new Step_0__Factory<T1, T2>(Value1Methods.Value1<T1, T2>(value));
                }
            }

            /// <summary>
            /// Candidate constructor types:
            ///     <seealso cref="MyClassA{T1, T2}"/>
            ///     <seealso cref="MyClassB{T1, T2}"/>
            /// </summary>
            public struct Step_0__Factory<T1, T2>
            {
                private readonly System.Func<T1, T2> _factory1__parameter;
                public Step_0__Factory(in System.Func<T1, T2> factory1)
                {
                    this._factory1__parameter = factory1;
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClassA{T1, T2}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public MyClassA<T1, T2> CreateString(in System.Func<T1, T2, string> value)
                {
                    return new MyClassA<T1, T2>(this._factory1__parameter, MyClassACreateMethods.CreateString<T1, T2>(value));
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClassA{T1, T2}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public MyClassA<T1, T2> CreateString(in string value)
                {
                    return new MyClassA<T1, T2>(this._factory1__parameter, MyClassACreateMethods.CreateString<T1, T2>(value));
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClassB{T1, T2}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public MyClassB<T1, T2> CreateInt(in System.Func<T1, T2, int> value)
                {
                    return new MyClassB<T1, T2>(this._factory1__parameter, MyClassBCreateMethods.CreateInt<T1, T2>(value));
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClassB{T1, T2}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public MyClassB<T1, T2> CreateInt(in int value)
                {
                    return new MyClassB<T1, T2>(this._factory1__parameter, MyClassBCreateMethods.CreateInt<T1, T2>(value));
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
                    (typeof(FluentFactoryGenerator), "Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Given_multiple_converter_methods_exist_Should_not_apply_converters_if_not_requested_by_first_class()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            [FluentFactory]
            public static partial class Factory;

            public class MyClassA<T1, T2>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyClassA(
                    [FluentMethod("Value")]Func<T1, T2> factory1,
                    [FluentMethod("Value")]Func<T1, T2, string> factory2)
                {
                    Factory1 = factory1;
                    Factory2 = factory2;
                }

                public Func<T1, T2> Factory1 { get; set; }
                public Func<T1, T2, string> Factory2 { get; set; }
            }

            public class MyClassB<T1, T2>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyClassB(
                    [MultipleFluentMethods(typeof(Overloads))]Func<T1, T2> factory1,
                    [MultipleFluentMethods(typeof(Overloads))]Func<T1, T2, int> factory2)
                {
                    Factory1 = factory1;
                    Factory2 = factory2;
                }

                public Func<T1, T2> Factory1 { get; set; }
                public Func<T1, T2, int> Factory2 { get; set; }
            }

            public static class Overloads
            {
                [FluentMethodTemplate]
                public static Func<T1, T2> Value<T1, T2>(Func<T1, T2> value)
                {
                    return value;
                }

                [FluentMethodTemplate]
                public static Func<T1, T2> Value<T1, T2>(T2 value)
                {
                    return _ => value;
                }

                [FluentMethodTemplate]
                public static Func<T1, T2, T3> Value<T1, T2, T3>(T3 value)
                {
                    return (_, _) => value;
                }
            }
            """;

        const string expected =
            """
            using System;

            public static partial class Factory
            {
                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClassA{T1, T2}"/>
                ///     <seealso cref="MyClassB{T1, T2}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public static Step_0__Factory<T1, T2> Value<T1, T2>(in System.Func<T1, T2> factory1)
                {
                    return new Step_0__Factory<T1, T2>(factory1);
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClassA{T1, T2}"/>
                ///     <seealso cref="MyClassB{T1, T2}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public static Step_0__Factory<T1, T2> Value<T1, T2>(in T2 value)
                {
                    return new Step_0__Factory<T1, T2>(Overloads.Value<T1, T2>(value));
                }
            }

            /// <summary>
            /// Candidate constructor types:
            ///     <seealso cref="MyClassA{T1, T2}"/>
            ///     <seealso cref="MyClassB{T1, T2}"/>
            /// </summary>
            public struct Step_0__Factory<T1, T2>
            {
                private readonly System.Func<T1, T2> _factory1__parameter;
                public Step_0__Factory(in System.Func<T1, T2> factory1)
                {
                    this._factory1__parameter = factory1;
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClassA{T1, T2}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public MyClassA<T1, T2> Value(in System.Func<T1, T2, string> factory2)
                {
                    return new MyClassA<T1, T2>(this._factory1__parameter, factory2);
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClassB{T1, T2}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public MyClassB<T1, T2> Value(in int value)
                {
                    return new MyClassB<T1, T2>(this._factory1__parameter, Overloads.Value<T1, T2, int>(value));
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
                    (typeof(FluentFactoryGenerator), "Factory.g.cs", expected)
                },
                ExpectedDiagnostics =
                {
                    DiagnosticResult.CompilerWarning("MOTIV002")
                        .WithSpan(SourceFile, 26, 32, 26, 49)
                        .WithSpan(SourceFile, 11, 45, 11, 53)
                        .WithArguments(
                            "Overloads.Value<T1, T2>(System.Func<T1, T2> value)",
                            "System.Func<T1, T2> factory1", "MyClassB<T1, T2>.MyClassB(System.Func<T1, T2> factory1, System.Func<T1, T2, int> factory2)",
                            "the parameter 'System.Func<T1, T2> factory1' in the constructor 'MyClassA<T1, T2>.MyClassA(System.Func<T1, T2> factory1, System.Func<T1, T2, string> factory2)' was used as the basis for the fluent method. Perhaps the ignored method-template can be removed or modified."),
                    DiagnosticResult.CompilerWarning("MOTIV003")
                        .WithSpan(SourceFile, 26, 32, 26, 49)
                        .WithSpan(SourceFile, 52, 36, 52, 41)
                        .WithArguments(
                            "System.Func<T1, T2, T3> Overloads.Value<T1, T2, T3>(T3 value)",
                            "System.Func<T1, T2> factory1"),
                    DiagnosticResult.CompilerWarning("MOTIV003")
                        .WithSpan(SourceFile, 27, 32, 27, 49)
                        .WithSpan(SourceFile, 40, 32, 40, 37)
                        .WithArguments(
                            "System.Func<T1, T2> Overloads.Value<T1, T2>(System.Func<T1, T2> value)",
                            "System.Func<T1, T2, int> factory2"),
                    DiagnosticResult.CompilerWarning("MOTIV003")
                        .WithSpan(SourceFile, 27, 32, 27, 49)
                        .WithSpan(SourceFile, 46, 32, 46, 37)
                        .WithArguments(
                            "System.Func<T1, T2> Overloads.Value<T1, T2>(T2 value)",
                            "System.Func<T1, T2, int> factory2"),
                    new DiagnosticResult("MOTIV007", DiagnosticSeverity.Info)
                        .WithSpan(SourceFile, 40, 32, 40, 37)
                        .WithArguments(
                            "System.Func<T1, T2> Overloads.Value<T1, T2>(System.Func<T1, T2> value)",
                            "System.Func<T1, T2> factory1",
                            "MyClassB<T1, T2>.MyClassB(System.Func<T1, T2> factory1, System.Func<T1, T2, int> factory2)",
                            "System.Func<T1, T2> factory1",
                            "MyClassA<T1, T2>.MyClassA(System.Func<T1, T2> factory1, System.Func<T1, T2, string> factory2)")
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Given_multiple_converter_methods_exist_Should_not_apply_converters_if_not_requested_by_second_class()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            [FluentFactory]
            public static partial class Factory;

            public class MyClassA<T1, T2>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyClassA(
                    [MultipleFluentMethods(typeof(FirstStep))]Func<T1, T2> factory1,
                    [MultipleFluentMethods(typeof(SecondStep))]Func<T1, T2, string> factory2)
                {
                    Factory1 = factory1;
                    Factory2 = factory2;
                }

                public Func<T1, T2> Factory1 { get; set; }
                public Func<T1, T2, string> Factory2 { get; set; }
            }

            public class MyClassB<T1, T2>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyClassB(
                    [FluentMethod("Value1")]Func<T1, T2> factory1,
                    [FluentMethod("Create")]Func<T1, T2, int> factory2)
                {
                    Factory1 = factory1;
                    Factory2 = factory2;
                }

                public Func<T1, T2> Factory1 { get; set; }
                public Func<T1, T2, int> Factory2 { get; set; }
            }

            public static class FirstStep
            {
                [FluentMethodTemplate]
                public static Func<T1, T2> Value1<T1, T2>(Func<T1, T2> value)
                {
                    return value;
                }

                [FluentMethodTemplate]
                public static Func<T1, T2> Value1<T1, T2>(T2 value)
                {
                    return _ => value;
                }
            }

            public static class SecondStep
            {
                [FluentMethodTemplate]
                public static Func<T1, T2, string> Create<T1, T2>(Func<T1, T2, string> value)
                {
                    return value;
                }

                [FluentMethodTemplate]
                public static Func<T1, T2, T3> Create<T1, T2, T3>(T3 value)
                {
                    return (_, _) => value;
                }
            }
            """;

        const string expected =
            """
            using System;

            public static partial class Factory
            {
                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClassA{T1, T2}"/>
                ///     <seealso cref="MyClassB{T1, T2}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public static Step_0__Factory<T1, T2> Value1<T1, T2>(in System.Func<T1, T2> factory1)
                {
                    return new Step_0__Factory<T1, T2>(factory1);
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClassA{T1, T2}"/>
                ///     <seealso cref="MyClassB{T1, T2}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public static Step_0__Factory<T1, T2> Value1<T1, T2>(in T2 value)
                {
                    return new Step_0__Factory<T1, T2>(FirstStep.Value1<T1, T2>(value));
                }
            }

            /// <summary>
            /// Candidate constructor types:
            ///     <seealso cref="MyClassA{T1, T2}"/>
            ///     <seealso cref="MyClassB{T1, T2}"/>
            /// </summary>
            public struct Step_0__Factory<T1, T2>
            {
                private readonly System.Func<T1, T2> _factory1__parameter;
                public Step_0__Factory(in System.Func<T1, T2> factory1)
                {
                    this._factory1__parameter = factory1;
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClassA{T1, T2}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public MyClassA<T1, T2> Create(in System.Func<T1, T2, string> value)
                {
                    return new MyClassA<T1, T2>(this._factory1__parameter, SecondStep.Create<T1, T2>(value));
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClassA{T1, T2}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public MyClassA<T1, T2> Create(in string value)
                {
                    return new MyClassA<T1, T2>(this._factory1__parameter, SecondStep.Create<T1, T2, string>(value));
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClassB{T1, T2}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public MyClassB<T1, T2> Create(in System.Func<T1, T2, int> factory2)
                {
                    return new MyClassB<T1, T2>(this._factory1__parameter, factory2);
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
                    (typeof(FluentFactoryGenerator), "Factory.g.cs", expected)
                }
            },
            ExpectedDiagnostics =
            {
                DiagnosticResult.CompilerWarning(ContainsSupersededFluentMethodTemplate.Id)
                    .WithSpan(SourceFile, 11, 32, 11, 49)
                    .WithSpan(SourceFile, 11, 64, 11, 72)
                    .WithArguments(
                        "FirstStep.Value1<T1, T2>(System.Func<T1, T2> value)",
                        "System.Func<T1, T2> factory1",
                        "MyClassA<T1, T2>.MyClassA(System.Func<T1, T2> factory1, System.Func<T1, T2, string> factory2)",
                        "the parameter 'System.Func<T1, T2> factory1' in the constructor 'MyClassA<T1, T2>.MyClassA(System.Func<T1, T2> factory1, System.Func<T1, T2, string> factory2)' was used as the basis for the fluent method. Perhaps the ignored method-template can be removed or modified."),
                new DiagnosticResult(FluentMethodTemplateSuperseded.Id, DiagnosticSeverity.Info)
                    .WithSpan(SourceFile, 40, 32, 40, 38)
                    .WithArguments(
                        "System.Func<T1, T2> FirstStep.Value1<T1, T2>(System.Func<T1, T2> value)",
                        "System.Func<T1, T2> factory1",
                        "MyClassA<T1, T2>.MyClassA(System.Func<T1, T2> factory1, System.Func<T1, T2, string> factory2)",
                        "System.Func<T1, T2> factory1",
                        "MyClassA<T1, T2>.MyClassA(System.Func<T1, T2> factory1, System.Func<T1, T2, string> factory2)"),


            }
        }.RunAsync();
    }

    [Fact]
    public async Task Given_multiple_multiple_constructable_classes_Should_not_apply_converters_if_not_requested()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            [FluentFactory]
            public static partial class Factory;

            public class MyClassA<T1, T2>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyClassA(
                    [MultipleFluentMethods(typeof(Value1Methods))]Func<T1, T2> factory1,
                    [FluentMethod("Create")]Func<T1, T2, string> factory2)
                {
                    Factory1 = factory1;
                    Factory2 = factory2;
                }

                public Func<T1, T2> Factory1 { get; set; }
                public Func<T1, T2, string> Factory2 { get; set; }
            }

            public class MyClassB<T1, T2>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyClassB(
                    [MultipleFluentMethods(typeof(Value1Methods))]Func<T1, T2> factory1,
                    [MultipleFluentMethods(typeof(CreateMethods))]Func<T1, T2, int> factory2)
                {
                    Factory1 = factory1;
                    Factory2 = factory2;
                }

                public Func<T1, T2> Factory1 { get; set; }
                public Func<T1, T2, int> Factory2 { get; set; }
            }

            public static class Value1Methods
            {
                [FluentMethodTemplate]
                public static Func<T1, T2> Value1<T1, T2>(Func<T1, T2> value)
                {
                    return value;
                }

                [FluentMethodTemplate]
                public static Func<T1, T2> Value1<T1, T2>(T2 value)
                {
                    return _ => value;
                }
            }

            public static class CreateMethods
            {
                [FluentMethodTemplate]
                public static Func<T1, T2, T3> Create<T1, T2, T3>(Func<T1, T2, T3> value)
                {
                    return value;
                }

                [FluentMethodTemplate]
                public static Func<T1, T2, T3> Create<T1, T2, T3>(T3 value)
                {
                    return (_, _) => value;
                }
            }
            """;

        const string expected =
            """
            using System;

            public static partial class Factory
            {
                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClassA{T1, T2}"/>
                ///     <seealso cref="MyClassB{T1, T2}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public static Step_0__Factory<T1, T2> Value1<T1, T2>(in System.Func<T1, T2> value)
                {
                    return new Step_0__Factory<T1, T2>(Value1Methods.Value1<T1, T2>(value));
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClassA{T1, T2}"/>
                ///     <seealso cref="MyClassB{T1, T2}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public static Step_0__Factory<T1, T2> Value1<T1, T2>(in T2 value)
                {
                    return new Step_0__Factory<T1, T2>(Value1Methods.Value1<T1, T2>(value));
                }
            }

            /// <summary>
            /// Candidate constructor types:
            ///     <seealso cref="MyClassA{T1, T2}"/>
            ///     <seealso cref="MyClassB{T1, T2}"/>
            /// </summary>
            public struct Step_0__Factory<T1, T2>
            {
                private readonly System.Func<T1, T2> _factory1__parameter;
                public Step_0__Factory(in System.Func<T1, T2> factory1)
                {
                    this._factory1__parameter = factory1;
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClassA{T1, T2}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public MyClassA<T1, T2> Create(in System.Func<T1, T2, string> factory2)
                {
                    return new MyClassA<T1, T2>(this._factory1__parameter, factory2);
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClassB{T1, T2}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public MyClassB<T1, T2> Create(in System.Func<T1, T2, int> value)
                {
                    return new MyClassB<T1, T2>(this._factory1__parameter, CreateMethods.Create<T1, T2, int>(value));
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClassB{T1, T2}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public MyClassB<T1, T2> Create(in int value)
                {
                    return new MyClassB<T1, T2>(this._factory1__parameter, CreateMethods.Create<T1, T2, int>(value));
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
                    (typeof(FluentFactoryGenerator), "Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Given_duplicate_non_converter_and_converter_parameters_Should_afford_precedence_to_non_converter()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;


            [FluentFactory]
            public static partial class Factory;

            public class MyClassA<T>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyClassA(
                    [MultipleFluentMethods(typeof(Overloads))]Func<T> value1,
                    [FluentMethod("Create")]Func<T> value2)
                {
                    Value1 = value1;
                    Value2 = value2;
                }

                public Func<T> Value1 { get; set; }
                public Func<T> Value2 { get; set; }
            }

            public class MyClassB
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyClassB(
                    [FluentMethod("Value")]string value1,
                    [FluentMethod("Create")]Func<string> value2)
                {
                    Value1 = value1;
                    Value2 = value2;
                }

                public string Value1 { get; set; }
                public Func<string> Value2 { get; set; }
            }

            public class MyClassC
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyClassC(
                    [MultipleFluentMethods(typeof(Overloads))]Func<string> value1,
                    [FluentMethod("Create")]Func<int> value2)
                {
                    Value1 = value1;
                    Value2 = value2;
                }

                public Func<string> Value1 { get; set; }
                public Func<int> Value2 { get; set; }
            }

            public static class Overloads
            {
                [FluentMethodTemplate]
                public static Func<T> Value<T>(T value)
                {
                    return () => value;
                }

                [FluentMethodTemplate]
                public static Func<string> Value(string value)
                {
                    return () => value;
                }
            }
            """;

        const string expected =
            """
            using System;

            public static partial class Factory
            {
                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClassA{T}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public static Step_0__Factory<T> Value<T>(in T value)
                {
                    return new Step_0__Factory<T>(Overloads.Value<T>(value));
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClassB"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public static Step_1__Factory Value(in string value1)
                {
                    return new Step_1__Factory(value1);
                }
            }

            /// <summary>
            /// Candidate constructor types:
            ///     <seealso cref="MyClassA{T}"/>
            /// </summary>
            public struct Step_0__Factory<T>
            {
                private readonly System.Func<T> _value1__parameter;
                public Step_0__Factory(in System.Func<T> value1)
                {
                    this._value1__parameter = value1;
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClassA{T}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public MyClassA<T> Create(in System.Func<T> value2)
                {
                    return new MyClassA<T>(this._value1__parameter, value2);
                }
            }

            /// <summary>
            /// Candidate constructor types:
            ///     <seealso cref="MyClassB"/>
            /// </summary>
            public struct Step_1__Factory
            {
                private readonly string _value1__parameter;
                public Step_1__Factory(in string value1)
                {
                    this._value1__parameter = value1;
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClassB"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public MyClassB Create(in System.Func<string> value2)
                {
                    return new MyClassB(this._value1__parameter, value2);
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
                    (typeof(FluentFactoryGenerator), "Factory.g.cs", expected)
                },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(UnreachableConstructor.Id, DiagnosticSeverity.Error)
                        .WithSpan(SourceFile, 42, 64, 42, 70)
                        .WithSpan(SourceFile, 27, 39, 27, 45)
                        .WithArguments(
                            "MyClassC.MyClassC(System.Func<string> value1, System.Func<int> value2)",
                            "Value(string value1)",
                            "This involves the constructor parameter 'System.Func<string> value1'. The parameter value is obtained from the fluent-method template 'System.Func<string> Overloads.Value<string>(string value)'. The issue is with the fluent-method template 'System.Func<string> Overloads.Value<string>(string value)'. The clashing methods have differing return-types, which is caused by different constructor parameter types. Try removing or renaming the template method, or changing its signature"),
                    new DiagnosticResult(ContainsSupersededFluentMethodTemplate.Id, DiagnosticSeverity.Warning)
                        .WithSpan(SourceFile, 12, 32, 12, 49)
                        .WithSpan(SourceFile, 27, 39, 27, 45)
                        .WithArguments(
                            "Overloads.Value(string value)",
                            "System.Func<T> value1", "MyClassA<T>.MyClassA(System.Func<T> value1, System.Func<T> value2)",
                            "the parameter 'string value1' in the constructor 'MyClassB.MyClassB(string value1, System.Func<string> value2)' was used as the basis for the fluent method. Perhaps the ignored method-template can be removed or modified."),
                    new DiagnosticResult(FluentMethodTemplateSuperseded.Id, DiagnosticSeverity.Info)
                        .WithSpan(SourceFile, 62, 32, 62, 37)
                        .WithArguments(
                            "System.Func<string> Overloads.Value(string value)",
                            "System.Func<T> value1",
                            "MyClassA<T>.MyClassA(System.Func<T> value1, System.Func<T> value2)",
                            "string value1",
                            "MyClassB.MyClassB(string value1, System.Func<string> value2)")
                },
            }
        }.RunAsync();
    }




        [Fact]
    public async Task Given_that_a_template_method_has_additional_parameters_Should_include_additional_parameters_in_step_method()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            [FluentFactory]
            public static partial class Factory;

            public class MyClassA<T1, T2>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyClassA(
                    [MultipleFluentMethods(typeof(Value1Methods))]Func<T1, T2> factory
                {
                    Factory = factory;
                }

                public Func<T1, T2> Factory { get; set; }
            }

            public static class Value1Methods
            {
                [FluentMethodTemplate]
                public static Func<T1, T2> Value1<T1, T2>(Func<T1, T2> value)
                {
                    return value;
                }

                [FluentMethodTemplate]
                public static Func<T1, T2> Value1<T1, T2>(T2 value)
                {
                    return _ => value;
                }
            }
            """;

        const string expected =
            """
            using System;

            public static partial class Factory
            {
                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClassA{T1, T2}"/>
                ///     <seealso cref="MyClassB{T1, T2}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public static Step_0__Factory<T1, T2> Value1<T1, T2>(in System.Func<T1, T2> value)
                {
                    return new Step_0__Factory<T1, T2>(Value1Methods.Value1<T1, T2>(value));
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClassA{T1, T2}"/>
                ///     <seealso cref="MyClassB{T1, T2}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public static Step_0__Factory<T1, T2> Value1<T1, T2>(in T2 value)
                {
                    return new Step_0__Factory<T1, T2>(Value1Methods.Value1<T1, T2>(value));
                }
            }

            /// <summary>
            /// Candidate constructor types:
            ///     <seealso cref="MyClassA{T1, T2}"/>
            ///     <seealso cref="MyClassB{T1, T2}"/>
            /// </summary>
            public struct Step_0__Factory<T1, T2>
            {
                private readonly System.Func<T1, T2> _factory1__parameter;
                public Step_0__Factory(in System.Func<T1, T2> factory1)
                {
                    this._factory1__parameter = factory1;
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClassA{T1, T2}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public MyClassA<T1, T2> Create(in System.Func<T1, T2, string> factory2)
                {
                    return new MyClassA<T1, T2>(this._factory1__parameter, factory2);
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClassB{T1, T2}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public MyClassB<T1, T2> Create(in System.Func<T1, T2, int> value)
                {
                    return new MyClassB<T1, T2>(this._factory1__parameter, CreateMethods.Create<T1, T2, int>(value));
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClassB{T1, T2}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public MyClassB<T1, T2> Create(in int value)
                {
                    return new MyClassB<T1, T2>(this._factory1__parameter, CreateMethods.Create<T1, T2, int>(value));
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
                    (typeof(FluentFactoryGenerator), "Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }
}
