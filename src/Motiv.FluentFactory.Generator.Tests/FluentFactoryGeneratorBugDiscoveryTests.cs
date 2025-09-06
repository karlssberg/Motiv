using Microsoft.CodeAnalysis.Testing;
using VerifyCS =
    Motiv.FluentFactory.Generator.Tests.CSharpSourceGeneratorVerifier<Motiv.FluentFactory.Generator.FluentFactoryGenerator>;

namespace Motiv.FluentFactory.Generator.Tests;

public class FluentFactoryGeneratorBugDiscoveryTests
{
    private const string SourceFile = "Source.cs";
    [Fact]
    public async Task Shoula_allow_primary_constructor_selection_deterministically()
    {
        // This tests the potential bug in CreateConstructorContexts where it selects
        // "FirstOrDefault(c => c.Parameters.Length > 0)" which might be non-deterministic
        const string source = """
            using Motiv.FluentFactory.Generator;

            namespace Test.Namespace
            {
                [FluentFactory]
                public partial class MyTarget;

                [FluentFactory]
                [FluentConstructor(typeof(MyTarget))]
                public partial class MyBuildTarget
                {
                    public MyBuildTarget(int value) { }
                    public MyBuildTarget(string name) { }
                    public MyBuildTarget(double amount) { }
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { source } }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_error_when_invalid_enum_values_in_options_conversion()
    {
        // This tests the enum conversion logic with invalid enum values.  999 bits matches NoCreateMethod (i.e. 1).
        const string source = """
            using Motiv.FluentFactory.Generator;

            namespace Test.Namespace
            {
                [FluentFactory]
                public partial class MyTarget;

                [FluentFactory]
                [FluentConstructor(typeof(MyTarget), Options = (FluentOptions)999)] // Invalid enum value
                public partial record MyBuildTarget(int Value);
            }
            """;

        const string expected = """
            using System;

            namespace Test.Namespace
            {
                public partial class MyTarget
                {
                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget"/>
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
                Sources = { (SourceFile, source) },
                GeneratedSources =
                {
                    (typeof(FluentFactoryGenerator), "Test.Namespace.MyTarget.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_allow_multiple_fluent_constructor_attributes_with_different_create_method_names()
    {
        // Tests what happens when multiple FluentConstructor attributes have different CreateMethodName values
        const string source = """
            using Motiv.FluentFactory.Generator;

            namespace Test.Namespace
            {
                [FluentFactory]
                public partial class MyTarget;

                [FluentFactory]
                [FluentConstructor(typeof(MyTarget), CreateMethodName = "CreateFirst")]
                [FluentConstructor(typeof(MyTarget), CreateMethodName = "CreateSecond")]
                public partial record MyBuildTarget(int Value);
            }
            """;

        const string expected =
            """
            using System;

            namespace Test.Namespace
            {
                public partial class MyTarget
                {
                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Namespace_MyTarget WithValue(in int value)
                    {
                        return new Step_0__Test_Namespace_MyTarget(value);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.Namespace.MyBuildTarget"/>
                /// </summary>
                public struct Step_0__Test_Namespace_MyTarget
                {
                    private readonly int _value__parameter;
                    internal Step_0__Test_Namespace_MyTarget(in int value)
                    {
                        this._value__parameter = value;
                    }

                    /// <summary>
                    /// Creates a new instance using constructor Test.Namespace.MyBuildTarget.MyBuildTarget(int Value).
                    ///
                    ///     <seealso cref="Test.Namespace.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget CreateFirst()
                    {
                        return new MyBuildTarget(this._value__parameter);
                    }

                    /// <summary>
                    /// Creates a new instance using constructor Test.Namespace.MyBuildTarget.MyBuildTarget(int Value).
                    ///
                    ///     <seealso cref="Test.Namespace.MyBuildTarget"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget CreateSecond()
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
                Sources = { (SourceFile, source) },
                GeneratedSources =
                {
                    (typeof(FluentFactoryGenerator), "Test.Namespace.MyTarget.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Theory]
    [ClassData(typeof(InvalidMethodNames))]
    public async Task Should_error_when_invalid_create_method_names(string invalidMethodName)
    {
        // Tests CreateMethodName handling with edge case error values and putting red squiggles on them
        var source =
          $$"""
            using Motiv.FluentFactory.Generator;

            namespace Test.Namespace
            {
                [FluentFactory]
                public partial class MyTarget;

                [FluentConstructor(typeof(MyTarget), CreateMethodName = "{{invalidMethodName}}")]
                public partial record EmptyName(int Value);
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { (SourceFile, source) },
                ExpectedDiagnostics =
                {
                    DiagnosticResult.CompilerError("MOTIV007")
                        .WithSpan("Source.cs", 9, 27, 9, 36)
                        .WithMessage("CreateMethodName must be a valid identifier")
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_error_when_duplicate_fluent_constructor_attributes_with_identical_parameters()
    {
        // Tests what happens with completely identical FluentConstructor attributes
        const string source =
            """
            using Motiv.FluentFactory.Generator;

            namespace Test.Namespace
            {
                [FluentFactory]
                public partial class MyTarget;

                [FluentConstructor(typeof(MyTarget), CreateMethodName = "Create")]
                [FluentConstructor(typeof(MyTarget), CreateMethodName = "Create")] // Exact duplicate
                public partial record DuplicateAttributes(int Value);
            }
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (SourceFile, source) } },
            ExpectedDiagnostics =
            {
                DiagnosticResult.CompilerError("MOTIV008")
                    .WithSpan("Source.cs", 10, 27, 10, 46)
                    .WithMessage("CreateMethodName must be unique"),
                DiagnosticResult.CompilerError("MOTIV008")
                    .WithSpan("Source.cs", 10, 27, 10, 46)
                    .WithMessage("CreateMethodName must be unique")
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_allow_duplicates_method_names_located_on_different_decision_paths()
    {
        // Tests what happens with completely identical FluentConstructor attributes
        const string source =
            """
            using Motiv.FluentFactory.Generator;

            namespace Test.Namespace
            {
                [FluentFactory]
                public partial class MyTarget;

                // constructor with a string parameter
                [FluentConstructor(typeof(MyTarget), CreateMethodName = "Create")]
                public partial record MyRecordA(string Value);

                // constructor with an int parameter
                [FluentConstructor(typeof(MyTarget), CreateMethodName = "Create")]
                public partial record MyRecordB(int Value);
            }
            """;

        const string expected =
            """
            using System;

            namespace Test.Namespace
            {
                public partial class MyTarget
                {
                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyRecordA"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Namespace_MyTarget WithValue(in string value)
                    {
                        return new Step_0__Test_Namespace_MyTarget(value);
                    }

                    /// <summary>
                    ///     <seealso cref="Test.Namespace.MyRecordB"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_1__Test_Namespace_MyTarget WithValue(in int value)
                    {
                        return new Step_1__Test_Namespace_MyTarget(value);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.Namespace.MyRecordA"/>
                /// </summary>
                public struct Step_0__Test_Namespace_MyTarget
                {
                    private readonly string _value__parameter;
                    internal Step_0__Test_Namespace_MyTarget(in string value)
                    {
                        this._value__parameter = value;
                    }

                    /// <summary>
                    /// Creates a new instance using constructor Test.Namespace.MyRecordA.MyRecordA(string Value).
                    ///
                    ///     <seealso cref="Test.Namespace.MyRecordA"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyRecordA Create()
                    {
                        return new MyRecordA(this._value__parameter);
                    }
                }

                /// <summary>
                ///     <seealso cref="Test.Namespace.MyRecordB"/>
                /// </summary>
                public struct Step_1__Test_Namespace_MyTarget
                {
                    private readonly int _value__parameter;
                    internal Step_1__Test_Namespace_MyTarget(in int value)
                    {
                        this._value__parameter = value;
                    }

                    /// <summary>
                    /// Creates a new instance using constructor Test.Namespace.MyRecordB.MyRecordB(int Value).
                    ///
                    ///     <seealso cref="Test.Namespace.MyRecordB"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyRecordB Create()
                    {
                        return new MyRecordB(this._value__parameter);
                    }
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { (SourceFile, source) },
                GeneratedSources =
                {
                    (typeof(FluentFactoryGenerator), "Test.Namespace.MyTarget.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_error_when_fluent_factory_attribute_on_non_target_type()
    {
        // Tests what happens when FluentFactory is missing on referenced type
        const string source =
            """
            using Motiv.FluentFactory.Generator;

            namespace Test.Namespace
            {
                // Missing [FluentFactory] attribute
                public partial class MyTarget;

                [FluentFactory]
                [FluentConstructor(typeof(MyTarget))] // References type without FluentFactory
                public partial record InvalidTarget(int Value);
            }
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (SourceFile, source) } },
            ExpectedDiagnostics =
            {
                DiagnosticResult.CompilerError("MOTIV009")
                    .WithSpan("Source.cs", 10, 27, 10, 40)
                    .WithMessage("FluentConstructor references type 'Test.Namespace.MyTarget' which does not have the FluentFactory attribute"),
            }
        }.RunAsync();
    }
}
