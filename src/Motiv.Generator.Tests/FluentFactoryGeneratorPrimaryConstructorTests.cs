using Motiv.Generator.FluentFactory;
using VerifyCS =
    Motiv.Generator.Tests.CSharpSourceGeneratorVerifier<Motiv.Generator.FluentFactory.FluentFactoryGenerator>;

namespace Motiv.Generator.Tests;

public class FluentFactoryGeneratorPrimaryConstructorTests
{
    [Fact]
    public async Task Should_generate_when_applied_to_a_class_primary_constructor_with_a_single_parameter()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
            public class MyBuildTarget(int value)
            {
                public int Value { get; set; } = value;
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
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static MyBuildTarget WithValue(in int value)
                    {
                        return new MyBuildTarget(value);
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

#if NET6_0_OR_GREATER

    [Fact]
    public async Task Should_generate_when_applied_to_a_positional_record_primary_constructor_with_two_parameters()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
            public record MyBuildTarget(int Number, string text);
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
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory WithNumber(in int number)
                    {
                        return new Step_0__Test_Factory(number);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
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
                    /// Candidate constructor types:
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
#endif

    [Fact]
    public async Task Should_generate_when_applied_to_a_record_primary_constructor_with_two_parameters()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
            public record MyBuildTarget(
                int Number,
                string text)
            {
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
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory WithNumber(in int number)
                    {
                        return new Step_0__Test_Factory(number);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
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
                    /// Candidate constructor types:
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
    public async Task Should_generate_when_applied_to_a_struct_primary_constructor_with_three_parameters()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
            public struct MyBuildTarget(
                int number,
                string text,
                Guid id)
            {
                public int Number { get; set; } = number;

                public string Text { get; set; } = text;

                public Guid Id { get; set; } = id;
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
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory WithNumber(in int number)
                    {
                        return new Step_0__Test_Factory(number);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
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
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__Test_Factory WithText(in string text)
                    {
                        return new Step_1__Test_Factory(this._number__parameter, text);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
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
                    /// Candidate constructor types:
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
    public async Task Should_generate_when_applied_to_a_ref_struct_primary_constructor_with_four_parameters()
    {
        const string code =
            """
            using System;
            using System.Text.RegularExpressions;
            using Motiv.Generator.Attributes;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
            public ref struct MyBuildTarget(
                int number,
                string text,
                Guid id,
                Regex regex)
            {
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
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory WithNumber(in int number)
                    {
                        return new Step_0__Test_Factory(number);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
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
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__Test_Factory WithText(in string text)
                    {
                        return new Step_1__Test_Factory(this._number__parameter, text);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
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
                    /// Candidate constructor types:
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_2__Test_Factory WithId(in System.Guid id)
                    {
                        return new Step_2__Test_Factory(this._number__parameter, this._text__parameter, id);
                    }
                }

                /// <summary>
                /// Candidate constructor types:
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
                    /// Candidate constructor types:
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
