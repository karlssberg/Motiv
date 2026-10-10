namespace Motiv.Explain.Tests;

public class ExplainGeneratorTests
{
    private const string Models =
        """
        namespace Shop
        {
            public enum Status { Draft, Paid, Shipped }
            public sealed class Customer
            {
                public Customer(bool isActive, int age) { IsActive = isActive; Age = age; }
                public bool IsActive { get; }
                public int Age { get; }
            }
            public sealed class Order
            {
                public Order(decimal total, Customer? customer, Status status = Status.Paid, params int[] lines)
                {
                    Total = total; Customer = customer; Status = status; Lines = lines;
                }
                public decimal Total { get; }
                public Customer? Customer { get; }
                public Status Status { get; }
                public int[] Lines { get; }
            }
        }
        """;

    private static string Program(string rules, string probeBody) =>
        $$"""
        using System.Collections.Generic;
        using Motiv.Explain;
        using Shop;

        {{Models}}

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

    [Fact]
    public void An_explain_build_logs_the_decomposed_justification_of_a_static_method()
    {
        var source = Program(
            """
            namespace Shop
            {
                public static partial class Rules
                {
                    [Explain]
                    public static bool IsEligible(Order o) =>
                        o.Total > 100 && (o.Customer!.IsActive || o.Customer.Age >= 65);
                }
            }
            """,
            """log.Add("result: " + Rules.IsEligible(new Order(150, new Customer(false, 70))));""");

        var lines = GeneratorHarness.Run(source, explain: true).Load().Run();

        lines.ShouldBe(
        [
            """
            [motiv-explain] Shop.Rules.IsEligible(Shop.Order) => True
            AND ALSO
                o.Total > 100 == true
                OR ELSE
                    o.Customer.Age >= 65 == true
            """.ReplaceLineEndings("\n"),
            "result: True"
        ]);
    }

    [Fact]
    public void A_build_without_the_switch_generates_no_interceptor_and_does_not_reference_motiv()
    {
        var source = Program(
            """
            namespace Shop
            {
                public static partial class Rules
                {
                    [Explain]
                    public static bool IsEligible(Order o) => o.Total > 100 && o.Customer!.IsActive;
                }
            }
            """,
            """log.Add("result: " + Rules.IsEligible(new Order(150, new Customer(true, 30))));""");

        var run = GeneratorHarness.Run(source, explain: false, referenceMotiv: false);
        var loaded = run.Load();

        run.Result.GeneratedTrees.Select(tree => Path.GetFileName(tree.FilePath))
            .ShouldBe(["Microsoft.CodeAnalysis.EmbeddedAttribute.cs", "ExplainAttribute.g.cs"], ignoreOrder: true);
        loaded.Run().ShouldBe(["result: True"]);
        loaded.ReferencedAssemblyNames.ShouldNotContain("Motiv");
    }

    [Fact]
    public void The_attribute_is_left_out_of_a_prod_assembly_s_metadata()
    {
        var source = Program(
            """
            namespace Shop
            {
                public static partial class Rules
                {
                    [Explain]
                    public static bool IsEligible(Order o) => o.Total > 100;
                }
            }
            """,
            "");

        var assembly = GeneratorHarness.Run(source, explain: false, referenceMotiv: false).Load().Assembly;

        assembly.GetType("Shop.Rules")!.GetMethod("IsEligible")!.GetCustomAttributes(false)
            .Select(attribute => attribute.GetType().Name)
            .ShouldNotContain("ExplainAttribute");
    }

    [Theory]
    [InlineData(50, true, 30, false)]
    [InlineData(150, true, 30, true)]
    [InlineData(150, false, 30, false)]
    [InlineData(150, false, 70, true)]
    [InlineData(50, false, 70, false)]
    public void An_explained_call_returns_what_the_original_method_would(decimal total, bool active, int age, bool expected)
    {
        var source = Program(
            """
            namespace Shop
            {
                public static partial class Rules
                {
                    [Explain]
                    public static bool IsEligible(Order o) =>
                        o.Total > 100 && (o.Customer!.IsActive || o.Customer.Age >= 65);
                }
            }
            """,
            $$"""log.Add("result: " + Rules.IsEligible(new Order({{total}}m, new Customer({{(active ? "true" : "false")}}, {{age}}))));""");

        var lines = GeneratorHarness.Run(source, explain: true).Load().Run();

        lines.Last().ShouldBe($"result: {expected}");
    }

    [Fact]
    public void An_instance_method_can_read_private_state_through_this()
    {
        var source = Program(
            """
            namespace Shop
            {
                public partial class Pricing
                {
                    private readonly decimal _threshold;
                    public Pricing(decimal threshold) => _threshold = threshold;

                    [Explain]
                    public bool QualifiesForDiscount(Order o) => o.Total >= _threshold && this.IsAdult(o.Customer!);

                    private bool IsAdult(Customer c) => c.Age >= 18;
                }
            }
            """,
            """log.Add("result: " + new Pricing(200).QualifiesForDiscount(new Order(150, new Customer(true, 70))));""");

        var lines = GeneratorHarness.Run(source, explain: true).Load().Run();

        lines.ShouldBe(
        [
            """
            [motiv-explain] Shop.Pricing.QualifiesForDiscount(Shop.Order) => False
            AND ALSO
                o.Total >= _threshold == false
            """.ReplaceLineEndings("\n"),
            "result: False"
        ]);
    }

    [Fact]
    public void A_method_group_is_not_intercepted_but_still_returns_the_right_answer()
    {
        var source = Program(
            """
            namespace Shop
            {
                public static partial class Rules
                {
                    [Explain]
                    public static bool IsEligible(Order o) => o.Total > 100;
                }
            }
            """,
            """
            System.Func<Order, bool> isEligible = Rules.IsEligible;
            log.Add("result: " + isEligible(new Order(150, null)));
            """);

        GeneratorHarness.Run(source, explain: true).Load().Run().ShouldBe(["result: True"]);
    }

    [Fact]
    public void Leaves_may_use_constructs_an_expression_tree_cannot_hold()
    {
        var source = Program(
            """
            namespace Shop
            {
                public static partial class Rules
                {
                    [Explain]
                    public static bool CanShip(Order o) =>
                        o.Status is Status.Paid or Status.Shipped && o.Customer?.IsActive == true;
                }
            }
            """,
            """log.Add("result: " + Rules.CanShip(new Order(10, new Customer(true, 30), Status.Shipped)));""");

        var lines = GeneratorHarness.Run(source, explain: true).Load().Run();

        lines.ShouldBe(
        [
            """
            [motiv-explain] Shop.Rules.CanShip(Shop.Order) => True
            AND ALSO
                o.Status is Status.Paid or Status.Shipped == true
                o.Customer?.IsActive == true
            """.ReplaceLineEndings("\n"),
            "result: True"
        ]);
    }

    [Fact]
    public void A_variable_a_pattern_declares_keeps_the_clauses_that_use_it_together()
    {
        var source = Program(
            """
            namespace Shop
            {
                public static partial class Rules
                {
                    [Explain]
                    public static bool IsSeniorPaid(Order o) =>
                        o.Status == Status.Paid && o.Customer is { } c && c.Age >= 65;
                }
            }
            """,
            """log.Add("result: " + Rules.IsSeniorPaid(new Order(10, new Customer(true, 70))));""");

        var lines = GeneratorHarness.Run(source, explain: true).Load().Run();

        lines.ShouldBe(
        [
            """
            [motiv-explain] Shop.Rules.IsSeniorPaid(Shop.Order) => True
            AND ALSO
                o.Status == Status.Paid == true
                (o.Customer is { } c && c.Age >= 65) == true
            """.ReplaceLineEndings("\n"),
            "result: True"
        ]);
    }

    [Fact]
    public void Several_parameters_become_one_model()
    {
        var source = Program(
            """
            namespace Shop
            {
                public static partial class Rules
                {
                    [Explain]
                    public static bool Fits(Order o, decimal limit, int maxLines) => o.Total <= limit && o.Lines.Length <= maxLines;
                }
            }
            """,
            """log.Add("result: " + Rules.Fits(new Order(10, null, Status.Paid, 1, 2, 3), 100m, 2));""");

        var lines = GeneratorHarness.Run(source, explain: true).Load().Run();

        lines.Last().ShouldBe("result: False");
        lines.First().ShouldContain("o.Lines.Length <= maxLines == false");
    }

    [Fact]
    public void The_file_s_using_directives_are_available_to_the_clauses()
    {
        var source = "using System.Linq;\n" + Program(
            """
            namespace Shop
            {
                public static partial class Rules
                {
                    [Explain]
                    public static bool HasLines(Order o) => o.Lines.Any(line => line > 0) && o.Total > 0;
                }
            }
            """,
            """log.Add("result: " + Rules.HasLines(new Order(10, null, Status.Paid, 1)));""");

        GeneratorHarness.Run(source, explain: true).Load().Run().Last().ShouldBe("result: True");
    }

    [Fact]
    public void Overloads_with_the_same_name_are_each_explained()
    {
        var source = Program(
            """
            namespace Shop
            {
                public static partial class Rules
                {
                    [Explain]
                    public static bool IsBig(Order o) => o.Total > 100;

                    [Explain]
                    public static bool IsBig(Order o, decimal limit) => o.Total > limit;
                }
            }
            """,
            """
            log.Add("one: " + Rules.IsBig(new Order(150, null)));
            log.Add("two: " + Rules.IsBig(new Order(150, null), 200));
            """);

        var lines = GeneratorHarness.Run(source, explain: true).Load().Run();

        lines.Count.ShouldBe(4);
        lines[0].ShouldStartWith("[motiv-explain] Shop.Rules.IsBig(Shop.Order) => True");
        lines[1].ShouldBe("one: True");
        lines[2].ShouldStartWith("[motiv-explain] Shop.Rules.IsBig(Shop.Order, decimal) => False");
        lines[3].ShouldBe("two: False");
    }

    [Fact]
    public void Eager_operators_still_evaluate_both_sides()
    {
        var source = Program(
            """
            namespace Shop
            {
                public static partial class Rules
                {
                    public static int Calls;
                    private static bool Count(bool value) { Calls++; return value; }

                    [Explain]
                    public static bool Mixed(Order o) => (Count(false) & Count(true)) | !(Count(true) ^ Count(false));
                }
            }
            """,
            """
            log.Add("result: " + Rules.Mixed(new Order(1, null)));
            log.Add("calls: " + Rules.Calls);
            """);

        var lines = GeneratorHarness.Run(source, explain: true).Load().Run();

        lines.Skip(1).ShouldBe(["result: False", "calls: 4"]);
    }

    [Fact]
    public void A_single_return_statement_body_is_explained()
    {
        var source = Program(
            """
            namespace Shop
            {
                public static partial class Rules
                {
                    [Explain]
                    public static bool IsBig(Order o)
                    {
                        return o.Total > 100 || o.Lines.Length > 10;
                    }
                }
            }
            """,
            """log.Add("result: " + Rules.IsBig(new Order(150, null)));""");

        var lines = GeneratorHarness.Run(source, explain: true).Load().Run();

        lines.First().ShouldStartWith("[motiv-explain] Shop.Rules.IsBig(Shop.Order) => True");
        lines.Last().ShouldBe("result: True");
    }

    [Fact]
    public void Splitting_a_null_check_from_its_dereference_raises_no_nullable_warning()
    {
        var source = Program(
            """
            namespace Shop
            {
                public static partial class Rules
                {
                    [Explain]
                    public static bool IsAdult(Order o) => o.Customer != null && o.Customer.Age >= 18;
                }
            }
            """,
            """log.Add("result: " + Rules.IsAdult(new Order(1, null)));""");

        GeneratorHarness.Run(source, explain: true).Load().Run().Last().ShouldBe("result: False");
    }

    [Fact]
    public void A_method_the_analyzer_would_reject_is_left_alone()
    {
        var source = Program(
            """
            namespace Shop
            {
                public static class NotPartial
                {
                    [Explain]
                    public static bool IsBig(Order o) => o.Total > 100;
                }
            }
            """,
            """log.Add("result: " + NotPartial.IsBig(new Order(150, null)));""");

        GeneratorHarness.Run(source, explain: true).Load().Run().ShouldBe(["result: True"]);
    }

    [Fact]
    public void A_parameterless_static_method_is_explained()
    {
        var source = Program(
            """
            namespace Shop
            {
                public static partial class Flags
                {
                    public static bool Beta = true;
                    public static int Percent = 10;

                    [Explain]
                    public static bool IsOn() => Beta && Percent > 50;
                }
            }
            """,
            """log.Add("result: " + Flags.IsOn());""");

        var lines = GeneratorHarness.Run(source, explain: true).Load().Run();

        lines.First().ShouldContain("Percent > 50 == false");
        lines.Last().ShouldBe("result: False");
    }

    [Fact]
    public void Nested_records_and_keyword_parameter_names_are_explained()
    {
        var source = Program(
            """
            namespace Shop
            {
                public static partial class Outer
                {
                    public partial record Policy(int Minimum)
                    {
                        private static int Bonus = 1;

                        [Explain]
                        public bool Allows(int @class, Order o) => @class + Bonus >= Minimum && o.Lines.Length > 0;
                    }
                }
            }
            """,
            """log.Add("result: " + new Outer.Policy(3).Allows(2, new Order(1, null, Status.Paid, 5)));""");

        var lines = GeneratorHarness.Run(source, explain: true).Load().Run();

        lines.First().ShouldStartWith("[motiv-explain] Shop.Outer.Policy.Allows(int, Shop.Order) => True");
        lines.First().ShouldContain("@class + Bonus >= Minimum == true");
        lines.Last().ShouldBe("result: True");
    }

    [Fact]
    public void A_type_in_the_global_namespace_is_explained()
    {
        var source = Program(
            """
            public static partial class GlobalRules
            {
                [Explain]
                public static bool IsBig(Order o) => o.Total > 100;
            }
            """,
            """log.Add("result: " + GlobalRules.IsBig(new Order(150, null)));""");

        GeneratorHarness.Run(source, explain: true).Load().Run()
            .ShouldBe(["[motiv-explain] GlobalRules.IsBig(Shop.Order) => True\no.Total > 100 == true", "result: True"]);
    }

    [Fact]
    public void Implicit_this_inside_a_nested_lambda_and_conditional_calls_are_handled()
    {
        var source = "using System.Linq;\n" + Program(
            """
            namespace Shop
            {
                public partial class Basket
                {
                    private readonly int _minimum;
                    public Basket(int minimum) => _minimum = minimum;

                    [Explain]
                    public bool HasLargeLine(Order o) => o.Lines.Any(line => line >= _minimum);

                    [Explain]
                    public bool IsReady() => _minimum > 0 && HasLargeLine(new Order(1, null, Status.Paid, _minimum));
                }
            }
            """,
            """
            Basket? basket = new Basket(5);
            log.Add("conditional: " + basket?.HasLargeLine(new Order(1, null, Status.Paid, 7)));
            log.Add("internal: " + basket!.IsReady());
            """);

        var lines = GeneratorHarness.Run(source, explain: true).Load().Run();

        // IsReady's own clause calls HasLargeLine from generated code, which no interceptor reaches,
        // so only the conditional call logs HasLargeLine
        lines.Count(line => line.StartsWith("[motiv-explain] Shop.Basket.HasLargeLine(Shop.Order) => True")).ShouldBe(1);
        lines.ShouldContain("conditional: True");
        lines.ShouldContain(line => line.StartsWith("[motiv-explain] Shop.Basket.IsReady() => True"));
        lines.Last().ShouldBe("internal: True");
    }

    [Fact]
    public void Generated_names_do_not_clash_with_the_method_s_own_names()
    {
        var source = Program(
            """
            namespace Shop
            {
                public sealed class Config { public bool Enabled { get; set; } = true; }

                public static partial class Flags
                {
                    public static Config Instance = new Config();

                    [Explain]
                    public static bool IsOn(int result) => Instance.Enabled && result > 0;
                }
            }
            """,
            """log.Add("result: " + Flags.IsOn(1));""");

        GeneratorHarness.Run(source, explain: true).Load().Run().Last().ShouldBe("result: True");
    }

    [Fact]
    public void A_using_inside_a_namespace_still_resolves_relative_to_it()
    {
        var source = Program(
            """
            namespace Shop.Models
            {
                public static class Limits { public const int Max = 10; }
            }

            namespace Shop
            {
                using Models;

                public static partial class Rules
                {
                    [Explain]
                    public static bool IsSmall(int n) => n < Limits.Max && n > 0;
                }
            }
            """,
            """log.Add("result: " + Rules.IsSmall(3));""");

        GeneratorHarness.Run(source, explain: true).Load().Run().Last().ShouldBe("result: True");
    }

    [Fact]
    public void Methods_whose_sanitised_names_coincide_are_both_explained()
    {
        var source = Program(
            """
            namespace Shop
            {
                public static partial class A_B
                {
                    [Explain]
                    public static bool C(int n) => n > 1;
                }

                public static partial class A
                {
                    [Explain]
                    public static bool B_C(int n) => n > 2;
                }
            }
            """,
            """
            log.Add("one: " + A_B.C(2));
            log.Add("two: " + A.B_C(2));
            """);

        var lines = GeneratorHarness.Run(source, explain: true).Load().Run();

        lines.ShouldContain("one: True");
        lines.ShouldContain("two: False");
        lines.Count.ShouldBe(4);
    }

    [Fact]
    public void A_property_pattern_on_another_instance_is_not_mistaken_for_this()
    {
        var source = Program(
            """
            namespace Shop
            {
                public partial class Node
                {
                    public Node? Next { get; set; }

                    [Explain]
                    public bool LinksTwoAhead(Node other) => other is { Next.Next: not null } && Next != null;
                }
            }
            """,
            """log.Add("result: " + new Node { Next = new Node() }.LinksTwoAhead(new Node { Next = new Node { Next = new Node() } }));""");

        GeneratorHarness.Run(source, explain: true).Load().Run().Last().ShouldBe("result: True");
    }
}
