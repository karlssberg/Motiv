using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Motiv.Generator.FluentFactory;
using VerifyCS =
    Motiv.Generator.Tests.CSharpSourceGeneratorVerifier<Motiv.Generator.FluentFactory.FluentFactoryGenerator>;

namespace Motiv.Generator.Tests;

public class FluentFactoryXmlDocumentationTests
{
    private const string SourceFile = "Source.cs";

    [Fact]
    public async Task Should_extract_parameter_specific_documentation_for_regular_method()
    {
        const string code =
            """
            using Motiv.Generator.Attributes;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            public class MyBuildTarget
            {
                /// <summary>
                /// Constructs a new instance of MyBuildTarget.
                /// </summary>
                /// <param name="value">The initial value for the target.</param>
                [FluentConstructor(typeof(Factory))]
                public MyBuildTarget(string value)
                {
                    Value = value;
                }

                public string Value { get; set; }
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
                    /// The initial value for the target.
                    ///
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory WithValue(in string value)
                    {
                        return new Step_0__Test_Factory(value);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.MyBuildTarget"/>
                /// </summary>
                public struct Step_0__Test_Factory
                {
                    private readonly string _value__parameter;
                    public Step_0__Test_Factory(in string value)
                    {
                        this._value__parameter = value;
                    }

                    /// <summary>
                    /// Creates a new instance using constructor Test.MyBuildTarget.MyBuildTarget(string value).
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
                Sources = { (SourceFile, code) },
                GeneratedSources =
                {
                    (typeof(FluentFactoryGenerator), "Test.Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_fallback_to_constructor_documentation_when_parameter_doc_unavailable()
    {
        const string code =
            """
            using Motiv.Generator.Attributes;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            public class MyBuildTarget
            {
                /// <summary>
                /// Constructs a new instance with the given value.
                /// </summary>
                [FluentConstructor(typeof(Factory))]
                public MyBuildTarget(string value)
                {
                    Value = value;
                }

                public string Value { get; set; }
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
                    /// Constructs a new instance with the given value.
                    ///
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory WithValue(in string value)
                    {
                        return new Step_0__Test_Factory(value);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.MyBuildTarget"/>
                /// </summary>
                public struct Step_0__Test_Factory
                {
                    private readonly string _value__parameter;
                    public Step_0__Test_Factory(in string value)
                    {
                        this._value__parameter = value;
                    }

                    /// <summary>
                    /// Creates a new instance using constructor Test.MyBuildTarget.MyBuildTarget(string value).
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
                Sources = { (SourceFile, code) },
                GeneratedSources =
                {
                    (typeof(FluentFactoryGenerator), "Test.Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_use_template_method_documentation_for_multiple_fluent_methods()
    {
        const string code =
            """
            using Motiv.Generator.Attributes;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            public class MyBuildTarget
            {
                /// <summary>
                /// Constructs a new instance of MyBuildTarget.
                /// </summary>
                /// <param name="value">The input value to process.</param>
                [FluentConstructor(typeof(Factory))]
                public MyBuildTarget([MultipleFluentMethods(typeof(Methods))]string value)
                {
                    Value = value;
                }

                public string Value { get; set; }
            }

            public static class Methods
            {
                /// <summary>
                /// Sets the value directly from a string.
                /// </summary>
                /// <param name="value">The string value to set.</param>
                /// <returns>The processed string.</returns>
                [FluentMethodTemplate]
                public static string SetValue(string value) => value;

                /// <summary>
                /// Sets the value from an integer by converting to string.
                /// </summary>
                /// <param name="value">The integer value to convert.</param>
                /// <returns>The converted string.</returns>
                [FluentMethodTemplate]
                public static string SetValue(int value) => value.ToString();
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
                    /// Sets the value directly from a string.
                    ///
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    /// <param name="value">The string value to set.</param>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory SetValue(in string value)
                    {
                        return new Step_0__Test_Factory(Methods.SetValue(value));
                    }

                    /// <summary>
                    /// Sets the value from an integer by converting to string.
                    ///
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    /// <param name="value">The integer value to convert.</param>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory SetValue(in int value)
                    {
                        return new Step_0__Test_Factory(Methods.SetValue(value));
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.MyBuildTarget"/>
                /// </summary>
                public struct Step_0__Test_Factory
                {
                    private readonly string _value__parameter;
                    public Step_0__Test_Factory(in string value)
                    {
                        this._value__parameter = value;
                    }

                    /// <summary>
                    /// Creates a new instance using constructor Test.MyBuildTarget.MyBuildTarget(string value).
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
                Sources = { (SourceFile, code) },
                GeneratedSources =
                {
                    (typeof(FluentFactoryGenerator), "Test.Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_handle_multiple_parameters_with_individual_documentation()
    {
        const string code =
            """
            using Motiv.Generator.Attributes;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            public class MyBuildTarget
            {
                /// <summary>
                /// Constructs a new instance with multiple parameters.
                /// </summary>
                /// <param name="name">The name of the instance.</param>
                /// <param name="value">The numeric value to store.</param>
                [FluentConstructor(typeof(Factory))]
                public MyBuildTarget(string name, int value)
                {
                    Name = name;
                    Value = value;
                }

                public string Name { get; set; }
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
                    /// The name of the instance.
                    ///
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory WithName(in string name)
                    {
                        return new Step_0__Test_Factory(name);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.MyBuildTarget"/>
                /// </summary>
                public struct Step_0__Test_Factory
                {
                    private readonly string _name__parameter;
                    public Step_0__Test_Factory(in string name)
                    {
                        this._name__parameter = name;
                    }

                    /// <summary>
                    /// The numeric value to store.
                    ///
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public Step_1__Test_Factory WithValue(in int value)
                    {
                        return new Step_1__Test_Factory(this._name__parameter, value);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.MyBuildTarget"/>
                /// </summary>
                public struct Step_1__Test_Factory
                {
                    private readonly string _name__parameter;
                    private readonly int _value__parameter;
                    public Step_1__Test_Factory(in string name, in int value)
                    {
                        this._name__parameter = name;
                        this._value__parameter = value;
                    }

                    /// <summary>
                    /// Creates a new instance using constructor Test.MyBuildTarget.MyBuildTarget(string name, int value).
                    ///
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget Create()
                    {
                        return new MyBuildTarget(this._name__parameter, this._value__parameter);
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
    public async Task Should_handle_malformed_xml_documentation_gracefully()
    {
        const string code =
            """
            using Motiv.Generator.Attributes;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            public class MyBuildTarget
            {
                /// <summary>
                /// Constructs a new instance.
                /// <param name="value">Unclosed parameter tag
                /// </summary>
                [FluentConstructor(typeof(Factory))]
                public MyBuildTarget(string value)
                {
                    Value = value;
                }

                public string Value { get; set; }
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
                    /// <!-- Badly formed XML comment ignored for member "M:Test.MyBuildTarget.#ctor(System.String)" -->
                    ///
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory WithValue(in string value)
                    {
                        return new Step_0__Test_Factory(value);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.MyBuildTarget"/>
                /// </summary>
                public struct Step_0__Test_Factory
                {
                    private readonly string _value__parameter;
                    public Step_0__Test_Factory(in string value)
                    {
                        this._value__parameter = value;
                    }

                    /// <summary>
                    /// Creates a new instance using constructor Test.MyBuildTarget.MyBuildTarget(string value).
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
                Sources = { (SourceFile, code) },
                GeneratedSources =
                {
                    (typeof(FluentFactoryGenerator), "Test.Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_prioritize_template_method_documentation_over_parameter_documentation()
    {
        const string code =
            """
            using Motiv.Generator.Attributes;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            public class MyBuildTarget
            {
                /// <summary>
                /// Constructs a new instance.
                /// </summary>
                /// <param name="processor">This parameter documentation should be ignored.</param>
                [FluentConstructor(typeof(Factory))]
                public MyBuildTarget([MultipleFluentMethods(typeof(Methods))]string processor)
                {
                    Value = processor;
                }

                public string Value { get; set; }
            }

            public static class Methods
            {
                /// <summary>
                /// This template method documentation should take precedence.
                /// </summary>
                [FluentMethodTemplate]
                public static string Process(string input) => input;
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
                    /// This template method documentation should take precedence.
                    ///
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Factory Process(in string input)
                    {
                        return new Step_0__Test_Factory(Methods.Process(input));
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.MyBuildTarget"/>
                /// </summary>
                public struct Step_0__Test_Factory
                {
                    private readonly string _processor__parameter;
                    public Step_0__Test_Factory(in string processor)
                    {
                        this._processor__parameter = processor;
                    }

                    /// <summary>
                    /// Creates a new instance using constructor Test.MyBuildTarget.MyBuildTarget(string processor).
                    ///
                    ///     <seealso cref="Test.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget Create()
                    {
                        return new MyBuildTarget(this._processor__parameter);
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
}
