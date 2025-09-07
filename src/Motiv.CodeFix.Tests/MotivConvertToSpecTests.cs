using VerifyCS =
    Motiv.CodeFix.Tests.CSharpDiagnosticAnalyzerVerifier<Motiv.CodeFix.MotivAnalyzer>;

namespace Motiv.CodeFix.Tests;

public class MotivConvertToSpecTests
{
    [Fact]
    public async Task Should_analyze_and_identify_boolean_expressions_that_can_be_converted_to_specs()
    {
        // Diagnostic analyzer test
        const string code =
            """
            namespace MyNamespace;

            public class MyClass
            {
                public bool IsValid(int value)
                {
                    return value > 0;
                }
            }
            """;


        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { code },
                ExpectedDiagnostics =
                {

                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_convert_boolean_expressions_that_can_be_converted_to_spec()
    {
        // Code fix test
        const string code =
            """
            namespace MyNamespace;

            public class MyClass
            {
                public bool IsValid(int value)
                {
                    return value > 0;
                }
            }
            """;

        const string expected =
            """
            namespace MyNamespace;

            public class MyClass
            {
                public bool IsValid(int value)
                {
                    var evaluation = new Proposition().IsSatisfiedBy(value);
                    return  evaluation.Satisfied;
                }
            }

            public class Proposition : Spec<int, string>(
                Spec.Build((int value) => value > 0 && value < 100
                    .WhenTrue("value > 0 == true")
                    .WhenFalse("value > 0 == false")
                    .Create())
            {
            };
            """;


        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { code },
                GeneratedSources =
                {
                    (typeof(MotivCodeFixProvider), "Test.Factory.g.cs", expected)
                }
            }
        }.RunAsync();
    }
}
