using VerifyCS =
    Motiv.FluentFactory.Generator.Tests.CSharpSourceGeneratorVerifier<Motiv.FluentFactory.Generator.FluentFactoryGenerator>;

namespace Motiv.FluentFactory.Generator.Tests;

public class FluentFactoryGeneratorGenericTests
{
    [Fact]
    public async Task Should_generate_when_applied_to_a_class_constructor_with_a_single_parameter()
    {
        const string code =
            """
            using Motiv.FluentFactory.Generator;

            namespace Test.Namespace
            {
                [FluentFactory]
                public static partial class Factory;

                public class MyBuildTarget<T>
                {
                    [FluentConstructor(typeof(Factory))]
                    public MyBuildTarget(T value)
                    {
                        Value = value;
                    }

                    public T Value { get; set; }
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
                        return new Step_0__Test_Namespace_Factory<T>(value);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.Namespace.MyBuildTarget{T}"/>
                /// </summary>
                public struct Step_0__Test_Namespace_Factory<T>
                {
                    private readonly T _value__parameter;
                    internal Step_0__Test_Namespace_Factory(in T value)
                    {
                        this._value__parameter = value;
                    }

                    /// <summary>
                    /// Creates a new instance using constructor Test.Namespace.MyBuildTarget<T>.MyBuildTarget(T value).
                    ///
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T> Create()
                    {
                        return new MyBuildTarget<T>(this._value__parameter);
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
    public async Task Should_generate_when_applied_to_a_class_constructor_with_two_parameters()
    {
        const string code =
            """
            using Motiv.FluentFactory.Generator;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            public class MyBuildTarget<T1, T2>
            {
                [FluentConstructor(typeof(Factory))]
                public MyBuildTarget(
                    T1 value1,
                    T2 value2)
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
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory<T1> WithValue1<T1>(in T1 value1)
                    {
                        return new Step_0__Test_Factory<T1>(value1);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.MyBuildTarget{T1, T2}"/>
                /// </summary>
                public struct Step_0__Test_Factory<T1>
                {
                    private readonly T1 _value1__parameter;
                    internal Step_0__Test_Factory(in T1 value1)
                    {
                        this._value1__parameter = value1;
                    }

                    /// <summary>
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__Test_Factory<T1, T2> WithValue2<T2>(in T2 value2)
                    {
                        return new Step_1__Test_Factory<T1, T2>(this._value1__parameter, value2);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.MyBuildTarget{T1, T2}"/>
                /// </summary>
                public struct Step_1__Test_Factory<T1, T2>
                {
                    private readonly T1 _value1__parameter;
                    private readonly T2 _value2__parameter;
                    internal Step_1__Test_Factory(in T1 value1, in T2 value2)
                    {
                        this._value1__parameter = value1;
                        this._value2__parameter = value2;
                    }

                    /// <summary>
                    /// Creates a new instance using constructor Test.MyBuildTarget<T1, T2>.MyBuildTarget(T1 value1, T2 value2).
                    ///
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T1, T2> Create()
                    {
                        return new MyBuildTarget<T1, T2>(this._value1__parameter, this._value2__parameter);
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
    public async Task Should_generate_when_applied_to_a_class_constructor_with_three_parameters()
    {
        const string code =
            """
            using Motiv.FluentFactory.Generator;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            public class MyBuildTarget<T1, T2, T3>
            {
                [FluentConstructor(typeof(Factory))]
                public MyBuildTarget(
                    T1 value1,
                    T2 value2,
                    T3 value3)
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
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory<T1> WithValue1<T1>(in T1 value1)
                    {
                        return new Step_0__Test_Factory<T1>(value1);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3}"/>
                /// </summary>
                public struct Step_0__Test_Factory<T1>
                {
                    private readonly T1 _value1__parameter;
                    internal Step_0__Test_Factory(in T1 value1)
                    {
                        this._value1__parameter = value1;
                    }

                    /// <summary>
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__Test_Factory<T1, T2> WithValue2<T2>(in T2 value2)
                    {
                        return new Step_1__Test_Factory<T1, T2>(this._value1__parameter, value2);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3}"/>
                /// </summary>
                public struct Step_1__Test_Factory<T1, T2>
                {
                    private readonly T1 _value1__parameter;
                    private readonly T2 _value2__parameter;
                    internal Step_1__Test_Factory(in T1 value1, in T2 value2)
                    {
                        this._value1__parameter = value1;
                        this._value2__parameter = value2;
                    }

                    /// <summary>
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_2__Test_Factory<T1, T2, T3> WithValue3<T3>(in T3 value3)
                    {
                        return new Step_2__Test_Factory<T1, T2, T3>(this._value1__parameter, this._value2__parameter, value3);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3}"/>
                /// </summary>
                public struct Step_2__Test_Factory<T1, T2, T3>
                {
                    private readonly T1 _value1__parameter;
                    private readonly T2 _value2__parameter;
                    private readonly T3 _value3__parameter;
                    internal Step_2__Test_Factory(in T1 value1, in T2 value2, in T3 value3)
                    {
                        this._value1__parameter = value1;
                        this._value2__parameter = value2;
                        this._value3__parameter = value3;
                    }

                    /// <summary>
                    /// Creates a new instance using constructor Test.MyBuildTarget<T1, T2, T3>.MyBuildTarget(T1 value1, T2 value2, T3 value3).
                    ///
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T1, T2, T3> Create()
                    {
                        return new MyBuildTarget<T1, T2, T3>(this._value1__parameter, this._value2__parameter, this._value3__parameter);
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
    public async Task Should_generate_when_applied_to_a_class_constructor_with_four_parameters()
    {
        const string code =
            """
            using Motiv.FluentFactory.Generator;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            public class MyBuildTarget<T1, T2, T3, T4>
            {
                [FluentConstructor(typeof(Factory))]
                public MyBuildTarget(
                    T1 value1,
                    T2 value2,
                    T3 value3,
                    T4 value4)
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
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3, T4}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory<T1> WithValue1<T1>(in T1 value1)
                    {
                        return new Step_0__Test_Factory<T1>(value1);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3, T4}"/>
                /// </summary>
                public struct Step_0__Test_Factory<T1>
                {
                    private readonly T1 _value1__parameter;
                    internal Step_0__Test_Factory(in T1 value1)
                    {
                        this._value1__parameter = value1;
                    }

                    /// <summary>
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3, T4}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__Test_Factory<T1, T2> WithValue2<T2>(in T2 value2)
                    {
                        return new Step_1__Test_Factory<T1, T2>(this._value1__parameter, value2);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3, T4}"/>
                /// </summary>
                public struct Step_1__Test_Factory<T1, T2>
                {
                    private readonly T1 _value1__parameter;
                    private readonly T2 _value2__parameter;
                    internal Step_1__Test_Factory(in T1 value1, in T2 value2)
                    {
                        this._value1__parameter = value1;
                        this._value2__parameter = value2;
                    }

                    /// <summary>
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3, T4}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_2__Test_Factory<T1, T2, T3> WithValue3<T3>(in T3 value3)
                    {
                        return new Step_2__Test_Factory<T1, T2, T3>(this._value1__parameter, this._value2__parameter, value3);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3, T4}"/>
                /// </summary>
                public struct Step_2__Test_Factory<T1, T2, T3>
                {
                    private readonly T1 _value1__parameter;
                    private readonly T2 _value2__parameter;
                    private readonly T3 _value3__parameter;
                    internal Step_2__Test_Factory(in T1 value1, in T2 value2, in T3 value3)
                    {
                        this._value1__parameter = value1;
                        this._value2__parameter = value2;
                        this._value3__parameter = value3;
                    }

                    /// <summary>
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3, T4}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_3__Test_Factory<T1, T2, T3, T4> WithValue4<T4>(in T4 value4)
                    {
                        return new Step_3__Test_Factory<T1, T2, T3, T4>(this._value1__parameter, this._value2__parameter, this._value3__parameter, value4);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3, T4}"/>
                /// </summary>
                public struct Step_3__Test_Factory<T1, T2, T3, T4>
                {
                    private readonly T1 _value1__parameter;
                    private readonly T2 _value2__parameter;
                    private readonly T3 _value3__parameter;
                    private readonly T4 _value4__parameter;
                    internal Step_3__Test_Factory(in T1 value1, in T2 value2, in T3 value3, in T4 value4)
                    {
                        this._value1__parameter = value1;
                        this._value2__parameter = value2;
                        this._value3__parameter = value3;
                        this._value4__parameter = value4;
                    }

                    /// <summary>
                    /// Creates a new instance using constructor Test.MyBuildTarget<T1, T2, T3, T4>.MyBuildTarget(T1 value1, T2 value2, T3 value3, T4 value4).
                    ///
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3, T4}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T1, T2, T3, T4> Create()
                    {
                        return new MyBuildTarget<T1, T2, T3, T4>(this._value1__parameter, this._value2__parameter, this._value3__parameter, this._value4__parameter);
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
    public async Task Should_generate_a_generic_root_fluent_factory_type_when_the_original_is_generic()
    {
        const string code =
            """
            using Motiv.FluentFactory.Generator;

            namespace Test.Namespace
            {
                [FluentFactory]
                public partial class Factory<T>;

                public class MyBuildTarget<T>
                {
                    [FluentConstructor(typeof(Factory<>))]
                    public MyBuildTarget(T value)
                    {
                        Value = value;
                    }

                    public T Value { get; set; }
                }
            }
            """;

        const string expected =
            """
            namespace Test.Namespace
            {
                public partial class Factory<T>
                {
                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Namespace_Factory____<T> WithValue(in T value)
                    {
                        return new Step_0__Test_Namespace_Factory____<T>(value);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.Namespace.MyBuildTarget{T}"/>
                /// </summary>
                public struct Step_0__Test_Namespace_Factory____<T>
                {
                    private readonly T _value__parameter;
                    internal Step_0__Test_Namespace_Factory____(in T value)
                    {
                        this._value__parameter = value;
                    }

                    /// <summary>
                    /// Creates a new instance using constructor Test.Namespace.MyBuildTarget<T>.MyBuildTarget(T value).
                    ///
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T> Create()
                    {
                        return new MyBuildTarget<T>(this._value__parameter);
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
                    (typeof(FluentFactoryGenerator), "Test.Namespace.Factory____.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_generate_a_generic_root_fluent_factory_type_when_the_original_is_generic_and_multiple_steps_exist()
    {
        const string code =
            """
            using Motiv.FluentFactory.Generator;

            namespace Test.Namespace
            {
                [FluentFactory]
                public partial class Factory<T>;

                public class MyBuildTarget<T, TAlt>
                {
                    [FluentConstructor(typeof(Factory<>))]
                    public MyBuildTarget(T value1, TAlt value2)
                    {
                        Value1 = value1;
                        Value2 = value2;
                    }

                    public T Value1 { get; set; }
                    public TAlt Value2 { get; set; }
                }
            }
            """;

        const string expected =
            """
            namespace Test.Namespace
            {
                public partial class Factory<T>
                {
                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T, TAlt}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Namespace_Factory____<T> WithValue1(in T value1)
                    {
                        return new Step_0__Test_Namespace_Factory____<T>(value1);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.Namespace.MyBuildTarget{T, TAlt}"/>
                /// </summary>
                public struct Step_0__Test_Namespace_Factory____<T>
                {
                    private readonly T _value1__parameter;
                    internal Step_0__Test_Namespace_Factory____(in T value1)
                    {
                        this._value1__parameter = value1;
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T, TAlt}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__Test_Namespace_Factory____<T, TAlt> WithValue2<TAlt>(in TAlt value2)
                    {
                        return new Step_1__Test_Namespace_Factory____<T, TAlt>(this._value1__parameter, value2);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.Namespace.MyBuildTarget{T, TAlt}"/>
                /// </summary>
                public struct Step_1__Test_Namespace_Factory____<T, TAlt>
                {
                    private readonly T _value1__parameter;
                    private readonly TAlt _value2__parameter;
                    internal Step_1__Test_Namespace_Factory____(in T value1, in TAlt value2)
                    {
                        this._value1__parameter = value1;
                        this._value2__parameter = value2;
                    }

                    /// <summary>
                    /// Creates a new instance using constructor Test.Namespace.MyBuildTarget<T, TAlt>.MyBuildTarget(T value1, TAlt value2).
                    ///
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T, TAlt}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T, TAlt> Create()
                    {
                        return new MyBuildTarget<T, TAlt>(this._value1__parameter, this._value2__parameter);
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
                    (typeof(FluentFactoryGenerator), "Test.Namespace.Factory____.g.cs", expected)
                }
            }
        }.RunAsync();
    }
}
