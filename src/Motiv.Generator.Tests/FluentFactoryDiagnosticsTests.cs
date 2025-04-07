
using Microsoft.CodeAnalysis.Testing;
using Motiv.Generator.FluentFactory;
using static Microsoft.CodeAnalysis.DiagnosticSeverity;
using VerifyCS =
    Motiv.Generator.Tests.CSharpSourceGeneratorVerifier<Motiv.Generator.FluentFactory.FluentFactoryGenerator>;

namespace Motiv.Generator.Tests;


public class FluentFactoryDiagnosticsTests
{
    [Fact]
    public async Task Given_two_regular_methods_collide_when_and_there_are_no_more_steps_Should_generate_an_ambiguous_constructor_selection_error()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            namespace MyNamespace
            {
                [FluentFactory]
                public static partial class Factory;

                public partial class Person
                {
                    [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                    public Person(string name)
                    {
                        Name = name;
                    }

                    public string Name { get; }
                }

                public partial class Company
                {
                    [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                    public Company(string name)
                    {
                        Name = name;
                    }

                    public string Name { get; }
                }
            }
            """;

        const string expected =
            """
            using System;

            namespace MyNamespace
            {
                public static partial class Factory
                {
                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="MyNamespace.Company"/>
                    ///     <seealso cref="MyNamespace.Person"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Company WithName(in string name)
                    {
                        return new Company(name);
                    }
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { ("Source.cs", code) },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult("MOTIV001", Error)
                        .WithSpan("Source.cs",12, 30, 12, 34)
                },
                GeneratedSources =
                {
                    (typeof(FluentFactoryGenerator), "MyNamespace.Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Given_a_regular_and_multi_methods_collide_when_they_service_different_different_source_parameter_types_Should_generate_a_step_method_collisions_error()
    {
        const string code =
            """
            using System;
            using Motiv.Generator.Attributes;

            namespace MyNamespace
            {
                [FluentFactory]
                public static partial class Factory;

                public partial class Person
                {
                    [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                    public Person(string name)
                    {
                        Name = name;
                    }

                    public string Name { get; }
                }

                public partial class Company
                {
                    [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                    public Company([MultipleFluentMethods(typeof(Convert))]int? id)
                    {
                        Id = id;
                    }

                    public int? Id { get; }
                }

                public class Convert
                {
                    [FluentMethodTemplate]
                    public static int? WithName(string name)
                    {
                        return int.TryParse(name, out var id) ? id : null;
                    }
                }
            }
            """;

        const string expected =
            """
            using System;

            namespace MyNamespace
            {
                public static partial class Factory
                {
                    /// <summary>
                    /// Candidate constructor types:
                    ///     <seealso cref="MyNamespace.Person"/>
                    /// </summary>
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Person WithName(in string name)
                    {
                        return new Person(name);
                    }
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { ("Source.cs", code) },

                ExpectedDiagnostics =
                {
                    new DiagnosticResult("MOTIV001", Error)
                        .WithSpan("Source.cs", 23, 69, 23, 71)
                },
                GeneratedSources =
                {
                    (typeof(FluentFactoryGenerator), "MyNamespace.Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }


}
