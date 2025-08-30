using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Motiv.Generator.FluentFactory;
using static Motiv.Generator.FluentFactory.FluentFactoryGenerator;
using VerifyCS =
    Motiv.Generator.Tests.CSharpSourceGeneratorVerifier<Motiv.Generator.FluentFactory.FluentFactoryGenerator>;

namespace Motiv.Generator.Tests;

public class FluentFactoryGeneratorMergeDissimilarStepsTests
{
    private const string SourceFile = "Source.cs";
    [Fact]
    public async Task Given_class_constructors_with_a_single_and_dual_parameters_Should_create_methods_both_target_types()
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
                public MyBuildTarget(T value1)
                {
                    Value1 = value1;
                }

                public T Value1 { get; set; }
            }

            public class MyBuildTarget
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyBuildTarget(string value1, string value2)
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
                    ///     <seealso cref="Test.MyBuildTarget{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static MyBuildTarget<T> WithValue1<T>(in T value1)
                    {
                        return new MyBuildTarget<T>(value1);
                    }

                    /// <summary>
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory WithValue1(in string value1)
                    {
                        return new Step_0__Test_Factory(value1);
                    }
                }

                /// <summary>
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
    public async Task Given_class_constructors_with_two_and_three_parameters_Should_create_methods_both_target_types()
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

            public class MyBuildTarget
            {
                [FluentConstructor(typeof(Factory))]
                public MyBuildTarget(
                    String string1,
                    String string2,
                    String string3)
                {
                    String1 = string1;
                    String2 = string2;
                    String3 = string3;
                }

                public String String1 { get; set; }

                public String String2 { get; set; }

                public String String3 { get; set; }
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
                    public static Step_0__Test_Factory<T1> WithValue1<T1>(in T1 value1)
                    {
                        return new Step_0__Test_Factory<T1>(value1);
                    }

                    /// <summary>
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_2__Test_Factory WithString1(in string string1)
                    {
                        return new Step_2__Test_Factory(string1);
                    }
                }

                /// <summary>
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
                    public Step_1__Test_Factory(in T1 value1, in T2 value2)
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

                /// <summary>
                ///     <seealso cref="Test.MyBuildTarget"/>
                /// </summary>
                public struct Step_2__Test_Factory
                {
                    private readonly string _string1__parameter;
                    public Step_2__Test_Factory(in string string1)
                    {
                        this._string1__parameter = string1;
                    }

                    /// <summary>
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_3__Test_Factory WithString2(in string string2)
                    {
                        return new Step_3__Test_Factory(this._string1__parameter, string2);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.MyBuildTarget"/>
                /// </summary>
                public struct Step_3__Test_Factory
                {
                    private readonly string _string1__parameter;
                    private readonly string _string2__parameter;
                    public Step_3__Test_Factory(in string string1, in string string2)
                    {
                        this._string1__parameter = string1;
                        this._string2__parameter = string2;
                    }

                    /// <summary>
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_4__Test_Factory WithString3(in string string3)
                    {
                        return new Step_4__Test_Factory(this._string1__parameter, this._string2__parameter, string3);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.MyBuildTarget"/>
                /// </summary>
                public struct Step_4__Test_Factory
                {
                    private readonly string _string1__parameter;
                    private readonly string _string2__parameter;
                    private readonly string _string3__parameter;
                    public Step_4__Test_Factory(in string string1, in string string2, in string string3)
                    {
                        this._string1__parameter = string1;
                        this._string2__parameter = string2;
                        this._string3__parameter = string3;
                    }

                    /// <summary>
                    /// Creates a new instance using constructor Test.MyBuildTarget.MyBuildTarget(string string1, string string2, string string3).
                    ///
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget Create()
                    {
                        return new MyBuildTarget(this._string1__parameter, this._string2__parameter, this._string3__parameter);
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
    public async Task Given_class_constructors_with_two_and_three_parameters_Should_create_methods_for_both_target_types_with_divergence_on_the_second_step()
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
                [FluentConstructor(typeof(Factory))]
                public MyBuildTarget(
                    T1 value1,
                    String value2,
                    T2 value3)
                {
                    Value1 = value1;
                    Value2 = value2;
                    Value3 = value3;
                }

                [FluentConstructor(typeof(Factory))]
                public MyBuildTarget(
                    T1 value1,
                    T2 value3)
                {
                    Value1 = value1;
                    Value3 = value3;
                }

                public T1 Value1 { get; set; }

                public String Value2 { get; set; } = "";

                public T2 Value3 { get; set; }
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
                    public Step_0__Test_Factory(in T1 value1)
                    {
                        this._value1__parameter = value1;
                    }

                    /// <summary>
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__Test_Factory<T1> WithValue2(in string value2)
                    {
                        return new Step_1__Test_Factory<T1>(this._value1__parameter, value2);
                    }

                    /// <summary>
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_3__Test_Factory<T1, T2> WithValue3<T2>(in T2 value3)
                    {
                        return new Step_3__Test_Factory<T1, T2>(this._value1__parameter, value3);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.MyBuildTarget{T1, T2}"/>
                /// </summary>
                public struct Step_1__Test_Factory<T1>
                {
                    private readonly T1 _value1__parameter;
                    private readonly string _value2__parameter;
                    public Step_1__Test_Factory(in T1 value1, in string value2)
                    {
                        this._value1__parameter = value1;
                        this._value2__parameter = value2;
                    }

                    /// <summary>
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_2__Test_Factory<T1, T2> WithValue3<T2>(in T2 value3)
                    {
                        return new Step_2__Test_Factory<T1, T2>(this._value1__parameter, this._value2__parameter, value3);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.MyBuildTarget{T1, T2}"/>
                /// </summary>
                public struct Step_2__Test_Factory<T1, T2>
                {
                    private readonly T1 _value1__parameter;
                    private readonly string _value2__parameter;
                    private readonly T2 _value3__parameter;
                    public Step_2__Test_Factory(in T1 value1, in string value2, in T2 value3)
                    {
                        this._value1__parameter = value1;
                        this._value2__parameter = value2;
                        this._value3__parameter = value3;
                    }

                    /// <summary>
                    /// Creates a new instance using constructor Test.MyBuildTarget<T1, T2>.MyBuildTarget(T1 value1, string value2, T2 value3).
                    ///
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T1, T2> Create()
                    {
                        return new MyBuildTarget<T1, T2>(this._value1__parameter, this._value2__parameter, this._value3__parameter);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.MyBuildTarget{T1, T2}"/>
                /// </summary>
                public struct Step_3__Test_Factory<T1, T2>
                {
                    private readonly T1 _value1__parameter;
                    private readonly T2 _value3__parameter;
                    public Step_3__Test_Factory(in T1 value1, in T2 value3)
                    {
                        this._value1__parameter = value1;
                        this._value3__parameter = value3;
                    }

                    /// <summary>
                    /// Creates a new instance using constructor Test.MyBuildTarget<T1, T2>.MyBuildTarget(T1 value1, T2 value3).
                    ///
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T1, T2> Create()
                    {
                        return new MyBuildTarget<T1, T2>(this._value1__parameter, this._value3__parameter);
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
    public async Task Given_a_constructors_with_dissimilar_parameters_Should_diverge_steps_from_root()
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
                [FluentConstructor(typeof(Factory))]
                public MyBuildTarget(
                    T1 value1,
                    T2 value2)
                {
                    Value1 = value1;
                    Value2 = value2;
                }

                [FluentConstructor(typeof(Factory))]
                public MyBuildTarget(
                    T2 value2,
                    T1 value1)
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

                    /// <summary>
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_2__Test_Factory<T2> WithValue2<T2>(in T2 value2)
                    {
                        return new Step_2__Test_Factory<T2>(value2);
                    }
                }

                /// <summary>
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
                    public Step_1__Test_Factory(in T1 value1, in T2 value2)
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

                /// <summary>
                ///     <seealso cref="Test.MyBuildTarget{T1, T2}"/>
                /// </summary>
                public struct Step_2__Test_Factory<T2>
                {
                    private readonly T2 _value2__parameter;
                    public Step_2__Test_Factory(in T2 value2)
                    {
                        this._value2__parameter = value2;
                    }

                    /// <summary>
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_3__Test_Factory<T2, T1> WithValue1<T1>(in T1 value1)
                    {
                        return new Step_3__Test_Factory<T2, T1>(this._value2__parameter, value1);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.MyBuildTarget{T1, T2}"/>
                /// </summary>
                public struct Step_3__Test_Factory<T2, T1>
                {
                    private readonly T2 _value2__parameter;
                    private readonly T1 _value1__parameter;
                    public Step_3__Test_Factory(in T2 value2, in T1 value1)
                    {
                        this._value2__parameter = value2;
                        this._value1__parameter = value1;
                    }

                    /// <summary>
                    /// Creates a new instance using constructor Test.MyBuildTarget<T1, T2>.MyBuildTarget(T2 value2, T1 value1).
                    ///
                    ///     <seealso cref="Test.MyBuildTarget{T1, T2}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T1, T2> Create()
                    {
                        return new MyBuildTarget<T1, T2>(this._value2__parameter, this._value1__parameter);
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
    public async Task Given_records_diverge_from_the_second_step_when_no_create_methods_are_requested_Should_merge_steps_and_methods()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            namespace Test;

            [FluentFactory]
            public static partial class Shape;

            [FluentConstructor(typeof(Shape), Options = FluentOptions.NoCreateMethod)]
            public partial record Square(int Width)
            {
                public int Width { get; set; } = Width;
            }

            [FluentConstructor(typeof(Shape), Options = FluentOptions.NoCreateMethod)]
            public partial class Rectangle(int width, in int height)
            {
                public int Width { get; set; } = width;
                public int Height { get; set; } = height;
            }

            [FluentConstructor(typeof(Shape), Options = FluentOptions.NoCreateMethod)]
            public partial record Cuboid(in int Width, int Height, in int Depth)
            {
                public int Width { get; set; } = Width;
                public int Height { get; set; } = Height;
                public int Depth { get; set; } = Depth;
            }
            """;

        const string expected =
            """
            using System;

            namespace Test
            {
                public static partial class Shape
                {
                    /// <summary>
                    ///     <seealso cref="Test.Cuboid"/>
                    ///     <seealso cref="Test.Rectangle"/>
                    ///     <seealso cref="Test.Square"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Square WithWidth(in int width)
                    {
                        return new Square(width);
                    }
                }

                public partial record Square
                {
                    /// <summary>
                    ///     <seealso cref="Test.Cuboid"/>
                    ///     <seealso cref="Test.Rectangle"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Rectangle WithHeight(in int height)
                    {
                        return new Rectangle(this.Width, height);
                    }
                }

                public partial class Rectangle
                {
                    /// <summary>
                    ///     <seealso cref="Test.Cuboid"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Cuboid WithDepth(in int depth)
                    {
                        return new Cuboid(this.Width, this.Height, depth);
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
                    (typeof(FluentFactoryGenerator), "Test.Shape.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Given_class_constructors_with_a_single_double_and_triple_parameters_Should_ensure_signature_clashes_involving_existing_type()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            namespace TestFactory
            {
                [FluentFactory]
                public static partial class Factory;
            }

            namespace TestA
            {
                public partial class MyBuildTargetA<T>
                {
                    [FluentConstructor(typeof(TestFactory.Factory), Options = FluentOptions.NoCreateMethod)]
                    public MyBuildTargetA(
                        [FluentMethod("WithValueA")]T valueA1)
                    {
                        ValueA1 = valueA1;
                    }

                    public T ValueA1 { get; set; }
                }
            }

            namespace TestB
            {
                public class MyBuildTargetB<T>
                {
                    [FluentConstructor(typeof(TestFactory.Factory), Options = FluentOptions.NoCreateMethod)]
                    public MyBuildTargetB(
                        [FluentMethod("WithValueA")]T valueB1,
                        [FluentMethod("WithValueB")]string valueB2,
                        [FluentMethod("WithValueC")]int valueB3)
                    {
                        ValueB1 = valueB1;
                        ValueB2 = valueB2;
                        ValueB3 = valueB3;
                    }

                    public T ValueB1 { get; set; }

                    public String ValueB2 { get; set; }

                    public int ValueB3 { get; set; }
                }
            }

            namespace TestC
            {
                public partial class MyBuildTargetC<T>
                {
                    [FluentConstructor(typeof(TestFactory.Factory), Options = FluentOptions.NoCreateMethod)]
                    public MyBuildTargetC(
                        [FluentMethod("WithValueA")]T valueC1,
                        [FluentMethod("WithValueB")]string valueC2)
                    {
                        ValueC1 = valueC1;
                        ValueC2 = valueC2;
                    }

                    public T ValueC1 { get; set; }

                    public string ValueC2 { get; set; }
                }
            }

            namespace TestD
            {
                public partial class MyBuildTargetD<T>
                {
                    [FluentConstructor(typeof(TestFactory.Factory), Options = FluentOptions.NoCreateMethod)]
                    public MyBuildTargetD(
                        [FluentMethod("WithValueA")]T valueD1,
                        [FluentMethod("WithValueB")]string valueD2,
                        [FluentMethod("WithValueC")]string valueD3)
                    {
                        ValueD1 = valueD1;
                        ValueD2 = valueD2;
                        ValueD3 = valueD3;
                    }

                    public T ValueD1 { get; set; }

                    public string ValueD2 { get; set; }

                    public string ValueD3 { get; set; }
                }
            }
            """;

        const string expected =
            """
            using System;
            using TestA;
            using TestB;
            using TestC;
            using TestD;

            namespace TestFactory
            {
                public static partial class Factory
                {
                    /// <summary>
                    ///     <seealso cref="TestA.MyBuildTargetA{T}"/>
                    ///     <seealso cref="TestB.MyBuildTargetB{T}"/>
                    ///     <seealso cref="TestC.MyBuildTargetC{T}"/>
                    ///     <seealso cref="TestD.MyBuildTargetD{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static TestA.MyBuildTargetA<T> WithValueA<T>(in T valueA1)
                    {
                        return new TestA.MyBuildTargetA<T>(valueA1);
                    }
                }
            }

            namespace TestA
            {
                public partial class MyBuildTargetA<T>
                {
                    /// <summary>
                    ///     <seealso cref="TestB.MyBuildTargetB{T}"/>
                    ///     <seealso cref="TestC.MyBuildTargetC{T}"/>
                    ///     <seealso cref="TestD.MyBuildTargetD{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public TestC.MyBuildTargetC<T> WithValueB(in string valueB2)
                    {
                        return new TestC.MyBuildTargetC<T>(this.ValueA1, valueB2);
                    }
                }
            }

            namespace TestC
            {
                public partial class MyBuildTargetC<T>
                {
                    /// <summary>
                    ///     <seealso cref="TestB.MyBuildTargetB{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public TestB.MyBuildTargetB<T> WithValueC(in int valueB3)
                    {
                        return new TestB.MyBuildTargetB<T>(this.ValueC1, this.ValueC2, valueB3);
                    }

                    /// <summary>
                    ///     <seealso cref="TestD.MyBuildTargetD{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public TestD.MyBuildTargetD<T> WithValueC(in string valueD3)
                    {
                        return new TestD.MyBuildTargetD<T>(this.ValueC1, this.ValueC2, valueD3);
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
                    (typeof(FluentFactoryGenerator), "TestFactory.Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }


    [Fact]
    public async Task Given_class_constructors_with_different_parameters_including_multiple_fluent_methods_Should_ensure_type_converters_are_generated_with_existing_types()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            namespace TestFactory
            {
                [FluentFactory]
                public static partial class Factory;
            }

            namespace TestA
            {
                public partial class MyBuildTargetA<T>
                {
                    [FluentConstructor(typeof(TestFactory.Factory), Options = FluentOptions.NoCreateMethod)]
                    public MyBuildTargetA(
                        [FluentMethod("WithValueA")]T valueA1)
                    {
                        ValueA1 = valueA1;
                    }

                    public T ValueA1 { get; set; }
                }
            }

            namespace TestC
            {
                public partial class MyBuildTargetC<T>
                {
                    [FluentConstructor(typeof(TestFactory.Factory), Options = FluentOptions.NoCreateMethod)]
                    public MyBuildTargetC(
                        [FluentMethod("WithValueA")]T valueC1,
                        [FluentMethod("WithValueB")]string valueC2)
                    {
                        ValueC1 = valueC1;
                        ValueC2 = valueC2;
                    }

                    public T ValueC1 { get; set; }

                    public string ValueC2 { get; set; }
                }
            }

            namespace TestB
            {
                public class MyBuildTargetB<T>
                {
                    [FluentConstructor(typeof(TestFactory.Factory), Options = FluentOptions.NoCreateMethod)]
                    public MyBuildTargetB(
                        [FluentMethod("WithValueA")]T valueB1,
                        [MultipleFluentMethods(typeof(MultipleMethods))]Func<string> valueB2,
                        [FluentMethod("WithValueC")]string valueB3)
                    {
                        ValueB1 = valueB1;
                        ValueB2 = valueB2;
                        ValueB3 = valueB3;
                    }

                    public T ValueB1 { get; set; }

                    public Func<string> ValueB2 { get; set; }

                    public String ValueB3 { get; set; }
                }

                public class MultipleMethods
                {
                    [FluentMethodTemplate]
                    public static Func<string> WithValueB(string multipleMethodValue1)
                    {
                        return () => multipleMethodValue1;
                    }

                    [FluentMethodTemplate]
                    public static Func<string> WithValueB(Func<string> multipleMethodValue2)
                    {
                        return multipleMethodValue2;
                    }

                    [FluentMethodTemplate]
                    public static Func<string> WithValueB<T>(T multipleMethodValue1)
                    {
                        return () => multipleMethodValue1 switch
                        {
                            not null when multipleMethodValue1.ToString() is {} txt => txt,
                            _ => string.Empty
                        };
                    }
                }
            }
            """;

        const string expected =
            """
            using System;
            using TestA;
            using TestB;
            using TestC;

            namespace TestA
            {
                public partial class MyBuildTargetA<T>
                {
                    /// <summary>
                    ///     <seealso cref="TestC.MyBuildTargetC{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public TestC.MyBuildTargetC<T> WithValueB(in string valueC2)
                    {
                        return new TestC.MyBuildTargetC<T>(this.ValueA1, valueC2);
                    }

                    /// <summary>
                    ///     <seealso cref="TestB.MyBuildTargetB{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public TestFactory.Step_1__TestFactory_Factory<T> WithValueB(in System.Func<string> multipleMethodValue2)
                    {
                        return new TestFactory.Step_1__TestFactory_Factory<T>(this.ValueA1, TestB.MultipleMethods.WithValueB(multipleMethodValue2));
                    }

                    /// <summary>
                    ///     <seealso cref="TestB.MyBuildTargetB{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public TestFactory.Step_1__TestFactory_Factory<T> WithValueB(in T multipleMethodValue1)
                    {
                        return new TestFactory.Step_1__TestFactory_Factory<T>(this.ValueA1, TestB.MultipleMethods.WithValueB<T>(multipleMethodValue1));
                    }
                }
            }

            namespace TestFactory
            {
                public static partial class Factory
                {
                    /// <summary>
                    ///     <seealso cref="TestA.MyBuildTargetA{T}"/>
                    ///     <seealso cref="TestB.MyBuildTargetB{T}"/>
                    ///     <seealso cref="TestC.MyBuildTargetC{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static TestA.MyBuildTargetA<T> WithValueA<T>(in T valueA1)
                    {
                        return new TestA.MyBuildTargetA<T>(valueA1);
                    }
                }

                /// <summary>
                ///     <seealso cref="TestB.MyBuildTargetB{T}"/>
                /// </summary>
                public struct Step_1__TestFactory_Factory<T>
                {
                    private readonly T _valueB1__parameter;
                    private readonly System.Func<string> _valueB2__parameter;
                    public Step_1__TestFactory_Factory(in T valueB1, in System.Func<string> valueB2)
                    {
                        this._valueB1__parameter = valueB1;
                        this._valueB2__parameter = valueB2;
                    }

                    /// <summary>
                    ///     <seealso cref="TestB.MyBuildTargetB{T}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public TestB.MyBuildTargetB<T> WithValueC(in string valueB3)
                    {
                        return new TestB.MyBuildTargetB<T>(this._valueB1__parameter, this._valueB2__parameter, valueB3);
                    }
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { (SourceFile, code) },
                ExpectedDiagnostics = {
                    DiagnosticResult
                        .CompilerWarning(ContainsSupersededFluentMethodTemplate.Id)
                        .WithSpan(SourceFile, 51, 36, 51, 59)
                        .WithSpan(SourceFile, 32, 48, 32, 55)
                        .WithArguments(
                            "TestB.MultipleMethods.WithValueB(string multipleMethodValue1)",
                            "System.Func<string> valueB2",
                            "TestB.MyBuildTargetB<T>.MyBuildTargetB(T valueB1, System.Func<string> valueB2, string valueB3)",
                            "the parameter 'string valueC2' in the constructor 'TestC.MyBuildTargetC<T>.MyBuildTargetC(T valueC1, string valueC2)' was used as the basis for the fluent method. Perhaps the ignored method-template can be removed or modified."),
                    new DiagnosticResult(FluentMethodTemplateSuperseded.Id, DiagnosticSeverity.Info)
                        .WithSpan(SourceFile, 69, 36, 69, 46)
                        .WithArguments(
                            "System.Func<string> TestB.MultipleMethods.WithValueB(string multipleMethodValue1)",
                            "System.Func<string> valueB2",
                            "TestB.MyBuildTargetB<T>.MyBuildTargetB(T valueB1, System.Func<string> valueB2, string valueB3)",
                            "string valueC2",
                            "TestC.MyBuildTargetC<T>.MyBuildTargetC(T valueC1, string valueC2)"
                        )
                },
                GeneratedSources =
                {
                    (typeof(FluentFactoryGenerator), "TestFactory.Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_ensure_existing_member_names_are_used_verbatim()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            namespace Test;

            [FluentFactory]
            public static partial class Shape;

            [FluentConstructor(typeof(Shape), Options = FluentOptions.NoCreateMethod)]
            public partial record Square(int Width)
            {
                public int A { get; set; } = Width;
            }

            [FluentConstructor(typeof(Shape), Options = FluentOptions.NoCreateMethod)]
            public partial class Rectangle(int width, in int height)
            {
                public int B { get; set; } = width;
                public int C { get; set; } = height;
            }

            [FluentConstructor(typeof(Shape), Options = FluentOptions.NoCreateMethod)]
            public partial record Cuboid(in int Width, int Height, in int Depth)
            {
                public int D { get; set; } = Width;
                public int E { get; set; } = Height;
                public int F { get; set; } = Depth;
            }
            """;

        const string expected =
            """
            using System;

            namespace Test
            {
                public static partial class Shape
                {
                    /// <summary>
                    ///     <seealso cref="Test.Cuboid"/>
                    ///     <seealso cref="Test.Rectangle"/>
                    ///     <seealso cref="Test.Square"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Square WithWidth(in int width)
                    {
                        return new Square(width);
                    }
                }

                public partial record Square
                {
                    /// <summary>
                    ///     <seealso cref="Test.Cuboid"/>
                    ///     <seealso cref="Test.Rectangle"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Rectangle WithHeight(in int height)
                    {
                        return new Rectangle(this.Width, height);
                    }
                }

                public partial class Rectangle
                {
                    /// <summary>
                    ///     <seealso cref="Test.Cuboid"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Cuboid WithDepth(in int depth)
                    {
                        return new Cuboid(this.B, this.C, depth);
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
                    (typeof(FluentFactoryGenerator), "Test.Shape.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Given_custom_step_Should_ensure_that_converter_arguments_are_correctly_populated()
    {
        const string code =
            """
            using System;
            using System.Collections.Generic;
            using Motiv.Generator.Attributes;

            namespace Test;

            [FluentFactory]
            public static partial class Spec
            {
            }

            public abstract class PolicyResultBase<TMetadata>
            {
            }

            [FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
            public readonly partial struct PolicyResultPredicatePropositionFactory<TModel, TMetadata>(
                [MultipleFluentMethods(typeof(PolicyResultBuildOverloads))]Func<TModel, PolicyResultBase<TMetadata>> predicate)
            {
            }

            [FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
            public readonly partial struct MultiAssertionExplanationFromPolicyPropositionFactory<TModel, TMetadata>(
                [MultipleFluentMethods(typeof(PolicyResultBuildOverloads))]Func<TModel, PolicyResultBase<TMetadata>> predicate,
                [MultipleFluentMethods(typeof(WhenTrueYieldOverloads))]Func<TModel, PolicyResultBase<TMetadata>, IEnumerable<string>> trueBecause,
                [MultipleFluentMethods(typeof(WhenFalseYieldOverloads))]Func<TModel, PolicyResultBase<TMetadata>, IEnumerable<string>> falseBecause)
            {
            }

            internal static class PolicyResultBuildOverloads
            {
                [FluentMethodTemplate]
                internal static Func<TModel, PolicyResultBase<TMetadata>> Build<TModel, TMetadata>(Func<TModel, PolicyResultBase<TMetadata>> resultFactory) => resultFactory;

                [FluentMethodTemplate]
                internal static Func<TModel, PolicyResultBase<string>> Build<TModel>(Func<TModel, PolicyResultBase<string>> resultFactory) => resultFactory;
            }

            internal class WhenTrueYieldOverloads
            {
                [FluentMethodTemplate]
                internal static Func<TModel, TResult, IEnumerable<TMetadata>> WhenTrueYield<TModel, TMetadata, TResult>(Func<TModel, TResult, IEnumerable<TMetadata>> function)
                {
                    return function;
                }
            }

            internal class WhenFalseYieldOverloads
            {
                [FluentMethodTemplate]
                internal static Func<TModel, TResult, IEnumerable<TMetadata>> WhenFalseYield<TModel, TMetadata, TResult>(Func<TModel, TResult, IEnumerable<TMetadata>> function)
                {
                    return function;
                }
            }
            """;

        const string expected =
            """
            using System;

            namespace Test
            {
                public static partial class Spec
                {
                    /// <summary>
                    ///     <seealso cref="Test.MultiAssertionExplanationFromPolicyPropositionFactory{TModel, TMetadata}"/>
                    ///     <seealso cref="Test.PolicyResultPredicatePropositionFactory{TModel, TMetadata}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static PolicyResultPredicatePropositionFactory<TModel, TMetadata> Build<TModel, TMetadata>(in System.Func<TModel, PolicyResultBase<TMetadata>> resultFactory)
                    {
                        return new PolicyResultPredicatePropositionFactory<TModel, TMetadata>(PolicyResultBuildOverloads.Build<TModel, TMetadata>(resultFactory));
                    }

                    /// <summary>
                    ///     <seealso cref="Test.MultiAssertionExplanationFromPolicyPropositionFactory{TModel, TMetadata}"/>
                    ///     <seealso cref="Test.PolicyResultPredicatePropositionFactory{TModel, TMetadata}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static PolicyResultPredicatePropositionFactory<TModel, string> Build<TModel>(in System.Func<TModel, PolicyResultBase<string>> resultFactory)
                    {
                        return new PolicyResultPredicatePropositionFactory<TModel, string>(PolicyResultBuildOverloads.Build<TModel>(resultFactory));
                    }
                }

                public readonly partial struct PolicyResultPredicatePropositionFactory<TModel, TMetadata>
                {
                    /// <summary>
                    ///     <seealso cref="Test.MultiAssertionExplanationFromPolicyPropositionFactory{TModel, TMetadata}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__Test_Spec<TModel, TMetadata> WhenTrueYield(in System.Func<TModel, PolicyResultBase<TMetadata>, System.Collections.Generic.IEnumerable<string>> function)
                    {
                        return new Step_1__Test_Spec<TModel, TMetadata>(predicate, WhenTrueYieldOverloads.WhenTrueYield<TModel, string, PolicyResultBase<TMetadata>>(function));
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.MultiAssertionExplanationFromPolicyPropositionFactory{TModel, TMetadata}"/>
                /// </summary>
                public struct Step_1__Test_Spec<TModel, TMetadata>
                {
                    private readonly System.Func<TModel, PolicyResultBase<TMetadata>> _predicate__parameter;
                    private readonly System.Func<TModel, PolicyResultBase<TMetadata>, System.Collections.Generic.IEnumerable<string>> _trueBecause__parameter;
                    public Step_1__Test_Spec(in System.Func<TModel, PolicyResultBase<TMetadata>> predicate, in System.Func<TModel, PolicyResultBase<TMetadata>, System.Collections.Generic.IEnumerable<string>> trueBecause)
                    {
                        this._predicate__parameter = predicate;
                        this._trueBecause__parameter = trueBecause;
                    }

                    /// <summary>
                    ///     <seealso cref="Test.MultiAssertionExplanationFromPolicyPropositionFactory{TModel, TMetadata}"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MultiAssertionExplanationFromPolicyPropositionFactory<TModel, TMetadata> WhenFalseYield(in System.Func<TModel, PolicyResultBase<TMetadata>, System.Collections.Generic.IEnumerable<string>> function)
                    {
                        return new MultiAssertionExplanationFromPolicyPropositionFactory<TModel, TMetadata>(this._predicate__parameter, this._trueBecause__parameter, WhenFalseYieldOverloads.WhenFalseYield<TModel, string, PolicyResultBase<TMetadata>>(function));
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
                    (typeof(FluentFactoryGenerator), "Test.Spec.g.cs", expected)
                }
            }
        }.RunAsync();
    }
}
