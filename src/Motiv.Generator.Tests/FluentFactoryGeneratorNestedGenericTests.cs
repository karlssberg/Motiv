using VerifyCS =
    Motiv.Generator.Tests.CSharpSourceGeneratorVerifier<Motiv.Generator.FluentFactoryGenerator>;

namespace Motiv.Generator.Tests;

public class FluentFactoryGeneratorNestedGenericTests
{
    [Fact]
    public async Task Should_generate_when_applied_to_a_class_constructor_with_a_single_parameter()
    {
        const string code =
            """
            using System;
            using Motiv.Generator;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            public class MyBuildTarget<T>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyBuildTarget(Func<T, bool> value)
                {
                    Value = value;
                }

                public Func<T, bool>  Value { get; set; }
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
                    ///     <seealso cref="Test.MyBuildTarget{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static MyBuildTarget<T> WithValue<T>(in System.Func<T, bool> value)
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
                Sources = { code },
                GeneratedSources =
                {
                    (typeof(FluentFactoryGenerator), "Test.Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_generate_when_applied_to_a_class_constructor_with_two_parameters()
    {
        const string code =
            """
            using System;
            using System.Collections.Generic;
            using Motiv.Generator;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            public class MyBuildTarget<T1, T2>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyBuildTarget(
                    Func<T1, bool> value1,
                    IEnumerable<T2> value2)
                {
                    Value1 = value1;
                    Value2 = value2;
                }

                public Func<T1, bool> Value1 { get; set; }

                public IEnumerable<T2> Value2 { get; set; }
            }
            """;

        const string expected =
            """
            using System;
            using System.Collections.Generic;

            namespace Test
            {
                public static partial class Factory
                {
                    /// <summary>
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory<T1> WithValue1<T1>(in System.Func<T1, bool> value1)
                    {
                        return new Step_0__Test_Factory<T1>(value1);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.MyBuildTarget{T1, T2}"/>
                /// </summary>
                public struct Step_0__Test_Factory<T1>
                {
                    private readonly System.Func<T1, bool> _value1__parameter;
                    internal Step_0__Test_Factory(in System.Func<T1, bool> value1)
                    {
                        this._value1__parameter = value1;
                    }

                    /// <summary>
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T1, T2> WithValue2<T2>(in System.Collections.Generic.IEnumerable<T2> value2)
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
    public async Task Should_generate_when_applied_to_a_class_constructor_with_two_parameters_with_deeply_nested_type_parameters()
    {
        const string code =
            """
            using System;
            using System.Collections.Generic;
            using Motiv.Generator;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            public class MyBuildTarget<T1, T2>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyBuildTarget(
                    [FluentMethod("SetValue1")]Func<IEnumerable<KeyValuePair<T1, T1>>, bool> value1,
                    [FluentMethod("SetValue2")]Func<IEnumerable<KeyValuePair<T2, T2>>, bool> value2)
                {
                    Value1 = value1;
                    Value2 = value2;
                }

                public Func<IEnumerable<KeyValuePair<T1,T1>>, bool> Value1 { get; set; }

                public Func<IEnumerable<KeyValuePair<T2,T2>>, bool> Value2 { get; set; }
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
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory<T1> SetValue1<T1>(in System.Func<System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<T1, T1>>, bool> value1)
                    {
                        return new Step_0__Test_Factory<T1>(value1);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.MyBuildTarget{T1, T2}"/>
                /// </summary>
                public struct Step_0__Test_Factory<T1>
                {
                    private readonly System.Func<System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<T1, T1>>, bool> _value1__parameter;
                    internal Step_0__Test_Factory(in System.Func<System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<T1, T1>>, bool> value1)
                    {
                        this._value1__parameter = value1;
                    }

                    /// <summary>
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T1, T2> SetValue2<T2>(in System.Func<System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<T2, T2>>, bool> value2)
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
    public async Task Should_generate_when_applied_to_a_class_constructor_with_three_parameters()
    {
        const string code =
            """
            using System;
            using System.Collections.Generic;
            using Motiv.Generator;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            public class MyBuildTarget<T1, T2, T3>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyBuildTarget(
                    Func<T1, bool> value1,
                    IEnumerable<T2> value2,
                    Func<Func<T1, bool>, T3> value3)
                {
                    Value1 = value1;
                    Value2 = value2;
                    Value3 = value3;
                }

                public Func<T1, bool> Value1 { get; set; }

                public IEnumerable<T2> Value2 { get; set; }

                public Func<Func<T1, bool>, T3> Value3 { get; set; }
            }
            """;

        const string expected =
            """
            using System;
            using System.Collections.Generic;

            namespace Test
            {
                public static partial class Factory
                {
                    /// <summary>
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory<T1> WithValue1<T1>(in System.Func<T1, bool> value1)
                    {
                        return new Step_0__Test_Factory<T1>(value1);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3}"/>
                /// </summary>
                public struct Step_0__Test_Factory<T1>
                {
                    private readonly System.Func<T1, bool> _value1__parameter;
                    internal Step_0__Test_Factory(in System.Func<T1, bool> value1)
                    {
                        this._value1__parameter = value1;
                    }

                    /// <summary>
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__Test_Factory<T1, T2> WithValue2<T2>(in System.Collections.Generic.IEnumerable<T2> value2)
                    {
                        return new Step_1__Test_Factory<T1, T2>(this._value1__parameter, value2);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3}"/>
                /// </summary>
                public struct Step_1__Test_Factory<T1, T2>
                {
                    private readonly System.Func<T1, bool> _value1__parameter;
                    private readonly System.Collections.Generic.IEnumerable<T2> _value2__parameter;
                    internal Step_1__Test_Factory(in System.Func<T1, bool> value1, in System.Collections.Generic.IEnumerable<T2> value2)
                    {
                        this._value1__parameter = value1;
                        this._value2__parameter = value2;
                    }

                    /// <summary>
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T1, T2, T3> WithValue3<T3>(in System.Func<System.Func<T1, bool>, T3> value3)
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
    public async Task Should_generate_when_multiple_type_parameters_appear_on_a_constructor_parameter()
    {
        const string code =
            """
            using System;
            using Motiv.Generator;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            public class MyBuildTarget<T1, T2, T3, T4, T5, T6>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyBuildTarget(
                    Func<T1, T2> value1,
                    Func<T3, T4> value2,
                    Func<T5, T6> value3)
                {
                    Value1 = value1;
                    Value2 = value2;
                    Value3 = value3;
                }

                public Func<T1, T2> Value1 { get; set; }

                public Func<T3, T4> Value2 { get; set; }

                public Func<T5, T6> Value3 { get; set; }
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
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3, T4, T5, T6}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory<T1, T2> WithValue1<T1, T2>(in System.Func<T1, T2> value1)
                    {
                        return new Step_0__Test_Factory<T1, T2>(value1);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3, T4, T5, T6}"/>
                /// </summary>
                public struct Step_0__Test_Factory<T1, T2>
                {
                    private readonly System.Func<T1, T2> _value1__parameter;
                    internal Step_0__Test_Factory(in System.Func<T1, T2> value1)
                    {
                        this._value1__parameter = value1;
                    }

                    /// <summary>
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3, T4, T5, T6}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__Test_Factory<T1, T2, T3, T4> WithValue2<T3, T4>(in System.Func<T3, T4> value2)
                    {
                        return new Step_1__Test_Factory<T1, T2, T3, T4>(this._value1__parameter, value2);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3, T4, T5, T6}"/>
                /// </summary>
                public struct Step_1__Test_Factory<T1, T2, T3, T4>
                {
                    private readonly System.Func<T1, T2> _value1__parameter;
                    private readonly System.Func<T3, T4> _value2__parameter;
                    internal Step_1__Test_Factory(in System.Func<T1, T2> value1, in System.Func<T3, T4> value2)
                    {
                        this._value1__parameter = value1;
                        this._value2__parameter = value2;
                    }

                    /// <summary>
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3, T4, T5, T6}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T1, T2, T3, T4, T5, T6> WithValue3<T5, T6>(in System.Func<T5, T6> value3)
                    {
                        return new MyBuildTarget<T1, T2, T3, T4, T5, T6>(this._value1__parameter, this._value2__parameter, value3);
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
