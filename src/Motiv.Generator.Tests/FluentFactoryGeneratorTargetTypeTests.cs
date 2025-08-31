using Motiv.Generator.FluentFactory;

namespace Motiv.Generator.Tests;
using VerifyCS = CSharpSourceGeneratorVerifier<FluentFactoryGenerator>;

public class FluentFactoryGeneratorTargetTypeTests
{
    [Fact]
    public async Task Should_generate_for_a_class_target_type()
    {
        const string code =
            """
            using System;
            using Motiv.Generator;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            public class MyBuildTarget
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                public MyBuildTarget(in int value)
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


    [Fact]
    public async Task Should_generate_for_a_class_target_type_with_primary_constructor()
    {
        const string code =
            """
            using System;
            using Motiv.Generator;

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

    [Fact]
    public async Task Should_generate_for_a_struct_target_type()
    {
        const string code =
            """
            using System;
            using Motiv.Generator;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            public struct MyBuildTarget
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
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

    [Fact]
    public async Task Should_generate_for_a_struct_target_type_with_primary_constructor()
    {
        const string code =
            """
            using System;
            using Motiv.Generator;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
            public struct MyBuildTarget(int value)
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

    [Fact]
    public async Task Should_generate_for_a_record_target_type()
    {
        const string code =
            """
            using System;
            using Motiv.Generator;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            public record MyBuildTarget
            {
                [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
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

    [Fact]
    public async Task Should_generate_for_a_record_target_type_with_primary_constructor()
    {
        const string code =
            """
            using System;
            using Motiv.Generator;

            namespace Test;

            [FluentFactory]
            public static partial class Factory;

            [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
            public record MyBuildTarget(int Value)
            {
                public int Value { get; set; } = Value;
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
}
