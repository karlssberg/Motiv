using Microsoft.CodeAnalysis.Testing;
using Motiv.Generator.FluentFactory;
using static Microsoft.CodeAnalysis.DiagnosticSeverity;
using static Motiv.Generator.FluentFactory.MotivDiagnosticDescriptor;
using VerifyCS =
    Motiv.Generator.Tests.CSharpSourceGeneratorVerifier<Motiv.Generator.FluentFactory.FluentFactoryGenerator>;

namespace Motiv.Generator.Tests;

public class FluentFactoryGeneratorMergeTests
{
    private const string SourceFile = "Source.cs";

    [Fact]
    public async Task Should_merge_generated_when_applied_to_class_constructors_with_a_single_parameter()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            public class MyClass<T>
            {
                [FluentConstructor(typeof(MyClass), Options = FluentOptions.NoCreateMethod)]
                public MyClass([FluentMethod("Create")]Func<T> value)
                {
                    Value = value;
                }

                public Func<T> Value { get; set; }
            }

            [FluentFactory]
            public partial class MyClass
            {
                [FluentConstructor(typeof(MyClass), Options = FluentOptions.NoCreateMethod)]
                public MyClass([FluentMethod("Create")]Func<string> value)
                {
                    Value = value;
                }

                public Func<String> Value { get; set; }
            }
            """;

        const string expected =
            """
            using System;

            public partial class MyClass
            {
                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass{T}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public static MyClass<T> Create<T>(in System.Func<T> value)
                {
                    return new MyClass<T>(value);
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MyClass"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public static MyClass Create(in System.Func<string> value)
                {
                    return new MyClass(value);
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
                    (typeof(FluentFactoryGenerator), "MyClass.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_merge_generated_when_applied_to_class_constructors_with_two_parameters()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            public class MyBuildTarget<T1, T2>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
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

            public class MyBuildTarget
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyBuildTarget(
                    String value1,
                    String value2)
                {
                    Value1 = value1;
                    Value2 = value2;
                }

                public String Value1 { get; set; }

                public String Value2 { get; set; }
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
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory<T1> WithValue1<T1>(in T1 value1)
                    {
                        return new Step_0__Test_Factory<T1>(value1);
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_1__Test_Factory WithValue1(in string value1)
                    {
                        return new Step_1__Test_Factory(value1);
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
                    public MyBuildTarget<T1, T2> WithValue2<T2>(in T2 value2)
                    {
                        return new MyBuildTarget<T1, T2>(this._value1__parameter, value2);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="Test.MyBuildTarget"/>
                /// </summary>
                public struct Step_1__Test_Factory
                {
                    private readonly string _value1__parameter;
                    public Step_1__Test_Factory(in string value1)
                    {
                        this._value1__parameter = value1;
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget WithValue2(in string value2)
                    {
                        return new MyBuildTarget(this._value1__parameter, value2);
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
    public async Task Should_merge_generated_from_different_namespaces_when_applied_to_class_constructors_with_two_parameters()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            namespace MyFactory
            {
                [FluentFactory]
                public static partial class Factory;
            }

            namespace TestA
            {
                public class MyBuildTarget<T1, T2>
                {
                    [FluentConstructor(typeof(MyFactory.Factory), Options = FluentOptions.NoCreateMethod)]
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
            }

            namespace TestB
            {
                public class MyBuildTarget
                {
                    [FluentConstructor(typeof(MyFactory.Factory), Options = FluentOptions.NoCreateMethod)]
                    public MyBuildTarget(
                        String value1,
                        String value2)
                    {
                        Value1 = value1;
                        Value2 = value2;
                    }

                    public String Value1 { get; set; }

                    public String Value2 { get; set; }
                }
            }
            """;

        const string expected =
            """
            using System;
            using TestA;
            using TestB;

            namespace MyFactory
            {
                public static partial class Factory
                {
                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="TestA.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__MyFactory_Factory<T1> WithValue1<T1>(in T1 value1)
                    {
                        return new Step_0__MyFactory_Factory<T1>(value1);
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="TestB.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_1__MyFactory_Factory WithValue1(in string value1)
                    {
                        return new Step_1__MyFactory_Factory(value1);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="TestA.MyBuildTarget{T1, T2}"/>
                /// </summary>
                public struct Step_0__MyFactory_Factory<T1>
                {
                    private readonly T1 _value1__parameter;
                    public Step_0__MyFactory_Factory(in T1 value1)
                    {
                        this._value1__parameter = value1;
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="TestA.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public TestA.MyBuildTarget<T1, T2> WithValue2<T2>(in T2 value2)
                    {
                        return new TestA.MyBuildTarget<T1, T2>(this._value1__parameter, value2);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="TestB.MyBuildTarget"/>
                /// </summary>
                public struct Step_1__MyFactory_Factory
                {
                    private readonly string _value1__parameter;
                    public Step_1__MyFactory_Factory(in string value1)
                    {
                        this._value1__parameter = value1;
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="TestB.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public TestB.MyBuildTarget WithValue2(in string value2)
                    {
                        return new TestB.MyBuildTarget(this._value1__parameter, value2);
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
                    (typeof(FluentFactoryGenerator), "MyFactory.Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_merge_generated_when_applied_to_class_constructors_with_two_parameters_with_divergence_on_the_second_step()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            public class MyBuildTarget<T1, T2>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
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

            public class MyBuildTarget<T1>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyBuildTarget(
                    T1 value1,
                    String value2)
                {
                    Value1 = value1;
                    Value2 = value2;
                }

                public T1 Value1 { get; set; }

                public String Value2 { get; set; }
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
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2}"/>
                    ///     <seealso cref="Test.MyBuildTarget{T1}"/>
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
                ///     <seealso cref="Test.MyBuildTarget{T1}"/>
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
                    public MyBuildTarget<T1, T2> WithValue2<T2>(in T2 value2)
                    {
                        return new MyBuildTarget<T1, T2>(this._value1__parameter, value2);
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget{T1}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T1> WithValue2(in string value2)
                    {
                        return new MyBuildTarget<T1>(this._value1__parameter, value2);
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
    public async Task Should_merge_generated_when_applied_to_class_constructors_with_three_parameters()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            public class MyBuildTarget<T1, T2, T3>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
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

            public class MyBuildTarget
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyBuildTarget(
                    String value1,
                    String value2,
                    String value3)
                {
                    Value1 = value1;
                    Value2 = value2;
                    Value3 = value3;
                }

                public String Value1 { get; set; }

                public String Value2 { get; set; }

                public String Value3 { get; set; }
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
                    public static Step_0__Test_Factory<T1> WithValue1<T1>(in T1 value1)
                    {
                        return new Step_0__Test_Factory<T1>(value1);
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_2__Test_Factory WithValue1(in string value1)
                    {
                        return new Step_2__Test_Factory(value1);
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
                    public MyBuildTarget<T1, T2, T3> WithValue3<T3>(in T3 value3)
                    {
                        return new MyBuildTarget<T1, T2, T3>(this._value1__parameter, this._value2__parameter, value3);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="Test.MyBuildTarget"/>
                /// </summary>
                public struct Step_2__Test_Factory
                {
                    private readonly string _value1__parameter;
                    public Step_2__Test_Factory(in string value1)
                    {
                        this._value1__parameter = value1;
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_3__Test_Factory WithValue2(in string value2)
                    {
                        return new Step_3__Test_Factory(this._value1__parameter, value2);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="Test.MyBuildTarget"/>
                /// </summary>
                public struct Step_3__Test_Factory
                {
                    private readonly string _value1__parameter;
                    private readonly string _value2__parameter;
                    public Step_3__Test_Factory(in string value1, in string value2)
                    {
                        this._value1__parameter = value1;
                        this._value2__parameter = value2;
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget WithValue3(in string value3)
                    {
                        return new MyBuildTarget(this._value1__parameter, this._value2__parameter, value3);
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
    public async Task Should_merge_generated_when_applied_to_class_constructors_with_four_parameters_where_divergence_occurs_on_the_second_step()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            public class MyBuildTarget<T1, T2, T3, T4>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
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

            public class MyBuildTarget<T1>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyBuildTarget(
                    T1 value1,
                    String value2,
                    String value3,
                    String value4)
                {
                    Value1 = value1;
                    Value2 = value2;
                    Value3 = value3;
                    Value4 = value4;
                }

                public T1 Value1 { get; set; }

                public String Value2 { get; set; }

                public String Value3 { get; set; }

                public String Value4 { get; set; }
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
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3, T4}"/>
                    ///     <seealso cref="Test.MyBuildTarget{T1}"/>
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
                ///     <seealso cref="Test.MyBuildTarget{T1}"/>
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

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget{T1}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_3__Test_Factory<T1> WithValue2(in string value2)
                    {
                        return new Step_3__Test_Factory<T1>(this._value1__parameter, value2);
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
                    public MyBuildTarget<T1, T2, T3, T4> WithValue4<T4>(in T4 value4)
                    {
                        return new MyBuildTarget<T1, T2, T3, T4>(this._value1__parameter, this._value2__parameter, this._value3__parameter, value4);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="Test.MyBuildTarget{T1}"/>
                /// </summary>
                public struct Step_3__Test_Factory<T1>
                {
                    private readonly T1 _value1__parameter;
                    private readonly string _value2__parameter;
                    public Step_3__Test_Factory(in T1 value1, in string value2)
                    {
                        this._value1__parameter = value1;
                        this._value2__parameter = value2;
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget{T1}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_4__Test_Factory<T1> WithValue3(in string value3)
                    {
                        return new Step_4__Test_Factory<T1>(this._value1__parameter, this._value2__parameter, value3);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="Test.MyBuildTarget{T1}"/>
                /// </summary>
                public struct Step_4__Test_Factory<T1>
                {
                    private readonly T1 _value1__parameter;
                    private readonly string _value2__parameter;
                    private readonly string _value3__parameter;
                    public Step_4__Test_Factory(in T1 value1, in string value2, in string value3)
                    {
                        this._value1__parameter = value1;
                        this._value2__parameter = value2;
                        this._value3__parameter = value3;
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget{T1}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T1> WithValue4(in string value4)
                    {
                        return new MyBuildTarget<T1>(this._value1__parameter, this._value2__parameter, this._value3__parameter, value4);
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
    public async Task Should_not_merge_generated_when_applied_to_different_root_types()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            [FluentFactory]
            public partial class MyBuildTarget
            {
                [FluentConstructor(typeof(Factory))]
                [FluentConstructor(typeof(MyBuildTarget))]
                public MyBuildTarget(
                    string value1,
                    string value2)
                {
                    Value1 = value1;
                    Value2 = value2;
                }

                public string Value1 { get; set; }

                public string Value2 { get; set; }
            }
            """;

        const string expectedFactory =
            """
            using System;

            namespace Test
            {
                public static partial class Factory
                {
                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory WithValue1(in string value1)
                    {
                        return new Step_0__Test_Factory(value1);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="Test.MyBuildTarget"/>
                /// </summary>
                public struct Step_0__Test_Factory
                {
                    private readonly string _value1__parameter;
                    public Step_0__Test_Factory(in string value1)
                    {
                        this._value1__parameter = value1;
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__Test_Factory WithValue2(in string value2)
                    {
                        return new Step_1__Test_Factory(this._value1__parameter, value2);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="Test.MyBuildTarget"/>
                /// </summary>
                public struct Step_1__Test_Factory
                {
                    private readonly string _value1__parameter;
                    private readonly string _value2__parameter;
                    public Step_1__Test_Factory(in string value1, in string value2)
                    {
                        this._value1__parameter = value1;
                        this._value2__parameter = value2;
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget Create()
                    {
                        return new MyBuildTarget(this._value1__parameter, this._value2__parameter);
                    }
                }
            }
            """;

        const string expectedMyBuildTarget =
            """
            using System;

            namespace Test
            {
                public partial class MyBuildTarget
                {
                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_MyBuildTarget WithValue1(in string value1)
                    {
                        return new Step_0__Test_MyBuildTarget(value1);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="Test.MyBuildTarget"/>
                /// </summary>
                public struct Step_0__Test_MyBuildTarget
                {
                    private readonly string _value1__parameter;
                    public Step_0__Test_MyBuildTarget(in string value1)
                    {
                        this._value1__parameter = value1;
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__Test_MyBuildTarget WithValue2(in string value2)
                    {
                        return new Step_1__Test_MyBuildTarget(this._value1__parameter, value2);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="Test.MyBuildTarget"/>
                /// </summary>
                public struct Step_1__Test_MyBuildTarget
                {
                    private readonly string _value1__parameter;
                    private readonly string _value2__parameter;
                    public Step_1__Test_MyBuildTarget(in string value1, in string value2)
                    {
                        this._value1__parameter = value1;
                        this._value2__parameter = value2;
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget Create()
                    {
                        return new MyBuildTarget(this._value1__parameter, this._value2__parameter);
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
                    (typeof(FluentFactoryGenerator), "Test.Factory.g.cs", expectedFactory),
                    (typeof(FluentFactoryGenerator), "Test.MyBuildTarget.g.cs", expectedMyBuildTarget)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Given_matching_fluent_method_names_and_types_Should_merge_parameters_where_their_types_match_but_when_parameter_names_are_dissimilar()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            public class MyBuildTarget<T>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyBuildTarget(
                    [FluentMethod("WithValueA")]string valueX1,
                    [FluentMethod("WithValueB")]string valueX2,
                    [FluentMethod("WithValueC")]T valueX3)
                {
                    ValueX1 = valueX1;
                    ValueX2 = valueX2;
                    ValueX3 = valueX3;
                }

                public string ValueX1 { get; set; }

                public string ValueX2 { get; set; }

                public T ValueX3 { get; set; }
            }

            public class MyBuildTarget
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyBuildTarget(
                    [FluentMethod("WithValueA")]string valueY1,
                    [FluentMethod("WithValueB")]string valueY2,
                    [FluentMethod("WithValueC")]string valueY3)
                {
                    ValueY1 = valueY1;
                    ValueY2 = valueY2;
                    ValueY3 = valueY3;
                }

                public String ValueY1 { get; set; }

                public String ValueY2 { get; set; }

                public String ValueY3 { get; set; }
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
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    ///     <seealso cref="Test.MyBuildTarget{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory WithValueA(in string valueX1)
                    {
                        return new Step_0__Test_Factory(valueX1);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="Test.MyBuildTarget"/>
                ///     <seealso cref="Test.MyBuildTarget{T}"/>
                /// </summary>
                public struct Step_0__Test_Factory
                {
                    private readonly string _valueX1__parameter;
                    public Step_0__Test_Factory(in string valueX1)
                    {
                        this._valueX1__parameter = valueX1;
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    ///     <seealso cref="Test.MyBuildTarget{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__Test_Factory WithValueB(in string valueX2)
                    {
                        return new Step_1__Test_Factory(this._valueX1__parameter, valueX2);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="Test.MyBuildTarget"/>
                ///     <seealso cref="Test.MyBuildTarget{T}"/>
                /// </summary>
                public struct Step_1__Test_Factory
                {
                    private readonly string _valueX1__parameter;
                    private readonly string _valueX2__parameter;
                    public Step_1__Test_Factory(in string valueX1, in string valueX2)
                    {
                        this._valueX1__parameter = valueX1;
                        this._valueX2__parameter = valueX2;
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T> WithValueC<T>(in T valueX3)
                    {
                        return new MyBuildTarget<T>(this._valueX1__parameter, this._valueX2__parameter, valueX3);
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget WithValueC(in string valueY3)
                    {
                        return new MyBuildTarget(this._valueX1__parameter, this._valueX2__parameter, valueY3);
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
    public async Task Given_matching_fluent_method_names_Should_merge_parameters_where_their_types_match_but_when_parameter_names_are_dissimilar()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            public class MyBuildTarget<T1, T2, T3>
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyBuildTarget(
                    [FluentMethod("WithValueA")]T1 valueX1,
                    [FluentMethod("WithValueB")]T2 valueX2,
                    [FluentMethod("WithValueC")]T3 valueX3)
                {
                    ValueX1 = valueX1;
                    ValueX2 = valueX2;
                    ValueX3 = valueX3;
                }

                public T1 ValueX1 { get; set; }

                public T2 ValueX2 { get; set; }

                public T3 ValueX3 { get; set; }
            }

            public class MyBuildTarget
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyBuildTarget(
                    [FluentMethod("WithValueA")]string valueY1,
                    [FluentMethod("WithValueB")]string valueY2,
                    [FluentMethod("WithValueC")]string valueY3)
                {
                    ValueY1 = valueY1;
                    ValueY2 = valueY2;
                    ValueY3 = valueY3;
                }

                public String ValueY1 { get; set; }

                public String ValueY2 { get; set; }

                public String ValueY3 { get; set; }
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
                    public static Step_0__Test_Factory<T1> WithValueA<T1>(in T1 valueX1)
                    {
                        return new Step_0__Test_Factory<T1>(valueX1);
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_2__Test_Factory WithValueA(in string valueY1)
                    {
                        return new Step_2__Test_Factory(valueY1);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3}"/>
                /// </summary>
                public struct Step_0__Test_Factory<T1>
                {
                    private readonly T1 _valueX1__parameter;
                    public Step_0__Test_Factory(in T1 valueX1)
                    {
                        this._valueX1__parameter = valueX1;
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__Test_Factory<T1, T2> WithValueB<T2>(in T2 valueX2)
                    {
                        return new Step_1__Test_Factory<T1, T2>(this._valueX1__parameter, valueX2);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3}"/>
                /// </summary>
                public struct Step_1__Test_Factory<T1, T2>
                {
                    private readonly T1 _valueX1__parameter;
                    private readonly T2 _valueX2__parameter;
                    public Step_1__Test_Factory(in T1 valueX1, in T2 valueX2)
                    {
                        this._valueX1__parameter = valueX1;
                        this._valueX2__parameter = valueX2;
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2, T3}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T1, T2, T3> WithValueC<T3>(in T3 valueX3)
                    {
                        return new MyBuildTarget<T1, T2, T3>(this._valueX1__parameter, this._valueX2__parameter, valueX3);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="Test.MyBuildTarget"/>
                /// </summary>
                public struct Step_2__Test_Factory
                {
                    private readonly string _valueY1__parameter;
                    public Step_2__Test_Factory(in string valueY1)
                    {
                        this._valueY1__parameter = valueY1;
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_3__Test_Factory WithValueB(in string valueY2)
                    {
                        return new Step_3__Test_Factory(this._valueY1__parameter, valueY2);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="Test.MyBuildTarget"/>
                /// </summary>
                public struct Step_3__Test_Factory
                {
                    private readonly string _valueY1__parameter;
                    private readonly string _valueY2__parameter;
                    public Step_3__Test_Factory(in string valueY1, in string valueY2)
                    {
                        this._valueY1__parameter = valueY1;
                        this._valueY2__parameter = valueY2;
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget WithValueC(in string valueY3)
                    {
                        return new MyBuildTarget(this._valueY1__parameter, this._valueY2__parameter, valueY3);
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
    public async Task Given_that_generic_type_parameters_have_been_resolved_by_earlier_steps_Should_not_use_resolved_type_parameters_when_merging_methods()
    {
        const string code =
            """
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
                [MultipleFluentMethods(typeof(WhenFalseYieldOverloads))]Func<TModel, IEnumerable<string>> falseBecause)
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
            """;

        const string expected =
            """
            using System;

            public static partial class Spec
            {
                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="ExplanationPropositionFactory{TModel}"/>
                ///     <seealso cref="ExplanationWithNamePropositionFactory{TModel}"/>
                ///     <seealso cref="MultiAssertionExplanationWithNamePropositionFactory{TModel}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public static Step_0__Spec<TModel> Build<TModel>(in System.Func<TModel, bool> predicate)
                {
                    return new Step_0__Spec<TModel>(predicate);
                }
            }

            /// <summary>
            /// Candidate constructor types:
            ///     <seealso cref="ExplanationPropositionFactory{TModel}"/>
            ///     <seealso cref="ExplanationWithNamePropositionFactory{TModel}"/>
            ///     <seealso cref="MultiAssertionExplanationWithNamePropositionFactory{TModel}"/>
            /// </summary>
            public struct Step_0__Spec<TModel>
            {
                private readonly System.Func<TModel, bool> _predicate__parameter;
                public Step_0__Spec(in System.Func<TModel, bool> predicate)
                {
                    this._predicate__parameter = predicate;
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="ExplanationWithNamePropositionFactory{TModel}"/>
                ///     <seealso cref="MultiAssertionExplanationWithNamePropositionFactory{TModel}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public Step_1__Spec<TModel> WhenTrue(in string trueBecause)
                {
                    return new Step_1__Spec<TModel>(this._predicate__parameter, trueBecause);
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="ExplanationPropositionFactory{TModel}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public Step_2__Spec<TModel> WhenTrue(in System.Func<TModel, string> whenTrue)
                {
                    return new Step_2__Spec<TModel>(this._predicate__parameter, WhenTrueOverloads.WhenTrue<TModel, string>(whenTrue));
                }
            }

            /// <summary>
            /// Candidate constructor types:
            ///     <seealso cref="ExplanationWithNamePropositionFactory{TModel}"/>
            ///     <seealso cref="MultiAssertionExplanationWithNamePropositionFactory{TModel}"/>
            /// </summary>
            public struct Step_1__Spec<TModel>
            {
                private readonly System.Func<TModel, bool> _predicate__parameter;
                private readonly string _trueBecause__parameter;
                public Step_1__Spec(in System.Func<TModel, bool> predicate, in string trueBecause)
                {
                    this._predicate__parameter = predicate;
                    this._trueBecause__parameter = trueBecause;
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="ExplanationWithNamePropositionFactory{TModel}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public ExplanationWithNamePropositionFactory<TModel> WhenFalse(in System.Func<TModel, string> whenFalse)
                {
                    return new ExplanationWithNamePropositionFactory<TModel>(this._predicate__parameter, this._trueBecause__parameter, WhenFalseOverloads.WhenFalse<TModel, string>(whenFalse));
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="ExplanationWithNamePropositionFactory{TModel}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public ExplanationWithNamePropositionFactory<TModel> WhenFalse(in string whenFalse)
                {
                    return new ExplanationWithNamePropositionFactory<TModel>(this._predicate__parameter, this._trueBecause__parameter, WhenFalseOverloads.WhenFalse<TModel, string>(whenFalse));
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="MultiAssertionExplanationWithNamePropositionFactory{TModel}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public MultiAssertionExplanationWithNamePropositionFactory<TModel> WhenFalseYield(in System.Func<TModel, System.Collections.Generic.IEnumerable<string>> whenFalse)
                {
                    return new MultiAssertionExplanationWithNamePropositionFactory<TModel>(this._predicate__parameter, this._trueBecause__parameter, WhenFalseYieldOverloads.WhenFalseYield<TModel, string>(whenFalse));
                }
            }

            /// <summary>
            /// Candidate constructor types:
            ///     <seealso cref="ExplanationPropositionFactory{TModel}"/>
            /// </summary>
            public struct Step_2__Spec<TModel>
            {
                private readonly System.Func<TModel, bool> _predicate__parameter;
                private readonly System.Func<TModel, string> _whenTrue__parameter;
                public Step_2__Spec(in System.Func<TModel, bool> predicate, in System.Func<TModel, string> whenTrue)
                {
                    this._predicate__parameter = predicate;
                    this._whenTrue__parameter = whenTrue;
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="ExplanationPropositionFactory{TModel}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public ExplanationPropositionFactory<TModel> WhenFalse(in System.Func<TModel, string> whenFalse)
                {
                    return new ExplanationPropositionFactory<TModel>(this._predicate__parameter, this._whenTrue__parameter, WhenFalseOverloads.WhenFalse<TModel, string>(whenFalse));
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="ExplanationPropositionFactory{TModel}"/>
                /// </summary>
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public ExplanationPropositionFactory<TModel> WhenFalse(in string whenFalse)
                {
                    return new ExplanationPropositionFactory<TModel>(this._predicate__parameter, this._whenTrue__parameter, WhenFalseOverloads.WhenFalse<TModel, string>(whenFalse));
                }
            }
            """;
        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { (SourceFile,code) },
                GeneratedSources =
                {
                    (typeof(FluentFactoryGenerator), "Spec.g.cs", expected)
                },
                ExpectedDiagnostics =
                {
                    DiagnosticResult
                        .CompilerWarning(ContainsSupersededFluentMethodTemplate.Id)
                        .WithSpan(SourceFile, 21, 28, 21, 59)
                        .WithSpan(SourceFile, 13, 77, 13, 89)
                        .WithArguments(
                            "WhenFalseYieldOverloads.WhenFalse<TModel, string>(System.Func<TModel, string> whenFalse)",
                            "System.Func<TModel, System.Collections.Generic.IEnumerable<string>> falseBecause",
                            "MultiAssertionExplanationWithNamePropositionFactory<TModel>.MultiAssertionExplanationWithNamePropositionFactory(System.Func<TModel, bool> predicate, string trueBecause, System.Func<TModel, System.Collections.Generic.IEnumerable<string>> falseBecause)",
                            "the parameter 'System.Func<TModel, string> falseBecause' in the constructor 'ExplanationWithNamePropositionFactory<TModel>.ExplanationWithNamePropositionFactory(System.Func<TModel, bool> predicate, string trueBecause, System.Func<TModel, string> falseBecause)' was used as the basis for the fluent method. Perhaps the ignored method-template can be removed or modified."),
                    DiagnosticResult
                        .CompilerWarning(ContainsSupersededFluentMethodTemplate.Id)
                        .WithSpan(SourceFile, 21, 28, 21, 59)
                        .WithSpan(SourceFile, 13, 77, 13, 89)
                        .WithArguments(
                            "WhenFalseYieldOverloads.WhenFalse<TModel, string>(string whenFalse)",
                            "System.Func<TModel, System.Collections.Generic.IEnumerable<string>> falseBecause",
                            "MultiAssertionExplanationWithNamePropositionFactory<TModel>.MultiAssertionExplanationWithNamePropositionFactory(System.Func<TModel, bool> predicate, string trueBecause, System.Func<TModel, System.Collections.Generic.IEnumerable<string>> falseBecause)",
                            "the parameter 'System.Func<TModel, string> falseBecause' in the constructor 'ExplanationWithNamePropositionFactory<TModel>.ExplanationWithNamePropositionFactory(System.Func<TModel, bool> predicate, string trueBecause, System.Func<TModel, string> falseBecause)' was used as the basis for the fluent method. Perhaps the ignored method-template can be removed or modified."),
                    new DiagnosticResult(FluentMethodTemplateSuperseded.Id, Info)
                        .WithSpan(SourceFile, 67, 58, 67, 67)
                        .WithArguments(
                            "System.Func<TModel, System.Collections.Generic.IEnumerable<string>> WhenFalseYieldOverloads.WhenFalse<TModel, string>(System.Func<TModel, string> whenFalse)",
                            "System.Func<TModel, System.Collections.Generic.IEnumerable<string>> falseBecause",
                            "MultiAssertionExplanationWithNamePropositionFactory<TModel>.MultiAssertionExplanationWithNamePropositionFactory(System.Func<TModel, bool> predicate, string trueBecause, System.Func<TModel, System.Collections.Generic.IEnumerable<string>> falseBecause)",
                            "System.Func<TModel, string> falseBecause",
                            "ExplanationWithNamePropositionFactory<TModel>.ExplanationWithNamePropositionFactory(System.Func<TModel, bool> predicate, string trueBecause, System.Func<TModel, string> falseBecause)"),
                    new DiagnosticResult(FluentMethodTemplateSuperseded.Id, Info)
                        .WithSpan(SourceFile, 73, 58, 73, 67)
                        .WithArguments(
                            "System.Func<TModel, System.Collections.Generic.IEnumerable<string>> WhenFalseYieldOverloads.WhenFalse<TModel, string>(string whenFalse)",
                            "System.Func<TModel, System.Collections.Generic.IEnumerable<string>> falseBecause",
                            "MultiAssertionExplanationWithNamePropositionFactory<TModel>.MultiAssertionExplanationWithNamePropositionFactory(System.Func<TModel, bool> predicate, string trueBecause, System.Func<TModel, System.Collections.Generic.IEnumerable<string>> falseBecause)",
                            "System.Func<TModel, string> falseBecause", "ExplanationWithNamePropositionFactory<TModel>.ExplanationWithNamePropositionFactory(System.Func<TModel, bool> predicate, string trueBecause, System.Func<TModel, string> falseBecause)"),
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_treat_generic_constructor_parameters_as_invariant_when_merging()
    {
        const string code =
            """
            using System;
            using System.Collections.Generic;
            using Motiv.Generator.Attributes;

            namespace Test.Namespace
            {
                [FluentFactory]
                public static partial class Factory;

                public partial class MyEnumerableBuildTarget<T1, T2>
                {
                    [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                    public MyEnumerableBuildTarget(
                        [MultipleFluentMethods(typeof(EnumerableOverloads))]Func<T1, IEnumerable<T2>> first,
                        string second)
                    {
                        First = first;
                        Second = second;
                    }

                    public Func<T1, IEnumerable<T2>> First { get; set; }
                    public string Second { get; set; }
                }


                public partial class MyCollectionBuildTarget<T1, T2>
                {
                    [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                    public MyCollectionBuildTarget(
                        [MultipleFluentMethods(typeof(CollectionOverloads))]Func<T1, ICollection<T2>> first,
                        string second)
                    {
                        First = first;
                        Second = second;
                    }

                    public Func<T1, ICollection<T2>> First { get; set; }
                    public string Second { get; set; }
                }

                internal static class EnumerableOverloads
                {

                    [FluentMethodTemplate]
                    internal static Func<T1, IEnumerable<T2>> Build<T1, T2>(Func<T1, IEnumerable<T2>> resultFactory) => resultFactory;


                    [FluentMethodTemplate]
                    internal static Func<T1, IEnumerable<string>> Build<T1>(Func<T1, IEnumerable<string>> resultFactory) => resultFactory;
                }

                internal static class CollectionOverloads
                {

                    [FluentMethodTemplate]
                    internal static Func<T1, ICollection<T2>> Build<T1, T2>(Func<T1, ICollection<T2>> resultFactory) => resultFactory;


                    [FluentMethodTemplate]
                    internal static Func<T1, ICollection<string>> Build<T1>(Func<T1, ICollection<string>> resultFactory) => resultFactory;
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
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.Namespace.MyEnumerableBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Namespace_Factory<T1, T2> Build<T1, T2>(in System.Func<T1, System.Collections.Generic.IEnumerable<T2>> resultFactory)
                    {
                        return new Step_0__Test_Namespace_Factory<T1, T2>(EnumerableOverloads.Build<T1, T2>(resultFactory));
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.Namespace.MyEnumerableBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Namespace_Factory<T1, string> Build<T1>(in System.Func<T1, System.Collections.Generic.IEnumerable<string>> resultFactory)
                    {
                        return new Step_0__Test_Namespace_Factory<T1, string>(EnumerableOverloads.Build<T1>(resultFactory));
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.Namespace.MyCollectionBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_1__Test_Namespace_Factory<T1, T2> Build<T1, T2>(in System.Func<T1, System.Collections.Generic.ICollection<T2>> resultFactory)
                    {
                        return new Step_1__Test_Namespace_Factory<T1, T2>(CollectionOverloads.Build<T1, T2>(resultFactory));
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.Namespace.MyCollectionBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_1__Test_Namespace_Factory<T1, string> Build<T1>(in System.Func<T1, System.Collections.Generic.ICollection<string>> resultFactory)
                    {
                        return new Step_1__Test_Namespace_Factory<T1, string>(CollectionOverloads.Build<T1>(resultFactory));
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="Test.Namespace.MyEnumerableBuildTarget{T1, T2}"/>
                /// </summary>
                public struct Step_0__Test_Namespace_Factory<T1, T2>
                {
                    private readonly System.Func<T1, System.Collections.Generic.IEnumerable<T2>> _first__parameter;
                    public Step_0__Test_Namespace_Factory(in System.Func<T1, System.Collections.Generic.IEnumerable<T2>> first)
                    {
                        this._first__parameter = first;
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.Namespace.MyEnumerableBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyEnumerableBuildTarget<T1, T2> WithSecond(in string second)
                    {
                        return new MyEnumerableBuildTarget<T1, T2>(this._first__parameter, second);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
                ///     <seealso cref="Test.Namespace.MyCollectionBuildTarget{T1, T2}"/>
                /// </summary>
                public struct Step_1__Test_Namespace_Factory<T1, T2>
                {
                    private readonly System.Func<T1, System.Collections.Generic.ICollection<T2>> _first__parameter;
                    public Step_1__Test_Namespace_Factory(in System.Func<T1, System.Collections.Generic.ICollection<T2>> first)
                    {
                        this._first__parameter = first;
                    }

                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.Namespace.MyCollectionBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyCollectionBuildTarget<T1, T2> WithSecond(in string second)
                    {
                        return new MyCollectionBuildTarget<T1, T2>(this._first__parameter, second);
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
}
