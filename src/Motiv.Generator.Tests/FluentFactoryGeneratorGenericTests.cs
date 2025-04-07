using Motiv.Generator.FluentFactory;
using VerifyCS =
    Motiv.Generator.Tests.CSharpSourceGeneratorVerifier<Motiv.Generator.FluentFactory.FluentFactoryGenerator>;

namespace Motiv.Generator.Tests;

public class FluentFactoryGeneratorGenericTests
{
    [Fact]
    public async Task Should_generate_when_applied_to_a_class_constructor_with_a_single_parameter()
    {
        const string code =
            """
            using Motiv.Generator.Attributes;

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
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.Namespace.MyBuildTarget{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Namespace_Factory<T> WithValue<T>(in T value)
                    {
                        return new Step_0__Test_Namespace_Factory<T>(value);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="Test.Namespace.MyBuildTarget{T}"/>
                /// </summary>
                public struct Step_0__Test_Namespace_Factory<T>
                {
                    private readonly T _value__parameter;
                    public Step_0__Test_Namespace_Factory(in T value)
                    {
                        this._value__parameter = value;
                    }

                    /// <summary>
                    /// Candidate constructor types:
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
            using Motiv.Generator.Attributes;

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
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory<T1> WithValue1<T1>(in T1 value1)
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
                    public Step_1__Test_Factory<T1, T2> WithValue2<T2>(in T2 value2)
                    {
                        return new Step_1__Test_Factory<T1, T2>(this._value1__parameter, value2);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="Test.MyBuildTarget{T1, T2}"/>
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
            using Motiv.Generator.Attributes;

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
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory<T1> WithValue1<T1>(in T1 value1)
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
                    public Step_1__Test_Factory<T1, T2> WithValue2<T2>(in T2 value2)
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
                    public Step_2__Test_Factory<T1, T2, T3> WithValue3<T3>(in T3 value3)
                    {
                        return new Step_2__Test_Factory<T1, T2, T3>(this._value1__parameter, this._value2__parameter, value3);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3}"/>
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
            using Motiv.Generator.Attributes;

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
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3, T4}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory<T1> WithValue1<T1>(in T1 value1)
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
                    public Step_1__Test_Factory<T1, T2> WithValue2<T2>(in T2 value2)
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
                    public Step_2__Test_Factory<T1, T2, T3> WithValue3<T3>(in T3 value3)
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
                    public Step_3__Test_Factory<T1, T2, T3, T4> WithValue4<T4>(in T4 value4)
                    {
                        return new Step_3__Test_Factory<T1, T2, T3, T4>(this._value1__parameter, this._value2__parameter, this._value3__parameter, value4);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3, T4}"/>
                /// </summary>
                public struct Step_3__Test_Factory<T1, T2, T3, T4>
                {
                    private readonly T1 _value1__parameter;
                    private readonly T2 _value2__parameter;
                    private readonly T3 _value3__parameter;
                    private readonly T4 _value4__parameter;
                    public Step_3__Test_Factory(in T1 value1, in T2 value2, in T3 value3, in T4 value4)
                    {
                        this._value1__parameter = value1;
                        this._value2__parameter = value2;
                        this._value3__parameter = value3;
                        this._value4__parameter = value4;
                    }

                    /// <summary>
                    /// Candidate constructor types:
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
}
