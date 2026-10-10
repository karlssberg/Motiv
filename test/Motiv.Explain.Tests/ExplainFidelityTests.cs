namespace Motiv.Explain.Tests;

/// <summary>
///     Each program runs twice, as a prod build and as an explain build, and must print the same lines in
///     both. The explain build must also have logged a decision, so a method that was quietly skipped fails.
/// </summary>
public class ExplainFidelityTests
{
    private static string Program(string rules, string probeBody) =>
        $$"""
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using Motiv.Explain;

        {{rules}}

        public static class Probe
        {
            public static List<string> Run()
            {
                var log = new List<string>();
        #if MOTIV_EXPLAIN
                MotivExplainLog.Sink = decision => log.Add(decision.ToString());
        #endif
                {{probeBody}}
                return log;
            }
        }
        """;

    private static void ShouldExplainWithoutChangingTheAnswer(string rules, string probeBody, bool allowUnsafe = false)
    {
        var source = Program(rules, probeBody);
        var prod = GeneratorHarness.Run(source, explain: false, allowUnsafe: allowUnsafe).Load().Run();

        var explained = GeneratorHarness.Run(source, explain: true, allowUnsafe: allowUnsafe).Load().Run();

        explained.ShouldContain(line => line.StartsWith("[motiv-explain]"));
        explained.Where(line => !line.StartsWith("[motiv-explain]")).ShouldBe(prod);
    }

    [Fact]
    public void A_static_call_named_like_an_object_member_still_binds_to_the_user_s_method()
    {
        ShouldExplainWithoutChangingTheAnswer(
            """
            public static partial class Rules
            {
                public static new bool Equals(object a, object b) => true;
                private static string ToString(int n) => "n";

                [Explain]
                public static bool IsMatch(int n) => n > 0 && Equals(n, 5) && ToString(n) == "n";
            }
            """,
            """log.Add("result: " + Rules.IsMatch(1));""");
    }

    [Theory]
    [InlineData("[Explain] public static partial bool IsSmall(int n);", "public static partial bool IsSmall(int n) => n > 0 && n < 3;")]
    [InlineData("public static partial bool IsSmall(int n);", "[Explain] public static partial bool IsSmall(int n) => n > 0 && n < 3;")]
    public void A_partial_method_is_explained_whichever_half_is_marked(string definition, string implementation)
    {
        ShouldExplainWithoutChangingTheAnswer(
            $$"""
            public static partial class Rules
            {
                {{definition}}
                {{implementation}}
            }
            """,
            """log.Add("result: " + Rules.IsSmall(2));""");
    }

    [Fact]
    public void An_unsafe_method_is_explained()
    {
        ShouldExplainWithoutChangingTheAnswer(
            """
            public static partial class Rules
            {
                [Explain]
                public static unsafe bool IsBig(int n) => n > 0 && *&n > 1;
            }
            """,
            """log.Add("result: " + Rules.IsBig(2));""",
            allowUnsafe: true);
    }

    [Fact]
    public void A_parameter_named_with_an_underscore_or_this_is_unpacked_by_name()
    {
        ShouldExplainWithoutChangingTheAnswer(
            """
            public partial class Rules
            {
                private int _floor = 0;

                [Explain]
                public bool AreBig(int _, int @this) => _ > _floor && @this > _floor;
            }
            """,
            """log.Add("result: " + new Rules().AreBig(1, 2));""");
    }

    [Fact]
    public void Types_and_namespaces_named_after_keywords_are_explained()
    {
        ShouldExplainWithoutChangingTheAnswer(
            """
            namespace Shop.@event
            {
                public static partial class @class
                {
                    [Explain]
                    public static bool IsSmall(int n) => n > 0 && n < 3;
                }
            }
            """,
            """log.Add("result: " + Shop.@event.@class.IsSmall(1));""");
    }

    [Fact]
    public void A_call_inside_an_expression_tree_is_left_alone()
    {
        var source = Program(
            """
            public static partial class Rules
            {
                [Explain]
                public static bool IsSmall(int n) => n > 0 && n < 3;
            }
            """,
            """
            System.Linq.Expressions.Expression<Func<bool>> tree = () => Rules.IsSmall(2);
            log.Add("method: " + ((System.Linq.Expressions.MethodCallExpression)tree.Body).Method.DeclaringType!.Name);
            log.Add("tree: " + tree.Compile()());
            """);

        GeneratorHarness.Run(source, explain: true).Load().Run().ShouldBe(["method: Rules", "tree: True"]);
    }

    [Fact]
    public void A_call_inside_a_queryable_query_is_left_alone()
    {
        var source = Program(
            """
            public static partial class Rules
            {
                [Explain]
                public static bool IsSmall(int n) => n > 0 && n < 3;
            }
            """,
            """
            var query = from n in new[] { 1, 5 }.AsQueryable() where Rules.IsSmall(n) select n;
            log.Add("query: " + string.Join(",", query));
            """);

        GeneratorHarness.Run(source, explain: true).Load().Run().ShouldBe(["query: 1"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Projects_that_share_internals_do_not_see_each_other_s_generated_types(bool explain)
    {
        var library = GeneratorHarness.Run(
            """
            [assembly: System.Runtime.CompilerServices.InternalsVisibleTo("App")]

            namespace Library
            {
                public static partial class Rules
                {
                    [Motiv.Explain.Explain]
                    public static bool IsSmall(int n) => n > 0 && n < 3;

                    public static bool Check(int n) => IsSmall(n);
                }
            }
            """,
            explain);

        var app = GeneratorHarness.Run(
            Program(
                """
                public static partial class AppRules
                {
                    [Explain]
                    public static bool IsTiny(int n) => n > 0 && n < 2;
                }
                """,
                """log.Add("result: " + AppRules.IsTiny(1));"""),
            explain,
            assemblyName: "App",
            references: library.ToReference());

        app.Problems.ShouldBeEmpty(string.Join(Environment.NewLine, app.Problems));
    }
}
