using Microsoft.CodeAnalysis.Testing;
using VerifyCS =
    Motiv.CodeFix.Tests.CSharpCodeFixVerifier<Motiv.Analyzer.MotivAnalyzer, Motiv.CodeFix.MotivCodeFixProvider>;

namespace Motiv.CodeFix.Tests;

public class MotivConvertToSpecInPlaceTests
{
    private const string Source = "Source.cs";

    [Fact]
    public async Task Should_replace_only_the_argument_when_expression_is_a_method_argument()
    {
        const string booleanExpression = "n > 0 && n < 10";

        const string source =
          $$"""
            using System;

            namespace MyNamespace;

            public class MyClass
            {
                public void Print(int n)
                {
                    Console.WriteLine({{booleanExpression}});
                }
            }
            """;

        const string expectedTransformedCode =
          $$"""
            using System;
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                private static readonly IsNPositiveAndLessThan10Proposition IsNPositiveAndLessThan10Proposition = new();

                public void Print(int n)
                {
                    Console.WriteLine(IsNPositiveAndLessThan10Proposition.Matches(n));
                }
            }

            public class IsNPositiveAndLessThan10Proposition() : Spec<int>(() =>
            {
                var isNPositive = Spec
                    .Build((int n) => n > 0)
                    .Create("n > 0");

                var isNLessThan10 = Spec
                    .Build((int n) => n < 10)
                    .Create("n < 10");

                return isNPositive.AndAlso(isNLessThan10);
            });
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 9, 27, 9, 27 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_evaluate_in_place_when_the_assignment_target_is_evaluated_first()
    {
        const string booleanExpression = "i > 0 && i < 5";

        const string source =
          $$"""
            namespace MyNamespace;

            public class Counter
            {
                public void Record(bool[] seen, int i)
                {
                    seen[i++] = {{booleanExpression}};
                }
            }
            """;

        const string expectedTransformedCode =
          $$"""
            using Motiv;

            namespace MyNamespace;

            public class Counter
            {
                private static readonly IsIPositiveAndLessThan5Proposition IsIPositiveAndLessThan5Proposition = new();

                public void Record(bool[] seen, int i)
                {
                    seen[i++] = IsIPositiveAndLessThan5Proposition.Matches(i);
                }
            }

            public class IsIPositiveAndLessThan5Proposition() : Spec<int>(() =>
            {
                var isIPositive = Spec
                    .Build((int i) => i > 0)
                    .Create("i > 0");

                var isILessThan5 = Spec
                    .Build((int i) => i < 5)
                    .Create("i < 5");

                return isIPositive.AndAlso(isILessThan5);
            });
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 7, 21, 7, 21 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_replace_only_the_condition_when_expression_is_an_if_condition()
    {
        const string booleanExpression = "n > 0 && n < 10";

        const string source =
          $$"""
            namespace MyNamespace;

            public class MyClass
            {
                public int Clamp(int n)
                {
                    if ({{booleanExpression}})
                        return n;

                    return 0;
                }
            }
            """;

        const string expectedTransformedCode =
          $$"""
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                private static readonly IsNPositiveAndLessThan10Proposition IsNPositiveAndLessThan10Proposition = new();

                public int Clamp(int n)
                {
                    if (IsNPositiveAndLessThan10Proposition.Matches(n))
                        return n;

                    return 0;
                }
            }

            public class IsNPositiveAndLessThan10Proposition() : Spec<int>(() =>
            {
                var isNPositive = Spec
                    .Build((int n) => n > 0)
                    .Create("n > 0");

                var isNLessThan10 = Spec
                    .Build((int n) => n < 10)
                    .Create("n < 10");

                return isNPositive.AndAlso(isNLessThan10);
            });
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 7, 13, 7, 13 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_replace_only_the_declaration_and_keep_the_other_statements_of_the_method()
    {
        const string booleanExpression = "n > 0 && n < 10";

        const string source =
          $$"""
            namespace MyNamespace;

            public class MyClass
            {
                public string Describe(int n)
                {
                    var prefix = "n is ";
                    var isInRange = {{booleanExpression}};
                    return prefix + (isInRange ? "in range" : "out of range");
                }
            }
            """;

        const string expectedTransformedCode =
          $$"""
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                private static readonly IsInRangeProposition IsInRangeProposition = new();

                public string Describe(int n)
                {
                    var prefix = "n is ";
                    // {{booleanExpression}}
                    var isInRangeResult = IsInRangeProposition.Evaluate(n);
                    var isInRange = isInRangeResult.Satisfied;
                    return prefix + (isInRange ? "in range" : "out of range");
                }
            }

            public class IsInRangeProposition() : Spec<int>(() =>
            {
                var isNPositive = Spec
                    .Build((int n) => n > 0)
                    .Create("n > 0");

                var isNLessThan10 = Spec
                    .Build((int n) => n < 10)
                    .Create("n < 10");

                return isNPositive.AndAlso(isNLessThan10);
            });
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 8, 25, 8, 25 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_evaluate_in_place_so_the_debug_tap_fires_when_converting_with_debug_output()
    {
        const string booleanExpression = "n > 0 && n < 10";

        const string source =
          $$"""
            namespace MyNamespace;

            public class MyClass
            {
                public int Clamp(int n)
                {
                    if ({{booleanExpression}})
                        return n;

                    return 0;
                }
            }
            """;

        const string expectedTransformedCode =
          $$"""
            using System.Diagnostics;
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                private static readonly SpecBase<int, string> IsNPositiveAndLessThan10Proposition = new IsNPositiveAndLessThan10Proposition()
                    .Tap((model, result) =>
                        Debug.WriteLine($"[Motiv] IsNPositiveAndLessThan10Proposition | Model: {model} | Satisfied: {result.Satisfied} | Reason: {result.Reason}"));

                public int Clamp(int n)
                {
                    if (IsNPositiveAndLessThan10Proposition.Evaluate(n).Satisfied)
                        return n;

                    return 0;
                }
            }

            public class IsNPositiveAndLessThan10Proposition() : Spec<int>(() =>
            {
                var isNPositive = Spec
                    .Build((int n) => n > 0)
                    .Create("n > 0");

                var isNLessThan10 = Spec
                    .Build((int n) => n < 10)
                    .Create("n < 10");

                return isNPositive.AndAlso(isNLessThan10);
            });
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 7, 13, 7, 13 + booleanExpression.Length)
            },
            CodeActionEquivalenceKey = "ConvertToSpecWithDebugOutput"
        }.RunAsync();
    }

    [Fact]
    public async Task Should_hold_the_spec_in_a_static_field_when_expression_is_a_field_initializer_of_a_struct()
    {
        const string booleanExpression = "Limit > 0 && Limit < 10";

        const string source =
          $$"""
            namespace MyNamespace;

            public struct Limits
            {
                private static readonly int Limit = 5;

                public static readonly bool IsInRange = {{booleanExpression}};
            }
            """;

        const string expectedTransformedCode =
          $$"""
            using Motiv;

            namespace MyNamespace;

            public struct Limits
            {
                private static readonly int Limit = 5;
                private static readonly IsInRangeProposition IsInRangeProposition = new();

                public static readonly bool IsInRange = IsInRangeProposition.Matches(Limit);
            }

            public class IsInRangeProposition() : Spec<int>(() =>
            {
                var isLimitPositive = Spec
                    .Build((int Limit) => Limit > 0)
                    .Create("Limit > 0");

                var isLimitLessThan10 = Spec
                    .Build((int Limit) => Limit < 10)
                    .Create("Limit < 10");

                return isLimitPositive.AndAlso(isLimitLessThan10);
            });
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 7, 45, 7, 45 + booleanExpression.Length)
            }
        }.RunAsync();
    }


    [Fact]
    public async Task Should_declare_the_method_type_parameters_on_a_self_holding_spec_when_expression_is_generic()
    {
        const string booleanExpression = "first is null && second is null";

        const string source =
          $$"""
            namespace MyNamespace;

            public class MyClass
            {
                public bool AreBothMissing<T>(T first, T second) where T : class => {{booleanExpression}};
            }
            """;

        const string expectedTransformedCode =
          $$"""
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                public bool AreBothMissing<T>(T first, T second) where T : class
                {
                    // {{booleanExpression}}
                    var areBothMissingResult = AreBothMissingProposition<T>.Instance.Evaluate(new AreBothMissingProposition<T>.Model(first, second));
                    return areBothMissingResult.Satisfied;
                }
            }

            public class AreBothMissingProposition<T>() : Spec<AreBothMissingProposition<T>.Model>(() =>
            {
                var isFirstNull = Spec
                    .Build((Model m) => m.First is null)
                    .Create("first is null");

                var isSecondNull = Spec
                    .Build((Model m) => m.Second is null)
                    .Create("second is null");

                return isFirstNull.AndAlso(isSecondNull);
            })
                where T : class
            {
                public static readonly AreBothMissingProposition<T> Instance = new();

                public readonly record struct Model(T First, T Second);
            }
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 5, 73, 5, 73 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_not_offer_a_fix_when_a_generic_expression_calls_an_instance_method()
    {
        const string booleanExpression = "value is not null && !IsMissing(value)";

        const string source =
          $$"""
            namespace MyNamespace;

            public class MyClass
            {
                public bool IsKnownValue<T>(T value) => {{booleanExpression}};

                private bool IsMissing(object value) => ReferenceEquals(value, null);
            }
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            // No fix is registered, so the document is left as it was
            FixedState = { Sources = { (Source, source) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 5, 45, 5, 45 + booleanExpression.Length)
            }
        }.RunAsync();
    }


    [Fact]
    public async Task Should_give_a_single_clause_generic_spec_a_body_to_hold_its_instance()
    {
        const string booleanExpression = "value is not null";

        const string source =
          $$"""
            namespace MyNamespace;

            public class MyClass
            {
                public bool IsPresent<T>(T value)
                {
                    if ({{booleanExpression}})
                        return true;

                    return false;
                }
            }
            """;

        const string expectedTransformedCode =
          $$"""
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                public bool IsPresent<T>(T value)
                {
                    if (IsValueNotNullProposition<T>.Instance.Matches(value))
                        return true;

                    return false;
                }
            }

            public class IsValueNotNullProposition<T>() : Spec<T>(() =>
                Spec.Build((T value) => {{booleanExpression}})
                    .Create("{{booleanExpression}}"))
            {
                public static readonly IsValueNotNullProposition<T> Instance = new();
            }
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 7, 13, 7, 13 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_not_offer_a_fix_in_top_level_statements_where_no_type_can_hold_the_spec()
    {
        const string booleanExpression = "args.Length > 0 && args.Length < 10";

        const string source =
          $$"""
            System.Console.WriteLine({{booleanExpression}});
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { (Source, source) },
                OutputKind = Microsoft.CodeAnalysis.OutputKind.ConsoleApplication
            },
            // No fix is registered, so the document is left as it was
            FixedState = { Sources = { (Source, source) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 1, 26, 1, 26 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_keep_the_comment_on_one_line_when_the_disjunction_is_nested()
    {
        const string booleanExpression = "a && (b || c)";

        const string source =
          $$"""
            namespace MyNamespace;

            public static class Rules
            {
                public static bool Check(bool a, bool b, bool c) => {{booleanExpression}};
            }
            """;

        const string expectedTransformedCode =
          $$"""
            using Motiv;

            namespace MyNamespace;

            public static class Rules
            {
                private static readonly CheckProposition CheckProposition = new();

                public static bool Check(bool a, bool b, bool c)
                {
                    // {{booleanExpression}}
                    var checkResult = CheckProposition.Evaluate(new CheckProposition.Model(a, b, c));
                    return checkResult.Satisfied;
                }
            }

            public class CheckProposition() : Spec<CheckProposition.Model>(() =>
            {
                var isA = Spec
                    .Build((Model m) => m.A)
                    .Create("a");

                var isB = Spec
                    .Build((Model m) => m.B)
                    .Create("b");

                var isC = Spec
                    .Build((Model m) => m.C)
                    .Create("c");

                return isA.AndAlso(isB.OrElse(isC));
            })
            {
                public readonly record struct Model(bool A, bool B, bool C);
            }
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 5, 57, 5, 57 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_name_model_members_without_underscores_and_types_as_the_source_does()
    {
        const string booleanExpression = "items.Count < _limit && _limit > 0";

        const string source =
          $$"""
            using System.Collections.Generic;

            namespace MyNamespace;

            public class Basket
            {
                private readonly int _limit = 10;

                public bool HasRoom(List<int> items) => {{booleanExpression}};
            }
            """;

        const string expectedTransformedCode =
          $$"""
            using System.Collections.Generic;
            using Motiv;

            namespace MyNamespace;

            public class Basket
            {
                private readonly int _limit = 10;
                private static readonly HasRoomProposition HasRoomProposition = new();

                public bool HasRoom(List<int> items)
                {
                    // {{booleanExpression}}
                    var hasRoomResult = HasRoomProposition.Evaluate(new HasRoomProposition.Model(items, _limit));
                    return hasRoomResult.Satisfied;
                }
            }

            public class HasRoomProposition() : Spec<HasRoomProposition.Model>(() =>
            {
                var isItemsCountLessThanLimit = Spec
                    .Build((Model m) => m.Items.Count < m.Limit)
                    .Create("items.Count < _limit");

                var isLimitPositive = Spec
                    .Build((Model m) => m.Limit > 0)
                    .Create("_limit > 0");

                return isItemsCountLessThanLimit.AndAlso(isLimitPositive);
            })
            {
                public readonly record struct Model(List<int> Items, int Limit);
            }
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 9, 45, 9, 45 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_keep_the_underscore_when_dropping_it_would_give_two_values_one_model_member()
    {
        const string booleanExpression = "limit > 0 && limit <= _limit";

        const string source =
          $$"""
            namespace MyNamespace;

            public class Basket
            {
                private readonly int _limit = 10;

                public bool IsWithin(int limit) => {{booleanExpression}};
            }
            """;

        const string expectedTransformedCode =
          $$"""
            using Motiv;

            namespace MyNamespace;

            public class Basket
            {
                private readonly int _limit = 10;
                private static readonly IsWithinProposition IsWithinProposition = new();

                public bool IsWithin(int limit)
                {
                    // {{booleanExpression}}
                    var isWithinResult = IsWithinProposition.Evaluate(new IsWithinProposition.Model(limit, _limit));
                    return isWithinResult.Satisfied;
                }
            }

            public class IsWithinProposition() : Spec<IsWithinProposition.Model>(() =>
            {
                var isLimitPositive = Spec
                    .Build((Model m) => m.Limit > 0)
                    .Create("limit > 0");

                var isLimitAtMostLimit = Spec
                    .Build((Model m) => m.Limit <= m._limit)
                    .Create("limit <= _limit");

                return isLimitPositive.AndAlso(isLimitAtMostLimit);
            })
            {
                public readonly record struct Model(int Limit, int _limit);
            }
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 7, 40, 7, 40 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_declare_the_spec_in_the_namespace_that_encloses_the_expression()
    {
        const string booleanExpression = "item.Price > 0 && quantity > 0";

        const string source =
          $$"""
            namespace Outer
            {
                namespace Inner
                {
                    public class Item
                    {
                        public int Price { get; set; }
                    }

                    public class Order
                    {
                        public bool IsValid(Item item, int quantity) => {{booleanExpression}};
                    }
                }
            }
            """;

        const string expectedTransformedCode =
          $$"""
            using Motiv;

            namespace Outer
            {
                namespace Inner
                {
                    public class Item
                    {
                        public int Price { get; set; }
                    }

                    public class Order
                    {
                        private static readonly IsValidProposition IsValidProposition = new();

                        public bool IsValid(Item item, int quantity)
                        {
                            // {{booleanExpression}}
                            var isValidResult = IsValidProposition.Evaluate(new IsValidProposition.Model(item, quantity));
                            return isValidResult.Satisfied;
                        }
                    }

                    public class IsValidProposition() : Spec<IsValidProposition.Model>(() =>
                    {
                        var isItemPricePositive = Spec
                            .Build((Model m) => m.Item.Price > 0)
                            .Create("item.Price > 0");

                        var isQuantityPositive = Spec
                            .Build((Model m) => m.Quantity > 0)
                            .Create("quantity > 0");

                        return isItemPricePositive.AndAlso(isQuantityPositive);
                    })
                    {
                        public readonly record struct Model(Item Item, int Quantity);
                    }
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 12, 61, 12, 61 + booleanExpression.Length)
            }
        }.RunAsync();
    }
}
