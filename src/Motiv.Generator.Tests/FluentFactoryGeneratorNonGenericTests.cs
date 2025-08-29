using Motiv.Generator.FluentFactory;
using VerifyCS =
    Motiv.Generator.Tests.CSharpSourceGeneratorVerifier<Motiv.Generator.FluentFactory.FluentFactoryGenerator>;

namespace Motiv.Generator.Tests;

public class FluentFactoryGeneratorNonGenericTests
{
    [Fact]
    public async Task Should_generate_when_applied_to_a_class_constructor_with_a_single_parameter()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            public class MyBuildTarget
            {
                [FluentConstructor(typeof(Factory))]
                public MyBuildTarget(int value)
                {
                    Value = value;
                }

                public int Value { get; set; }
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
                    /// Constructor type:
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory WithValue(in int value)
                    {
                        return new Step_0__Test_Factory(value);
                    }
                }

                /// <summary>
                /// Constructor type:
                ///     <seealso cref="Test.MyBuildTarget"/>
                /// </summary>
                public struct Step_0__Test_Factory
                {
                    private readonly int _value__parameter;
                    public Step_0__Test_Factory(in int value)
                    {
                        this._value__parameter = value;
                    }

                    /// <summary>
                    /// Creates a new instance using constructor Test.MyBuildTarget.MyBuildTarget(int value).
                    ///
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget Create()
                    {
                        return new MyBuildTarget(this._value__parameter);
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
            using Motiv.Generator.Attributes;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            public class MyBuildTarget
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyBuildTarget(
                    int number,
                    string text)
                {
                    Number = number;
                    Text = text;
                }

                public int Number { get; set; }

                public string Text { get; set; }
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
                    /// Constructor type:
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory WithNumber(in int number)
                    {
                        return new Step_0__Test_Factory(number);
                    }
                }

                /// <summary>
                /// Constructor type:
                ///     <seealso cref="Test.MyBuildTarget"/>
                /// </summary>
                public struct Step_0__Test_Factory
                {
                    private readonly int _number__parameter;
                    public Step_0__Test_Factory(in int number)
                    {
                        this._number__parameter = number;
                    }

                    /// <summary>
                    /// Constructor type:
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget WithText(in string text)
                    {
                        return new MyBuildTarget(this._number__parameter, text);
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
            using Motiv.Generator.Attributes;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            public class MyBuildTarget
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyBuildTarget(
                    int number,
                    string text,
                    Guid id)
                {
                    Number = number;
                    Text = text;
                    Id = id;
                }

                public int Number { get; set; }

                public string Text { get; set; }

                public Guid Id { get; set; }
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
                    /// Constructor type:
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory WithNumber(in int number)
                    {
                        return new Step_0__Test_Factory(number);
                    }
                }

                /// <summary>
                /// Constructor type:
                ///     <seealso cref="Test.MyBuildTarget"/>
                /// </summary>
                public struct Step_0__Test_Factory
                {
                    private readonly int _number__parameter;
                    public Step_0__Test_Factory(in int number)
                    {
                        this._number__parameter = number;
                    }

                    /// <summary>
                    /// Constructor type:
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__Test_Factory WithText(in string text)
                    {
                        return new Step_1__Test_Factory(this._number__parameter, text);
                    }
                }

                /// <summary>
                /// Constructor type:
                ///     <seealso cref="Test.MyBuildTarget"/>
                /// </summary>
                public struct Step_1__Test_Factory
                {
                    private readonly int _number__parameter;
                    private readonly string _text__parameter;
                    public Step_1__Test_Factory(in int number, in string text)
                    {
                        this._number__parameter = number;
                        this._text__parameter = text;
                    }

                    /// <summary>
                    /// Constructor type:
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget WithId(in System.Guid id)
                    {
                        return new MyBuildTarget(this._number__parameter, this._text__parameter, id);
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
            using System;
            using System.Text.RegularExpressions;
            using Motiv.Generator.Attributes;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            public class MyBuildTarget
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyBuildTarget(
                    int number,
                    string text,
                    Guid id,
                    Regex regex)
                {
                    Number = number;
                    Text = text;
                    Id = id;
                    Regex = regex;
                }

                public int Number { get; set; }

                public string Text { get; set; }

                public Guid Id { get; set; }

                public Regex Regex { get; set; }
            }
            """;

        const string expected =
            """
            using System;
            using System.Text.RegularExpressions;

            namespace Test
            {
                public static partial class Factory
                {
                    /// <summary>
                    /// Constructor type:
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory WithNumber(in int number)
                    {
                        return new Step_0__Test_Factory(number);
                    }
                }

                /// <summary>
                /// Constructor type:
                ///     <seealso cref="Test.MyBuildTarget"/>
                /// </summary>
                public struct Step_0__Test_Factory
                {
                    private readonly int _number__parameter;
                    public Step_0__Test_Factory(in int number)
                    {
                        this._number__parameter = number;
                    }

                    /// <summary>
                    /// Constructor type:
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__Test_Factory WithText(in string text)
                    {
                        return new Step_1__Test_Factory(this._number__parameter, text);
                    }
                }

                /// <summary>
                /// Constructor type:
                ///     <seealso cref="Test.MyBuildTarget"/>
                /// </summary>
                public struct Step_1__Test_Factory
                {
                    private readonly int _number__parameter;
                    private readonly string _text__parameter;
                    public Step_1__Test_Factory(in int number, in string text)
                    {
                        this._number__parameter = number;
                        this._text__parameter = text;
                    }

                    /// <summary>
                    /// Constructor type:
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_2__Test_Factory WithId(in System.Guid id)
                    {
                        return new Step_2__Test_Factory(this._number__parameter, this._text__parameter, id);
                    }
                }

                /// <summary>
                /// Constructor type:
                ///     <seealso cref="Test.MyBuildTarget"/>
                /// </summary>
                public struct Step_2__Test_Factory
                {
                    private readonly int _number__parameter;
                    private readonly string _text__parameter;
                    private readonly System.Guid _id__parameter;
                    public Step_2__Test_Factory(in int number, in string text, in System.Guid id)
                    {
                        this._number__parameter = number;
                        this._text__parameter = text;
                        this._id__parameter = id;
                    }

                    /// <summary>
                    /// Constructor type:
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget WithRegex(in System.Text.RegularExpressions.Regex regex)
                    {
                        return new MyBuildTarget(this._number__parameter, this._text__parameter, this._id__parameter, regex);
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
