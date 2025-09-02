using VerifyCS =
    Motiv.FluentFactory.Generator.Tests.CSharpSourceGeneratorVerifier<Motiv.FluentFactory.Generator.FluentFactoryGenerator>;

namespace Motiv.FluentFactory.Generator.Tests;

public class FluentFactoryGeneratorBugDiscoveryTests
{
    private const string SourceFile = "Source.cs";
    [Fact]
    public async Task Should_handle_primary_constructor_selection_deterministically()
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
    public async Task Should_handle_invalid_enum_values_in_options_conversion()
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
    public async Task Should_handle_multiple_fluent_constructor_attributes_with_conflicting_create_method_names()
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

    [Fact]
    public async Task Should_handle_empty_or_whitespace_create_method_names()
    {
        // Tests CreateMethodName handling with edge case values
        const string source = """
            using Motiv.FluentFactory.Generator;

            namespace Test.Namespace
            {
                [FluentFactory]
                public partial class MyTarget;

                [FluentFactory]
                [FluentConstructor(typeof(MyTarget), CreateMethodName = "")]
                public partial record EmptyName(int Value);

                [FluentFactory]
                [FluentConstructor(typeof(MyTarget), CreateMethodName = "   ")]
                public partial record WhitespaceName(int Value);

                [FluentFactory]
                [FluentConstructor(typeof(MyTarget), CreateMethodName = null)]
                public partial record NullName(int Value);
            }
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { source } }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_handle_generic_type_attribute_matching_edge_cases()
    {
        // Tests the IsRootTypeDecoratedWithAttribute logic with complex generic scenarios
        const string source = """
            using Motiv.FluentFactory.Generator;

            namespace Test.Namespace
            {
                [FluentFactory]
                public partial class MyTarget<T>;

                public partial class MyTarget<T, U>; // No FluentFactory attribute

                [FluentFactory]
                [FluentConstructor(typeof(MyTarget<>))]   // Should match MyTarget<T>
                public partial record ValidMatch<T>(T Value);

                [FluentFactory]
                [FluentConstructor(typeof(MyTarget<,>))]  // Should NOT match MyTarget<T>
                public partial record InvalidMatch<T>(T Value);
            }
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { source } }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_handle_enum_flag_combinations_correctly()
    {
        // Tests the complex enum flag parsing logic
        const string source = """
            using Motiv.FluentFactory.Generator;

            namespace Test.Namespace
            {
                [FluentFactory]
                public partial class MyTarget;

                [FluentFactory]
                [FluentConstructor(typeof(MyTarget), Options = FluentOptions.NoCreateMethod | FluentOptions.None)]
                public partial record FlagCombination1(int Value);

                [FluentFactory]
                [FluentConstructor(typeof(MyTarget), Options = (FluentOptions)0)]
                public partial record ExplicitZero(int Value);

                [FluentFactory]
                [FluentConstructor(typeof(MyTarget), Options = (FluentOptions)(-1))]
                public partial record NegativeValue(int Value);
            }
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { source } }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_handle_duplicate_fluent_constructor_attributes_with_identical_parameters()
    {
        // Tests what happens with completely identical FluentConstructor attributes
        const string source = """
            using Motiv.FluentFactory.Generator;

            namespace Test.Namespace
            {
                [FluentFactory]
                public partial class MyTarget;

                [FluentFactory]
                [FluentConstructor(typeof(MyTarget), CreateMethodName = "Create")]
                [FluentConstructor(typeof(MyTarget), CreateMethodName = "Create")] // Exact duplicate
                public partial record DuplicateAttributes(int Value);
            }
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { source } }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_handle_fluent_factory_attribute_on_non_target_type()
    {
        // Tests what happens when FluentFactory is missing on referenced type
        const string source = """
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
            TestState = { Sources = { source } }
        }.RunAsync();
    }
}
