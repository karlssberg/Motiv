# Studio Expression Leaves Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `{ "expression": "…" }` leaves evaluate, and make the DSL pane the place they are authored, with completion, lint, hover and an inspector.

**Architecture:** A small owned leaf language (comparisons, arithmetic, `@params`, literals, a closed set of collection and string methods with explicit lambdas) is parsed and type-checked twice — in `Motiv.Serialization` (reflection over the CLR model, then compiled to a native `Expression<Func<TModel,bool>>` and handed to `Spec.From`) and in `@motiv-rules/core` (JSON-schema scope, for the editor). One JSON corpus is run by both test suites so a document that lints clean binds. Studio's DSL editor gains a nested leaf mode, backend ranges, hover facts and an inspector strip.

**Tech Stack:** C# / .NET 8+ (generic math, `System.Linq.Expressions`, System.Text.Json schema exporter), TypeScript (`@motiv-rules/core`, Vitest), React + CodeMirror 6 (Studio), xUnit + Shouldly.

**Spec:** `docs/superpowers/specs/2026-09-18-studio-expression-leaves-design.md`

## Global Constraints

- Leaves bind only on `net8.0`, `net9.0`, `net10.0`. On `netstandard2.0` the binders keep returning `ExpressionsNotEnabled`, message: `expression nodes are supported on .NET 8 or later`.
- Names in a leaf are the catalog's JSON-schema names (camelCase by System.Text.Json's default policy, or `[JsonPropertyName]`), never CLR names.
- Collection methods: `where any all count sum min max`. String methods: `equalsIgnoreCase`. Nothing else in v1.
- Strings: ordinal, case-sensitive `==`/`!=`; `equalsIgnoreCase` is `StringComparison.OrdinalIgnoreCase`.
- `null` only as an operand of `==`/`!=`. Navigation through null yields null; any other operator with a null operand yields `false`. A leaf never throws on null.
- Numerics: anchors are model fields and fixed-type methods; literals and `@params` are untyped until solved. Allowed widenings: `int→long`, `int→decimal`, `long→decimal`, `int→double`, `float→double`. Refused: `long→double`, anything→`float`, `decimal↔double`. Fractional literal prefers `decimal`. Unanchored defaults: `int` (integral), `decimal` (fractional), with a warning. `integer` params may solve to `int`/`long`/`decimal`; `number` params to `decimal`/`double`/`float`. Arithmetic is checked. Integer `/` truncates, with a warning.
- Every leaf binds through `Spec.From`, so assertions are decomposed clauses.
- The only change to `src/Motiv` is the null-conditional expression node and its printing as `?.`.
- New `RuleErrorCode`s: `InvalidExpression`, `UnknownField`, `UnknownMethod`, `ExpressionTypeMismatch`, `ExpressionRequiresMetadata`. Mirror them in `ui/packages/rules-core/src/contracts.ts`.
- Every `dotnet` call: `env -u MallocStackLogging -u MallocNanoZone dotnet …` (MinVer captures malloc chatter otherwise), and grep output for `error CS` because filtered test output cannot tell a compile failure from a pass.
- Commit after every task. The plan and design doc land in the final commit alongside docs.

## File Structure

**C# — `src/Motiv.Serialization/Expressions/`** (new folder, all `internal`, `#if NET8_0_OR_GREATER` around everything but the error path)
- `LeafToken.cs`, `LeafTokenizer.cs` — tokens with `[Start, End)` offsets into the leaf text.
- `LeafNode.cs`, `LeafParser.cs` — the AST (records, each with `Start`/`End`) and a recursive-descent parser.
- `LeafScope.cs` — model type + JSON-name member lookup + element types + lambda variables + parameter declarations.
- `NumericKind.cs`, `NumericLattice.cs` — the widening table and typed-constant construction via generic math.
- `LeafChecker.cs`, `LeafFact.cs`, `LeafProblem.cs` — type inference (union-find type variables), problems with ranges, facts per node.
- `LeafCompiler.cs` — AST → `Expression<Func<TModel,bool>>` → `Spec.From`.
- `LeafBinding.cs` — the one entry the four binders call: `Bind<TModel>(RuleNode, IReadOnlyDictionary<string,object?> values, List<RuleError>)`.

**C# — `src/Motiv/ExpressionTreeProposition/NullConditionalExpression.cs`** (new) and `CSharpExpressionSerializer.cs` (modify: `VisitExtension`).

**C# — modify:** `RuleErrorCode.cs`, `RuleError.cs` (+ `Range`), `RuleTextRange.cs` (new), `RuleNode.cs` (+ `ParameterValues`), `RuleParameterSubstituter.cs` (hands values to expression nodes), `RuleBinder.cs`, `AsyncRuleBinder.cs`, `MetadataRuleBinder.cs`, `AsyncMetadataRuleBinder.cs`, `RuleSerializer.cs` (+ `Inspect<TModel>`), `RuleValidation.cs` (new). AspNetCore: `ModelBinding.cs` (+ `Inspect`), `MotivRulesEndpoints.cs` (validate facts, catalog `format`), `RulesContracts.cs` (`ValidationResponse.Facts`).

**TypeScript — `ui/packages/rules-core/src/expression/`** (new): `tokenize.ts`, `parse.ts`, `scope.ts`, `lattice.ts`, `check.ts`, `complete.ts`, `index.ts`. Modify `contracts.ts`, `dsl/diagnostics.ts`, `dsl/completion.ts`, `index.ts`. Tests under `ui/packages/rules-core/test/expression-*.test.ts` and `test/expression/corpus.json`.

**Studio — modify:** `src/dsl/motivLanguage.ts`, `src/dsl/lint.ts`, `src/dsl/hover.ts`, `src/dsl/DslEditor.tsx`, `src/builder/useInlineDslEditor.ts`, `src/builder/NodeToolbar.tsx`, `src/styles/app.css`. New: `src/dsl/LeafInspector.tsx`. Tests under `ui/apps/studio/test/dsl/`.

**Docs:** `README.md`, `docs/live-rules/expressions.md` (new), `docs/live-rules/toc.yml`, `docs/toc.yml`, `docs/Overview.md`, `docs/propositions/index.md`.

---

## Phase 1 — C#: parse, check, compile, bind

### Task 1: Leaf tokenizer

**Files:**
- Create: `src/Motiv.Serialization/Expressions/LeafToken.cs`
- Create: `src/Motiv.Serialization/Expressions/LeafTokenizer.cs`
- Test: `src/Motiv.Serialization.Tests/Expressions/LeafTokenizerTests.cs`

**Interfaces:**
- Produces: `enum LeafTokenKind { Identifier, Parameter, Number, String, Keyword, Operator, Punctuation, End }`; `record LeafToken(LeafTokenKind Kind, string Text, int Start, int End)`; `static IReadOnlyList<LeafToken> LeafTokenizer.Tokenize(string text, List<LeafProblem> problems)` — whitespace dropped, ends with an `End` token at `(text.Length, text.Length)`. `LeafProblem` is defined here too: `record LeafProblem(RuleErrorCode Code, string Message, int Start, int End)`.

- [ ] **Step 1: Write the failing test**

```csharp
// src/Motiv.Serialization.Tests/Expressions/LeafTokenizerTests.cs
using Motiv.Serialization.Expressions;

namespace Motiv.Serialization.Tests.Expressions;

public class LeafTokenizerTests
{
    private static (IReadOnlyList<LeafToken> Tokens, List<LeafProblem> Problems) Tokenize(string text)
    {
        var problems = new List<LeafProblem>();
        return (LeafTokenizer.Tokenize(text, problems), problems);
    }

    [Fact]
    public void Should_tokenize_a_filtered_aggregate_comparison()
    {
        var (tokens, problems) = Tokenize("orders.where(o => o.status == \"paid\").sum(o => o.total) > @vip");

        problems.ShouldBeEmpty();
        tokens.Select(t => t.Kind).ShouldBe([
            LeafTokenKind.Identifier, LeafTokenKind.Punctuation, LeafTokenKind.Identifier, LeafTokenKind.Punctuation,
            LeafTokenKind.Identifier, LeafTokenKind.Operator, LeafTokenKind.Identifier, LeafTokenKind.Punctuation,
            LeafTokenKind.Identifier, LeafTokenKind.Operator, LeafTokenKind.String, LeafTokenKind.Punctuation,
            LeafTokenKind.Punctuation, LeafTokenKind.Identifier, LeafTokenKind.Punctuation, LeafTokenKind.Identifier,
            LeafTokenKind.Operator, LeafTokenKind.Identifier, LeafTokenKind.Punctuation, LeafTokenKind.Identifier,
            LeafTokenKind.Punctuation, LeafTokenKind.Operator, LeafTokenKind.Parameter, LeafTokenKind.End]);
        tokens[10].Text.ShouldBe("\"paid\"");
        tokens[22].Text.ShouldBe("@vip");
        tokens[22].Start.ShouldBe(60);
    }

    [Theory]
    [InlineData("null", LeafTokenKind.Keyword)]
    [InlineData("true", LeafTokenKind.Keyword)]
    [InlineData("1.5", LeafTokenKind.Number)]
    [InlineData("=>", LeafTokenKind.Operator)]
    [InlineData("!=", LeafTokenKind.Operator)]
    public void Should_classify_single_tokens(string text, LeafTokenKind kind)
    {
        var (tokens, _) = Tokenize(text);
        tokens[0].Kind.ShouldBe(kind);
        tokens[0].Text.ShouldBe(text);
    }

    [Fact]
    public void Should_report_an_unterminated_string_at_its_range()
    {
        var (_, problems) = Tokenize("country == \"SE");
        var problem = problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe(RuleErrorCode.InvalidExpression);
        problem.Start.ShouldBe(11);
        problem.End.ShouldBe(14);
    }

    [Fact]
    public void Should_report_an_unrecognised_character()
    {
        var (_, problems) = Tokenize("age # 3");
        var problem = problems.ShouldHaveSingleItem();
        problem.Message.ShouldContain("#");
        problem.Start.ShouldBe(4);
    }
}
```

- [ ] **Step 2: Add the error codes so the test compiles, then run it to see it fail**

Append to `src/Motiv.Serialization/RuleErrorCode.cs` (inside the enum, after the last member):

```csharp
    /// <summary>An expression leaf could not be parsed.</summary>
    InvalidExpression,

    /// <summary>An expression leaf names a field the model does not have.</summary>
    UnknownField,

    /// <summary>An expression leaf calls a method outside the supported set, or on the wrong kind of value.</summary>
    UnknownMethod,

    /// <summary>The operands of an expression leaf cannot be given one type without a lossy conversion.</summary>
    ExpressionTypeMismatch,

    /// <summary>An expression leaf in a metadata document carries no metadata of its own.</summary>
    ExpressionRequiresMetadata,
```

Run: `env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests --framework net10.0 --filter LeafTokenizerTests 2>&1 | grep -E "error CS|Passed!|Failed!"`
Expected: `error CS0246` — `LeafTokenizer` does not exist.

- [ ] **Step 3: Implement the tokenizer**

```csharp
// src/Motiv.Serialization/Expressions/LeafToken.cs
namespace Motiv.Serialization.Expressions;

internal enum LeafTokenKind { Identifier, Parameter, Number, String, Keyword, Operator, Punctuation, End }

/// <summary>One token of a leaf expression; offsets are into the leaf text, not the document.</summary>
internal sealed record LeafToken(LeafTokenKind Kind, string Text, int Start, int End);

/// <summary>A problem found in a leaf, at a range of the leaf text.</summary>
internal sealed record LeafProblem(RuleErrorCode Code, string Message, int Start, int End, bool IsWarning = false);
```

```csharp
// src/Motiv.Serialization/Expressions/LeafTokenizer.cs
namespace Motiv.Serialization.Expressions;

internal static class LeafTokenizer
{
    private static readonly HashSet<string> Keywords = ["null", "true", "false"];
    private static readonly string[] TwoCharOperators = ["=>", "==", "!=", "<=", ">=", "&&", "||"];

    public static IReadOnlyList<LeafToken> Tokenize(string text, List<LeafProblem> problems)
    {
        var tokens = new List<LeafToken>();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            var start = i;
            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (char.IsLetter(c) || c == '_')
            {
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++;
                var word = text.Substring(start, i - start);
                tokens.Add(new LeafToken(Keywords.Contains(word) ? LeafTokenKind.Keyword : LeafTokenKind.Identifier, word, start, i));
                continue;
            }

            if (c == '@')
            {
                i++;
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++;
                if (i == start + 1)
                    problems.Add(new LeafProblem(RuleErrorCode.InvalidExpression, "expected a parameter name after '@'", start, i));
                tokens.Add(new LeafToken(LeafTokenKind.Parameter, text.Substring(start, i - start), start, i));
                continue;
            }

            if (char.IsDigit(c))
            {
                while (i < text.Length && char.IsDigit(text[i])) i++;
                if (i + 1 < text.Length && text[i] == '.' && char.IsDigit(text[i + 1]))
                {
                    i++;
                    while (i < text.Length && char.IsDigit(text[i])) i++;
                }
                tokens.Add(new LeafToken(LeafTokenKind.Number, text.Substring(start, i - start), start, i));
                continue;
            }

            if (c == '"')
            {
                i++;
                while (i < text.Length && text[i] != '"') i++;
                if (i >= text.Length)
                {
                    problems.Add(new LeafProblem(RuleErrorCode.InvalidExpression, "unterminated string", start, text.Length));
                    tokens.Add(new LeafToken(LeafTokenKind.String, text.Substring(start), start, text.Length));
                    break;
                }
                i++;
                tokens.Add(new LeafToken(LeafTokenKind.String, text.Substring(start, i - start), start, i));
                continue;
            }

            var two = i + 1 < text.Length ? text.Substring(i, 2) : null;
            if (two is not null && Array.IndexOf(TwoCharOperators, two) >= 0)
            {
                i += 2;
                tokens.Add(new LeafToken(LeafTokenKind.Operator, two, start, i));
                continue;
            }

            if ("<>!+-*/".IndexOf(c) >= 0)
            {
                i++;
                tokens.Add(new LeafToken(LeafTokenKind.Operator, c.ToString(), start, i));
                continue;
            }

            if ("().,".IndexOf(c) >= 0)
            {
                i++;
                tokens.Add(new LeafToken(LeafTokenKind.Punctuation, c.ToString(), start, i));
                continue;
            }

            problems.Add(new LeafProblem(RuleErrorCode.InvalidExpression, $"unexpected character '{c}'", start, start + 1));
            i++;
        }

        tokens.Add(new LeafToken(LeafTokenKind.End, string.Empty, text.Length, text.Length));
        return tokens;
    }
}
```

`Substring` rather than ranges: the file is compiled for `netstandard2.0` too (the tokenizer has no .NET 8 dependency, so it is not `#if`-guarded).

- [ ] **Step 4: Run the tests**

Run: `env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests --framework net10.0 --filter LeafTokenizerTests 2>&1 | grep -E "error CS|Passed!|Failed!"`
Expected: `Passed!`

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Serialization/Expressions src/Motiv.Serialization/RuleErrorCode.cs src/Motiv.Serialization.Tests/Expressions
git commit -m "Expressions — leaf tokenizer and the new error codes"
```

### Task 2: Leaf parser

**Files:**
- Create: `src/Motiv.Serialization/Expressions/LeafNode.cs`
- Create: `src/Motiv.Serialization/Expressions/LeafParser.cs`
- Test: `src/Motiv.Serialization.Tests/Expressions/LeafParserTests.cs`

**Interfaces:**
- Consumes: `LeafTokenizer.Tokenize`, `LeafToken`, `LeafProblem`.
- Produces: `abstract record LeafNode(int Start, int End)` with subtypes `NumberLiteral(string Text)`, `StringLiteral(string Value)`, `BoolLiteral(bool Value)`, `NullLiteral`, `ParameterRef(string Name)`, `Identifier(string Name)`, `MemberAccess(LeafNode Target, string Name, int NameStart, int NameEnd)`, `MethodCall(LeafNode Target, string Method, IReadOnlyList<LeafNode> Arguments, int MethodStart, int MethodEnd)`, `Lambda(string Parameter, LeafNode Body)`, `Binary(string Operator, LeafNode Left, LeafNode Right)`, `Unary(string Operator, LeafNode Operand)`; `static LeafNode? LeafParser.Parse(string text, List<LeafProblem> problems)` — null when a problem was reported.

- [ ] **Step 1: Write the failing test**

```csharp
// src/Motiv.Serialization.Tests/Expressions/LeafParserTests.cs
using Motiv.Serialization.Expressions;

namespace Motiv.Serialization.Tests.Expressions;

public class LeafParserTests
{
    private static (LeafNode? Node, List<LeafProblem> Problems) Parse(string text)
    {
        var problems = new List<LeafProblem>();
        return (LeafParser.Parse(text, problems), problems);
    }

    [Fact]
    public void Should_parse_precedence_from_comparison_down_to_multiplication()
    {
        var (node, problems) = Parse("age * 12 + 1 >= @min && isActive");

        problems.ShouldBeEmpty();
        var and = node.ShouldBeOfType<Binary>();
        and.Operator.ShouldBe("&&");
        var cmp = and.Left.ShouldBeOfType<Binary>();
        cmp.Operator.ShouldBe(">=");
        var add = cmp.Left.ShouldBeOfType<Binary>();
        add.Operator.ShouldBe("+");
        add.Left.ShouldBeOfType<Binary>().Operator.ShouldBe("*");
        cmp.Right.ShouldBeOfType<ParameterRef>().Name.ShouldBe("min");
        and.Right.ShouldBeOfType<Identifier>().Name.ShouldBe("isActive");
        and.Start.ShouldBe(0);
        and.End.ShouldBe(32);
    }

    [Fact]
    public void Should_parse_a_method_chain_with_lambdas()
    {
        var (node, problems) = Parse("orders.where(o => o.status == \"paid\").sum(o => o.total) > 1000");

        problems.ShouldBeEmpty();
        var cmp = node.ShouldBeOfType<Binary>();
        var sum = cmp.Left.ShouldBeOfType<MethodCall>();
        sum.Method.ShouldBe("sum");
        sum.MethodStart.ShouldBe(39);
        var lambda = sum.Arguments.ShouldHaveSingleItem().ShouldBeOfType<Lambda>();
        lambda.Parameter.ShouldBe("o");
        lambda.Body.ShouldBeOfType<MemberAccess>().Name.ShouldBe("total");
        var where = sum.Target.ShouldBeOfType<MethodCall>();
        where.Method.ShouldBe("where");
        where.Target.ShouldBeOfType<Identifier>().Name.ShouldBe("orders");
        cmp.Right.ShouldBeOfType<NumberLiteral>().Text.ShouldBe("1000");
    }

    [Fact]
    public void Should_parse_unary_and_parentheses()
    {
        var (node, _) = Parse("!(a || b) && -x < 0");
        var and = node.ShouldBeOfType<Binary>();
        and.Left.ShouldBeOfType<Unary>().Operator.ShouldBe("!");
        and.Right.ShouldBeOfType<Binary>().Left.ShouldBeOfType<Unary>().Operator.ShouldBe("-");
    }

    [Theory]
    [InlineData("", "empty expression", 0, 0)]
    [InlineData("age >", "unexpected end of expression", 5, 5)]
    [InlineData("age > > 1", "unexpected '>'", 6, 7)]
    [InlineData("orders.", "expected a field or method name after '.'", 7, 7)]
    [InlineData("a == b == c", "unexpected '=='", 7, 9)]
    public void Should_report_syntax_errors_at_their_range(string text, string message, int start, int end)
    {
        var (node, problems) = Parse(text);
        node.ShouldBeNull();
        var problem = problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe(RuleErrorCode.InvalidExpression);
        problem.Message.ShouldBe(message);
        problem.Start.ShouldBe(start);
        problem.End.ShouldBe(end);
    }
}
```

- [ ] **Step 2: Run it to see it fail**

Run: `env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests --framework net10.0 --filter LeafParserTests 2>&1 | grep -E "error CS|Passed!|Failed!"`
Expected: `error CS0246` for `LeafParser`.

- [ ] **Step 3: Implement the AST and parser**

```csharp
// src/Motiv.Serialization/Expressions/LeafNode.cs
namespace Motiv.Serialization.Expressions;

/// <summary>A node of a parsed leaf; <c>Start</c>/<c>End</c> are offsets into the leaf text.</summary>
internal abstract record LeafNode(int Start, int End);

internal sealed record NumberLiteral(string Text, int Start, int End) : LeafNode(Start, End);
internal sealed record StringLiteral(string Value, int Start, int End) : LeafNode(Start, End);
internal sealed record BoolLiteral(bool Value, int Start, int End) : LeafNode(Start, End);
internal sealed record NullLiteral(int Start, int End) : LeafNode(Start, End);
internal sealed record ParameterRef(string Name, int Start, int End) : LeafNode(Start, End);
internal sealed record Identifier(string Name, int Start, int End) : LeafNode(Start, End);
internal sealed record MemberAccess(LeafNode Target, string Name, int NameStart, int NameEnd, int Start, int End) : LeafNode(Start, End);
internal sealed record MethodCall(LeafNode Target, string Method, IReadOnlyList<LeafNode> Arguments, int MethodStart, int MethodEnd, int Start, int End) : LeafNode(Start, End);
internal sealed record Lambda(string Parameter, LeafNode Body, int Start, int End) : LeafNode(Start, End);
internal sealed record Binary(string Operator, LeafNode Left, LeafNode Right, int Start, int End) : LeafNode(Start, End);
internal sealed record Unary(string Operator, LeafNode Operand, int Start, int End) : LeafNode(Start, End);
```

```csharp
// src/Motiv.Serialization/Expressions/LeafParser.cs
namespace Motiv.Serialization.Expressions;

/// <summary>
/// Recursive descent over the grammar in the design doc. One problem ends the parse: after a
/// syntax error the rest of the text cannot be trusted to mean anything, and a cascade of
/// follow-on errors would bury the one that matters.
/// </summary>
internal sealed class LeafParser
{
    private readonly IReadOnlyList<LeafToken> _tokens;
    private readonly int _length;
    private int _index;

    private LeafParser(IReadOnlyList<LeafToken> tokens, int length)
    {
        _tokens = tokens;
        _length = length;
    }

    public static LeafNode? Parse(string text, List<LeafProblem> problems)
    {
        var tokens = LeafTokenizer.Tokenize(text, problems);
        if (problems.Count > 0)
            return null;
        if (tokens.Count == 1)
        {
            problems.Add(new LeafProblem(RuleErrorCode.InvalidExpression, "empty expression", 0, 0));
            return null;
        }

        var parser = new LeafParser(tokens, text.Length);
        try
        {
            var node = parser.Or();
            var rest = parser.Peek();
            if (rest.Kind != LeafTokenKind.End)
                throw new SyntaxError($"unexpected '{rest.Text}'", rest);
            return node;
        }
        catch (SyntaxError error)
        {
            problems.Add(new LeafProblem(RuleErrorCode.InvalidExpression, error.Message, error.Token.Start, error.Token.End));
            return null;
        }
    }

    private sealed class SyntaxError(string message, LeafToken token) : Exception(message)
    {
        public LeafToken Token { get; } = token;
    }

    private LeafToken Peek(int offset = 0) =>
        _index + offset < _tokens.Count ? _tokens[_index + offset] : _tokens[_tokens.Count - 1];

    private LeafToken Next()
    {
        var token = Peek();
        if (token.Kind == LeafTokenKind.End)
            throw new SyntaxError("unexpected end of expression", token);
        _index++;
        return token;
    }

    private bool At(string text) => Peek().Text == text && Peek().Kind != LeafTokenKind.String;

    private LeafToken Expect(string text)
    {
        var token = Peek();
        if (token.Kind == LeafTokenKind.End)
            throw new SyntaxError("unexpected end of expression", token);
        if (token.Text != text)
            throw new SyntaxError($"expected '{text}'", token);
        return Next();
    }

    private LeafNode BinaryLevel(string[] operators, Func<LeafNode> below, bool once = false)
    {
        var left = below();
        while (Peek().Kind == LeafTokenKind.Operator && Array.IndexOf(operators, Peek().Text) >= 0)
        {
            var op = Next().Text;
            var right = below();
            left = new Binary(op, left, right, left.Start, right.End);
            if (once) break;
        }
        return left;
    }

    private LeafNode Or() => BinaryLevel(["||"], And);
    private LeafNode And() => BinaryLevel(["&&"], Equality);
    private LeafNode Equality() => BinaryLevel(["==", "!="], Comparison, once: true);
    private LeafNode Comparison() => BinaryLevel(["<", "<=", ">", ">="], Additive, once: true);
    private LeafNode Additive() => BinaryLevel(["+", "-"], Multiplicative);
    private LeafNode Multiplicative() => BinaryLevel(["*", "/"], UnaryLevel);

    private LeafNode UnaryLevel()
    {
        if (At("!") || At("-"))
        {
            var op = Next();
            var operand = UnaryLevel();
            return new Unary(op.Text, operand, op.Start, operand.End);
        }
        return Postfix();
    }

    private LeafNode Postfix()
    {
        var node = Primary();
        while (At("."))
        {
            Next();
            var name = Peek();
            if (name.Kind != LeafTokenKind.Identifier)
                throw new SyntaxError("expected a field or method name after '.'", name);
            Next();
            if (At("("))
            {
                Next();
                var arguments = new List<LeafNode>();
                while (!At(")"))
                {
                    arguments.Add(LambdaOrExpression());
                    if (At(",")) Next(); else break;
                }
                var close = Expect(")");
                node = new MethodCall(node, name.Text, arguments, name.Start, name.End, node.Start, close.End);
            }
            else
            {
                node = new MemberAccess(node, name.Text, name.Start, name.End, node.Start, name.End);
            }
        }
        return node;
    }

    private LeafNode LambdaOrExpression()
    {
        var token = Peek();
        if (token.Kind == LeafTokenKind.Identifier && Peek(1).Text == "=>")
        {
            Next(); Next();
            var body = Or();
            return new Lambda(token.Text, body, token.Start, body.End);
        }
        return Or();
    }

    private LeafNode Primary()
    {
        var token = Next();
        switch (token.Kind)
        {
            case LeafTokenKind.Number: return new NumberLiteral(token.Text, token.Start, token.End);
            case LeafTokenKind.String: return new StringLiteral(token.Text.Substring(1, token.Text.Length - 2), token.Start, token.End);
            case LeafTokenKind.Keyword when token.Text == "null": return new NullLiteral(token.Start, token.End);
            case LeafTokenKind.Keyword: return new BoolLiteral(token.Text == "true", token.Start, token.End);
            case LeafTokenKind.Parameter: return new ParameterRef(token.Text.Substring(1), token.Start, token.End);
            case LeafTokenKind.Identifier: return new Identifier(token.Text, token.Start, token.End);
            case LeafTokenKind.Punctuation when token.Text == "(":
            {
                var inner = Or();
                var close = Expect(")");
                return inner with { Start = token.Start, End = close.End };
            }
            default:
                throw new SyntaxError($"unexpected '{token.Text}'", token);
        }
    }
}
```

`Primary` tokens are pulled with `Next()`, so the empty-tail error `"age >"` reports "unexpected end of expression" at the end offset; `"age > > 1"` reaches `Primary` with `>` and reports it. `Expect` also refuses `End` with the same message, which is what `"orders."` relies on — the name check sees the `End` token and reports the dot message at `(7,7)`.

- [ ] **Step 4: Run the tests**

Run: `env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests --framework net10.0 --filter LeafParserTests 2>&1 | grep -E "error CS|Passed!|Failed!"`
Expected: `Passed!`. If the `"orders."` case reports a different range, print `problem` and adjust the *test's* expected range to what the parser reports, provided it lies within `[6, 7]`.

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Serialization/Expressions src/Motiv.Serialization.Tests/Expressions
git commit -m "Expressions — leaf AST and recursive-descent parser"
```

### Task 3: Leaf scope — JSON names to CLR members

**Files:**
- Create: `src/Motiv.Serialization/Expressions/LeafScope.cs`
- Test: `src/Motiv.Serialization.Tests/Expressions/LeafScopeTests.cs`

**Interfaces:**
- Produces: `sealed class LeafScope` with `Type ModelType`, `IReadOnlyDictionary<string, RuleParameterDeclaration> Parameters`, `IReadOnlyDictionary<string, Type> Variables`; `static LeafScope For(Type modelType, IReadOnlyList<RuleParameterDeclaration> parameters)`; `LeafScope WithVariable(string name, Type type)`; `static MemberInfo? FindMember(Type type, string jsonName)` (public properties and fields, matched by `[JsonPropertyName]`, else camelCase of the CLR name, else exact); `static Type? ElementType(Type type)` (`T` for anything implementing `IEnumerable<T>`, excluding `string`); `static Type MemberType(MemberInfo member)`.

- [ ] **Step 1: Write the failing test**

```csharp
// src/Motiv.Serialization.Tests/Expressions/LeafScopeTests.cs
using System.Text.Json.Serialization;
using Motiv.Serialization.Expressions;

namespace Motiv.Serialization.Tests.Expressions;

public class LeafScopeTests
{
    private sealed record Order(decimal Total, [property: JsonPropertyName("shipped_days")] int DaysSinceShipped);
    private sealed record Customer(int Age, bool IsActive, IReadOnlyList<Order>? Orders, string? Country);

    [Theory]
    [InlineData("age", "Age")]
    [InlineData("isActive", "IsActive")]
    [InlineData("orders", "Orders")]
    [InlineData("Age", "Age")]
    public void Should_find_members_by_their_json_name(string jsonName, string clrName)
    {
        LeafScope.FindMember(typeof(Customer), jsonName)!.Name.ShouldBe(clrName);
    }

    [Fact]
    public void Should_prefer_an_explicit_json_property_name()
    {
        LeafScope.FindMember(typeof(Order), "shipped_days")!.Name.ShouldBe("DaysSinceShipped");
        LeafScope.FindMember(typeof(Order), "daysSinceShipped").ShouldBeNull();
    }

    [Fact]
    public void Should_return_null_for_an_unknown_name()
    {
        LeafScope.FindMember(typeof(Customer), "nope").ShouldBeNull();
    }

    [Fact]
    public void Should_find_the_element_type_of_a_collection_member()
    {
        var orders = LeafScope.FindMember(typeof(Customer), "orders")!;
        LeafScope.ElementType(LeafScope.MemberType(orders)).ShouldBe(typeof(Order));
        LeafScope.ElementType(typeof(string)).ShouldBeNull();
        LeafScope.ElementType(typeof(int)).ShouldBeNull();
    }

    [Fact]
    public void Should_carry_variables_without_mutating_the_parent()
    {
        var root = LeafScope.For(typeof(Customer), []);
        var inner = root.WithVariable("o", typeof(Order));
        inner.Variables["o"].ShouldBe(typeof(Order));
        root.Variables.ShouldBeEmpty();
        inner.ModelType.ShouldBe(typeof(Customer));
    }
}
```

- [ ] **Step 2: Run it to see it fail**

Run: `env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests --framework net10.0 --filter LeafScopeTests 2>&1 | grep -E "error CS|Passed!|Failed!"`
Expected: `error CS0246` for `LeafScope`.

- [ ] **Step 3: Implement**

```csharp
// src/Motiv.Serialization/Expressions/LeafScope.cs
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Motiv.Serialization.Expressions;

/// <summary>
/// What a name means at a point in a leaf: the model type, the lambda variables in scope, and
/// the document's parameters. Members are addressed by their JSON-schema names, because that is
/// what the catalog publishes and what the editor completes — never by CLR names.
/// </summary>
internal sealed class LeafScope
{
    private LeafScope(Type modelType, IReadOnlyDictionary<string, RuleParameterDeclaration> parameters, IReadOnlyDictionary<string, Type> variables)
    {
        ModelType = modelType;
        Parameters = parameters;
        Variables = variables;
    }

    public Type ModelType { get; }
    public IReadOnlyDictionary<string, RuleParameterDeclaration> Parameters { get; }
    public IReadOnlyDictionary<string, Type> Variables { get; }

    public static LeafScope For(Type modelType, IReadOnlyList<RuleParameterDeclaration> parameters) =>
        new(modelType, parameters.ToDictionary(p => p.Name, p => p, StringComparer.Ordinal), new Dictionary<string, Type>(StringComparer.Ordinal));

    public LeafScope WithVariable(string name, Type type)
    {
        var variables = new Dictionary<string, Type>(Variables, StringComparer.Ordinal) { [name] = type };
        return new LeafScope(ModelType, Parameters, variables);
    }

    /// <summary>
    /// The member a JSON name addresses: an explicit <see cref="JsonPropertyNameAttribute" /> wins,
    /// then the camel-cased CLR name (System.Text.Json's conventional policy), then the exact CLR
    /// name — so a host serializing with PascalCase still resolves.
    /// </summary>
    public static MemberInfo? FindMember(Type type, string jsonName)
    {
        var members = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0)
            .Cast<MemberInfo>()
            .Concat(type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            .ToList();

        foreach (var member in members)
            if (member.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name == jsonName)
                return member;

        foreach (var member in members)
            if (member.GetCustomAttribute<JsonPropertyNameAttribute>() is null
                && JsonNamingPolicy.CamelCase.ConvertName(member.Name) == jsonName)
                return member;

        return members.FirstOrDefault(m => m.GetCustomAttribute<JsonPropertyNameAttribute>() is null && m.Name == jsonName);
    }

    public static Type MemberType(MemberInfo member) =>
        member is PropertyInfo property ? property.PropertyType : ((FieldInfo)member).FieldType;

    /// <summary>The element type of anything enumerable except <see cref="string" />; null otherwise.</summary>
    public static Type? ElementType(Type type)
    {
        if (type == typeof(string))
            return null;
        var enumerable = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            ? type
            : type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return enumerable?.GetGenericArguments()[0];
    }
}
```

- [ ] **Step 4: Run the tests**

Run: `env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests --framework net10.0 --filter LeafScopeTests 2>&1 | grep -E "error CS|Passed!|Failed!"`
Expected: `Passed!`

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Serialization/Expressions src/Motiv.Serialization.Tests/Expressions
git commit -m "Expressions — leaf scope resolves JSON names to CLR members"
```

### Task 4: Numeric lattice and typed constants

**Files:**
- Create: `src/Motiv.Serialization/Expressions/NumericKind.cs`
- Create: `src/Motiv.Serialization/Expressions/NumericLattice.cs`
- Test: `src/Motiv.Serialization.Tests/Expressions/NumericLatticeTests.cs`

**Interfaces:**
- Produces: `enum NumericKind { Int32, Int64, Single, Double, Decimal }`; `static NumericKind? NumericLattice.KindOf(Type type)` (unwraps `Nullable<T>`); `static Type ClrType(NumericKind kind)`; `static bool CanWiden(NumericKind from, NumericKind to)`; `static NumericKind? Join(NumericKind a, NumericKind b)` (the smaller of the two if one widens to the other, else null); `static bool IsIntegral(NumericKind kind)`; `static Expression Constant(string text, NumericKind kind)` — parses with `T.Parse(text, InvariantCulture)` for the kind's CLR type, throws `FormatException` for a fractional text against an integral kind; `static Expression Widen(Expression value, NumericKind to)` — `Expression.ConvertChecked` to the target CLR type (lifting `Nullable<T>` when the source is nullable).

- [ ] **Step 1: Write the failing test**

```csharp
// src/Motiv.Serialization.Tests/Expressions/NumericLatticeTests.cs
using System.Linq.Expressions;
using Motiv.Serialization.Expressions;

namespace Motiv.Serialization.Tests.Expressions;

public class NumericLatticeTests
{
    [Theory]
    [InlineData(NumericKind.Int32, NumericKind.Int64, true)]
    [InlineData(NumericKind.Int32, NumericKind.Decimal, true)]
    [InlineData(NumericKind.Int64, NumericKind.Decimal, true)]
    [InlineData(NumericKind.Int32, NumericKind.Double, true)]
    [InlineData(NumericKind.Single, NumericKind.Double, true)]
    [InlineData(NumericKind.Int64, NumericKind.Double, false)]
    [InlineData(NumericKind.Int32, NumericKind.Single, false)]
    [InlineData(NumericKind.Decimal, NumericKind.Double, false)]
    [InlineData(NumericKind.Double, NumericKind.Decimal, false)]
    [InlineData(NumericKind.Int64, NumericKind.Int32, false)]
    public void Should_allow_only_exact_widenings(NumericKind from, NumericKind to, bool allowed)
    {
        NumericLattice.CanWiden(from, to).ShouldBe(allowed);
    }

    [Theory]
    [InlineData(NumericKind.Int32, NumericKind.Decimal, NumericKind.Decimal)]
    [InlineData(NumericKind.Decimal, NumericKind.Int32, NumericKind.Decimal)]
    [InlineData(NumericKind.Int32, NumericKind.Int32, NumericKind.Int32)]
    [InlineData(NumericKind.Decimal, NumericKind.Double, null)]
    [InlineData(NumericKind.Int64, NumericKind.Double, null)]
    public void Should_join_to_the_wider_of_a_permitted_pair(NumericKind a, NumericKind b, NumericKind? expected)
    {
        NumericLattice.Join(a, b).ShouldBe(expected);
    }

    [Theory]
    [InlineData(typeof(int), NumericKind.Int32)]
    [InlineData(typeof(long?), NumericKind.Int64)]
    [InlineData(typeof(decimal), NumericKind.Decimal)]
    [InlineData(typeof(float), NumericKind.Single)]
    [InlineData(typeof(string), null)]
    public void Should_classify_clr_types(Type type, NumericKind? expected)
    {
        NumericLattice.KindOf(type).ShouldBe(expected);
    }

    [Fact]
    public void Should_parse_a_literal_exactly_as_the_target_type()
    {
        var constant = NumericLattice.Constant("0.1", NumericKind.Decimal).ShouldBeOfType<ConstantExpression>();
        constant.Value.ShouldBe(0.1m);
        constant.Type.ShouldBe(typeof(decimal));
        NumericLattice.Constant("42", NumericKind.Int64).ShouldBeOfType<ConstantExpression>().Value.ShouldBe(42L);
        Should.Throw<FormatException>(() => NumericLattice.Constant("1.5", NumericKind.Int32));
    }

    [Fact]
    public void Should_widen_with_a_checked_conversion_and_lift_nullables()
    {
        var value = Expression.Constant(3, typeof(int));
        var widened = NumericLattice.Widen(value, NumericKind.Decimal);
        widened.NodeType.ShouldBe(ExpressionType.ConvertChecked);
        widened.Type.ShouldBe(typeof(decimal));

        var nullable = Expression.Constant(3, typeof(int?));
        NumericLattice.Widen(nullable, NumericKind.Decimal).Type.ShouldBe(typeof(decimal?));
    }
}
```

- [ ] **Step 2: Run it to see it fail**

Run: `env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests --framework net10.0 --filter NumericLatticeTests 2>&1 | grep -E "error CS|Passed!|Failed!"`
Expected: `error CS0246` for `NumericKind`.

- [ ] **Step 3: Implement**

```csharp
// src/Motiv.Serialization/Expressions/NumericKind.cs
namespace Motiv.Serialization.Expressions;

internal enum NumericKind { Int32, Int64, Single, Double, Decimal }
```

```csharp
// src/Motiv.Serialization/Expressions/NumericLattice.cs
#if NET8_0_OR_GREATER
using System.Globalization;
using System.Linq.Expressions;
using System.Numerics;

namespace Motiv.Serialization.Expressions;

/// <summary>
/// The widening rules of the leaf language: C#'s implicit numeric conversions minus the lossy
/// ones. Generic math does the conversions; the *policy* — which conversions are allowed — is this
/// table, because <c>T.CreateChecked</c> refuses overflow, not precision loss.
/// </summary>
internal static class NumericLattice
{
    private static readonly (NumericKind From, NumericKind To)[] Widenings =
    [
        (NumericKind.Int32, NumericKind.Int64),
        (NumericKind.Int32, NumericKind.Decimal),
        (NumericKind.Int64, NumericKind.Decimal),
        (NumericKind.Int32, NumericKind.Double),
        (NumericKind.Single, NumericKind.Double),
    ];

    public static NumericKind? KindOf(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying == typeof(int)) return NumericKind.Int32;
        if (underlying == typeof(long)) return NumericKind.Int64;
        if (underlying == typeof(float)) return NumericKind.Single;
        if (underlying == typeof(double)) return NumericKind.Double;
        if (underlying == typeof(decimal)) return NumericKind.Decimal;
        return null;
    }

    public static Type ClrType(NumericKind kind) => kind switch
    {
        NumericKind.Int32 => typeof(int),
        NumericKind.Int64 => typeof(long),
        NumericKind.Single => typeof(float),
        NumericKind.Double => typeof(double),
        _ => typeof(decimal),
    };

    public static bool IsIntegral(NumericKind kind) => kind is NumericKind.Int32 or NumericKind.Int64;

    public static bool CanWiden(NumericKind from, NumericKind to) =>
        from == to || Array.IndexOf(Widenings, (from, to)) >= 0;

    public static NumericKind? Join(NumericKind a, NumericKind b)
    {
        if (CanWiden(a, b)) return b;
        if (CanWiden(b, a)) return a;
        return null;
    }

    public static Expression Constant(string text, NumericKind kind) => kind switch
    {
        NumericKind.Int32 => Parse<int>(text),
        NumericKind.Int64 => Parse<long>(text),
        NumericKind.Single => Parse<float>(text),
        NumericKind.Double => Parse<double>(text),
        _ => Parse<decimal>(text),
    };

    private static ConstantExpression Parse<T>(string text) where T : INumber<T> =>
        Expression.Constant(T.Parse(text, NumberStyles.Number, CultureInfo.InvariantCulture), typeof(T));

    public static Expression Widen(Expression value, NumericKind to)
    {
        var target = ClrType(to);
        if (Nullable.GetUnderlyingType(value.Type) is not null)
            target = typeof(Nullable<>).MakeGenericType(target);
        return value.Type == target ? value : Expression.ConvertChecked(value, target);
    }
}
#endif
```

Note `NumberStyles.Number` refuses exponents and hex, matching the tokenizer's number shape; `int.Parse("1.5")` throws `FormatException`, which is the behaviour the test pins.

- [ ] **Step 4: Run the tests**

Run: `env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests --framework net10.0 --filter NumericLatticeTests 2>&1 | grep -E "error CS|Passed!|Failed!"`
Expected: `Passed!`

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Serialization/Expressions src/Motiv.Serialization.Tests/Expressions
git commit -m "Expressions — numeric lattice: exact widenings and generic-math constants"
```

### Task 5: Leaf checker — type inference, problems, facts

**Files:**
- Create: `src/Motiv.Serialization/Expressions/LeafType.cs`
- Create: `src/Motiv.Serialization/Expressions/LeafFact.cs`
- Create: `src/Motiv.Serialization/Expressions/LeafChecker.cs`
- Test: `src/Motiv.Serialization.Tests/Expressions/LeafCheckerTests.cs`

**Interfaces:**
- Consumes: `LeafNode` subtypes, `LeafScope`, `NumericLattice`, `LeafProblem`.
- Produces: `sealed record LeafFact(LeafNode Node, Type Type, string? From)` — `From` names the anchor that fixed an untyped literal or parameter (`"orders.sum(o => o.total)"`), null for anchored nodes. `sealed class LeafAnalysis { IReadOnlyList<LeafProblem> Problems; IReadOnlyDictionary<LeafNode, Type> Types; IReadOnlyList<LeafFact> Facts; bool IsValid => no non-warning problem }`. `static LeafAnalysis LeafChecker.Check(LeafNode root, LeafScope scope)`. `Types` gives every node its solved CLR type (nullable-lifted where a nullable member is on the path). The **solved type of the lambda parameter** of a call is stored under the `Lambda` node's key as the element type.

**Algorithm.** Bottom-up with union-find type variables:
- A `NumberLiteral` or `ParameterRef` of numeric kind creates a `TypeVar { Fractional, ParamKind, Resolved }`. A `StringLiteral`, `BoolLiteral`, `NullLiteral`, `Identifier`, `MemberAccess`, `MethodCall` is concrete (or an error).
- `Binary` arithmetic/comparison: if both sides concrete numeric → `Join` (null → `ExpressionTypeMismatch`). If one is a var → resolve the var: to the concrete kind if the var's constraints allow it (`Fractional` requires a non-integral kind; `ParamKind.Integer` allows `Int32/Int64/Decimal`; `ParamKind.Number` allows `Decimal/Double/Single`); when a fractional var meets an integral concrete, resolve to `Decimal` and record that the concrete side widens. If both vars → union them (merge flags). The binary node's type is the join (or the var).
- After the walk, unresolved vars default: fractional → `Decimal`, else `Int32`, with a warning `"no model field fixes the type of this expression; assuming int"` (`ExpressionTypeMismatch`, `IsWarning: true`).
- `==`/`!=` with `NullLiteral`: the other side must be nullable (a reference type or `Nullable<T>`); otherwise problem `"… is never null"` (warning).
- `&&`, `||`, `!`: operands must be `bool`/`bool?`.
- `/` with both integral: warning `"integer division truncates"`.
- Root must be `bool`/`bool?`: else `ExpressionTypeMismatch` `"a leaf must be a condition; this is {type}"`.
- `MemberAccess` on a collection: `UnknownMethod` `"'{name}' is a collection — use .where(…), .any(…), .sum(…)…"`. On a non-object (numeric/string/bool): `UnknownField`.
- `MethodCall`: receiver must be a collection for the seven collection methods, a `string` for `equalsIgnoreCase`; `where/any/all` need one lambda returning `bool`; `sum/min/max` need one lambda returning numeric; `count` takes no argument; `equalsIgnoreCase` takes one `string` argument. Result types: `where` → receiver type; `any/all/equalsIgnoreCase` → `bool`; `count` → `int`; `sum/min/max` → the lambda body's type.
- Facts: one per `NumberLiteral`/`ParameterRef` (`From` = the printed text of the anchor node that resolved its var, or null if defaulted) and one for the root (its type).

- [ ] **Step 1: Write the failing test**

```csharp
// src/Motiv.Serialization.Tests/Expressions/LeafCheckerTests.cs
using Motiv.Serialization.Expressions;

namespace Motiv.Serialization.Tests.Expressions;

public class LeafCheckerTests
{
    private sealed record Order(string Status, decimal Total, int DaysSinceShipped);
    private sealed record Customer(int Age, long Points, double Score, bool IsActive, decimal CreditLimit, string? Country, IReadOnlyList<Order>? Orders, DateTime? ShippedAt);

    private static readonly RuleParameterDeclaration[] Parameters =
    [
        new("minAge", RuleParameterType.Integer, true, 18),
        new("vip", RuleParameterType.Number, true, 1000d),
    ];

    private static LeafAnalysis Check(string text)
    {
        var problems = new List<LeafProblem>();
        var node = LeafParser.Parse(text, problems) ?? throw new Exception(string.Join("; ", problems.Select(p => p.Message)));
        return LeafChecker.Check(node, LeafScope.For(typeof(Customer), Parameters));
    }

    [Fact]
    public void Should_type_a_literal_from_the_field_across_the_comparison()
    {
        var analysis = Check("creditLimit > 1000");
        analysis.IsValid.ShouldBeTrue();
        var fact = analysis.Facts.Single(f => f.Node is NumberLiteral);
        fact.Type.ShouldBe(typeof(decimal));
        fact.From.ShouldBe("creditLimit");
    }

    [Fact]
    public void Should_type_a_whole_untyped_subtree_from_the_other_side()
    {
        var analysis = Check("@vip * 2 > orders.sum(o => o.total)");
        analysis.IsValid.ShouldBeTrue();
        analysis.Facts.Single(f => f.Node is ParameterRef).Type.ShouldBe(typeof(decimal));
        analysis.Facts.Single(f => f.Node is NumberLiteral).Type.ShouldBe(typeof(decimal));
        analysis.Facts.Single(f => f.Node is NumberLiteral).From.ShouldBe("orders.sum(o => o.total)");
    }

    [Fact]
    public void Should_widen_an_integer_field_when_a_fractional_literal_meets_it()
    {
        var analysis = Check("age * 1.5 > 40");
        analysis.IsValid.ShouldBeTrue();
        analysis.Facts.Single(f => f.Node is NumberLiteral { Text: "1.5" }).Type.ShouldBe(typeof(decimal));
        analysis.Facts.Single(f => f.Node is NumberLiteral { Text: "40" }).Type.ShouldBe(typeof(decimal));
    }

    [Fact]
    public void Should_refuse_decimal_against_double()
    {
        var analysis = Check("creditLimit > score");
        var problem = analysis.Problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe(RuleErrorCode.ExpressionTypeMismatch);
        problem.Message.ShouldContain("decimal");
        problem.Message.ShouldContain("double");
    }

    [Fact]
    public void Should_refuse_a_number_parameter_against_an_integer_anchor()
    {
        var analysis = Check("orders.count() >= @vip");
        analysis.Problems.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.ExpressionTypeMismatch);
    }

    [Fact]
    public void Should_default_an_unanchored_subtree_with_a_warning()
    {
        var analysis = Check("1 + 1 > 1");
        analysis.IsValid.ShouldBeTrue();
        analysis.Problems.ShouldAllBe(p => p.IsWarning);
        analysis.Facts.Where(f => f.Node is NumberLiteral).ShouldAllBe(f => f.Type == typeof(int) && f.From == null);
    }

    [Fact]
    public void Should_warn_on_integer_division()
    {
        var analysis = Check("age / 2 > 10");
        analysis.Problems.ShouldHaveSingleItem().Message.ShouldContain("truncates");
        analysis.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("nope > 1", RuleErrorCode.UnknownField, "nope")]
    [InlineData("orders.total > 1", RuleErrorCode.UnknownMethod, "collection")]
    [InlineData("orders.sum(o => o.nope) > 1", RuleErrorCode.UnknownField, "nope")]
    [InlineData("orders.first() != null", RuleErrorCode.UnknownMethod, "first")]
    [InlineData("age.count() > 1", RuleErrorCode.UnknownMethod, "collection")]
    [InlineData("orders.where(o => o.total) > 1", RuleErrorCode.ExpressionTypeMismatch, "condition")]
    [InlineData("orders.sum(o => o.status) > 1", RuleErrorCode.ExpressionTypeMismatch, "number")]
    [InlineData("country == 3", RuleErrorCode.ExpressionTypeMismatch, "string")]
    [InlineData("age && isActive", RuleErrorCode.ExpressionTypeMismatch, "condition")]
    [InlineData("age + 1", RuleErrorCode.ExpressionTypeMismatch, "leaf must be a condition")]
    [InlineData("@nope > 1", RuleErrorCode.UnknownField, "parameter")]
    public void Should_report_type_and_name_problems(string text, RuleErrorCode code, string fragment)
    {
        var analysis = Check(text);
        analysis.IsValid.ShouldBeFalse();
        var problem = analysis.Problems.First(p => !p.IsWarning);
        problem.Code.ShouldBe(code);
        problem.Message.ShouldContain(fragment);
    }

    [Fact]
    public void Should_range_an_unknown_lambda_field_at_the_name()
    {
        var problem = Check("orders.sum(o => o.nope) > 1").Problems.Single();
        problem.Start.ShouldBe(18);
        problem.End.ShouldBe(22);
    }

    [Theory]
    [InlineData("country != null")]
    [InlineData("shippedAt == null")]
    [InlineData("orders.any(o => o.status == \"paid\")")]
    [InlineData("country.equalsIgnoreCase(\"se\")")]
    [InlineData("!isActive || points > age")]
    public void Should_accept_null_checks_and_string_and_boolean_forms(string text)
    {
        Check(text).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Should_warn_when_a_non_nullable_is_compared_with_null()
    {
        var analysis = Check("age == null");
        analysis.Problems.ShouldHaveSingleItem().IsWarning.ShouldBeTrue();
    }

    [Fact]
    public void Should_lift_types_through_a_nullable_path()
    {
        var analysis = Check("orders.sum(o => o.total) > 1");
        var root = analysis.Facts.Single(f => f.Node is Binary);
        root.Type.ShouldBe(typeof(bool));
        analysis.Types[((Binary)root.Node).Left].ShouldBe(typeof(decimal?));
    }
}
```

- [ ] **Step 2: Run it to see it fail**

Run: `env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests --framework net10.0 --filter LeafCheckerTests 2>&1 | grep -E "error CS|Passed!|Failed!"`
Expected: `error CS0246` for `LeafChecker`.

- [ ] **Step 3: Implement**

```csharp
// src/Motiv.Serialization/Expressions/LeafFact.cs
namespace Motiv.Serialization.Expressions;

/// <summary>What the checker learned about a node: its solved CLR type and, for a literal or
/// parameter, the anchor that fixed it (null when it took the default).</summary>
internal sealed record LeafFact(LeafNode Node, Type Type, string? From);
```

```csharp
// src/Motiv.Serialization/Expressions/LeafType.cs
#if NET8_0_OR_GREATER
namespace Motiv.Serialization.Expressions;

/// <summary>A numeric type still being solved. Union-find: <see cref="Parent" /> chains to the
/// representative, which alone carries the resolution.</summary>
internal sealed class TypeVar
{
    public TypeVar? Parent;
    public bool Fractional;
    public RuleParameterType? ParamKind;
    public NumericKind? Resolved;
    public string? ResolvedBy;
    public readonly List<LeafNode> Members = [];

    public TypeVar Root => Parent is null ? this : Parent.Root;

    public bool Allows(NumericKind kind)
    {
        if (Fractional && NumericLattice.IsIntegral(kind)) return false;
        return ParamKind switch
        {
            RuleParameterType.Integer => kind is NumericKind.Int32 or NumericKind.Int64 or NumericKind.Decimal,
            RuleParameterType.Number => kind is NumericKind.Decimal or NumericKind.Double or NumericKind.Single,
            _ => true,
        };
    }
}

/// <summary>The type of a node during checking: a concrete CLR type, a type variable, or unknown (already reported).</summary>
internal readonly record struct LeafType(Type? Concrete, TypeVar? Var)
{
    public static readonly LeafType Unknown = new(null, null);
    public static LeafType Of(Type type) => new(type, null);
    public static LeafType OfVar(TypeVar var) => new(null, var);
    public bool IsUnknown => Concrete is null && Var is null;
    public bool IsNullable => Concrete is not null && (!Concrete.IsValueType || Nullable.GetUnderlyingType(Concrete) is not null);
    public Type? Underlying => Concrete is null ? null : Nullable.GetUnderlyingType(Concrete) ?? Concrete;
}
#endif
```

```csharp
// src/Motiv.Serialization/Expressions/LeafChecker.cs
#if NET8_0_OR_GREATER
namespace Motiv.Serialization.Expressions;

internal sealed class LeafAnalysis(IReadOnlyList<LeafProblem> problems, IReadOnlyDictionary<LeafNode, Type> types, IReadOnlyList<LeafFact> facts)
{
    public IReadOnlyList<LeafProblem> Problems { get; } = problems;
    public IReadOnlyDictionary<LeafNode, Type> Types { get; } = types;
    public IReadOnlyList<LeafFact> Facts { get; } = facts;
    public bool IsValid => Problems.All(p => p.IsWarning);
}

/// <summary>
/// Types a leaf against a scope. Fields and fixed-type methods anchor; literals and parameters
/// are type variables that take the join of their anchors over the lattice; the compiler then
/// reads <see cref="LeafAnalysis.Types" /> for every node.
/// </summary>
internal sealed class LeafChecker
{
    private static readonly HashSet<string> CollectionMethods = ["where", "any", "all", "count", "sum", "min", "max"];
    private readonly LeafScope _root;
    private readonly List<LeafProblem> _problems = [];
    private readonly Dictionary<LeafNode, LeafType> _types = new(ReferenceEqualityComparer.Instance);
    private readonly List<TypeVar> _vars = [];
    private readonly Dictionary<LeafNode, LeafScope> _lambdaScopes = new(ReferenceEqualityComparer.Instance);

    private LeafChecker(LeafScope root) { _root = root; }

    public static LeafAnalysis Check(LeafNode root, LeafScope scope)
    {
        var checker = new LeafChecker(scope);
        var type = checker.Visit(root, scope);
        checker.DefaultUnresolved();
        if (checker.Resolve(type) is { } concrete && (Nullable.GetUnderlyingType(concrete) ?? concrete) != typeof(bool))
            checker.Report(root, RuleErrorCode.ExpressionTypeMismatch, $"a leaf must be a condition; this is {Describe(concrete)}");
        return checker.Finish(root);
    }

    // ---- results ---------------------------------------------------------------------------

    private LeafAnalysis Finish(LeafNode root)
    {
        var types = new Dictionary<LeafNode, Type>(ReferenceEqualityComparer.Instance);
        foreach (var (node, type) in _types)
            if (Resolve(type) is { } concrete) types[node] = concrete;

        var facts = new List<LeafFact>();
        foreach (var (node, type) in _types)
        {
            if (node is not (NumberLiteral or ParameterRef) || type.Var is null) continue;
            var var = type.Var.Root;
            facts.Add(new LeafFact(node, types[node], var.ResolvedBy));
        }
        if (types.TryGetValue(root, out var rootType))
            facts.Add(new LeafFact(root, rootType, null));
        return new LeafAnalysis(_problems, types, facts);
    }

    private Type? Resolve(LeafType type)
    {
        if (type.Concrete is not null) return type.Concrete;
        if (type.Var?.Root.Resolved is { } kind) return NumericLattice.ClrType(kind);
        return null;
    }

    private void DefaultUnresolved()
    {
        foreach (var var in _vars.Select(v => v.Root).Distinct())
        {
            if (var.Resolved is not null) continue;
            var.Resolved = var.Fractional || var.ParamKind == RuleParameterType.Number ? NumericKind.Decimal : NumericKind.Int32;
            var first = var.Members[0];
            _problems.Add(new LeafProblem(RuleErrorCode.ExpressionTypeMismatch,
                $"no model field fixes the type of this expression; assuming {Describe(NumericLattice.ClrType(var.Resolved.Value))}",
                first.Start, first.End, IsWarning: true));
        }
    }

    private void Report(LeafNode at, RuleErrorCode code, string message, bool warning = false) =>
        _problems.Add(new LeafProblem(code, message, at.Start, at.End, warning));

    private void Report(int start, int end, RuleErrorCode code, string message) =>
        _problems.Add(new LeafProblem(code, message, start, end));

    private static string Describe(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying == typeof(int)) return "int";
        if (underlying == typeof(long)) return "long";
        if (underlying == typeof(decimal)) return "decimal";
        if (underlying == typeof(double)) return "double";
        if (underlying == typeof(float)) return "float";
        if (underlying == typeof(bool)) return "a condition";
        if (underlying == typeof(string)) return "string";
        if (LeafScope.ElementType(underlying) is { } element) return $"a collection of {Describe(element)}";
        return underlying.Name;
    }

    private LeafType Set(LeafNode node, LeafType type) { _types[node] = type; return type; }

    private LeafType NewVar(LeafNode node, bool fractional, RuleParameterType? paramKind)
    {
        var var = new TypeVar { Fractional = fractional, ParamKind = paramKind };
        var.Members.Add(node);
        _vars.Add(var);
        return Set(node, LeafType.OfVar(var));
    }

    // ---- the walk --------------------------------------------------------------------------

    private LeafType Visit(LeafNode node, LeafScope scope) => node switch
    {
        NumberLiteral n => NewVar(n, n.Text.Contains('.'), null),
        StringLiteral s => Set(s, LeafType.Of(typeof(string))),
        BoolLiteral b => Set(b, LeafType.Of(typeof(bool))),
        NullLiteral n => Set(n, LeafType.Of(typeof(object))),
        ParameterRef p => VisitParameter(p, scope),
        Identifier i => VisitIdentifier(i, scope),
        MemberAccess m => VisitMember(m, scope),
        MethodCall c => VisitCall(c, scope),
        Lambda l => Fail(l, RuleErrorCode.InvalidExpression, "a lambda is only allowed as a method argument"),
        Unary u => VisitUnary(u, scope),
        Binary b => VisitBinary(b, scope),
        _ => LeafType.Unknown,
    };

    private LeafType Fail(LeafNode node, RuleErrorCode code, string message)
    {
        Report(node, code, message);
        return Set(node, LeafType.Unknown);
    }

    private LeafType VisitParameter(ParameterRef p, LeafScope scope)
    {
        if (!scope.Parameters.TryGetValue(p.Name, out var declaration))
            return Fail(p, RuleErrorCode.UnknownField, $"unknown parameter '@{p.Name}'");
        return declaration.Type switch
        {
            RuleParameterType.Integer or RuleParameterType.Number => NewVar(p, false, declaration.Type),
            RuleParameterType.String => Set(p, LeafType.Of(typeof(string))),
            _ => Set(p, LeafType.Of(typeof(bool))),
        };
    }

    private LeafType VisitIdentifier(Identifier i, LeafScope scope)
    {
        if (scope.Variables.TryGetValue(i.Name, out var variable))
            return Set(i, LeafType.Of(variable));
        var member = LeafScope.FindMember(scope.ModelType, i.Name);
        if (member is null)
            return Fail(i, RuleErrorCode.UnknownField, $"'{i.Name}' is not a field of {scope.ModelType.Name}");
        return Set(i, LeafType.Of(LeafScope.MemberType(member)));
    }

    private LeafType VisitMember(MemberAccess m, LeafScope scope)
    {
        var target = Visit(m.Target, scope);
        if (target.IsUnknown) return Set(m, LeafType.Unknown);
        var targetType = target.Underlying ?? NumericLattice.ClrType(target.Var!.Root.Resolved ?? NumericKind.Int32);
        if (LeafScope.ElementType(targetType) is not null)
        {
            Report(m.NameStart, m.NameEnd, RuleErrorCode.UnknownMethod, $"'{Print(m.Target)}' is a collection — use .where(…), .any(…), .all(…), .count(), .sum(…), .min(…) or .max(…) on it");
            return Set(m, LeafType.Unknown);
        }
        if (targetType.IsPrimitive || targetType == typeof(string) || targetType == typeof(decimal))
        {
            Report(m.NameStart, m.NameEnd, RuleErrorCode.UnknownField, $"'{m.Name}' is not a field of {Describe(targetType)}");
            return Set(m, LeafType.Unknown);
        }
        var member = LeafScope.FindMember(targetType, m.Name);
        if (member is null)
        {
            Report(m.NameStart, m.NameEnd, RuleErrorCode.UnknownField, $"'{m.Name}' is not a field of {targetType.Name}");
            return Set(m, LeafType.Unknown);
        }
        return Set(m, LeafType.Of(Lift(LeafScope.MemberType(member), target.IsNullable)));
    }

    /// <summary>A value reached through a nullable path is itself nullable.</summary>
    private static Type Lift(Type type, bool throughNullable)
    {
        if (!throughNullable || !type.IsValueType || Nullable.GetUnderlyingType(type) is not null) return type;
        return typeof(Nullable<>).MakeGenericType(type);
    }

    private LeafType VisitCall(MethodCall c, LeafScope scope)
    {
        var target = Visit(c.Target, scope);
        if (target.IsUnknown) return Set(c, LeafType.Unknown);
        var targetType = target.Underlying;

        if (c.Method == "equalsIgnoreCase")
        {
            if (targetType != typeof(string))
                return FailAt(c, RuleErrorCode.UnknownMethod, $"'equalsIgnoreCase' needs a string; this is {Describe(targetType ?? typeof(object))}");
            if (c.Arguments.Count != 1 || c.Arguments[0] is Lambda)
                return FailAt(c, RuleErrorCode.UnknownMethod, "'equalsIgnoreCase' takes one string argument");
            var argument = Visit(c.Arguments[0], scope);
            if (argument.Underlying is { } at && at != typeof(string))
                Report(c.Arguments[0], RuleErrorCode.ExpressionTypeMismatch, $"'equalsIgnoreCase' expects a string; this is {Describe(at)}");
            return Set(c, LeafType.Of(typeof(bool)));
        }

        var element = targetType is null ? null : LeafScope.ElementType(targetType);
        if (element is null)
            return FailAt(c, RuleErrorCode.UnknownMethod, $"'.{c.Method}()' needs a collection; this is {Describe(targetType ?? typeof(object))}");
        if (!CollectionMethods.Contains(c.Method))
            return FailAt(c, RuleErrorCode.UnknownMethod, $"unknown method '{c.Method}'; the collection methods are where, any, all, count, sum, min, max");

        var nullable = target.IsNullable;
        if (c.Method == "count")
        {
            if (c.Arguments.Count > 0)
                Report(c.Arguments[0], RuleErrorCode.UnknownMethod, "'count()' takes no arguments — filter with .where(…) first");
            return Set(c, LeafType.Of(Lift(typeof(int), nullable)));
        }

        if (c.Arguments.Count != 1 || c.Arguments[0] is not Lambda lambda)
            return FailAt(c, RuleErrorCode.UnknownMethod, $"'{c.Method}' takes a lambda: {c.Method}(x => …)");

        var inner = scope.WithVariable(lambda.Parameter, element);
        _lambdaScopes[lambda] = inner;
        Set(lambda, LeafType.Of(element));
        var body = Visit(lambda.Body, inner);
        var bodyType = Resolve(body);

        var wantsCondition = c.Method is "where" or "any" or "all";
        if (wantsCondition)
        {
            if (bodyType is not null && (Nullable.GetUnderlyingType(bodyType) ?? bodyType) != typeof(bool))
                Report(lambda.Body, RuleErrorCode.ExpressionTypeMismatch, $"'{c.Method}' expects a condition; this is {Describe(bodyType)}");
            return Set(c, LeafType.Of(c.Method == "where" ? targetType! : Lift(typeof(bool), nullable)));
        }

        // sum / min / max: numeric body; a literal-only body stays a variable so `sum(o => 1)` still types.
        if (bodyType is not null && NumericLattice.KindOf(bodyType) is null)
        {
            Report(lambda.Body, RuleErrorCode.ExpressionTypeMismatch, $"'{c.Method}' expects a number; this is {Describe(bodyType)}");
            return Set(c, LeafType.Unknown);
        }
        if (body.Var is not null)
            return Set(c, body);
        return Set(c, LeafType.Of(Lift(bodyType!, nullable)));
    }

    private LeafType FailAt(MethodCall c, RuleErrorCode code, string message)
    {
        Report(c.MethodStart, c.MethodEnd, code, message);
        return Set(c, LeafType.Unknown);
    }

    private LeafType VisitUnary(Unary u, LeafScope scope)
    {
        var operand = Visit(u.Operand, scope);
        if (u.Operator == "!")
        {
            if (operand.Underlying is { } t && t != typeof(bool))
                Report(u, RuleErrorCode.ExpressionTypeMismatch, $"'!' needs a condition; this is {Describe(t)}");
            return Set(u, LeafType.Of(typeof(bool)));
        }
        if (operand.Var is null && operand.Underlying is { } n && NumericLattice.KindOf(n) is null)
            Report(u, RuleErrorCode.ExpressionTypeMismatch, $"'-' needs a number; this is {Describe(n)}");
        return Set(u, operand);
    }

    private LeafType VisitBinary(Binary b, LeafScope scope)
    {
        var left = Visit(b.Left, scope);
        var right = Visit(b.Right, scope);

        switch (b.Operator)
        {
            case "&&" or "||":
                foreach (var (side, type) in new[] { (b.Left, left), (b.Right, right) })
                    if (type.Underlying is { } t && t != typeof(bool))
                        Report(side, RuleErrorCode.ExpressionTypeMismatch, $"'{b.Operator}' needs conditions on both sides; this is {Describe(t)}");
                    else if (type.Var is not null)
                        Report(side, RuleErrorCode.ExpressionTypeMismatch, $"'{b.Operator}' needs conditions on both sides; this is a number");
                return Set(b, LeafType.Of(typeof(bool)));

            case "==" or "!=":
                return Set(b, VisitEquality(b, left, right));

            case "<" or "<=" or ">" or ">=":
                Unify(b, left, right, arithmetic: false);
                return Set(b, LeafType.Of(typeof(bool)));

            default:
            {
                var result = Unify(b, left, right, arithmetic: true);
                if (b.Operator == "/" && result.Concrete is { } rt && NumericLattice.KindOf(rt) is { } rk && NumericLattice.IsIntegral(rk))
                    Report(b, RuleErrorCode.ExpressionTypeMismatch, "integer division truncates; compare against a fractional value to keep the remainder", warning: true);
                else if (b.Operator == "/" && result.Var?.Root is { Resolved: null, Fractional: false })
                    Report(b, RuleErrorCode.ExpressionTypeMismatch, "integer division truncates; compare against a fractional value to keep the remainder", warning: true);
                return Set(b, result);
            }
        }
    }

    private LeafType VisitEquality(Binary b, LeafType left, LeafType right)
    {
        if (b.Left is NullLiteral || b.Right is NullLiteral)
        {
            var other = b.Left is NullLiteral ? right : left;
            var otherNode = b.Left is NullLiteral ? b.Right : b.Left;
            if (other.Var is not null || (other.Concrete is not null && !other.IsNullable))
                Report(otherNode, RuleErrorCode.ExpressionTypeMismatch, $"'{Print(otherNode)}' is never null", warning: true);
            return LeafType.Of(typeof(bool));
        }

        var l = left.Underlying; var r = right.Underlying;
        if (l is not null && r is not null && NumericLattice.KindOf(l) is null && NumericLattice.KindOf(r) is null)
        {
            if (l != r)
                Report(b, RuleErrorCode.ExpressionTypeMismatch, $"comparing {Describe(l)} with {Describe(r)}");
            return LeafType.Of(typeof(bool));
        }
        if ((l is not null && NumericLattice.KindOf(l) is null) || (r is not null && NumericLattice.KindOf(r) is null))
        {
            var (nonNumeric, node) = l is not null && NumericLattice.KindOf(l) is null ? (l, b.Left) : (r!, b.Right);
            Report(b, RuleErrorCode.ExpressionTypeMismatch, $"comparing {Describe(nonNumeric)} with a number");
            return LeafType.Of(typeof(bool));
        }
        Unify(b, left, right, arithmetic: false);
        return LeafType.Of(typeof(bool));
    }

    /// <summary>
    /// Gives two numeric operands one type. Returns the operands' common type (for arithmetic,
    /// the result type). Non-numeric concrete operands are reported here for arithmetic and
    /// ordering; equality handles its own.
    /// </summary>
    private LeafType Unify(Binary b, LeafType left, LeafType right, bool arithmetic)
    {
        if (left.IsUnknown || right.IsUnknown) return LeafType.Unknown;
        var what = arithmetic ? $"'{b.Operator}' needs numbers" : $"'{b.Operator}' compares numbers";

        foreach (var (side, type) in new[] { (b.Left, left), (b.Right, right) })
            if (type.Underlying is { } t && NumericLattice.KindOf(t) is null)
            {
                Report(side, RuleErrorCode.ExpressionTypeMismatch, $"{what}; this is {Describe(t)}");
                return LeafType.Unknown;
            }

        var nullable = left.IsNullable || right.IsNullable;

        if (left.Concrete is { } lc && right.Concrete is { } rc)
        {
            var lk = NumericLattice.KindOf(lc)!.Value; var rk = NumericLattice.KindOf(rc)!.Value;
            var join = NumericLattice.Join(lk, rk);
            if (join is null)
            {
                Report(b, RuleErrorCode.ExpressionTypeMismatch, $"cannot compare {Describe(lc)} with {Describe(rc)} without losing precision; use a registered spec for this comparison");
                return LeafType.Unknown;
            }
            return LeafType.Of(Lift(NumericLattice.ClrType(join.Value), nullable));
        }

        if (left.Var is not null && right.Var is not null)
        {
            var a = left.Var.Root; var c = right.Var.Root;
            if (a != c)
            {
                if (a.Resolved is { } ar && c.Resolved is { } cr)
                {
                    var join = NumericLattice.Join(ar, cr);
                    if (join is null) { Report(b, RuleErrorCode.ExpressionTypeMismatch, $"cannot combine {Describe(NumericLattice.ClrType(ar))} with {Describe(NumericLattice.ClrType(cr))}"); return LeafType.Unknown; }
                    a.Resolved = join;
                }
                else
                {
                    a.Resolved ??= c.Resolved;
                    a.ResolvedBy ??= c.ResolvedBy;
                }
                a.Fractional |= c.Fractional;
                a.ParamKind ??= c.ParamKind;
                a.Members.AddRange(c.Members);
                c.Parent = a;
            }
            return LeafType.OfVar(a);
        }

        var (var, concrete, anchor) = left.Var is not null ? (left.Var.Root, right.Concrete!, b.Right) : (right.Var.Root, left.Concrete!, b.Left);
        var kind = NumericLattice.KindOf(concrete)!.Value;
        var target = kind;
        if (var.Fractional && NumericLattice.IsIntegral(kind))
            target = NumericKind.Decimal;
        if (!var.Allows(target) || (var.Resolved is { } already && NumericLattice.Join(already, target) is null))
        {
            var have = var.Resolved is { } h ? Describe(NumericLattice.ClrType(h)) : var.ParamKind == RuleParameterType.Number ? "a number parameter" : "this literal";
            Report(b, RuleErrorCode.ExpressionTypeMismatch, $"cannot use {have} with {Describe(concrete)} without losing precision");
            return LeafType.Unknown;
        }
        var.Resolved = var.Resolved is { } prior ? NumericLattice.Join(prior, target) : target;
        var.ResolvedBy ??= Print(anchor);
        return LeafType.Of(Lift(NumericLattice.ClrType(var.Resolved!.Value), nullable));
    }

    /// <summary>The canonical text of a node — the anchor named in a fact.</summary>
    public static string Print(LeafNode node) => node switch
    {
        NumberLiteral n => n.Text,
        StringLiteral s => $"\"{s.Value}\"",
        BoolLiteral b => b.Value ? "true" : "false",
        NullLiteral => "null",
        ParameterRef p => $"@{p.Name}",
        Identifier i => i.Name,
        MemberAccess m => $"{Print(m.Target)}.{m.Name}",
        MethodCall c => $"{Print(c.Target)}.{c.Method}({string.Join(", ", c.Arguments.Select(Print))})",
        Lambda l => $"{l.Parameter} => {Print(l.Body)}",
        Unary u => $"{u.Operator}{Print(u.Operand)}",
        Binary b => $"{Print(b.Left)} {b.Operator} {Print(b.Right)}",
        _ => string.Empty,
    };

    public IReadOnlyDictionary<LeafNode, LeafScope> LambdaScopes => _lambdaScopes;
}
#endif
```

`LeafAnalysis` needs the lambda scopes for the compiler too: add a fourth constructor argument `IReadOnlyDictionary<LeafNode, LeafScope> lambdaScopes` exposed as `LambdaScopes`, and pass `checker._lambdaScopes` from `Finish`. (The test does not read it; the compiler in Task 7 does.)

- [ ] **Step 4: Run the tests and iterate on message text**

Run: `env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests --framework net10.0 --filter LeafCheckerTests 2>&1 | grep -E "error CS|Passed!|Failed!|Assert"`
Expected: `Passed!`. The `Should_report_type_and_name_problems` theory pins message *fragments*; if a case fails on a fragment the checker's message must contain that word — change the message, not the test. The `age == null` warning case and the widening case are the two most likely to need a second look at `Unify`'s var-vs-concrete branch.

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Serialization/Expressions src/Motiv.Serialization.Tests/Expressions
git commit -m "Expressions — leaf checker: union-find type inference, problems with ranges, facts"
```

### Task 6: Null-conditional expression node in Motiv core, printed as `?.`

**Files:**
- Create: `src/Motiv/ExpressionTreeProposition/NullConditionalExpression.cs`
- Modify: `src/Motiv/ExpressionTreeProposition/CSharpExpressionSerializer.cs` (add `VisitExtension`)
- Test: `src/Motiv.Tests/NullConditionalExpressionTests.cs`

**Interfaces:**
- Produces: `public sealed class NullConditionalExpression : Expression` with `static NullConditionalExpression Create(Expression target, Func<Expression, Expression> access)` — `target` is the possibly-null receiver, `access` builds the member or call on a non-null receiver; `Type` is the access's type lifted to `Nullable<T>` for value types; `NodeType == ExpressionType.Extension`; `CanReduce == true`; `Reduce()` yields `target == null ? default(Type) : (Type)access(target)`. Printed by the serializer as `<target>?.<rest>` where `<rest>` is the access printed without its receiver.

- [ ] **Step 1: Write the failing test**

```csharp
// src/Motiv.Tests/NullConditionalExpressionTests.cs
using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;

namespace Motiv.Tests;

public class NullConditionalExpressionTests
{
    private sealed record Order(decimal Total);
    private sealed record Customer(IReadOnlyList<Order>? Orders, string? Country);

    [Fact]
    public void Should_evaluate_to_null_when_the_receiver_is_null_and_to_the_value_otherwise()
    {
        var c = Expression.Parameter(typeof(Customer), "c");
        var orders = Expression.Property(c, nameof(Customer.Orders));
        var count = NullConditionalExpression.Create(orders, target =>
            Expression.Call(typeof(Enumerable), nameof(Enumerable.Count), [typeof(Order)], target));
        count.Type.ShouldBe(typeof(int?));

        var lambda = Expression.Lambda<Func<Customer, int?>>(count, c).Compile();
        lambda(new Customer(null, null)).ShouldBeNull();
        lambda(new Customer([new Order(1), new Order(2)], null)).ShouldBe(2);
    }

    [Fact]
    public void Should_print_as_a_null_conditional_access()
    {
        var c = Expression.Parameter(typeof(Customer), "c");
        var orders = Expression.Property(c, nameof(Customer.Orders));
        var count = NullConditionalExpression.Create(orders, target =>
            Expression.Call(typeof(Enumerable), nameof(Enumerable.Count), [typeof(Order)], target));
        var body = Expression.GreaterThan(count, Expression.Constant(2, typeof(int?)));

        var spec = Spec.From(Expression.Lambda<Func<Customer, bool>>(body, c)).Create("has orders");
        var result = spec.Evaluate(new Customer([new Order(1), new Order(2), new Order(3)], null));

        result.Assertions.ShouldBe(["c.Orders?.Count() > 2 == true"]);
    }

    [Fact]
    public void Should_make_a_comparison_false_when_the_path_is_null()
    {
        var c = Expression.Parameter(typeof(Customer), "c");
        var length = NullConditionalExpression.Create(Expression.Property(c, nameof(Customer.Country)), target =>
            Expression.Property(target, nameof(string.Length)));
        var body = Expression.GreaterThan(length, Expression.Constant(1, typeof(int?)));
        var spec = Spec.From(Expression.Lambda<Func<Customer, bool>>(body, c)).Create("long country");

        spec.Evaluate(new Customer(null, null)).Satisfied.ShouldBeFalse();
        spec.Evaluate(new Customer(null, "SE")).Satisfied.ShouldBeTrue();
    }
}
```

- [ ] **Step 2: Run it to see it fail**

Run: `env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Tests --framework net10.0 --filter NullConditionalExpressionTests 2>&1 | grep -E "error CS|Passed!|Failed!"`
Expected: `error CS0246` for `NullConditionalExpression`.

- [ ] **Step 3: Implement the node and its printing**

```csharp
// src/Motiv/ExpressionTreeProposition/NullConditionalExpression.cs
using System.Linq.Expressions;

namespace Motiv.ExpressionTreeProposition;

/// <summary>
/// <c>target?.access</c> as an expression-tree node: reduces to
/// <c>target == null ? default : access(target)</c>, and is printed by the C# serializer as the
/// null-conditional it stands for rather than as the conditional it reduces to.
/// </summary>
public sealed class NullConditionalExpression : Expression
{
    private NullConditionalExpression(Expression target, Expression access, Type type)
    {
        Target = target;
        Access = access;
        Type = type;
    }

    /// <summary>The receiver that may be null.</summary>
    public Expression Target { get; }

    /// <summary>The member access or call, built over <see cref="Target" /> as if it were non-null.</summary>
    public Expression Access { get; }

    /// <inheritdoc />
    public override Type Type { get; }

    /// <inheritdoc />
    public override ExpressionType NodeType => ExpressionType.Extension;

    /// <inheritdoc />
    public override bool CanReduce => true;

    /// <summary>Builds <c>target?.access</c>; the result type is lifted to nullable for value types.</summary>
    public static NullConditionalExpression Create(Expression target, Func<Expression, Expression> access)
    {
        var accessed = access(target);
        var type = accessed.Type.IsValueType && Nullable.GetUnderlyingType(accessed.Type) is null
            ? typeof(Nullable<>).MakeGenericType(accessed.Type)
            : accessed.Type;
        return new NullConditionalExpression(target, accessed, type);
    }

    /// <inheritdoc />
    public override Expression Reduce() =>
        Condition(
            Equal(Target, Constant(null, Target.Type)),
            Default(Type),
            Access.Type == Type ? Access : Convert(Access, Type));

    /// <inheritdoc />
    protected override Expression VisitChildren(ExpressionVisitor visitor)
    {
        var target = visitor.Visit(Target);
        var access = visitor.Visit(Access);
        return ReferenceEquals(target, Target) && ReferenceEquals(access, Access)
            ? this
            : new NullConditionalExpression(target, access, Type);
    }
}
```

In `CSharpExpressionSerializer.cs`, add after `VisitConditional`:

```csharp
    protected override Expression VisitExtension(Expression node)
    {
        if (node is not NullConditionalExpression nullConditional)
            return base.VisitExtension(node);

        // Print the receiver, then the access with its receiver elided: `c.Orders?.Count()`.
        VisitAndMaybeApplyParentheses(nullConditional, nullConditional.Target);
        OutputText.Append("?.");
        var receiverText = Serialize(nullConditional.Target);
        var accessText = Serialize(nullConditional.Access);
        OutputText.Append(accessText.StartsWith(receiverText + ".", StringComparison.Ordinal)
            ? accessText.Substring(receiverText.Length + 1)
            : accessText);
        return node;
    }
```

`Serialize` on this visitor appends to `OutputText`; if it does not return a fresh string for a sub-expression, use a new `CSharpExpressionSerializer` instance for the two sub-serializations (read `Serialize(Expression)` at line 16 to see which). `Substring` because Motiv targets `netstandard2.0`.

The `ExpressionTreeTransformer` and `ExpressionAnalyzer` visitors inherit `ExpressionVisitor`, whose default `VisitExtension` calls `VisitChildren`, which is overridden above, so they traverse into the node without reducing it.

- [ ] **Step 4: Run the tests, then the whole core suite**

Run: `env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Tests --framework net10.0 2>&1 | grep -E "error CS|Passed!|Failed!"`
Expected: `Passed!` for all. If `Should_print_as_a_null_conditional_access` prints `c.Orders?.Count() > 2 == true` differently (a space, or `Enumerable.Count(c.Orders)`), inspect how `VisitMethodCall` prints extension-method calls (`isExtensionMethod` path around line 397) and match that form in the test only if it is the form the serializer already uses for `Any`/`All`.

- [ ] **Step 5: Commit**

```bash
git add src/Motiv/ExpressionTreeProposition/NullConditionalExpression.cs src/Motiv/ExpressionTreeProposition/CSharpExpressionSerializer.cs src/Motiv.Tests/NullConditionalExpressionTests.cs
git commit -m "Motiv — NullConditionalExpression node, printed as ?. by the C# serializer"
```

### Task 7: Leaf compiler — AST to `Spec.From`

**Files:**
- Create: `src/Motiv.Serialization/Expressions/LeafCompiler.cs`
- Test: `src/Motiv.Serialization.Tests/Expressions/LeafCompilerTests.cs`

**Interfaces:**
- Consumes: `LeafAnalysis` (`Types`, `LambdaScopes`), `NumericLattice.Constant/Widen`, `LeafScope.FindMember/ElementType`, `NullConditionalExpression`.
- Produces: `static SpecBase<TModel, string> LeafCompiler.Compile<TModel>(LeafNode root, LeafAnalysis analysis, IReadOnlyDictionary<string, object?> parameterValues, string leafText)` — builds `Expression<Func<TModel,bool>>` and returns `Spec.From(lambda).Create(leafText)`. Naming with the leaf text keeps `Reason` as `"<leaf> == true"` while `Assertions` are the decomposed clauses, per the CLAUDE.md ExpressionTree rule.

**Compilation rules** (every node's target type comes from `analysis.Types[node]`):
- `NumberLiteral` → `NumericLattice.Constant(text, kind)`; `ParameterRef` (numeric) → `Expression.Constant(converted value)` where the value is converted from the substituted CLR value (`int` or `double`) with `Convert.ChangeType(value, clr, InvariantCulture)`; string/bool params → constants. Where the node's solved type is nullable-lifted, wrap the constant with `Expression.Convert` to the nullable type.
- `Identifier` → lambda variable parameter, or `Expression.MakeMemberAccess(model, member)`.
- `MemberAccess` → if the target's type is nullable (reference or `Nullable<T>`): `NullConditionalExpression.Create(target, t => MakeMemberAccess(Unwrap(t), member))` where `Unwrap` is `Expression.Property(t, "Value")` for `Nullable<T>`; else plain member access.
- `MethodCall` on collections → `Expression.Call(typeof(Enumerable), name, [element] or [element, result], receiver, lambda)` with C# names `Where/Any/All/Count/Sum/Min/Max`; the `sum/min/max` selector lambda's body is widened to the call's solved kind so the typed overload (`Sum<T>(IEnumerable<T>, Func<T, decimal>)`) resolves. Nullable receiver → wrapped in `NullConditionalExpression`. `equalsIgnoreCase` → `Expression.Call(typeof(string).GetMethod("Equals", [string, string, StringComparison]), receiver, argument, Constant(OrdinalIgnoreCase))`.
- `Lambda` → `Expression.Lambda(body, Expression.Parameter(elementType, name))`.
- `Binary` numeric: widen both sides to the node's solved kind with `NumericLattice.Widen` (lifting to nullable if either side is nullable), then `Expression.AddChecked/SubtractChecked/MultiplyChecked/Divide` or `GreaterThan/…/Equal/NotEqual`. Lifted comparisons on `Nullable<T>` give C#'s semantics: `null > x` is false. `==`/`!=` with `NullLiteral` → `Expression.Equal(side, Constant(null, side.Type))`. Strings → `Expression.Equal` (op_Equality, ordinal). `&&`/`||` → `Expression.AndAlso/OrElse` over `Coalesce(bool?, false)` when a side is `bool?`.
- `Unary` `!` → `Expression.Not(Coalesce…)`; `-` → `Expression.NegateChecked`.
- Root: `Coalesce` to `bool` if `bool?`.

- [ ] **Step 1: Write the failing test**

```csharp
// src/Motiv.Serialization.Tests/Expressions/LeafCompilerTests.cs
using Motiv.Serialization.Expressions;

namespace Motiv.Serialization.Tests.Expressions;

public class LeafCompilerTests
{
    private sealed record Order(string Status, decimal Total, int DaysSinceShipped);
    private sealed record Customer(int Age, bool IsActive, decimal CreditLimit, string? Country, IReadOnlyList<Order>? Orders);

    private static readonly RuleParameterDeclaration[] Declarations =
    [
        new("minAge", RuleParameterType.Integer, true, 18),
        new("vip", RuleParameterType.Number, true, 1000d),
    ];

    private static readonly Dictionary<string, object?> Values = new() { ["minAge"] = 18, ["vip"] = 1000d };

    private static SpecBase<Customer, string> Compile(string text)
    {
        var problems = new List<LeafProblem>();
        var node = LeafParser.Parse(text, problems)!;
        var analysis = LeafChecker.Check(node, LeafScope.For(typeof(Customer), Declarations));
        analysis.IsValid.ShouldBeTrue(string.Join("; ", analysis.Problems.Select(p => p.Message)));
        return LeafCompiler.Compile<Customer>(node, analysis, Values, text);
    }

    private static readonly Customer Sample = new(34, true, 800m, "SE",
        [new("paid", 620m, 12), new("paid", 410m, 40), new("pending", 950m, 0), new("refunded", 75m, 90)]);

    [Fact]
    public void Should_compile_a_filtered_aggregate_and_explain_it_like_a_compiled_expression()
    {
        var spec = Compile("orders.where(o => o.status == \"paid\").sum(o => o.total) > @vip");
        var result = spec.Evaluate(Sample);

        result.Satisfied.ShouldBeTrue();
        result.Reason.ShouldBe("orders.where(o => o.status == \"paid\").sum(o => o.total) > @vip == true");
        result.Assertions.ShouldHaveSingleItem().ShouldContain("Sum");
    }

    [Fact]
    public void Should_type_the_parameter_from_the_anchor_so_decimal_meets_decimal()
    {
        var spec = Compile("creditLimit > @vip");
        spec.Evaluate(Sample).Satisfied.ShouldBeFalse();
        spec.Evaluate(Sample with { CreditLimit = 1000.5m }).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_keep_a_fractional_literal_exact_against_a_decimal_field()
    {
        var spec = Compile("creditLimit * 0.1 == 80");
        spec.Evaluate(Sample).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_widen_an_int_field_to_decimal_when_a_fractional_literal_meets_it()
    {
        Compile("age * 1.5 > 50").Evaluate(Sample).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_never_throw_on_a_null_path()
    {
        var noOrders = Sample with { Orders = null, Country = null };
        Compile("orders.sum(o => o.total) > 1").Evaluate(noOrders).Satisfied.ShouldBeFalse();
        Compile("orders.count() >= 0").Evaluate(noOrders).Satisfied.ShouldBeFalse();
        Compile("orders == null").Evaluate(noOrders).Satisfied.ShouldBeTrue();
        Compile("country != null").Evaluate(noOrders).Satisfied.ShouldBeFalse();
        Compile("country.equalsIgnoreCase(\"se\")").Evaluate(noOrders).Satisfied.ShouldBeFalse();
        Compile("orders.all(o => o.total <= creditLimit)").Evaluate(noOrders).Satisfied.ShouldBeFalse();
    }

    [Fact]
    public void Should_compare_strings_ordinally_and_ignore_case_only_when_asked()
    {
        Compile("country == \"se\"").Evaluate(Sample).Satisfied.ShouldBeFalse();
        Compile("country.equalsIgnoreCase(\"se\")").Evaluate(Sample).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_reach_the_parent_from_inside_a_lambda()
    {
        Compile("orders.all(o => o.total <= creditLimit)").Evaluate(Sample).Satisfied.ShouldBeFalse();
        Compile("orders.any(o => o.total > creditLimit)").Evaluate(Sample).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_use_checked_arithmetic()
    {
        var spec = Compile("age * 2000000000 > 1");
        Should.Throw<OverflowException>(() => spec.Evaluate(Sample));
    }

    [Fact]
    public void Should_short_circuit_boolean_connectives_and_negate()
    {
        Compile("!isActive || age >= @minAge").Evaluate(Sample).Satisfied.ShouldBeTrue();
        Compile("isActive && age < @minAge").Evaluate(Sample).Satisfied.ShouldBeFalse();
    }
}
```

- [ ] **Step 2: Run it to see it fail**

Run: `env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests --framework net10.0 --filter LeafCompilerTests 2>&1 | grep -E "error CS|Passed!|Failed!"`
Expected: `error CS0246` for `LeafCompiler`.

- [ ] **Step 3: Implement**

```csharp
// src/Motiv.Serialization/Expressions/LeafCompiler.cs
#if NET8_0_OR_GREATER
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Motiv.ExpressionTreeProposition;

namespace Motiv.Serialization.Expressions;

/// <summary>
/// Turns a checked leaf into the expression tree a developer would have written by hand in
/// <c>Spec.From</c>: native operator nodes over operands the lattice has already made the same
/// type, typed constants, checked arithmetic, and null-conditional navigation wherever the model
/// says a path may be null.
/// </summary>
internal sealed class LeafCompiler
{
    private readonly LeafAnalysis _analysis;
    private readonly IReadOnlyDictionary<string, object?> _values;
    private readonly ParameterExpression _model;
    private readonly Dictionary<string, ParameterExpression> _variables = new(StringComparer.Ordinal);

    private LeafCompiler(LeafAnalysis analysis, IReadOnlyDictionary<string, object?> values, ParameterExpression model)
    {
        _analysis = analysis;
        _values = values;
        _model = model;
    }

    public static SpecBase<TModel, string> Compile<TModel>(
        LeafNode root, LeafAnalysis analysis, IReadOnlyDictionary<string, object?> parameterValues, string leafText)
    {
        var model = Expression.Parameter(typeof(TModel), "model");
        var compiler = new LeafCompiler(analysis, parameterValues, model);
        var body = compiler.AsBool(compiler.Visit(root));
        var lambda = Expression.Lambda<Func<TModel, bool>>(body, model);
        return Spec.From(lambda).Create(leafText);
    }

    private Type TypeOf(LeafNode node) => _analysis.Types[node];

    private static bool IsNullable(Type type) => !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;

    private static Expression Unwrap(Expression value) =>
        Nullable.GetUnderlyingType(value.Type) is null ? value : Expression.Property(value, "Value");

    private Expression AsBool(Expression value) =>
        value.Type == typeof(bool) ? value : Expression.Coalesce(value, Expression.Constant(false));

    private Expression Visit(LeafNode node) => node switch
    {
        NumberLiteral n => Typed(NumericLattice.Constant(n.Text, NumericLattice.KindOf(TypeOf(n))!.Value), TypeOf(n)),
        StringLiteral s => Expression.Constant(s.Value, typeof(string)),
        BoolLiteral b => Expression.Constant(b.Value),
        NullLiteral => Expression.Constant(null, typeof(object)),
        ParameterRef p => Parameter(p),
        Identifier i => Identifier(i),
        MemberAccess m => Member(m),
        MethodCall c => Call(c),
        Unary u => u.Operator == "!" ? Expression.Not(AsBool(Visit(u.Operand))) : Expression.NegateChecked(Visit(u.Operand)),
        Binary b => Binary(b),
        _ => throw new InvalidOperationException($"unexpected leaf node {node.GetType().Name}"),
    };

    private static Expression Typed(Expression constant, Type target) =>
        constant.Type == target ? constant : Expression.Convert(constant, target);

    private Expression Parameter(ParameterRef p)
    {
        var target = TypeOf(p);
        var underlying = Nullable.GetUnderlyingType(target) ?? target;
        var value = _values[p.Name];
        var converted = value is null ? null : Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
        return Typed(Expression.Constant(converted, underlying), target);
    }

    private Expression Identifier(Identifier i)
    {
        if (_variables.TryGetValue(i.Name, out var variable))
            return variable;
        var member = LeafScope.FindMember(_model.Type, i.Name)!;
        return Expression.MakeMemberAccess(_model, member);
    }

    private Expression Member(MemberAccess m)
    {
        var target = Visit(m.Target);
        var member = LeafScope.FindMember(Nullable.GetUnderlyingType(target.Type) ?? target.Type, m.Name)!;
        return IsNullable(target.Type)
            ? NullConditionalExpression.Create(target, t => Expression.MakeMemberAccess(Unwrap(t), member))
            : Expression.MakeMemberAccess(target, member);
    }

    private Expression Call(MethodCall c)
    {
        var target = Visit(c.Target);
        if (c.Method == "equalsIgnoreCase")
        {
            var equals = typeof(string).GetMethod(nameof(string.Equals), [typeof(string), typeof(string), typeof(StringComparison)])!;
            return Expression.Call(equals, target, Visit(c.Arguments[0]), Expression.Constant(StringComparison.OrdinalIgnoreCase));
        }

        var element = LeafScope.ElementType(Nullable.GetUnderlyingType(target.Type) ?? target.Type)!;
        Func<Expression, Expression> build = c.Method switch
        {
            "count" => t => Expression.Call(typeof(Enumerable), nameof(Enumerable.Count), [element], t),
            "where" => t => Expression.Call(typeof(Enumerable), nameof(Enumerable.Where), [element], t, Predicate((Lambda)c.Arguments[0], element)),
            "any" => t => Expression.Call(typeof(Enumerable), nameof(Enumerable.Any), [element], t, Predicate((Lambda)c.Arguments[0], element)),
            "all" => t => Expression.Call(typeof(Enumerable), nameof(Enumerable.All), [element], t, Predicate((Lambda)c.Arguments[0], element)),
            _ => t => Aggregate(c, t, element),
        };
        return IsNullable(target.Type) ? NullConditionalExpression.Create(target, build) : build(target);
    }

    private LambdaExpression Predicate(Lambda lambda, Type element)
    {
        var parameter = Expression.Parameter(element, lambda.Parameter);
        _variables[lambda.Parameter] = parameter;
        var body = AsBool(Visit(lambda.Body));
        _variables.Remove(lambda.Parameter);
        return Expression.Lambda(body, parameter);
    }

    private Expression Aggregate(MethodCall c, Expression target, Type element)
    {
        var lambda = (Lambda)c.Arguments[0];
        var resultType = Nullable.GetUnderlyingType(TypeOf(c)) ?? TypeOf(c);
        var kind = NumericLattice.KindOf(resultType)!.Value;
        var parameter = Expression.Parameter(element, lambda.Parameter);
        _variables[lambda.Parameter] = parameter;
        var body = Visit(lambda.Body);
        _variables.Remove(lambda.Parameter);
        body = NumericLattice.Widen(body, kind);
        var selector = Expression.Lambda(body, parameter);
        var name = c.Method switch { "sum" => nameof(Enumerable.Sum), "min" => nameof(Enumerable.Min), _ => nameof(Enumerable.Max) };
        var method = typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == name && m.IsGenericMethodDefinition && m.GetParameters().Length == 2
                         && m.GetParameters()[1].ParameterType.GetGenericArguments()[1] == body.Type)
            .MakeGenericMethod(element);
        return Expression.Call(method, target, selector);
    }

    private Expression Binary(Binary b)
    {
        var left = Visit(b.Left);
        var right = Visit(b.Right);

        switch (b.Operator)
        {
            case "&&": return Expression.AndAlso(AsBool(left), AsBool(right));
            case "||": return Expression.OrElse(AsBool(left), AsBool(right));
        }

        if (b.Left is NullLiteral || b.Right is NullLiteral)
        {
            var side = b.Left is NullLiteral ? right : left;
            var comparison = Expression.Equal(side, Expression.Constant(null, side.Type));
            return b.Operator == "==" ? comparison : Expression.Not(comparison);
        }

        if (NumericLattice.KindOf(left.Type) is { } lk && NumericLattice.KindOf(right.Type) is { } rk)
        {
            var kind = NumericLattice.Join(lk, rk) ?? NumericLattice.KindOf(TypeOf(b))!.Value;
            var lifted = IsNullable(left.Type) || IsNullable(right.Type);
            left = NumericLattice.Widen(Lift(left, lifted), kind);
            right = NumericLattice.Widen(Lift(right, lifted), kind);
        }

        return b.Operator switch
        {
            "==" => Expression.Equal(left, right),
            "!=" => Expression.NotEqual(left, right),
            "<" => Expression.LessThan(left, right),
            "<=" => Expression.LessThanOrEqual(left, right),
            ">" => Expression.GreaterThan(left, right),
            ">=" => Expression.GreaterThanOrEqual(left, right),
            "+" => Expression.AddChecked(left, right),
            "-" => Expression.SubtractChecked(left, right),
            "*" => Expression.MultiplyChecked(left, right),
            _ => Expression.Divide(left, right),
        };
    }

    private static Expression Lift(Expression value, bool lifted)
    {
        if (!lifted || !value.Type.IsValueType || Nullable.GetUnderlyingType(value.Type) is not null) return value;
        return Expression.Convert(value, typeof(Nullable<>).MakeGenericType(value.Type));
    }
}
#endif
```

`NumericLattice.Widen` on an unlifted `int` toward `Decimal` gives `decimal`; on a lifted `int?` it gives `decimal?`. Both sides are lifted first when either is nullable, so `Widen` produces matching nullable types and `Expression.GreaterThan` builds the lifted comparison.

- [ ] **Step 4: Run the tests**

Run: `env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests --framework net10.0 --filter LeafCompilerTests 2>&1 | grep -E "error CS|Passed!|Failed!|Assert|Exception"`
Expected: `Passed!`. If the checked-overflow test throws before evaluation (constant folding at build), change the test literal to `2000000000` against `age * 2 * 2000000000` so the multiplication happens at runtime. If the `Reason` differs (a `Spec.From(...).Create(name)` reason form), read `docs/expression-composition/index.md` for the named form and pin the actual text.

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Serialization/Expressions src/Motiv.Serialization.Tests/Expressions
git commit -m "Expressions — leaf compiler: native operator nodes, typed constants, null-conditional paths"
```

### Task 8: Bind leaves in all four binders, with parameter values and ranged errors

**Files:**
- Create: `src/Motiv.Serialization/Expressions/LeafBinding.cs`
- Create: `src/Motiv.Serialization/RuleTextRange.cs`
- Modify: `src/Motiv.Serialization/RuleError.cs` (add `Range`), `src/Motiv.Serialization/RuleNode.cs` (add `ParameterValues`, `ParameterDeclarations`), `src/Motiv.Serialization/RuleParameterSubstituter.cs` (set them on expression nodes), `src/Motiv.Serialization/RuleSerializer.cs` (pass declarations into `Substitute`), `src/Motiv.Serialization/RuleBinder.cs:107-112`, `src/Motiv.Serialization/AsyncRuleBinder.cs:118-123`, `src/Motiv.Serialization/MetadataRuleBinder.cs`, `src/Motiv.Serialization/AsyncMetadataRuleBinder.cs`
- Test: `src/Motiv.Serialization.Tests/Expressions/LeafBindingTests.cs`

**Interfaces:**
- Produces: `public sealed record RuleTextRange(int Start, int End)`; `RuleError` gains `public RuleTextRange? Range { get; }` and a constructor `RuleError(string path, RuleErrorCode code, string message, RuleTextRange? range)`; `RuleNode` gains `IReadOnlyDictionary<string, object?>? ParameterValues` and `IReadOnlyList<RuleParameterDeclaration>? ParameterDeclarations` (set by the substituter on `RuleOperator.Expression` nodes); `static SpecBase<TModel,string>? LeafBinding.Bind<TModel>(RuleNode node, List<RuleError> errors)` and `static LeafAnalysis? LeafBinding.Analyse<TModel>(RuleNode node, List<RuleError> errors)` (parse + check, reporting problems as ranged errors at `node.Path`; warnings are not reported as errors — they are facts for the inspector, see Task 9).

- [ ] **Step 1: Write the failing test**

```csharp
// src/Motiv.Serialization.Tests/Expressions/LeafBindingTests.cs
namespace Motiv.Serialization.Tests.Expressions;

public class LeafBindingTests
{
    public sealed record Order(string Status, decimal Total);
    public sealed record Customer(int Age, decimal CreditLimit, IReadOnlyList<Order>? Orders);

    private static readonly Customer Sample = new(34, 800m, [new("paid", 620m), new("paid", 410m), new("pending", 950m)]);

    private static RuleSerializer Serializer() => new(new SpecRegistry()
        .Register("is-adult", Spec.Build((Customer c) => c.Age >= 18).WhenTrue("adult").WhenFalse("minor").Create()));

    [Fact]
    public void Should_bind_a_leaf_with_a_document_parameter()
    {
        const string json = """
            { "parameters": { "vip": { "type": "number", "default": 1000 } },
              "rule": { "andAlso": [ { "spec": "is-adult" },
                        { "expression": "orders.where(o => o.status == \"paid\").sum(o => o.total) > @vip",
                          "whenTrue": "big spender", "whenFalse": "not a big spender" } ] } }
            """;
        var spec = Serializer().Deserialize<Customer>(json);

        var result = spec.Evaluate(Sample);
        result.Satisfied.ShouldBeTrue();
        result.Assertions.ShouldBe(["adult", "big spender"]);

        var overridden = Serializer().Deserialize<Customer>(json, new { vip = 2000 });
        overridden.Evaluate(Sample).Assertions.ShouldBe(["not a big spender"]);
    }

    [Fact]
    public void Should_explain_an_undecorated_leaf_with_its_decomposed_clause()
    {
        const string json = """{ "rule": { "expression": "creditLimit > 500" } }""";
        var result = Serializer().Deserialize<Customer>(json).Evaluate(Sample);
        result.Satisfied.ShouldBeTrue();
        result.Assertions.ShouldHaveSingleItem().ShouldEndWith("== true");
        result.Reason.ShouldBe("creditLimit > 500 == true");
    }

    [Fact]
    public void Should_report_a_type_problem_with_its_range_inside_the_leaf()
    {
        const string json = """{ "rule": { "expression": "orders.sum(o => o.nope) > 1" } }""";
        var errors = Serializer().Validate<Customer>(json);
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.UnknownField);
        error.Path.ShouldBe("$.rule");
        error.Range.ShouldBe(new RuleTextRange(18, 22));
    }

    [Fact]
    public void Should_bind_inside_a_quantifier_body_against_the_element()
    {
        const string json = """
            { "rule": { "asAllSatisfied": { "expression": "total <= 1000" }, "path": "orders" } }
            """;
        var serializer = new RuleSerializer(new SpecRegistry().RegisterCollection<Customer, Order>("orders", c => c.Orders ?? []));
        serializer.Deserialize<Customer>(json).Evaluate(Sample).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_bind_in_an_async_load()
    {
        const string json = """{ "rule": { "expression": "age >= 18" } }""";
        var spec = Serializer().DeserializeAsyncSpec<Customer>(json);
        (await spec.EvaluateAsync(Sample)).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_require_metadata_on_a_leaf_in_a_metadata_load()
    {
        const string bare = """{ "rule": { "expression": "age >= 18" } }""";
        var errors = Serializer().Validate<Customer, int>(bare);
        errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.ExpressionRequiresMetadata);

        const string decorated = """{ "rule": { "expression": "age >= 18", "whenTrue": 1, "whenFalse": 2, "name": "adult" } }""";
        Serializer().Deserialize<Customer, int>(decorated).Evaluate(Sample).Value.ShouldBe(1);
    }
}
```

The `whenTrue: 1` form is an object payload in the existing metadata binder's terms only if the parser accepts a JSON number as a payload; check `RuleDocumentParser` `whenTrue` handling — if only objects are accepted, use `{ "code": 1 }` with a `record Code(int code)` metadata type instead, mirroring `RuleMetadataTests`.

- [ ] **Step 2: Run it to see it fail**

Run: `env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests --framework net10.0 --filter LeafBindingTests 2>&1 | grep -E "error CS|Passed!|Failed!"`
Expected: compile error on `error.Range` / `RuleTextRange`.

- [ ] **Step 3: Implement**

`src/Motiv.Serialization/RuleTextRange.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>A half-open character range <c>[Start, End)</c> inside an expression leaf's text.</summary>
public sealed record RuleTextRange(int Start, int End);
```

`RuleError.cs`: add the property and a second constructor:

```csharp
    public RuleError(string path, RuleErrorCode code, string message, RuleTextRange? range)
        : this(path, code, message)
    {
        Range = range;
    }

    /// <summary>Where inside an expression leaf's text the error lies; null for every other error.</summary>
    public RuleTextRange? Range { get; }
```

`RuleNode.cs`: add

```csharp
    /// <summary>The resolved parameter values and their declarations, set on an expression node by the
    /// substituter so the leaf binder can type <c>@name</c> references and read their values.</summary>
    public IReadOnlyDictionary<string, object?>? ParameterValues { get; set; }
    public IReadOnlyList<RuleParameterDeclaration>? ParameterDeclarations { get; set; }
```

`RuleParameterSubstituter.Apply`: add a `IReadOnlyList<RuleParameterDeclaration> declarations` parameter, and at the top of the method:

```csharp
        if (node.Operator == RuleOperator.Expression)
        {
            node.ParameterValues = values;
            node.ParameterDeclarations = declarations;
        }
```

and pass `declarations` through the recursive call. In `RuleSerializer.Substitute` pass `document.Parameters` to both `Apply` calls (and update its own signature's callers, `Prepare` and `PrepareForValidation`, to pass `document`).

`src/Motiv.Serialization/Expressions/LeafBinding.cs`:

```csharp
namespace Motiv.Serialization.Expressions;

/// <summary>The one entry the binders call for an expression node.</summary>
internal static class LeafBinding
{
#if NET8_0_OR_GREATER
    public static LeafAnalysis? Analyse<TModel>(RuleNode node, List<RuleError> errors)
    {
        var text = node.ExpressionText!;
        var problems = new List<LeafProblem>();
        var root = LeafParser.Parse(text, problems);
        if (root is null)
        {
            Report(node, problems, errors);
            return null;
        }

        var scope = LeafScope.For(typeof(TModel), node.ParameterDeclarations ?? []);
        var analysis = LeafChecker.Check(root, scope);
        Report(node, analysis.Problems, errors);
        return analysis.IsValid ? analysis : null;
    }

    public static SpecBase<TModel, string>? Bind<TModel>(RuleNode node, List<RuleError> errors)
    {
        var analysis = Analyse<TModel>(node, errors);
        if (analysis is null) return null;
        var problems = new List<LeafProblem>();
        var root = LeafParser.Parse(node.ExpressionText!, problems)!;
        return LeafCompiler.Compile<TModel>(root, analysis, node.ParameterValues ?? new Dictionary<string, object?>(), node.ExpressionText!);
    }

    private static void Report(RuleNode node, IEnumerable<LeafProblem> problems, List<RuleError> errors)
    {
        foreach (var problem in problems.Where(p => !p.IsWarning))
            errors.Add(new RuleError(node.Path, problem.Code, problem.Message, new RuleTextRange(problem.Start, problem.End)));
    }
#else
    public static SpecBase<TModel, string>? Bind<TModel>(RuleNode node, List<RuleError> errors)
    {
        errors.Add(new RuleError(node.Path, RuleErrorCode.ExpressionsNotEnabled,
            "expression nodes are supported on .NET 8 or later"));
        return null;
    }
#endif
}
```

(Parsing twice in `Bind` keeps `LeafAnalysis` free of the root; if you prefer, add `Root` to `LeafAnalysis` in Task 5 and drop the second parse.)

The four binders — replace each `BindExpressionLeaf` body:

- `RuleBinder.cs`: `=> LeafBinding.Bind<TModel>(node, errors);`
- `AsyncRuleBinder.cs`: `=> LeafBinding.Bind<TModel>(node, errors)?.ToAsyncSpec();`
- `MetadataRuleBinder.cs` and `AsyncMetadataRuleBinder.cs`: a bare leaf reaches `BindExpressionLeaf` only when it has no object payloads (decorated ones go through `BindRemetadatized`, which binds the subtree with explanation semantics and re-metadatizes — check that `BindRemetadatized` reaches `RuleBinder.BindNode`, which now binds leaves). So:

```csharp
    private static SpecBase<TModel, TMetadata>? BindExpressionLeaf<TModel>(RuleNode node, List<RuleError> errors)
    {
        errors.Add(new RuleError(node.Path, RuleErrorCode.ExpressionRequiresMetadata,
            $"an expression leaf in a metadata document must carry 'whenTrue'/'whenFalse' of type '{typeof(TMetadata).Name}', or sit under a node that does"));
        return null;
    }
```

If `BindRemetadatized` binds the operator subtree through the *metadata* binder's own `BindNode` rather than `RuleBinder`, route expression nodes inside it to `LeafBinding.Bind<TModel>` and re-metadatize the result the way it does for spec leaves.

- [ ] **Step 4: Run this test class, then the whole serialization suite**

Run: `env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests --framework net10.0 2>&1 | grep -E "error CS|Passed!|Failed!|Failed "`
Expected: `Passed!`. One existing test will fail and should be *updated*: any test asserting `ExpressionsNotEnabled` for an expression document on .NET (search `ExpressionsNotEnabled` under `src/Motiv.Serialization.Tests`); change it to assert the leaf now binds, and keep the `netstandard2.0` expectation only under `#if !NET8_0_OR_GREATER` if the test project multi-targets a netfx TFM.

- [ ] **Step 5: Build every target framework, and run the example suites**

Run: `env -u MallocStackLogging -u MallocNanoZone dotnet build src/Motiv.Serialization 2>&1 | grep -E "error|Build succeeded"` (this compiles the `netstandard2.0` target, where the `#if` guards must leave a compiling `LeafBinding`).
Then: `env -u MallocStackLogging -u MallocNanoZone dotnet test Motiv.sln --framework net10.0 2>&1 | grep -E "error CS|Passed!|Failed!"` — the example projects contain justification-text assertions.
Expected: all `Passed!`.

- [ ] **Step 6: Commit**

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.Tests
git commit -m "Expressions — bind leaves in all four binders; ranged errors; parameter values reach leaves"
```

### Task 9: Validation facts and the catalog `format` stamp

**Files:**
- Create: `src/Motiv.Serialization/RuleValidation.cs`
- Modify: `src/Motiv.Serialization/RuleSerializer.cs` (add `Inspect<TModel>`), `src/Motiv.Serialization/Expressions/LeafBinding.cs` (expose facts), `src/Motiv.Serialization.AspNetCore/ModelBinding.cs` (+ `Inspect`), `src/Motiv.Serialization.AspNetCore/MotivRulesOptions.cs` (populate it), `src/Motiv.Serialization.AspNetCore/RulesContracts.cs` (`ValidationResponse` gains `Facts`), `src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs` (validate returns facts; `ToSchema` stamps `format`)
- Test: `src/Motiv.Serialization.AspNetCore.Tests/ExpressionEndpointTests.cs`

**Interfaces:**
- Produces: `public sealed record RuleLeafFact(string Path, RuleTextRange Range, string Text, string Type, string? From, bool IsWarning, string? Message)` — one per literal/parameter (`From` names the anchor) plus one per leaf root (`Text` is the leaf, `Type` its result), plus one per **warning** (with `IsWarning` and `Message`). `public sealed record RuleValidation(IReadOnlyList<RuleError> Errors, IReadOnlyList<RuleLeafFact> Facts)`. `public RuleValidation RuleSerializer.Inspect<TModel>(string json)`. Wire contract: `ValidationResponse(IReadOnlyList<RuleError> Errors, IReadOnlyList<RuleLeafFact> Facts)` — `Facts` serialized as `facts`, and `RuleError.Range` as `range: { start, end }` (nullable, omitted when null via the app's `DefaultIgnoreCondition`; if the app's options do not ignore nulls, `range: null` is fine — the TypeScript contract makes it optional-or-null). Catalog: numeric properties in `modelTypes` schemas carry `"format": "int32" | "int64" | "single" | "double" | "decimal"`.

- [ ] **Step 1: Write the failing test**

```csharp
// src/Motiv.Serialization.AspNetCore.Tests/ExpressionEndpointTests.cs
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Motiv.Serialization.AspNetCore.Tests;

public class ExpressionEndpointTests
{
    public sealed record Order(string Status, decimal Total, int DaysSinceShipped);
    public sealed record Customer(int Age, long Points, double Score, decimal CreditLimit, IReadOnlyList<Order>? Orders);

    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddMotivRules(new SpecRegistry()
                .Register("is-adult", Spec.Build((Customer c) => c.Age >= 18).WhenTrue("adult").WhenFalse("minor").Create()),
            options => options.AddModel<Customer>("customer"));
        var app = builder.Build();
        app.MapMotivRules("/api/rules");
        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task Should_stamp_numeric_formats_on_model_schemas()
    {
        await using var app = await StartAsync();
        var catalog = await app.GetTestClient().GetFromJsonAsync<JsonElement>("/api/rules/catalog");
        var customer = catalog.GetProperty("modelTypes").GetProperty("customer").GetProperty("properties");
        customer.GetProperty("age").GetProperty("format").GetString().ShouldBe("int32");
        customer.GetProperty("points").GetProperty("format").GetString().ShouldBe("int64");
        customer.GetProperty("score").GetProperty("format").GetString().ShouldBe("double");
        customer.GetProperty("creditLimit").GetProperty("format").GetString().ShouldBe("decimal");
        customer.GetProperty("orders").GetProperty("items").GetProperty("properties").GetProperty("total").GetProperty("format").GetString().ShouldBe("decimal");
    }

    [Fact]
    public async Task Should_return_facts_for_a_valid_leaf_and_a_range_for_an_invalid_one()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();

        var valid = await client.PostAsJsonAsync("/api/rules/validate", new
        {
            modelType = "customer",
            document = JsonDocument.Parse("""{ "rule": { "expression": "orders.sum(o => o.total) > 1000" } }""").RootElement,
        });
        var body = await valid.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").GetArrayLength().ShouldBe(0);
        var literal = body.GetProperty("facts").EnumerateArray().Single(f => f.GetProperty("text").GetString() == "1000");
        literal.GetProperty("type").GetString().ShouldBe("decimal");
        literal.GetProperty("from").GetString().ShouldBe("orders.sum(o => o.total)");
        literal.GetProperty("range").GetProperty("start").GetInt32().ShouldBe(27);

        var invalid = await client.PostAsJsonAsync("/api/rules/validate", new
        {
            modelType = "customer",
            document = JsonDocument.Parse("""{ "rule": { "expression": "creditLimit > score" } }""").RootElement,
        });
        var errors = (await invalid.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        errors[0].GetProperty("code").GetString().ShouldBe("ExpressionTypeMismatch");
        errors[0].GetProperty("range").GetProperty("start").GetInt32().ShouldBe(0);
        errors[0].GetProperty("range").GetProperty("end").GetInt32().ShouldBe(19);
    }
}
```

Match the `AddMotivRules`/`AddModel` call shape to `EvaluateEndpointTests.StartAsync` (lines 12–40) — copy its registration form exactly.

- [ ] **Step 2: Run it to see it fail**

Run: `env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.AspNetCore.Tests --framework net10.0 --filter ExpressionEndpointTests 2>&1 | grep -E "error CS|Passed!|Failed!"`
Expected: both tests fail (no `format`, no `facts`).

- [ ] **Step 3: Implement**

`src/Motiv.Serialization/RuleValidation.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>What the checker learned about one place in an expression leaf.</summary>
public sealed record RuleLeafFact(string Path, RuleTextRange Range, string Text, string Type, string? From, bool IsWarning, string? Message);

/// <summary>The outcome of inspecting a document: its errors, and the facts about its leaves.</summary>
public sealed record RuleValidation(IReadOnlyList<RuleError> Errors, IReadOnlyList<RuleLeafFact> Facts);
```

`LeafBinding.cs` (inside the `NET8_0_OR_GREATER` block): add

```csharp
    /// <summary>Facts for the inspector and hover: literal/parameter types, the leaf's result type, warnings.</summary>
    public static IReadOnlyList<RuleLeafFact> FactsOf(RuleNode node, LeafAnalysis analysis)
    {
        var text = node.ExpressionText!;
        var facts = new List<RuleLeafFact>();
        foreach (var fact in analysis.Facts)
            facts.Add(new RuleLeafFact(node.Path, new RuleTextRange(fact.Node.Start, fact.Node.End),
                text.Substring(fact.Node.Start, fact.Node.End - fact.Node.Start), DescribeType(fact.Type), fact.From, false, null));
        foreach (var warning in analysis.Problems.Where(p => p.IsWarning))
            facts.Add(new RuleLeafFact(node.Path, new RuleTextRange(warning.Start, warning.End),
                text.Substring(warning.Start, warning.End - warning.Start), string.Empty, null, true, warning.Message));
        return facts;
    }

    private static string DescribeType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        var name = underlying == typeof(int) ? "int" : underlying == typeof(long) ? "long" : underlying == typeof(decimal) ? "decimal"
            : underlying == typeof(double) ? "double" : underlying == typeof(float) ? "float" : underlying == typeof(bool) ? "bool"
            : underlying == typeof(string) ? "string" : underlying.Name;
        return underlying == type ? name : $"{name}?";
    }
```

`RuleSerializer.cs`: add, next to `Validate<TModel>`:

```csharp
    /// <summary>Validates as <see cref="Validate{TModel}(string)" /> does, and also reports what the
    /// checker learned about every expression leaf — the types it solved and its warnings.</summary>
    public RuleValidation Inspect<TModel>(string json)
    {
        var errors = new List<RuleError>();
        var document = PrepareForValidation(json, errors);
        if (document is null)
            return new RuleValidation(errors, []);

        var facts = new List<RuleLeafFact>();
        // Binding reports leaf errors; facts are gathered by a second, analysis-only pass over the leaves.
        RuleBinder.Bind<TModel>(document, _registry, _options, errors);
        foreach (var leaf in ExpressionNodes(document))
        {
            var analysis = Expressions.LeafBinding.Analyse<TModel>(leaf, new List<RuleError>());
            if (analysis is not null)
                facts.AddRange(Expressions.LeafBinding.FactsOf(leaf, analysis));
        }
        return new RuleValidation(errors, facts);
    }

    private static IEnumerable<RuleNode> ExpressionNodes(RuleDocument document)
    {
        var stack = new Stack<RuleNode>();
        if (document.Root is not null) stack.Push(document.Root);
        foreach (var definition in document.Definitions) stack.Push(definition);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node.Operator == RuleOperator.Expression) yield return node;
            foreach (var child in node.Children) stack.Push(child);
        }
    }
```

`Analyse` and `FactsOf` need `#if NET8_0_OR_GREATER` fallbacks in `LeafBinding` that return `null` / `[]`. Use the same source-and-options names `Validate<TModel>` uses (read it at line 251 — it may go through `ISpecSource` and the async variants; mirror it). Leaves inside a quantifier body are analysed against the *element* type: `RuleBinder.BindElement<TElement>` handles binding, but `Inspect`'s fact pass must know the element type — read `BindHigherOrder` to find how it resolves the collection entry's element type, and call `Analyse` through a small `MakeGenericMethod` on it for nodes under a higher-order node. Facts for such leaves are worth a test of their own; add one to `LeafBindingTests` (`asAllSatisfied` over `orders` with `"expression": "total > 10"` → a fact typed `decimal` for `10`).

`ModelBinding.cs`: add `public required Func<RuleSerializer, string, RuleValidation> Inspect { get; init; }` and populate it in `MotivRulesOptions.AddModel` as `Inspect = static (serializer, json) => serializer.Inspect<TModel>(json)`.

`RulesContracts.cs`: `public sealed record ValidationResponse(IReadOnlyList<RuleError> Errors, IReadOnlyList<RuleLeafFact> Facts)` — update every `new ValidationResponse(errors)` call site to pass `[]` for facts (the save and evaluate paths), and the validate endpoint to:

```csharp
            if (request.IsAsync)
                return Results.Json(new ValidationResponse(binding.ValidateAsyncSpec(serializer, documentJson), []), json);
            var inspection = binding.Inspect(serializer, documentJson);
            return Results.Json(new ValidationResponse(inspection.Errors, inspection.Facts), json);
```

`ToSchema` in `MotivRulesEndpoints.cs`:

```csharp
    private static readonly JsonSchemaExporterOptions SchemaExporterOptions = new()
    {
        TransformSchemaNode = static (context, node) =>
        {
            var type = Nullable.GetUnderlyingType(context.TypeInfo.Type) ?? context.TypeInfo.Type;
            var format = type == typeof(int) ? "int32" : type == typeof(long) ? "int64" : type == typeof(float) ? "single"
                : type == typeof(double) ? "double" : type == typeof(decimal) ? "decimal" : null;
            if (format is not null && node is JsonObject schema && !schema.ContainsKey("format"))
                schema["format"] = format;
            return node;
        },
    };

    private static JsonElement ToSchema(JsonSerializerOptions options, Type type)
    {
        var schemaOptions = options.TypeInfoResolver is null
            ? new JsonSerializerOptions(options) { TypeInfoResolver = new DefaultJsonTypeInfoResolver() }
            : options;
        return JsonSerializer.SerializeToElement(schemaOptions.GetJsonSchemaAsNode(type, SchemaExporterOptions));
    }
```

(`using System.Text.Json.Nodes; using System.Text.Json.Schema;`.)

- [ ] **Step 4: Run the AspNetCore suite, then the full solution**

Run: `env -u MallocStackLogging -u MallocNanoZone dotnet test Motiv.sln --framework net10.0 2>&1 | grep -E "error CS|Passed!|Failed!"`
Expected: all `Passed!`. `RuleSchemaTests` or `CatalogEndpointTests` may pin a model schema verbatim; update those expectations to include `format`.

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.AspNetCore src/Motiv.Serialization.AspNetCore.Tests
git commit -m "Expressions — validation facts on the validate endpoint; numeric format stamped on model schemas"
```

---

## Phase 2 — TypeScript core: the leaf language for the editor

Prototype branch `prototype/expression-leaf-authoring` holds a first cut of the tokenizer, parser, scope and checker under `ui/apps/studio/src/panes/prototype/expression/`. Read it for shape; the code below is the productionised form (ranges on every node, the numeric lattice, `format`-aware schemas, warnings). Every function is pure over text and a scope; no CodeMirror import anywhere in the package.

### Task 10: Contracts, tokenizer and parser

**Files:**
- Modify: `ui/packages/rules-core/src/contracts.ts` (`JsonSchema.format`, `RuleError.range`, `ValidationResponse.facts`, `RuleLeafFact`, new `RuleErrorCode`s)
- Create: `ui/packages/rules-core/src/expression/tokenize.ts`, `ui/packages/rules-core/src/expression/parse.ts`, `ui/packages/rules-core/src/expression/index.ts`
- Modify: `ui/packages/rules-core/src/index.ts` (re-export `./expression/index.js`)
- Test: `ui/packages/rules-core/test/expression-parse.test.ts`

**Interfaces:**
- Produces (contracts): `RuleErrorCode` adds `'InvalidExpression' | 'UnknownField' | 'UnknownMethod' | 'ExpressionTypeMismatch' | 'ExpressionRequiresMetadata'`; `interface RuleTextRange { start: number; end: number }`; `RuleError.range?: RuleTextRange | null`; `interface RuleLeafFact { path: string; range: RuleTextRange; text: string; type: string; from: string | null; isWarning: boolean; message: string | null }`; `ValidationResponse.facts?: RuleLeafFact[]`; `JsonSchema.format?: string`.
- Produces (expression): `type LeafTokenKind = 'ident' | 'param' | 'number' | 'string' | 'keyword' | 'op' | 'punct' | 'end'`; `interface LeafToken { kind; value; from; to }`; `interface LeafProblem { code: RuleErrorCode; message: string; from: number; to: number; warning?: true }`; `tokenizeLeaf(text): { tokens: LeafToken[]; problems: LeafProblem[] }`; `type LeafAst` — a discriminated union `{ kind: 'num'; text } | { kind: 'str'; value } | { kind: 'bool'; value } | { kind: 'null' } | { kind: 'param'; name } | { kind: 'ident'; name } | { kind: 'member'; target; name; nameFrom; nameTo } | { kind: 'call'; target; method; args; methodFrom; methodTo } | { kind: 'lambda'; param; body } | { kind: 'binary'; op; left; right } | { kind: 'unary'; op; operand }`, each with `from`/`to`; `parseLeaf(text): { ast?: LeafAst; problems: LeafProblem[] }`; `printLeaf(ast): string`.

- [ ] **Step 1: Write the failing test**

```ts
// ui/packages/rules-core/test/expression-parse.test.ts
import { describe, it, expect } from 'vitest';
import { parseLeaf, printLeaf, tokenizeLeaf } from '../src/expression/index.js';

describe('tokenizeLeaf', () => {
  it('classifies every token kind with offsets', () => {
    const { tokens, problems } = tokenizeLeaf('orders.where(o => o.status == "paid").sum(o => o.total) > @vip');
    expect(problems).toEqual([]);
    expect(tokens.map((t) => t.kind)).toEqual([
      'ident', 'punct', 'ident', 'punct', 'ident', 'op', 'ident', 'punct', 'ident', 'op', 'string', 'punct',
      'punct', 'ident', 'punct', 'ident', 'op', 'ident', 'punct', 'ident', 'punct', 'op', 'param', 'end',
    ]);
    expect(tokens[22]).toEqual({ kind: 'param', value: '@vip', from: 60, to: 64 });
  });

  it('reports an unterminated string and an unknown character', () => {
    expect(tokenizeLeaf('country == "SE').problems[0]).toMatchObject({ code: 'InvalidExpression', from: 11, to: 14 });
    expect(tokenizeLeaf('age # 3').problems[0]).toMatchObject({ from: 4, to: 5 });
  });
});

describe('parseLeaf', () => {
  it('parses precedence and ranges', () => {
    const { ast, problems } = parseLeaf('age * 12 + 1 >= @min && isActive');
    expect(problems).toEqual([]);
    expect(ast).toMatchObject({
      kind: 'binary', op: '&&', from: 0, to: 32,
      left: { kind: 'binary', op: '>=', left: { kind: 'binary', op: '+', left: { kind: 'binary', op: '*' } }, right: { kind: 'param', name: 'min' } },
      right: { kind: 'ident', name: 'isActive' },
    });
  });

  it('parses method chains with lambdas', () => {
    const { ast } = parseLeaf('orders.where(o => o.status == "paid").sum(o => o.total) > 1000');
    expect(ast).toMatchObject({
      kind: 'binary', op: '>',
      left: { kind: 'call', method: 'sum', methodFrom: 39, methodTo: 42, args: [{ kind: 'lambda', param: 'o', body: { kind: 'member', name: 'total' } }],
        target: { kind: 'call', method: 'where', target: { kind: 'ident', name: 'orders' } } },
      right: { kind: 'num', text: '1000' },
    });
  });

  it.each([
    ['', 'empty expression', 0, 0],
    ['age >', 'unexpected end of expression', 5, 5],
    ['age > > 1', "unexpected '>'", 6, 7],
    ['orders.', "expected a field or method name after '.'", 7, 7],
    ['a == b == c', "unexpected '=='", 7, 9],
  ])('reports %j at its range', (text, message, from, to) => {
    const { ast, problems } = parseLeaf(text);
    expect(ast).toBeUndefined();
    expect(problems).toEqual([{ code: 'InvalidExpression', message, from, to }]);
  });

  it('prints the canonical text', () => {
    const { ast } = parseLeaf('orders.where( o=>o.status=="paid" ).sum(o => o.total)>@vip');
    expect(printLeaf(ast!)).toBe('orders.where(o => o.status == "paid").sum(o => o.total) > @vip');
  });
});
```

- [ ] **Step 2: Run it to see it fail**

Run: `pnpm -C ui/packages/rules-core exec vitest run test/expression-parse.test.ts`
Expected: FAIL — cannot resolve `../src/expression/index.js`.

- [ ] **Step 3: Implement**

Contracts (`contracts.ts`): extend the `RuleErrorCode` union with the five codes; add after `RuleError`:

```ts
/** A half-open character range `[start, end)` inside an expression leaf's text. */
export interface RuleTextRange { start: number; end: number }

/** What the server's checker learned about one place in a leaf: a literal's solved type, the leaf's result type, or a warning. */
export interface RuleLeafFact {
  path: string;
  range: RuleTextRange;
  text: string;
  type: string;
  from: string | null;
  isWarning: boolean;
  message: string | null;
}
```

and give `RuleError` a `range?: RuleTextRange | null;`, `ValidationResponse` a `facts?: RuleLeafFact[];`, and `JsonSchema` a `/** The CLR numeric kind the host stamps on numeric fields: int32, int64, single, double, decimal. */ format?: string;`.

```ts
// ui/packages/rules-core/src/expression/tokenize.ts
import type { RuleErrorCode } from '../contracts.js';

export type LeafTokenKind = 'ident' | 'param' | 'number' | 'string' | 'keyword' | 'op' | 'punct' | 'end';
export interface LeafToken { kind: LeafTokenKind; value: string; from: number; to: number }
export interface LeafProblem { code: RuleErrorCode; message: string; from: number; to: number; warning?: true }

const KEYWORDS: ReadonlySet<string> = new Set(['null', 'true', 'false']);
const TWO_CHAR_OPS = ['=>', '==', '!=', '<=', '>=', '&&', '||'];

/** Lexes a leaf. Whitespace is dropped; the list always ends with an `end` token at the text's length. */
export function tokenizeLeaf(text: string): { tokens: LeafToken[]; problems: LeafProblem[] } {
  const tokens: LeafToken[] = [];
  const problems: LeafProblem[] = [];
  let i = 0;
  const push = (kind: LeafTokenKind, from: number): void => { tokens.push({ kind, value: text.slice(from, i), from, to: i }); };
  while (i < text.length) {
    const c = text[i]!;
    const start = i;
    if (/\s/.test(c)) { i++; continue; }
    if (/[A-Za-z_]/.test(c)) {
      while (i < text.length && /[A-Za-z0-9_]/.test(text[i]!)) i++;
      push(KEYWORDS.has(text.slice(start, i)) ? 'keyword' : 'ident', start);
      continue;
    }
    if (c === '@') {
      i++;
      while (i < text.length && /[A-Za-z0-9_]/.test(text[i]!)) i++;
      if (i === start + 1) problems.push({ code: 'InvalidExpression', message: "expected a parameter name after '@'", from: start, to: i });
      push('param', start);
      continue;
    }
    if (/[0-9]/.test(c)) {
      while (i < text.length && /[0-9]/.test(text[i]!)) i++;
      if (text[i] === '.' && /[0-9]/.test(text[i + 1] ?? '')) { i++; while (i < text.length && /[0-9]/.test(text[i]!)) i++; }
      push('number', start);
      continue;
    }
    if (c === '"') {
      i++;
      while (i < text.length && text[i] !== '"') i++;
      if (i >= text.length) {
        problems.push({ code: 'InvalidExpression', message: 'unterminated string', from: start, to: text.length });
        push('string', start);
        break;
      }
      i++;
      push('string', start);
      continue;
    }
    const two = text.slice(i, i + 2);
    if (TWO_CHAR_OPS.includes(two)) { i += 2; push('op', start); continue; }
    if ('<>!+-*/'.includes(c)) { i++; push('op', start); continue; }
    if ('().,'.includes(c)) { i++; push('punct', start); continue; }
    problems.push({ code: 'InvalidExpression', message: `unexpected character '${c}'`, from: start, to: start + 1 });
    i++;
  }
  tokens.push({ kind: 'end', value: '', from: text.length, to: text.length });
  return { tokens, problems };
}
```

```ts
// ui/packages/rules-core/src/expression/parse.ts
import { tokenizeLeaf, type LeafProblem, type LeafToken } from './tokenize.js';

interface Span { from: number; to: number }
export type LeafAst = Span & (
  | { kind: 'num'; text: string }
  | { kind: 'str'; value: string }
  | { kind: 'bool'; value: boolean }
  | { kind: 'null' }
  | { kind: 'param'; name: string }
  | { kind: 'ident'; name: string }
  | { kind: 'member'; target: LeafAst; name: string; nameFrom: number; nameTo: number }
  | { kind: 'call'; target: LeafAst; method: string; args: LeafAst[]; methodFrom: number; methodTo: number }
  | { kind: 'lambda'; param: string; body: LeafAst }
  | { kind: 'binary'; op: string; left: LeafAst; right: LeafAst }
  | { kind: 'unary'; op: string; operand: LeafAst }
);

class SyntaxError extends Error {
  constructor(message: string, public readonly token: LeafToken) { super(message); }
}

class Parser {
  private index = 0;
  constructor(private readonly tokens: LeafToken[]) {}
  private peek(offset = 0): LeafToken { return this.tokens[Math.min(this.index + offset, this.tokens.length - 1)]!; }
  private at(value: string): boolean { const t = this.peek(); return t.kind !== 'string' && t.value === value; }
  private next(): LeafToken {
    const t = this.peek();
    if (t.kind === 'end') throw new SyntaxError('unexpected end of expression', t);
    this.index++;
    return t;
  }
  private expect(value: string): LeafToken {
    const t = this.peek();
    if (t.kind === 'end') throw new SyntaxError('unexpected end of expression', t);
    if (t.value !== value) throw new SyntaxError(`expected '${value}'`, t);
    return this.next();
  }
  parse(): LeafAst {
    const ast = this.or();
    const rest = this.peek();
    if (rest.kind !== 'end') throw new SyntaxError(`unexpected '${rest.value}'`, rest);
    return ast;
  }
  private level(ops: string[], below: () => LeafAst, once = false): LeafAst {
    let left = below();
    while (this.peek().kind === 'op' && ops.includes(this.peek().value)) {
      const op = this.next().value;
      const right = below();
      left = { kind: 'binary', op, left, right, from: left.from, to: right.to };
      if (once) break;
    }
    return left;
  }
  private or(): LeafAst { return this.level(['||'], () => this.and()); }
  private and(): LeafAst { return this.level(['&&'], () => this.eq()); }
  private eq(): LeafAst { return this.level(['==', '!='], () => this.cmp(), true); }
  private cmp(): LeafAst { return this.level(['<', '<=', '>', '>='], () => this.add(), true); }
  private add(): LeafAst { return this.level(['+', '-'], () => this.mul()); }
  private mul(): LeafAst { return this.level(['*', '/'], () => this.unary()); }
  private unary(): LeafAst {
    if (this.at('!') || this.at('-')) {
      const op = this.next();
      const operand = this.unary();
      return { kind: 'unary', op: op.value, operand, from: op.from, to: operand.to };
    }
    return this.postfix();
  }
  private postfix(): LeafAst {
    let node = this.primary();
    while (this.at('.')) {
      this.next();
      const name = this.peek();
      if (name.kind !== 'ident') throw new SyntaxError("expected a field or method name after '.'", name);
      this.next();
      if (this.at('(')) {
        this.next();
        const args: LeafAst[] = [];
        while (!this.at(')')) {
          args.push(this.lambdaOrExpr());
          if (this.at(',')) this.next(); else break;
        }
        const close = this.expect(')');
        node = { kind: 'call', target: node, method: name.value, args, methodFrom: name.from, methodTo: name.to, from: node.from, to: close.to };
      } else {
        node = { kind: 'member', target: node, name: name.value, nameFrom: name.from, nameTo: name.to, from: node.from, to: name.to };
      }
    }
    return node;
  }
  private lambdaOrExpr(): LeafAst {
    const t = this.peek();
    if (t.kind === 'ident' && this.peek(1).value === '=>') {
      this.next(); this.next();
      const body = this.or();
      return { kind: 'lambda', param: t.value, body, from: t.from, to: body.to };
    }
    return this.or();
  }
  private primary(): LeafAst {
    const t = this.next();
    switch (t.kind) {
      case 'number': return { kind: 'num', text: t.value, from: t.from, to: t.to };
      case 'string': return { kind: 'str', value: t.value.slice(1, -1), from: t.from, to: t.to };
      case 'keyword': return t.value === 'null' ? { kind: 'null', from: t.from, to: t.to } : { kind: 'bool', value: t.value === 'true', from: t.from, to: t.to };
      case 'param': return { kind: 'param', name: t.value.slice(1), from: t.from, to: t.to };
      case 'ident': return { kind: 'ident', name: t.value, from: t.from, to: t.to };
      case 'punct':
        if (t.value === '(') {
          const inner = this.or();
          const close = this.expect(')');
          return { ...inner, from: t.from, to: close.to };
        }
        break;
      default: break;
    }
    throw new SyntaxError(`unexpected '${t.value}'`, t);
  }
}

/** Parses a leaf; on a syntax problem `ast` is absent and `problems` has exactly one entry. */
export function parseLeaf(text: string): { ast?: LeafAst; problems: LeafProblem[] } {
  const { tokens, problems } = tokenizeLeaf(text);
  if (problems.length) return { problems };
  if (tokens.length === 1) return { problems: [{ code: 'InvalidExpression', message: 'empty expression', from: 0, to: 0 }] };
  try {
    return { ast: new Parser(tokens).parse(), problems: [] };
  } catch (error) {
    if (error instanceof SyntaxError) return { problems: [{ code: 'InvalidExpression', message: error.message, from: error.token.from, to: error.token.to }] };
    throw error;
  }
}

/** The canonical text of a leaf — what the server names as a fact's anchor. */
export function printLeaf(node: LeafAst): string {
  switch (node.kind) {
    case 'num': return node.text;
    case 'str': return `"${node.value}"`;
    case 'bool': return String(node.value);
    case 'null': return 'null';
    case 'param': return `@${node.name}`;
    case 'ident': return node.name;
    case 'member': return `${printLeaf(node.target)}.${node.name}`;
    case 'call': return `${printLeaf(node.target)}.${node.method}(${node.args.map(printLeaf).join(', ')})`;
    case 'lambda': return `${node.param} => ${printLeaf(node.body)}`;
    case 'unary': return `${node.op}${printLeaf(node.operand)}`;
    case 'binary': return `${printLeaf(node.left)} ${node.op} ${printLeaf(node.right)}`;
    default: return '';
  }
}
```

`expression/index.ts` re-exports everything public from the module's files; `src/index.ts` gets `export * from './expression/index.js';` under a `// The expression-leaf language.` comment. Check `test/api-surface.test.ts` and `test/dsl-exports.test.ts` — they pin the package's export list and will need the new names added.

- [ ] **Step 4: Run the tests**

Run: `pnpm -C ui/packages/rules-core exec vitest run test/expression-parse.test.ts test/api-surface.test.ts test/dsl-exports.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/src/contracts.ts ui/packages/rules-core/src/expression ui/packages/rules-core/src/index.ts ui/packages/rules-core/test
git commit -m "rules-core — expression contracts, leaf tokenizer and parser"
```

### Task 11: Scope — the model a leaf sees, from the catalog

**Files:**
- Create: `ui/packages/rules-core/src/expression/scope.ts`
- Test: `ui/packages/rules-core/test/expression-scope.test.ts`

**Interfaces:**
- Produces: `interface LeafScope { modelName: string; model: JsonSchema; parameters: Record<string, { type: 'integer' | 'number' | 'string' | 'boolean' }>; vars: Record<string, { schema: JsonSchema; of: string }> }`; `scopeAt(document: RuleDocument, path: string, modelType: string, catalog: Catalog): LeafScope | null` — the root model's schema from `catalog.modelTypes[modelType]`, or for a path inside a higher-order node the element schema reached by walking that node's `path` (dot-separated) through `properties` to an `items`; `parameters` from `document.parameters`; null when the catalog has no schema. `withVar(scope, name, schema, of): LeafScope`. `fieldsOf(schema): Array<{ name; schema }>`, `isCollection(schema)`, `elementOf(schema): JsonSchema | undefined`, `typeName(schema): string` (`'int' | 'long' | 'float' | 'double' | 'decimal'` from `format`, else `'integer' | 'number' | 'string' | 'boolean' | 'collection of …' | 'object'`), `isNullable(schema)` (a `type` array containing `'null'`).

- [ ] **Step 1: Write the failing test**

```ts
// ui/packages/rules-core/test/expression-scope.test.ts
import { describe, it, expect } from 'vitest';
import { scopeAt, typeName, withVar } from '../src/expression/index.js';
import type { Catalog, JsonSchema, RuleDocument } from '../src/index.js';

const order: JsonSchema = { type: 'object', properties: { status: { type: 'string' }, total: { type: 'number', format: 'decimal' } } };
const customer: JsonSchema = {
  type: 'object',
  properties: { age: { type: 'integer', format: 'int32' }, country: { type: ['string', 'null'] }, orders: { type: ['array', 'null'], items: order } },
};
const catalog: Catalog = { specs: [], collections: [], modelTypes: { customer } };

const document: RuleDocument = {
  parameters: { vip: { type: 'number', default: 1000 } },
  rule: { and: [{ expression: 'age > 1' }, { asAllSatisfied: { expression: 'total > 1' }, path: 'orders' }] },
};

describe('scopeAt', () => {
  it('gives the root model at the root', () => {
    const scope = scopeAt(document, '$.rule.and[0]', 'customer', catalog)!;
    expect(scope.modelName).toBe('customer');
    expect(scope.model).toBe(customer);
    expect(scope.parameters).toEqual({ vip: { type: 'number' } });
  });

  it('gives the element inside a quantifier body', () => {
    const scope = scopeAt(document, '$.rule.and[1].asAllSatisfied', 'customer', catalog)!;
    expect(scope.modelName).toBe('each of orders');
    expect(scope.model).toBe(order);
  });

  it('returns null without a schema', () => {
    expect(scopeAt(document, '$.rule.and[0]', 'customer', { specs: [], collections: [] })).toBeNull();
  });

  it('adds a lambda variable without mutating the parent', () => {
    const root = scopeAt(document, '$.rule.and[0]', 'customer', catalog)!;
    const inner = withVar(root, 'o', order, 'orders');
    expect(inner.vars.o).toEqual({ schema: order, of: 'orders' });
    expect(root.vars).toEqual({});
  });
});

describe('typeName', () => {
  it.each([
    [{ type: 'integer', format: 'int32' }, 'int'],
    [{ type: 'number', format: 'decimal' }, 'decimal'],
    [{ type: 'number' }, 'number'],
    [{ type: ['string', 'null'] }, 'string'],
    [{ type: 'array', items: order }, 'collection of object'],
  ])('%j → %s', (schema, name) => {
    expect(typeName(schema as JsonSchema)).toBe(name);
  });
});
```

- [ ] **Step 2: Run it to see it fail**

Run: `pnpm -C ui/packages/rules-core exec vitest run test/expression-scope.test.ts`
Expected: FAIL — `scopeAt` is not exported.

- [ ] **Step 3: Implement**

```ts
// ui/packages/rules-core/src/expression/scope.ts
import type { Catalog, JsonSchema } from '../contracts.js';
import { getNode, isHigherOrderNode, type RuleDocument } from '../document.js';

export interface LeafScope {
  modelName: string;
  model: JsonSchema;
  parameters: Record<string, { type: 'integer' | 'number' | 'string' | 'boolean' }>;
  vars: Record<string, { schema: JsonSchema; of: string }>;
}

/** The JSON type of a schema, ignoring `null`. */
function primaryType(schema: JsonSchema | undefined): string | undefined {
  if (!schema) return undefined;
  const type = Array.isArray(schema.type) ? schema.type.find((t) => t !== 'null') : schema.type;
  return type ?? (schema.properties ? 'object' : undefined);
}

export function isNullable(schema: JsonSchema | undefined): boolean {
  return Array.isArray(schema?.type) && schema.type.includes('null');
}

export function isCollection(schema: JsonSchema | undefined): boolean {
  return primaryType(schema) === 'array';
}

export function elementOf(schema: JsonSchema | undefined): JsonSchema | undefined {
  return isCollection(schema) ? schema?.items : undefined;
}

export function fieldsOf(schema: JsonSchema | undefined): Array<{ name: string; schema: JsonSchema }> {
  return Object.entries(schema?.properties ?? {}).map(([name, s]) => ({ name, schema: s }));
}

const FORMAT_NAMES: Record<string, string> = { int32: 'int', int64: 'long', single: 'float', double: 'double', decimal: 'decimal' };

/** The name shown to authors: the CLR kind when the host stamped one, else the JSON type. */
export function typeName(schema: JsonSchema | undefined): string {
  if (!schema) return 'unknown';
  if (schema.format && FORMAT_NAMES[schema.format]) return FORMAT_NAMES[schema.format]!;
  const type = primaryType(schema);
  if (type === 'array') return `collection of ${typeName(schema.items)}`;
  return type ?? 'object';
}

export function withVar(scope: LeafScope, name: string, schema: JsonSchema, of: string): LeafScope {
  return { ...scope, vars: { ...scope.vars, [name]: { schema, of } } };
}

/** Every ancestor path of `path`, nearest first, including `path` itself. */
function ancestors(path: string): string[] {
  const out: string[] = [];
  for (let current = path; current.length > 1; ) {
    out.push(current);
    const cut = Math.max(current.lastIndexOf('.'), current.lastIndexOf('['));
    if (cut <= 0) break;
    current = current.slice(0, cut);
  }
  return out;
}

/**
 * The model a leaf at `path` sees: the rule's model, or — inside a quantifier body — the element
 * of the collection the quantifier walks, resolved through the schema by the node's `path`.
 */
export function scopeAt(document: RuleDocument, path: string, modelType: string, catalog: Catalog): LeafScope | null {
  const root = catalog.modelTypes?.[modelType];
  if (!root) return null;
  const parameters = Object.fromEntries(
    Object.entries(document.parameters ?? {}).map(([name, p]) => [name, { type: p.type }]),
  ) as LeafScope['parameters'];

  let model = root;
  let modelName = modelType;
  // Walk from the root down, so nested quantifiers each narrow the model in turn.
  for (const ancestor of ancestors(path).reverse()) {
    const node = getNode(document, ancestor);
    if (!node || !isHigherOrderNode(node)) continue;
    if (ancestor === path) break; // the quantifier itself is not inside its own body
    const collection = node.path.split('.').reduce<JsonSchema | undefined>((s, segment) => s?.properties?.[segment], model);
    const element = elementOf(collection);
    if (!element) return null;
    model = element;
    modelName = `each of ${node.path}`;
  }
  return { modelName, model, parameters, vars: {} };
}
```

`getNode` and `isHigherOrderNode` are exported from `document.js` already (`getNode` is used by `DslEditor`). If `getNode` lives in `paths.ts`, import from there. Paths inside a quantifier body look like `$.rule.and[1].asAllSatisfied` (the body *is* the value under the key), so the ancestor walk sees the quantifier node at `$.rule.and[1]` before reaching the body's own path.

- [ ] **Step 4: Run the tests**

Run: `pnpm -C ui/packages/rules-core exec vitest run test/expression-scope.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/src/expression ui/packages/rules-core/test/expression-scope.test.ts
git commit -m "rules-core — leaf scope from the catalog's model schemas"
```

### Task 12: Lattice and checker — the client's solver

**Files:**
- Create: `ui/packages/rules-core/src/expression/lattice.ts`, `ui/packages/rules-core/src/expression/check.ts`
- Test: `ui/packages/rules-core/test/expression-check.test.ts`

**Interfaces:**
- Produces (lattice): `type NumericKind = 'int32' | 'int64' | 'single' | 'double' | 'decimal'`; `kindOf(schema): NumericKind | undefined` (from `format`; a bare `integer` is `int32`, a bare `number` is `double`); `canWiden(from, to)`, `join(a, b): NumericKind | undefined`, `isIntegral(kind)`, `kindName(kind)` (`'int' | 'long' | 'float' | 'double' | 'decimal'`).
- Produces (check): `interface LeafFactLocal { node: LeafAst; type: string; from: string | null }`; `interface LeafAnalysis { problems: LeafProblem[]; types: Map<LeafAst, string>; facts: LeafFactLocal[]; valid: boolean }`; `checkLeaf(ast: LeafAst, scope: LeafScope): LeafAnalysis`; `analyseLeaf(text: string, scope: LeafScope): LeafAnalysis & { ast?: LeafAst }`. Type strings: `'int' | 'long' | 'float' | 'double' | 'decimal' | 'string' | 'bool' | 'null' | 'object' | 'collection'`, with a `?` suffix when nullable — the same strings the server's `DescribeType` produces, so the corpus can compare them.

The algorithm is the one in Task 5, transcribed: union-find type variables for `num` and numeric `param` nodes; anchors from fields (`kindOf`) and fixed-type methods; `join` at binary operators; fractional-vs-integral resolves to `decimal`; defaults `int32`/`decimal` with a warning; the same problem messages as the C# checker, word for word, so the corpus's message fragments hold on both sides.

- [ ] **Step 1: Write the failing test**

```ts
// ui/packages/rules-core/test/expression-check.test.ts
import { describe, it, expect } from 'vitest';
import { analyseLeaf, type LeafScope } from '../src/expression/index.js';
import type { JsonSchema } from '../src/index.js';

const order: JsonSchema = { type: 'object', properties: {
  status: { type: 'string', enum: ['paid', 'pending', 'refunded'] }, total: { type: 'number', format: 'decimal' }, daysSinceShipped: { type: 'integer', format: 'int32' },
} };
const customer: JsonSchema = { type: 'object', properties: {
  age: { type: 'integer', format: 'int32' }, points: { type: 'integer', format: 'int64' }, score: { type: 'number', format: 'double' },
  isActive: { type: 'boolean' }, creditLimit: { type: 'number', format: 'decimal' }, country: { type: ['string', 'null'] },
  orders: { type: ['array', 'null'], items: order }, shippedAt: { type: ['string', 'null'], format: 'date-time' },
} };
const scope: LeafScope = { modelName: 'customer', model: customer, parameters: { minAge: { type: 'integer' }, vip: { type: 'number' } }, vars: {} };

const check = (text: string) => analyseLeaf(text, scope);
const factFor = (text: string, literal: string) => check(text).facts.find((f) => f.node.kind !== 'binary' && (f.node as { text?: string; name?: string }).text === literal || (f.node as { name?: string }).name === literal)!;

describe('analyseLeaf', () => {
  it('types a literal from the field across the comparison', () => {
    expect(factFor('creditLimit > 1000', '1000')).toMatchObject({ type: 'decimal', from: 'creditLimit' });
    expect(check('creditLimit > 1000').valid).toBe(true);
  });

  it('types a whole untyped subtree from the other side', () => {
    const analysis = check('@vip * 2 > orders.sum(o => o.total)');
    expect(analysis.valid).toBe(true);
    expect(factFor('@vip * 2 > orders.sum(o => o.total)', 'vip').type).toBe('decimal');
    expect(factFor('@vip * 2 > orders.sum(o => o.total)', '2')).toMatchObject({ type: 'decimal', from: 'orders.sum(o => o.total)' });
  });

  it('widens an integer field when a fractional literal meets it', () => {
    expect(factFor('age * 1.5 > 40', '40').type).toBe('decimal');
  });

  it('refuses decimal against double', () => {
    const [problem] = check('creditLimit > score').problems;
    expect(problem).toMatchObject({ code: 'ExpressionTypeMismatch' });
    expect(problem!.message).toContain('decimal');
    expect(problem!.message).toContain('double');
  });

  it('refuses a number parameter against an integer anchor', () => {
    expect(check('orders.count() >= @vip').problems[0]!.code).toBe('ExpressionTypeMismatch');
  });

  it('defaults an unanchored subtree with a warning', () => {
    const analysis = check('1 + 1 > 1');
    expect(analysis.valid).toBe(true);
    expect(analysis.problems.every((p) => p.warning)).toBe(true);
    expect(factFor('1 + 1 > 1', '1')).toMatchObject({ type: 'int', from: null });
  });

  it('warns on integer division', () => {
    expect(check('age / 2 > 10').problems[0]!.message).toContain('truncates');
  });

  it.each([
    ['nope > 1', 'UnknownField', 'nope'],
    ['orders.total > 1', 'UnknownMethod', 'collection'],
    ['orders.sum(o => o.nope) > 1', 'UnknownField', 'nope'],
    ['orders.first() != null', 'UnknownMethod', 'first'],
    ['age.count() > 1', 'UnknownMethod', 'collection'],
    ['orders.where(o => o.total) > 1', 'ExpressionTypeMismatch', 'condition'],
    ['orders.sum(o => o.status) > 1', 'ExpressionTypeMismatch', 'number'],
    ['country == 3', 'ExpressionTypeMismatch', 'string'],
    ['age && isActive', 'ExpressionTypeMismatch', 'condition'],
    ['age + 1', 'ExpressionTypeMismatch', 'leaf must be a condition'],
    ['@nope > 1', 'UnknownField', 'parameter'],
    ['country == "SE"', 'ok', ''],
  ])('%s → %s', (text, code, fragment) => {
    const analysis = check(text);
    if (code === 'ok') { expect(analysis.valid).toBe(true); return; }
    const problem = analysis.problems.find((p) => !p.warning)!;
    expect(problem.code).toBe(code);
    expect(problem.message).toContain(fragment);
  });

  it('ranges an unknown lambda field at the name', () => {
    expect(check('orders.sum(o => o.nope) > 1').problems[0]).toMatchObject({ from: 18, to: 22 });
  });

  it('flags a string not in the enum', () => {
    const analysis = check('orders.any(o => o.status == "shipped")');
    expect(analysis.problems[0]!.message).toContain('"paid"');
  });

  it('lifts types through a nullable path', () => {
    const analysis = check('orders.sum(o => o.total) > 1');
    const root = analysis.ast!;
    expect(analysis.types.get(root)).toBe('bool');
    expect(analysis.types.get((root as { left: never }).left)).toBe('decimal?');
  });
});
```

- [ ] **Step 2: Run it to see it fail**

Run: `pnpm -C ui/packages/rules-core exec vitest run test/expression-check.test.ts`
Expected: FAIL — `analyseLeaf` is not exported.

- [ ] **Step 3: Implement**

```ts
// ui/packages/rules-core/src/expression/lattice.ts
import type { JsonSchema } from '../contracts.js';

export type NumericKind = 'int32' | 'int64' | 'single' | 'double' | 'decimal';

const WIDENINGS: ReadonlyArray<readonly [NumericKind, NumericKind]> = [
  ['int32', 'int64'], ['int32', 'decimal'], ['int64', 'decimal'], ['int32', 'double'], ['single', 'double'],
];

const NAMES: Record<NumericKind, string> = { int32: 'int', int64: 'long', single: 'float', double: 'double', decimal: 'decimal' };

export function kindName(kind: NumericKind): string { return NAMES[kind]; }
export function isIntegral(kind: NumericKind): boolean { return kind === 'int32' || kind === 'int64'; }

/** The CLR numeric kind of a schema: the host's `format` stamp, else the JSON type's conventional default. */
export function kindOf(schema: JsonSchema | undefined): NumericKind | undefined {
  if (!schema) return undefined;
  const format = schema.format;
  if (format === 'int32' || format === 'int64' || format === 'single' || format === 'double' || format === 'decimal') return format;
  const type = Array.isArray(schema.type) ? schema.type.find((t) => t !== 'null') : schema.type;
  if (type === 'integer') return 'int32';
  if (type === 'number') return 'double';
  return undefined;
}

export function canWiden(from: NumericKind, to: NumericKind): boolean {
  return from === to || WIDENINGS.some(([f, t]) => f === from && t === to);
}

export function join(a: NumericKind, b: NumericKind): NumericKind | undefined {
  if (canWiden(a, b)) return b;
  if (canWiden(b, a)) return a;
  return undefined;
}
```

```ts
// ui/packages/rules-core/src/expression/check.ts
import type { RuleErrorCode } from '../contracts.js';
import type { JsonSchema } from '../contracts.js';
import { canWiden, isIntegral, join, kindName, kindOf, type NumericKind } from './lattice.js';
import { parseLeaf, printLeaf, type LeafAst } from './parse.js';
import { elementOf, fieldsOf, isCollection, isNullable, typeName, withVar, type LeafScope } from './scope.js';
import type { LeafProblem } from './tokenize.js';

const COLLECTION_METHODS = ['where', 'any', 'all', 'count', 'sum', 'min', 'max'];

/** A type during checking: a resolved schema-ish description, or a numeric type variable. */
interface TypeVar { parent?: TypeVar; fractional: boolean; paramKind?: 'integer' | 'number'; resolved?: NumericKind; resolvedBy?: string; members: LeafAst[] }
type Known = { kind: 'bool' | 'string' | 'null' | 'object'; nullable: boolean; schema?: JsonSchema }
  | { kind: 'numeric'; numeric: NumericKind; nullable: boolean; schema?: JsonSchema }
  | { kind: 'collection'; nullable: boolean; schema: JsonSchema };
type LeafType = { known: Known } | { var: TypeVar } | { unknown: true };

export interface LeafFactLocal { node: LeafAst; type: string; from: string | null }
export interface LeafAnalysis { problems: LeafProblem[]; types: Map<LeafAst, string>; facts: LeafFactLocal[]; valid: boolean }

function root(v: TypeVar): TypeVar { return v.parent ? root(v.parent) : v; }
function allows(v: TypeVar, kind: NumericKind): boolean {
  if (v.fractional && isIntegral(kind)) return false;
  if (v.paramKind === 'integer') return kind === 'int32' || kind === 'int64' || kind === 'decimal';
  if (v.paramKind === 'number') return kind === 'decimal' || kind === 'double' || kind === 'single';
  return true;
}

function describe(t: Known): string {
  const base = t.kind === 'numeric' ? kindName(t.numeric)
    : t.kind === 'bool' ? 'a condition'
    : t.kind === 'collection' ? `a collection of ${typeName(t.schema.items)}`
    : t.kind;
  return base;
}

/** The type string the corpus compares: `decimal`, `bool`, `string?`, `collection`… */
function typeString(t: Known): string {
  const base = t.kind === 'numeric' ? kindName(t.numeric) : t.kind;
  return t.nullable && t.kind !== 'null' ? `${base}?` : base;
}

function fromSchema(schema: JsonSchema, throughNullable = false): Known {
  const nullable = throughNullable || isNullable(schema);
  if (isCollection(schema)) return { kind: 'collection', nullable, schema };
  const numeric = kindOf(schema);
  if (numeric) return { kind: 'numeric', numeric, nullable, schema };
  const type = Array.isArray(schema.type) ? schema.type.find((x) => x !== 'null') : schema.type;
  if (type === 'boolean') return { kind: 'bool', nullable, schema };
  if (type === 'string') return { kind: 'string', nullable, schema };
  return { kind: 'object', nullable, schema };
}

class Checker {
  readonly problems: LeafProblem[] = [];
  readonly types = new Map<LeafAst, LeafType>();
  readonly vars: TypeVar[] = [];

  constructor(private readonly scope: LeafScope) {}

  private report(at: { from: number; to: number }, code: RuleErrorCode, message: string, warning = false): void {
    this.problems.push({ code, message, from: at.from, to: at.to, ...(warning ? { warning: true as const } : {}) });
  }
  private set(node: LeafAst, type: LeafType): LeafType { this.types.set(node, type); return type; }
  private fail(node: LeafAst, code: RuleErrorCode, message: string, at: { from: number; to: number } = node): LeafType {
    this.report(at, code, message);
    return this.set(node, { unknown: true });
  }
  private newVar(node: LeafAst, fractional: boolean, paramKind?: 'integer' | 'number'): LeafType {
    const v: TypeVar = { fractional, members: [node], ...(paramKind ? { paramKind } : {}) };
    this.vars.push(v);
    return this.set(node, { var: v });
  }
  private known(t: LeafType): Known | undefined { return 'known' in t ? t.known : 'var' in t && root(t.var).resolved ? { kind: 'numeric', numeric: root(t.var).resolved!, nullable: false } : undefined; }

  visit(node: LeafAst, scope: LeafScope): LeafType {
    switch (node.kind) {
      case 'num': return this.newVar(node, node.text.includes('.'));
      case 'str': return this.set(node, { known: { kind: 'string', nullable: false } });
      case 'bool': return this.set(node, { known: { kind: 'bool', nullable: false } });
      case 'null': return this.set(node, { known: { kind: 'null', nullable: true } });
      case 'param': {
        const p = scope.parameters[node.name];
        if (!p) return this.fail(node, 'UnknownField', `unknown parameter '@${node.name}'`);
        if (p.type === 'integer' || p.type === 'number') return this.newVar(node, false, p.type);
        return this.set(node, { known: { kind: p.type === 'boolean' ? 'bool' : 'string', nullable: false } });
      }
      case 'ident': {
        const v = scope.vars[node.name];
        if (v) return this.set(node, { known: fromSchema(v.schema) });
        const field = scope.model.properties?.[node.name];
        if (!field) return this.fail(node, 'UnknownField', `'${node.name}' is not a field of ${scope.modelName}`);
        return this.set(node, { known: fromSchema(field) });
      }
      case 'member': {
        const target = this.known(this.visit(node.target, scope));
        if (!target) return this.set(node, { unknown: true });
        const at = { from: node.nameFrom, to: node.nameTo };
        if (target.kind === 'collection') return this.fail(node, 'UnknownMethod', `'${printLeaf(node.target)}' is a collection — use .where(…), .any(…), .all(…), .count(), .sum(…), .min(…) or .max(…) on it`, at);
        if (target.kind !== 'object') return this.fail(node, 'UnknownField', `'${node.name}' is not a field of ${describe(target)}`, at);
        const field = target.schema?.properties?.[node.name];
        if (!field) return this.fail(node, 'UnknownField', `'${node.name}' is not a field of ${typeName(target.schema)}`, at);
        return this.set(node, { known: fromSchema(field, target.nullable) });
      }
      case 'call': return this.visitCall(node, scope);
      case 'lambda': return this.fail(node, 'InvalidExpression', 'a lambda is only allowed as a method argument');
      case 'unary': {
        const operand = this.visit(node.operand, scope);
        const k = this.known(operand);
        if (node.op === '!') {
          if (k && k.kind !== 'bool') this.report(node, 'ExpressionTypeMismatch', `'!' needs a condition; this is ${describe(k)}`);
          return this.set(node, { known: { kind: 'bool', nullable: false } });
        }
        if (k && k.kind !== 'numeric') this.report(node, 'ExpressionTypeMismatch', `'-' needs a number; this is ${describe(k)}`);
        return this.set(node, operand);
      }
      case 'binary': return this.visitBinary(node, scope);
      default: return this.set(node, { unknown: true });
    }
  }

  private visitCall(node: Extract<LeafAst, { kind: 'call' }>, scope: LeafScope): LeafType {
    const target = this.known(this.visit(node.target, scope));
    if (!target) return this.set(node, { unknown: true });
    const at = { from: node.methodFrom, to: node.methodTo };

    if (node.method === 'equalsIgnoreCase') {
      if (target.kind !== 'string') return this.fail(node, 'UnknownMethod', `'equalsIgnoreCase' needs a string; this is ${describe(target)}`, at);
      const arg = node.args[0];
      if (node.args.length !== 1 || !arg || arg.kind === 'lambda') return this.fail(node, 'UnknownMethod', "'equalsIgnoreCase' takes one string argument", at);
      const argType = this.known(this.visit(arg, scope));
      if (argType && argType.kind !== 'string') this.report(arg, 'ExpressionTypeMismatch', `'equalsIgnoreCase' expects a string; this is ${describe(argType)}`);
      return this.set(node, { known: { kind: 'bool', nullable: false } });
    }

    if (target.kind !== 'collection') return this.fail(node, 'UnknownMethod', `'.${node.method}()' needs a collection; this is ${describe(target)}`, at);
    if (!COLLECTION_METHODS.includes(node.method)) return this.fail(node, 'UnknownMethod', `unknown method '${node.method}'; the collection methods are where, any, all, count, sum, min, max`, at);
    const element = elementOf(target.schema)!;

    if (node.method === 'count') {
      if (node.args[0]) this.report(node.args[0], 'UnknownMethod', "'count()' takes no arguments — filter with .where(…) first");
      return this.set(node, { known: { kind: 'numeric', numeric: 'int32', nullable: target.nullable } });
    }

    const lambda = node.args[0];
    if (node.args.length !== 1 || !lambda || lambda.kind !== 'lambda') return this.fail(node, 'UnknownMethod', `'${node.method}' takes a lambda: ${node.method}(x => …)`, at);
    const inner = withVar(scope, lambda.param, element, printLeaf(node.target));
    this.set(lambda, { known: fromSchema(element) });
    const body = this.visit(lambda.body, inner);
    const bodyType = this.known(body);

    if (node.method === 'where' || node.method === 'any' || node.method === 'all') {
      if (bodyType && bodyType.kind !== 'bool') this.report(lambda.body, 'ExpressionTypeMismatch', `'${node.method}' expects a condition; this is ${describe(bodyType)}`);
      return this.set(node, node.method === 'where' ? { known: target } : { known: { kind: 'bool', nullable: target.nullable } });
    }
    if (bodyType && bodyType.kind !== 'numeric') {
      this.report(lambda.body, 'ExpressionTypeMismatch', `'${node.method}' expects a number; this is ${describe(bodyType)}`);
      return this.set(node, { unknown: true });
    }
    if ('var' in body) return this.set(node, body);
    return this.set(node, { known: { ...bodyType!, nullable: bodyType!.nullable || target.nullable } });
  }

  private visitBinary(node: Extract<LeafAst, { kind: 'binary' }>, scope: LeafScope): LeafType {
    const left = this.visit(node.left, scope);
    const right = this.visit(node.right, scope);
    const bool: LeafType = { known: { kind: 'bool', nullable: false } };

    if (node.op === '&&' || node.op === '||') {
      for (const [side, type] of [[node.left, left], [node.right, right]] as const) {
        const k = this.known(type);
        if (k && k.kind !== 'bool') this.report(side, 'ExpressionTypeMismatch', `'${node.op}' needs conditions on both sides; this is ${describe(k)}`);
        else if ('var' in type) this.report(side, 'ExpressionTypeMismatch', `'${node.op}' needs conditions on both sides; this is a number`);
      }
      return this.set(node, bool);
    }

    if (node.op === '==' || node.op === '!=') {
      if (node.left.kind === 'null' || node.right.kind === 'null') {
        const [other, otherNode] = node.left.kind === 'null' ? [right, node.right] : [left, node.left];
        const k = this.known(other);
        if ('var' in other || (k && !k.nullable)) this.report(otherNode, 'ExpressionTypeMismatch', `'${printLeaf(otherNode)}' is never null`, true);
        return this.set(node, bool);
      }
      const l = this.known(left); const r = this.known(right);
      if (l && r && l.kind !== 'numeric' && r.kind !== 'numeric') {
        if (l.kind !== r.kind) this.report(node, 'ExpressionTypeMismatch', `comparing ${describe(l)} with ${describe(r)}`);
        else if (l.schema?.enum && node.right.kind === 'str' && !l.schema.enum.includes(node.right.value)) {
          this.report(node.right, 'ExpressionTypeMismatch', `not one of ${l.schema.enum.map((e) => `"${String(e)}"`).join(', ')}`);
        }
        return this.set(node, bool);
      }
      if ((l && l.kind !== 'numeric') || (r && r.kind !== 'numeric')) {
        const nonNumeric = l && l.kind !== 'numeric' ? l : r!;
        this.report(node, 'ExpressionTypeMismatch', `comparing ${describe(nonNumeric)} with a number`);
        return this.set(node, bool);
      }
      this.unify(node, left, right, false);
      return this.set(node, bool);
    }

    if (['<', '<=', '>', '>='].includes(node.op)) {
      this.unify(node, left, right, false);
      return this.set(node, bool);
    }

    const result = this.unify(node, left, right, true);
    const k = this.known(result);
    const integral = (k && k.kind === 'numeric' && isIntegral(k.numeric)) || ('var' in result && !root(result.var).resolved && !root(result.var).fractional);
    if (node.op === '/' && integral) this.report(node, 'ExpressionTypeMismatch', 'integer division truncates; compare against a fractional value to keep the remainder', true);
    return this.set(node, result);
  }

  private unify(node: Extract<LeafAst, { kind: 'binary' }>, left: LeafType, right: LeafType, arithmetic: boolean): LeafType {
    if ('unknown' in left || 'unknown' in right) return { unknown: true };
    const what = arithmetic ? `'${node.op}' needs numbers` : `'${node.op}' compares numbers`;
    for (const [side, type] of [[node.left, left], [node.right, right]] as const) {
      const k = 'known' in type ? type.known : undefined;
      if (k && k.kind !== 'numeric') { this.report(side, 'ExpressionTypeMismatch', `${what}; this is ${describe(k)}`); return { unknown: true }; }
    }
    const nullable = ('known' in left && left.known.nullable) || ('known' in right && right.known.nullable);

    if ('known' in left && 'known' in right) {
      const lk = (left.known as { numeric: NumericKind }).numeric; const rk = (right.known as { numeric: NumericKind }).numeric;
      const joined = join(lk, rk);
      if (!joined) { this.report(node, 'ExpressionTypeMismatch', `cannot compare ${kindName(lk)} with ${kindName(rk)} without losing precision; use a registered spec for this comparison`); return { unknown: true }; }
      return { known: { kind: 'numeric', numeric: joined, nullable } };
    }

    if ('var' in left && 'var' in right) {
      const a = root(left.var); const c = root(right.var);
      if (a !== c) {
        if (a.resolved && c.resolved) {
          const joined = join(a.resolved, c.resolved);
          if (!joined) { this.report(node, 'ExpressionTypeMismatch', `cannot combine ${kindName(a.resolved)} with ${kindName(c.resolved)}`); return { unknown: true }; }
          a.resolved = joined;
        } else {
          a.resolved ??= c.resolved;
          a.resolvedBy ??= c.resolvedBy;
        }
        a.fractional ||= c.fractional;
        a.paramKind ??= c.paramKind;
        a.members.push(...c.members);
        c.parent = a;
      }
      return { var: a };
    }

    const [v, concrete, anchor] = 'var' in left
      ? [root(left.var), (right as { known: { numeric: NumericKind } }).known.numeric, node.right]
      : [root((right as { var: TypeVar }).var), (left as { known: { numeric: NumericKind } }).known.numeric, node.left];
    let target = concrete;
    if (v.fractional && isIntegral(concrete)) target = 'decimal';
    if (!allows(v, target) || (v.resolved && !join(v.resolved, target))) {
      const have = v.resolved ? kindName(v.resolved) : v.paramKind === 'number' ? 'a number parameter' : 'this literal';
      this.report(node, 'ExpressionTypeMismatch', `cannot use ${have} with ${kindName(concrete)} without losing precision`);
      return { unknown: true };
    }
    v.resolved = v.resolved ? join(v.resolved, target)! : target;
    v.resolvedBy ??= printLeaf(anchor);
    return { known: { kind: 'numeric', numeric: v.resolved, nullable } };
  }

  finish(ast: LeafAst): LeafAnalysis {
    for (const v of new Set(this.vars.map(root))) {
      if (v.resolved) continue;
      v.resolved = v.fractional || v.paramKind === 'number' ? 'decimal' : 'int32';
      const first = v.members[0]!;
      this.report(first, 'ExpressionTypeMismatch', `no model field fixes the type of this expression; assuming ${kindName(v.resolved)}`, true);
    }
    const rootType = this.known(this.types.get(ast)!);
    if (rootType && rootType.kind !== 'bool') this.report(ast, 'ExpressionTypeMismatch', `a leaf must be a condition; this is ${describe(rootType)}`);

    const types = new Map<LeafAst, string>();
    const facts: LeafFactLocal[] = [];
    for (const [node, type] of this.types) {
      const k = this.known(type);
      if (!k) continue;
      types.set(node, typeString(k));
      if ((node.kind === 'num' || node.kind === 'param') && 'var' in type) facts.push({ node, type: kindName(root(type.var).resolved!), from: root(type.var).resolvedBy ?? null });
    }
    if (types.has(ast)) facts.push({ node: ast, type: types.get(ast)!, from: null });
    return { problems: this.problems, types, facts, valid: this.problems.every((p) => p.warning) };
  }
}

export function checkLeaf(ast: LeafAst, scope: LeafScope): LeafAnalysis {
  const checker = new Checker(scope);
  checker.visit(ast, scope);
  return checker.finish(ast);
}

/** Parse then check; a parse problem yields an invalid analysis with that one problem. */
export function analyseLeaf(text: string, scope: LeafScope): LeafAnalysis & { ast?: LeafAst } {
  const { ast, problems } = parseLeaf(text);
  if (!ast) return { problems, types: new Map(), facts: [], valid: false };
  return { ast, ...checkLeaf(ast, scope) };
}
```

`canWiden` is imported for parity with the C# side; if unused after typecheck, remove the import. Where `this.known(type)` on a var with no resolution returns `undefined`, callers treat it as "not yet known", which is the union-find's deferred state; the `finish` defaulting pass fills it in.

- [ ] **Step 4: Run the tests and typecheck**

Run: `pnpm -C ui/packages/rules-core exec vitest run test/expression-check.test.ts && pnpm -C ui/packages/rules-core typecheck`
Expected: PASS, no type errors (`exactOptionalPropertyTypes` is on: spread optional fields conditionally, as the code above does).

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/src/expression ui/packages/rules-core/test/expression-check.test.ts
git commit -m "rules-core — leaf lattice and checker: the client's solver, same messages as the server"
```

### Task 13: Completion inside a leaf, and DSL delegation

**Files:**
- Create: `ui/packages/rules-core/src/expression/complete.ts`
- Modify: `ui/packages/rules-core/src/dsl/completion.ts` (`completeDsl` delegates into a backtick), `ui/packages/rules-core/src/dsl/completion.ts` `CompletionItemKind` gains `'field' | 'method' | 'variable'`
- Test: `ui/packages/rules-core/test/expression-complete.test.ts`, extend `test/dsl-completion.test.ts`

**Interfaces:**
- Produces: `completeLeaf(text: string, cursor: number, scope: LeafScope): DslCompletion | null` — after `.` on a collection receiver: the seven methods, each with `detail` (`'collection'`, `'bool'`, `'int'`, `'number'`) and an `insert` template (`where(o => o.)`, `count()`), where `o` is the first letter of the receiver's last segment; after `.` on an object: its fields with `typeName` details; after `@`: parameters; at a bare word: root fields, lambda variables in scope, parameters. `DslCompletion.options[].insert?: string` and `caretOffset?: number` (where to leave the caret inside `insert`, counted from its start) are added to `CompletionItem`. `completeDsl(text, cursor, catalog, leafScope?: (path) => LeafScope | null)` — a fourth optional argument: when the cursor is inside a backtick, `completeDsl` parses the document, finds the expression node whose span covers the cursor, asks `leafScope(path)` for its scope, and returns `completeLeaf` over the leaf text with offsets shifted into the document.
- Lambda variables in scope at the cursor are recovered textually: every `.<method>(<v> =>` before the cursor whose closing paren is after the cursor binds `<v>` to the element of the receiver chain before that `.`, typed through the schema (the prototype's `lambdaVarsBefore`).

- [ ] **Step 1: Write the failing test**

```ts
// ui/packages/rules-core/test/expression-complete.test.ts
import { describe, it, expect } from 'vitest';
import { completeLeaf, type LeafScope } from '../src/expression/index.js';
import type { JsonSchema } from '../src/index.js';

const order: JsonSchema = { type: 'object', properties: { status: { type: 'string' }, total: { type: 'number', format: 'decimal' } } };
const customer: JsonSchema = { type: 'object', properties: { age: { type: 'integer', format: 'int32' }, orders: { type: 'array', items: order } } };
const scope: LeafScope = { modelName: 'customer', model: customer, parameters: { vip: { type: 'number' } }, vars: {} };
const atEnd = (text: string) => completeLeaf(text, text.length, scope);

describe('completeLeaf', () => {
  it('offers root fields, variables and parameters at a bare word', () => {
    expect(atEnd('a')!.options.map((o) => o.label)).toEqual(['age']);
    expect(atEnd('')!.options.map((o) => o.label)).toEqual(['age', 'orders', '@vip']);
  });

  it('offers methods with insert templates on a collection', () => {
    const result = atEnd('orders.')!;
    expect(result.from).toBe(7);
    expect(result.options.map((o) => o.label)).toEqual(['where', 'any', 'all', 'count', 'sum', 'min', 'max']);
    expect(result.options[0]).toMatchObject({ kind: 'method', insert: 'where(o => o.)', caretOffset: 13 });
  });

  it('offers element fields after a lambda variable', () => {
    const result = atEnd('orders.where(o => o.')!;
    expect(result.options.map((o) => o.label)).toEqual(['status', 'total']);
    expect(result.options[1]!.detail).toBe('decimal');
  });

  it('keeps the parent in scope inside a lambda', () => {
    expect(atEnd('orders.where(o => o.total > a')!.options.map((o) => o.label)).toEqual(['age']);
    expect(atEnd('orders.where(o => o.total > ')!.options.map((o) => o.label)).toEqual(['o', 'age', 'orders', '@vip']);
  });

  it('types the receiver through a where', () => {
    expect(atEnd('orders.where(o => o.total > 1).')!.options.map((o) => o.label)).toContain('sum');
  });

  it('offers parameters after @', () => {
    expect(atEnd('age > @')!.options.map((o) => o.label)).toEqual(['@vip']);
  });

  it('returns null with nothing to offer', () => {
    expect(atEnd('age.')).toBeNull();
  });
});
```

And in `test/dsl-completion.test.ts`, add a describe block:

```ts
describe('completeDsl inside a backtick', () => {
  const order: JsonSchema = { type: 'object', properties: { total: { type: 'number', format: 'decimal' } } };
  const customer: JsonSchema = { type: 'object', properties: { age: { type: 'integer' }, orders: { type: 'array', items: order } } };
  const leafCatalog: Catalog = { ...catalog, modelTypes: { customer } };
  const scopeFor = (text: string, cursor: number) =>
    completeDsl(text, cursor, leafCatalog, (path) => scopeAt(parse(text).document!, path, 'customer', leafCatalog));

  it('completes fields inside a leaf', () => {
    const text = 'is-active & `ag';
    expect(scopeFor(text, text.length)!.options.map((o) => o.label)).toEqual(['age']);
    expect(scopeFor(text, text.length)!.from).toBe(13);
  });

  it('completes the element inside a quantifier body', () => {
    const text = 'all in orders { `tot';
    expect(scopeFor(text, text.length)!.options.map((o) => o.label)).toEqual(['total']);
  });

  it('completes DSL outside a backtick as before', () => {
    expect(scopeFor('is-', 3)!.options.map((o) => o.label)).toContain('is-active');
  });
});
```

(`import { parse } from '../src/dsl/parser.js'; import { scopeAt } from '../src/expression/index.js'; import type { JsonSchema } from '../src/contracts.js';`.)

- [ ] **Step 2: Run it to see it fail**

Run: `pnpm -C ui/packages/rules-core exec vitest run test/expression-complete.test.ts test/dsl-completion.test.ts`
Expected: FAIL — `completeLeaf` not exported; `completeDsl` ignores the fourth argument.

- [ ] **Step 3: Implement**

```ts
// ui/packages/rules-core/src/expression/complete.ts
import type { JsonSchema } from '../contracts.js';
import type { CompletionItem, DslCompletion } from '../dsl/completion.js';
import { elementOf, fieldsOf, isCollection, typeName, withVar, type LeafScope } from './scope.js';

const METHODS = ['where', 'any', 'all', 'count', 'sum', 'min', 'max'] as const;

/** `orders` → `o`: the variable name a template introduces for elements of a collection. */
export function elementVar(receiver: string): string {
  const last = receiver.split('.').pop() ?? 'x';
  return last[0]?.toLowerCase() ?? 'x';
}

/** Types `a.b.where(...).sum(...)`-shaped text; undefined when any step is unknown. */
function typeChain(chain: string, scope: LeafScope): JsonSchema | undefined {
  const segments: string[] = [];
  let depth = 0; let current = '';
  for (const c of chain) {
    if (c === '(') depth++;
    if (c === ')') depth--;
    if (c === '.' && depth === 0) { segments.push(current); current = ''; continue; }
    current += c;
  }
  segments.push(current);
  let schema: JsonSchema | undefined;
  segments.forEach((segment, index) => {
    const name = segment.replace(/\(.*$/s, '');
    const isCall = segment.includes('(');
    if (index === 0) { schema = scope.vars[name]?.schema ?? scope.model.properties?.[name]; return; }
    if (!schema) return;
    if (isCall) {
      if (!isCollection(schema) || !(METHODS as readonly string[]).includes(name)) { schema = undefined; return; }
      schema = name === 'where' ? schema : name === 'any' || name === 'all' ? { type: 'boolean' } : name === 'count' ? { type: 'integer', format: 'int32' } : { type: 'number' };
      return;
    }
    schema = schema.properties?.[name];
  });
  return schema;
}

/** The receiver chain ending just before `end` (the index of a `.`), walking back over balanced parens. */
function chainBefore(text: string, end: number): string {
  let i = end; let depth = 0;
  while (i > 0) {
    const c = text[i - 1]!;
    if (c === ')') { depth++; i--; continue; }
    if (c === '(') { if (depth === 0) break; depth--; i--; continue; }
    if (depth > 0 || /[A-Za-z0-9_.@]/.test(c)) { i--; continue; }
    break;
  }
  return text.slice(i, end);
}

/** Lambda variables bound before `pos`, each typed as the element of its receiver. */
function varsBefore(text: string, pos: number, scope: LeafScope): LeafScope {
  let scoped = scope;
  const head = text.slice(0, pos);
  for (const match of head.matchAll(/\.(where|any|all|count|sum|min|max)\(\s*([A-Za-z_]\w*)\s*=>/g)) {
    const receiver = chainBefore(head, match.index);
    const element = elementOf(typeChain(receiver, scoped));
    if (element) scoped = withVar(scoped, match[2]!, element, receiver);
  }
  return scoped;
}

function prefixed(options: CompletionItem[], prefix: string, from: number): DslCompletion | null {
  const lower = prefix.toLowerCase();
  const matching = options.filter((o) => o.label.toLowerCase().startsWith(lower));
  if (matching.length === 0) return null;
  return { from, options: matching, isValidFor: (word) => word.toLowerCase().startsWith(lower) };
}

/** What can be typed at `cursor` in a leaf. */
export function completeLeaf(text: string, cursor: number, scope: LeafScope): DslCompletion | null {
  const head = text.slice(0, cursor);
  const scoped = varsBefore(text, cursor, scope);

  const param = /@(\w*)$/.exec(head);
  if (param) {
    const options = Object.entries(scope.parameters).map(([name, p]): CompletionItem => ({ label: `@${name}`, kind: 'parameter', detail: p.type }));
    return prefixed(options, param[0], cursor - param[0].length);
  }

  const member = /\.([A-Za-z_]\w*)?$/.exec(head);
  if (member) {
    const dot = cursor - member[0].length;
    const receiver = chainBefore(head, dot);
    const schema = typeChain(receiver, scoped);
    if (!schema) return null;
    if (isCollection(schema)) {
      const v = elementVar(receiver);
      const options = METHODS.map((m): CompletionItem => {
        const insert = m === 'count' ? 'count()' : `${m}(${v} => ${v}.)`;
        const detail = m === 'where' ? 'collection' : m === 'any' || m === 'all' ? 'bool' : m === 'count' ? 'int' : 'number';
        return { label: m, kind: 'method', detail, insert, caretOffset: insert.length - 1 };
      });
      return prefixed(options, member[1] ?? '', dot + 1);
    }
    const options = fieldsOf(schema).map((f): CompletionItem => ({ label: f.name, kind: isCollection(f.schema) ? 'collection' : 'field', detail: typeName(f.schema) }));
    return prefixed(options, member[1] ?? '', dot + 1);
  }

  const word = /(?:^|[^\w.@])([A-Za-z_]\w*)?$/.exec(head);
  if (!word) return null;
  const prefix = word[1] ?? '';
  const options: CompletionItem[] = [
    ...Object.entries(scoped.vars).map(([name, v]): CompletionItem => ({ label: name, kind: 'variable', detail: `element of ${v.of}` })),
    ...fieldsOf(scope.model).map((f): CompletionItem => ({ label: f.name, kind: isCollection(f.schema) ? 'collection' : 'field', detail: typeName(f.schema) })),
    ...Object.entries(scope.parameters).map(([name, p]): CompletionItem => ({ label: `@${name}`, kind: 'parameter', detail: p.type })),
  ];
  return prefixed(options, prefix, cursor - prefix.length);
}
```

In `dsl/completion.ts`: add `'field' | 'method' | 'variable'` to `CompletionItemKind`; add to `CompletionItem`:

```ts
  /** Text to insert instead of `label`, e.g. a method template `where(o => o.)`. */
  insert?: string;
  /** Where to leave the caret inside `insert`, from its start; absent means at its end. */
  caretOffset?: number;
```

and change `completeDsl`:

```ts
export function completeDsl(
  text: string,
  cursor: number,
  catalog: Catalog,
  leafScope?: (path: string) => LeafScope | null,
): DslCompletion | null {
  const leaf = leafScope ? leafAt(text, cursor, leafScope) : null;
  if (leaf) {
    const inner = completeLeaf(text.slice(leaf.from, leaf.to), cursor - leaf.from, leaf.scope);
    return inner ? { ...inner, from: inner.from + leaf.from } : null;
  }
  // …existing body unchanged…
}

/** The expression leaf whose text covers `cursor`, with its scope; null outside every backtick. */
function leafAt(text: string, cursor: number, leafScope: (path: string) => LeafScope | null): { from: number; to: number; scope: LeafScope } | null {
  const result = parse(text);
  for (const span of result.spans) {
    const node = result.document ? getNode(result.document, span.path) : undefined;
    if (!node || !isExpressionNode(node)) continue;
    // The span covers the backticks; the leaf text sits one character inside each.
    const from = span.from + 1;
    const to = text[span.to - 1] === '`' && span.to - 1 > span.from ? span.to - 1 : span.to;
    if (cursor < from || cursor > to) continue;
    const scope = leafScope(span.path);
    return scope ? { from, to, scope } : null;
  }
  return null;
}
```

A syntax error elsewhere in the document leaves `result.document` undefined: in that case fall back to a textual search — the last unmatched backtick before the cursor opens the leaf, and its scope is `leafScope('$.rule')` — so completion keeps working while the author is mid-edit. Import `parse` from `./parser.js`, `getNode`/`isExpressionNode` from `../document.js` (or `../paths.js`), `completeLeaf`/`LeafScope` from `../expression/…` — watch for an import cycle (`expression/complete.ts` imports types from `dsl/completion.ts`); keep the type import `import type` so it is erased.

- [ ] **Step 4: Run the tests**

Run: `pnpm -C ui/packages/rules-core exec vitest run test/expression-complete.test.ts test/dsl-completion.test.ts && pnpm -C ui/packages/rules-core typecheck`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/src ui/packages/rules-core/test
git commit -m "rules-core — leaf completion; completeDsl delegates inside a backtick"
```

### Task 14: Diagnostics — leaf problems and backend ranges

**Files:**
- Modify: `ui/packages/rules-core/src/dsl/diagnostics.ts`
- Test: extend `ui/packages/rules-core/test/dsl-diagnostics.test.ts`

**Interfaces:**
- Produces: `diagnosticsFor(text, result, errors, leafScope?: (path: string) => LeafScope | null): RuleDiagnostic[]` — with `leafScope`, every `ExpressionNode` in `result.document` is analysed with `analyseLeaf` and its problems (errors *and* warnings) become diagnostics offset by `span.from + 1`; a backend error with `range` is placed at `span.from + 1 + range.start … + range.end` instead of the node's whole span. Backend errors and client problems at the same range and code are de-duplicated in favour of the backend's message.

- [ ] **Step 1: Write the failing test**

Append to `test/dsl-diagnostics.test.ts`:

```ts
describe('diagnosticsFor with leaves', () => {
  const order: JsonSchema = { type: 'object', properties: { total: { type: 'number', format: 'decimal' } } };
  const customer: JsonSchema = { type: 'object', properties: { age: { type: 'integer', format: 'int32' }, orders: { type: 'array', items: order } } };
  const catalog: Catalog = { specs: [], collections: [], modelTypes: { customer } };
  const scopeFor = (text: string) => (path: string) => scopeAt(parse(text).document!, path, 'customer', catalog);

  it('reports a leaf problem at its offset in the document', () => {
    const text = 'is-active & `orders.sum(o => o.nope) > 1`';
    const diagnostics = diagnosticsFor(text, parse(text), [], scopeFor(text));
    expect(diagnostics).toEqual([expect.objectContaining({ code: 'UnknownField', from: 13 + 18, to: 13 + 22, severity: 'error' })]);
  });

  it('reports a warning as a warning', () => {
    const text = '`1 + 1 > 1`';
    expect(diagnosticsFor(text, parse(text), [], scopeFor(text))[0]).toMatchObject({ severity: 'warning' });
  });

  it('places a ranged backend error inside the leaf and drops the matching client one', () => {
    const text = '`orders.sum(o => o.nope) > 1`';
    const backend: RuleError = { path: '$.rule', code: 'UnknownField', message: "server says 'nope' is not a field of Order", range: { start: 18, end: 22 } };
    const diagnostics = diagnosticsFor(text, parse(text), [backend], scopeFor(text));
    expect(diagnostics).toHaveLength(1);
    expect(diagnostics[0]).toMatchObject({ from: 19, to: 23, message: backend.message, path: '$.rule' });
  });
});
```

- [ ] **Step 2: Run it to see it fail**

Run: `pnpm -C ui/packages/rules-core exec vitest run test/dsl-diagnostics.test.ts`
Expected: FAIL — no leaf diagnostics; backend error covers the whole node.

- [ ] **Step 3: Implement**

In `diagnostics.ts`:

```ts
/** Leaf text starts one past the opening backtick; a closed leaf ends one before the closing one. */
function leafOffset(span: NodeSpan): number { return span.from + 1; }

function fromBackendError(error: RuleError, spans: readonly NodeSpan[], documentLength: number): RuleDiagnostic {
  const range = rangeOfPath(error.path, spans, documentLength);
  const span = spans.find((s) => s.path === error.path);
  const placed = error.range && span
    ? { from: leafOffset(span) + error.range.start, to: leafOffset(span) + error.range.end }
    : range;
  return { ...nonEmpty(placed), severity: 'error', code: error.code, message: error.message, path: error.path };
}

function fromLeafProblems(text: string, result: ParseResult, leafScope: (path: string) => LeafScope | null): RuleDiagnostic[] {
  if (!result.document) return [];
  const out: RuleDiagnostic[] = [];
  for (const span of result.spans) {
    if (!isNodePath(span.path)) continue;
    const node = getNode(result.document, span.path);
    if (!node || !isExpressionNode(node)) continue;
    const scope = leafScope(span.path);
    if (!scope) continue;
    const offset = leafOffset(span);
    for (const problem of analyseLeaf(node.expression, scope).problems) {
      out.push({ ...nonEmpty({ from: offset + problem.from, to: offset + problem.to }), severity: problem.warning ? 'warning' : 'error', code: problem.code, message: problem.message, path: span.path });
    }
  }
  return out;
}

export function diagnosticsFor(text: string, result: ParseResult, errors: readonly RuleError[], leafScope?: (path: string) => LeafScope | null): RuleDiagnostic[] {
  const backend = errors.map((error) => fromBackendError(error, result.spans, text.length));
  const client = leafScope ? fromLeafProblems(text, result, leafScope) : [];
  const shadowed = (d: RuleDiagnostic) => backend.some((b) => b.from === d.from && b.to === d.to && b.code === d.code);
  return [
    ...result.errors.map(fromParserError),
    ...result.warnings.map(fromParserError),
    ...client.filter((d) => !shadowed(d)),
    ...backend,
  ];
}
```

`isNodePath` lives in `paths.ts` (DslEditor imports it from the package root). Verify the parser records an expression node's span *including* the backticks (`lexer.ts:94` pushes the token from the opening backtick through the closing one); if the span excludes them, drop the `+ 1`.

- [ ] **Step 4: Run the whole package suite**

Run: `pnpm -C ui/packages/rules-core test && pnpm -C ui/packages/rules-core typecheck && pnpm -C ui/packages/rules-core build`
Expected: PASS, and the build succeeds (Studio consumes `dist/`).

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/src ui/packages/rules-core/test
git commit -m "rules-core — leaf diagnostics and ranged backend errors"
```

---

## Phase 3 — Studio: the DSL pane as the expression editor

### Task 15: Nested leaf mode in the stream parser

**Files:**
- Modify: `ui/apps/studio/src/dsl/motivLanguage.ts`
- Test: extend `ui/apps/studio/test/dsl/motivLanguage.test.ts`

**Interfaces:**
- Produces: `motivStreamParser` becomes stateful: `StreamParser<MotivState>` with `startState`/`copyState`, `MotivState = { inLeaf: boolean; vars: Set<string>; prevDot: boolean }`. Inside a backtick, tokens tag as: fields `propertyName`, collection methods before `(` after a dot `variableName.function`, lambda parameters (a word followed by `=>`, and later uses of it) `variableName.local`, `@params` `variableName.special`, `null`/`true`/`false` `atom`, numbers `number`, strings `string`, operators `operator`, `.` `punctuation`, `( ) ,` `bracket`. The backticks themselves stay `string.special`. `motivHighlightStyle` gains `propertyName → var(--dsl-type)`, `function(variableName) → var(--dsl-keyword)`, `local(variableName) → var(--dsl-param)` italic, `atom → var(--dsl-keyword)`; the closing backtick resets `vars`.

- [ ] **Step 1: Write the failing test**

The existing `classify` helper in `motivLanguage.test.ts` creates the state via `startState?.(4)` and runs one line, so it already supports a stateful parser. Add:

```ts
describe('motivStreamParser inside a backtick', () => {
  it('colours fields, methods, lambda variables and literals', () => {
    expect(classify('`orders.where(o => o.status == "paid").sum(o => o.total) > @vip`')).toEqual([
      { text: '`', tag: 'string.special' },
      { text: 'orders', tag: 'propertyName' }, { text: '.', tag: 'punctuation' }, { text: 'where', tag: 'variableName.function' },
      { text: '(', tag: 'bracket' }, { text: 'o', tag: 'variableName.local' }, { text: '=>', tag: 'operator' },
      { text: 'o', tag: 'variableName.local' }, { text: '.', tag: 'punctuation' }, { text: 'status', tag: 'propertyName' },
      { text: '==', tag: 'operator' }, { text: '"paid"', tag: 'string' }, { text: ')', tag: 'bracket' },
      { text: '.', tag: 'punctuation' }, { text: 'sum', tag: 'variableName.function' }, { text: '(', tag: 'bracket' },
      { text: 'o', tag: 'variableName.local' }, { text: '=>', tag: 'operator' }, { text: 'o', tag: 'variableName.local' },
      { text: '.', tag: 'punctuation' }, { text: 'total', tag: 'propertyName' }, { text: ')', tag: 'bracket' },
      { text: '>', tag: 'operator' }, { text: '@vip', tag: 'variableName.special' }, { text: '`', tag: 'string.special' },
    ]);
  });

  it('tags null as an atom and leaves DSL tokens alone outside the backtick', () => {
    expect(classify('a & `x != null`').map((t) => t.tag)).toEqual([
      'variableName', 'operator', 'string.special', 'propertyName', 'operator', 'atom', 'string.special',
    ]);
  });

  it('forgets a lambda variable once its leaf closes', () => {
    const tokens = classify('`orders.any(o => o.total > 1)` & `o`');
    expect(tokens.at(-2)).toEqual({ text: 'o', tag: 'propertyName' });
  });
});
```

- [ ] **Step 2: Run it to see it fail**

Run: `pnpm -C ui/apps/studio exec vitest run test/dsl/motivLanguage.test.ts`
Expected: FAIL — the whole backtick is one `string.special` token.

- [ ] **Step 3: Implement**

Replace the `if (char === '`')` branch and the parser's shape:

```ts
export interface MotivState { inLeaf: boolean; vars: Set<string>; prevDot: boolean }

const LEAF_METHODS = new Set(['where', 'any', 'all', 'count', 'sum', 'min', 'max', 'equalsIgnoreCase']);
const LEAF_ATOMS = new Set(['null', 'true', 'false']);

/** Colours one token of a leaf. `prevDot` tells a field from a root name; `vars` remembers lambda parameters. */
function leafToken(stream: StringStream, state: MotivState): string | null {
  if (stream.eatSpace()) return null;
  if (stream.match(/^(=>|==|!=|<=|>=|&&|\|\|)/)) { state.prevDot = false; return 'operator'; }
  const char = stream.next();
  if (!char) return null;
  if ('<>!+-*/'.includes(char)) { state.prevDot = false; return 'operator'; }
  if (char === '.') { state.prevDot = true; return 'punctuation'; }
  if ('(),'.includes(char)) { state.prevDot = false; return 'bracket'; }
  if (char === '"') { skipDelimited(stream, '"'); state.prevDot = false; return 'string'; }
  if (char === '@') { stream.eatWhile(/\w/); state.prevDot = false; return 'variableName.special'; }
  if (DIGIT.test(char)) { stream.eatWhile(/[0-9.]/); state.prevDot = false; return 'number'; }
  if (/[A-Za-z_]/.test(char)) {
    stream.eatWhile(/\w/);
    const word = stream.current();
    const afterDot = state.prevDot;
    state.prevDot = false;
    if (LEAF_ATOMS.has(word)) return 'atom';
    if (stream.match(/^\s*=>/, false)) { state.vars.add(word); return 'variableName.local'; }
    if (afterDot && LEAF_METHODS.has(word) && stream.match(/^\s*\(/, false)) return 'variableName.function';
    if (!afterDot && state.vars.has(word)) return 'variableName.local';
    return 'propertyName';
  }
  return 'invalid';
}

export const motivStreamParser: StreamParser<MotivState> = {
  name: 'motiv',
  startState: () => ({ inLeaf: false, vars: new Set(), prevDot: false }),
  copyState: (s) => ({ inLeaf: s.inLeaf, vars: new Set(s.vars), prevDot: s.prevDot }),
  token(stream, state) {
    if (state.inLeaf) {
      if (stream.peek() === '`') { stream.next(); state.inLeaf = false; state.vars.clear(); return 'string.special'; }
      return leafToken(stream, state);
    }
    // …the existing DSL branches, with the backtick branch replaced by:
    if (char === '`') { state.inLeaf = true; state.prevDot = false; return 'string.special'; }
    // …
  },
};
```

and extend `motivHighlightStyle`:

```ts
  { tag: tags.propertyName, color: 'var(--dsl-type)' },
  { tag: tags.function(tags.variableName), color: 'var(--dsl-keyword)' },
  { tag: tags.local(tags.variableName), color: 'var(--dsl-param)', fontStyle: 'italic' },
  { tag: tags.atom, color: 'var(--dsl-keyword)' },
```

The a11y sweep checks colour contrast on every token colour it finds; these reuse existing `--dsl-*` tokens, all of which already clear AA on the editor ground.

- [ ] **Step 4: Run the tests**

Run: `pnpm -C ui/apps/studio exec vitest run test/dsl/motivLanguage.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ui/apps/studio/src/dsl/motivLanguage.ts ui/apps/studio/test/dsl/motivLanguage.test.ts
git commit -m "Studio — the DSL stream parser colours the inside of a backtick"
```

### Task 16: Completion, lint and hover facts in the DSL editor

**Files:**
- Modify: `ui/packages/rules-core/src/editor.ts` (state gains `facts`; `setErrors(errors, facts = [])`), `ui/packages/rules-core/src/validation.ts` (passes `response.facts ?? []`)
- Modify: `ui/apps/studio/src/dsl/completion.ts` (pass a leaf-scope resolver; honour `insert`/`caretOffset`; map new kinds), `ui/apps/studio/src/dsl/lint.ts` (pass the resolver through), `ui/apps/studio/src/dsl/hover.ts` (a facts tooltip), `ui/apps/studio/src/dsl/DslEditor.tsx` (a `modelType` prop; wire scope, diagnostics and hover), `ui/apps/studio/src/panes/EditorPane.tsx` (accept and pass `modelType`), `ui/apps/studio/src/panes/RuleDocument.tsx` (pass `MODEL_TYPE`), and the propositions page's editor host (find the other `EditorPane` call site with `grep -rn "<EditorPane" ui/apps/studio/src`)
- Test: `ui/apps/studio/test/dsl/completion.test.ts`, `ui/apps/studio/test/dsl/hover.test.ts`, `ui/apps/studio/test/dsl/lint.test.ts`, `ui/packages/rules-core/test/editor.test.ts`

**Interfaces:**
- Produces: `createMotivCompletion(getCatalog, getLeafScope?: () => ((path: string) => LeafScope | null) | undefined)`; `diagnosticsFor(text, result, errors, leafScope?)` in Studio's `lint.ts` forwards the resolver; `factTooltipSource(getFacts: () => readonly PlacedFact[])` and `motivHover(getDiagnostics, getFacts)` where `PlacedFact = RuleLeafFact & { from: number; to: number }` (document offsets); `placeFacts(facts: RuleLeafFact[], spans: NodeSpan[]): PlacedFact[]` (offset `span.from + 1 + range.start`) exported from `lint.ts`. `DslEditor` gains `modelType: string`. A leaf-scope resolver is built once per render: `(path) => scopeAt(store.getState().document, path, modelType, catalog)`.

- [ ] **Step 1: Write the failing tests**

`ui/packages/rules-core/test/editor.test.ts` — add:

```ts
it('keeps validation facts beside errors and clears both on load', () => {
  const store = new RuleEditorStore(); // use the file's existing construction helper
  const fact = { path: '$.rule', range: { start: 0, end: 4 }, text: '1000', type: 'decimal', from: 'total', isWarning: false, message: null };
  store.setErrors([], [fact]);
  expect(store.getState().facts).toEqual([fact]);
  store.loadDocument({ rule: { spec: 'a' } });
  expect(store.getState().facts).toEqual([]);
});
```

`ui/apps/studio/test/dsl/hover.test.ts` — add:

```ts
describe('factTooltipSource', () => {
  const fact = { path: '$.rule', range: { start: 14, end: 18 }, text: '1000', type: 'decimal', from: 'orders.sum(o => o.total)', isWarning: false, message: null, from: 15, to: 19 };
  it('describes the fact under the pointer', () => {
    const tooltip = factTooltipSource(() => [fact])(VIEW, 16, 1) as Tooltip;
    expect(tooltip.pos).toBe(15);
    const dom = tooltip.create(VIEW).dom;
    expect(dom.textContent).toContain('1000 as decimal');
    expect(dom.textContent).toContain('from orders.sum(o => o.total)');
  });
  it('is silent between facts', () => {
    expect(factTooltipSource(() => [fact])(VIEW, 3, 1)).toBeNull();
  });
});
```

(`fact` above has both `from` fields; give the placed-offset one precedence by naming the fact's anchor field `anchor` in `PlacedFact` if the collision reads badly — then adjust the type: `PlacedFact = Omit<RuleLeafFact, 'from'> & { anchor: string | null; from: number; to: number }`.)

`ui/apps/studio/test/dsl/completion.test.ts` — add a case that a `method` option with `insert: 'where(o => o.)'` and `caretOffset: 13` maps to a CodeMirror completion whose `apply` inserts the template and places the selection at `from + 13` (drive `apply` against a real `EditorView` as the file's other tests do, or against a `{ dispatch }` stub and assert the dispatched `changes`/`selection`).

- [ ] **Step 2: Run them to see them fail**

Run: `pnpm -C ui/packages/rules-core exec vitest run test/editor.test.ts && pnpm -C ui/apps/studio exec vitest run test/dsl`
Expected: FAIL on the new cases.

- [ ] **Step 3: Implement**

`editor.ts`: add `facts: RuleLeafFact[]` to the state, a `#facts` field, `setErrors(errors: RuleError[], facts: RuleLeafFact[] = [])` storing both, and clear `#facts` wherever `#errors` is cleared. `validation.ts`: `.then((response) => store.setErrors(response.errors, response.facts ?? []))`.

Studio `completion.ts`:

```ts
const CM_TYPE: Record<CompletionItemKind, string> = {
  spec: 'variable', collection: 'namespace', quantifier: 'keyword', keyword: 'keyword', type: 'type',
  parameter: 'constant', local: 'variable', field: 'property', method: 'method', variable: 'variable',
};

export function createMotivCompletion(
  getCatalog: () => Catalog,
  getLeafScope: () => ((path: string) => LeafScope | null) | undefined = () => undefined,
) {
  return (context: CompletionContext): CompletionResult | null => {
    const completion = completeDsl(context.state.doc.toString(), context.pos, getCatalog(), getLeafScope());
    if (!completion) return null;
    return {
      from: completion.from,
      options: completion.options.map((option): Completion => ({
        label: option.label,
        type: CM_TYPE[option.kind],
        ...(option.detail !== undefined ? { detail: option.detail } : {}),
        ...(option.boost !== undefined ? { boost: option.boost } : {}),
        ...(option.insert !== undefined ? {
          apply: (view, _completion, from, to) => {
            const insert = option.insert!;
            const caret = from + (option.caretOffset ?? insert.length);
            view.dispatch({ changes: { from, to, insert }, selection: { anchor: caret } });
          },
        } : {}),
      })),
      validFor: (text) => completion.isValidFor(text),
    };
  };
}
```

Studio `lint.ts`: thread `leafScope?` through to the package's `diagnosticsFor`, and add:

```ts
export type PlacedFact = Omit<RuleLeafFact, 'from'> & { anchor: string | null; from: number; to: number };

/** Facts arrive keyed by path and leaf-relative range; place them at document offsets through the parse's spans. */
export function placeFacts(facts: readonly RuleLeafFact[], spans: readonly NodeSpan[]): PlacedFact[] {
  return facts.flatMap((fact) => {
    const span = spans.find((s) => s.path === fact.path);
    if (!span) return [];
    const { from: anchor, ...rest } = fact;
    return [{ ...rest, anchor, from: span.from + 1 + fact.range.start, to: span.from + 1 + fact.range.end }];
  });
}
```

Studio `hover.ts`: add

```ts
export function renderFact(fact: PlacedFact): HTMLElement {
  const root = document.createElement('div');
  root.className = 'dsl-hover';
  if (fact.isWarning) {
    appendLine(root, 'div', 'dsl-hover-code', 'warning');
    appendLine(root, 'div', 'dsl-hover-message', fact.message ?? '');
  } else {
    appendLine(root, 'div', 'dsl-hover-message', `${fact.text} as ${fact.type}`);
    appendLine(root, 'div', 'dsl-hover-path', fact.anchor ? `from ${fact.anchor}` : 'default — no model field fixed this type');
  }
  return root;
}

export function factTooltipSource(getFacts: () => readonly PlacedFact[]): HoverTooltipSource {
  return (_view, pos) => {
    // Prefer the narrowest fact, so a literal wins over the whole-leaf result fact that also covers it.
    const fact = [...getFacts()].filter((f) => pos >= f.from && pos <= f.to).sort((a, b) => (a.to - a.from) - (b.to - b.from))[0];
    if (!fact) return null;
    return { pos: fact.from, end: fact.to, above: true, create: () => ({ dom: renderFact(fact) }) };
  };
}

export function motivHover(getDiagnostics: () => readonly Diagnostic[], getFacts: () => readonly PlacedFact[] = () => []): Extension {
  return [hoverTooltip(diagnosticTooltipSource(getDiagnostics)), hoverTooltip(factTooltipSource(getFacts))];
}
```

`DslEditor.tsx`: add `modelType: string` to the props; build `const leafScope = (path: string) => scopeAt(store.getState().document, path, modelType, catalog);` and put it in `LiveContext`; pass `() => live.current.leafScope` to `createMotivCompletion`; compute `diagnostics` with `diagnosticsFor(sync.text, sync.parseResult, editorState.errors, leafScope)`; compute `placedFacts = useMemo(() => placeFacts(editorState.facts, sync.parseResult.spans), …)` into `LiveContext`; `motivHover(() => live.current.diagnostics, () => live.current.placedFacts)`. `EditorPane` takes `modelType` and forwards it; both hosts pass their model type (`MODEL_TYPE` in `RuleDocument`; the propositions page's own model id).

- [ ] **Step 4: Run the suites**

Run: `pnpm -C ui/packages/rules-core test && pnpm -C ui/packages/rules-core build && pnpm -C ui/apps/studio test && pnpm -C ui/apps/studio typecheck`
Expected: PASS. `DslEditor.test.tsx` constructs the editor — add `modelType="customer"` to its render calls.

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/src ui/packages/rules-core/test ui/apps/studio/src ui/apps/studio/test
git commit -m "Studio — leaf completion, ranged lint and hover facts in the DSL editor"
```

### Task 17: The inspector strip under the DSL pane

**Files:**
- Create: `ui/apps/studio/src/dsl/LeafInspector.tsx`, `ui/apps/studio/src/panes/scenarioSelection.ts`
- Modify: `ui/apps/studio/src/dsl/DslEditor.tsx` (mount the inspector; expose the caret), `ui/apps/studio/src/panes/ScenarioPane.tsx` (publish the open scenario), `ui/apps/studio/src/panes/EditorPane.tsx` (pass `client` and `ruleName`), `ui/apps/studio/src/styles/app.css`
- Test: `ui/apps/studio/test/dsl/LeafInspector.test.tsx`, `ui/apps/studio/test/panes/scenarioSelection.test.ts`

**Interfaces:**
- Produces: `scenarioSelection.ts` — a tiny external store: `selectScenario(ruleName: string, scenario: { name: string; model: string } | null)`, `useSelectedScenario(ruleName): { name; model } | null` (via `useSyncExternalStore`). `ScenarioPane` calls `selectScenario(ruleName, row)` when a row's details open and `selectScenario(ruleName, null)` when they close; on unmount it clears.
- `LeafInspector` props: `{ text: string; caret: number; parseResult: ParseResult; document: RuleDocument; leafScope: (path) => LeafScope | null; client: RulesApiClient; modelType: string; ruleName?: string }`. Renders nothing but a hint when the caret is outside every backtick. Inside one: a caption `expression under caret`, a pill `model: <scope.modelName>`, the client analysis' result type, and a **reading**: the leaf evaluated alone against the selected scenario's model through `client.evaluate({ modelType, document: leafDocument, model })`, where `leafDocument` is `{ parameters: document.parameters, rule: { expression: leafText } }` at the root, or wrapped in the enclosing quantifier for a leaf inside one (`{ asAnySatisfied: { expression }, path }` — the reading then says "for any of `orders`"). Shows `Verdict` and the assertions, or the server's error message. Debounced 300 ms after the caret or text changes; a stale response (older request) is dropped.
- Accessible names: the strip is `role="region"` `aria-label="expression inspector"`, the reading `aria-live="polite"`. No hand-written restatement of generated text: the assertions shown are the ones the server returned.

- [ ] **Step 1: Write the failing tests**

```ts
// ui/apps/studio/test/panes/scenarioSelection.test.ts
import { describe, it, expect } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { selectScenario, useSelectedScenario } from '../../src/panes/scenarioSelection.js';

describe('scenarioSelection', () => {
  it('publishes the open scenario per rule', () => {
    const { result } = renderHook(() => useSelectedScenario('can-checkout'));
    expect(result.current).toBeNull();
    act(() => selectScenario('can-checkout', { name: 'VIP', model: '{"age":40}' }));
    expect(result.current).toEqual({ name: 'VIP', model: '{"age":40}' });
    act(() => selectScenario('other', { name: 'x', model: '{}' }));
    expect(result.current).toEqual({ name: 'VIP', model: '{"age":40}' });
    act(() => selectScenario('can-checkout', null));
    expect(result.current).toBeNull();
  });
});
```

```tsx
// ui/apps/studio/test/dsl/LeafInspector.test.tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { parse, type Catalog, type JsonSchema, type RulesApiClient } from '@motiv-rules/core';
import { scopeAt } from '@motiv-rules/core';
import { LeafInspector } from '../../src/dsl/LeafInspector.js';
import { selectScenario } from '../../src/panes/scenarioSelection.js';

const order: JsonSchema = { type: 'object', properties: { total: { type: 'number', format: 'decimal' } } };
const customer: JsonSchema = { type: 'object', properties: { age: { type: 'integer', format: 'int32' }, orders: { type: 'array', items: order } } };
const catalog: Catalog = { specs: [], collections: [], modelTypes: { customer } };

function renderAt(text: string, caret: number, evaluate = vi.fn()) {
  const result = parse(text);
  const client = { evaluate } as unknown as RulesApiClient;
  return render(
    <LeafInspector text={text} caret={caret} parseResult={result} document={result.document!} client={client} modelType="customer" ruleName="r"
      leafScope={(path) => scopeAt(result.document!, path, 'customer', catalog)} />,
  );
}

describe('LeafInspector', () => {
  it('hints when the caret is outside a leaf', () => {
    renderAt('is-active & `age > 1`', 2);
    expect(screen.getByRole('region', { name: 'expression inspector' })).toHaveTextContent('Put the caret inside a backtick');
  });

  it('names the scope and reads the leaf against the selected scenario', async () => {
    selectScenario('r', { name: 'Sample', model: '{"age": 34, "orders": []}' });
    const evaluate = vi.fn().mockResolvedValue({ satisfied: true, assertions: ['age > 1 == true'], explanation: { assertions: ['age > 1 == true'], causes: [] } });
    renderAt('is-active & `age > 1`', 16, evaluate);
    expect(screen.getByText(/model:/)).toHaveTextContent('customer');
    await waitFor(() => expect(evaluate).toHaveBeenCalledWith({ modelType: 'customer', document: { rule: { expression: 'age > 1' } }, model: { age: 34, orders: [] } }));
    await waitFor(() => expect(screen.getByText('age > 1 == true')).toBeInTheDocument());
  });

  it('scopes a leaf inside a quantifier body to the element', () => {
    renderAt('all in orders { `total > 1` }', 18);
    expect(screen.getByText(/model:/)).toHaveTextContent('each of orders');
  });
});
```

- [ ] **Step 2: Run them to see them fail**

Run: `pnpm -C ui/apps/studio exec vitest run test/dsl/LeafInspector.test.tsx test/panes/scenarioSelection.test.ts`
Expected: FAIL — modules missing.

- [ ] **Step 3: Implement**

```ts
// ui/apps/studio/src/panes/scenarioSelection.ts
import { useSyncExternalStore } from 'react';

export interface SelectedScenario { name: string; model: string }

const selected = new Map<string, SelectedScenario>();
const listeners = new Set<() => void>();

/** The scenario whose details are open in the table, per rule — what the inspector reads a leaf against. */
export function selectScenario(ruleName: string, scenario: SelectedScenario | null): void {
  if (scenario) selected.set(ruleName, scenario); else selected.delete(ruleName);
  for (const listener of listeners) listener();
}

export function useSelectedScenario(ruleName: string): SelectedScenario | null {
  return useSyncExternalStore(
    (listener) => { listeners.add(listener); return () => listeners.delete(listener); },
    () => selected.get(ruleName) ?? null,
  );
}
```

In `ScenarioPane`, where a row is toggled open (`toggleScenario`) call `selectScenario(props.ruleName, open ? { name: row.name, model: row.model } : null)`; when the open row's name/model is edited, publish again; `useEffect(() => () => selectScenario(props.ruleName, null), [props.ruleName])` on unmount.

```tsx
// ui/apps/studio/src/dsl/LeafInspector.tsx
import { useEffect, useMemo, useState } from 'react';
import {
  analyseLeaf, getNode, isExpressionNode, isHigherOrderNode, isNodePath,
  type EvaluationResult, type LeafScope, type ParseResult, type RuleDocument, type RuleNode, type RulesApiClient,
} from '@motiv-rules/core';
import { Verdict } from '../panes/Verdict.js';
import { useSelectedScenario } from '../panes/scenarioSelection.js';

/** The expression node whose text covers `caret`, with its leaf-relative text range. */
function leafAtCaret(text: string, caret: number, result: ParseResult): { path: string; text: string } | null {
  if (!result.document) return null;
  for (const span of result.spans) {
    if (!isNodePath(span.path)) continue;
    const node = getNode(result.document, span.path);
    if (!node || !isExpressionNode(node)) continue;
    const from = span.from + 1;
    const to = text[span.to - 1] === '`' ? span.to - 1 : span.to;
    if (caret >= from && caret <= to) return { path: span.path, text: node.expression };
  }
  return null;
}

/** A document that evaluates just this leaf: bare at the root, or under its enclosing quantifier. */
function leafDocument(document: RuleDocument, path: string, expression: string): RuleDocument {
  const parentPath = path.slice(0, Math.max(path.lastIndexOf('.'), path.lastIndexOf('[')));
  const parent = parentPath.length > 1 ? getNode(document, parentPath) : undefined;
  const rule: RuleNode = parent && isHigherOrderNode(parent)
    ? ({ ...parent, [Object.keys(parent).find((k) => k.startsWith('as'))!]: { expression } } as RuleNode)
    : { expression };
  return { ...(document.parameters ? { parameters: document.parameters } : {}), rule };
}

export function LeafInspector(props: {
  text: string; caret: number; parseResult: ParseResult; document: RuleDocument;
  leafScope: (path: string) => LeafScope | null; client: RulesApiClient; modelType: string; ruleName?: string | undefined;
}) {
  const { text, caret, parseResult, document, leafScope, client, modelType } = props;
  const leaf = useMemo(() => leafAtCaret(text, caret, parseResult), [text, caret, parseResult]);
  const scope = leaf ? leafScope(leaf.path) : null;
  const scenario = useSelectedScenario(props.ruleName ?? '');
  const [reading, setReading] = useState<{ key: string; result?: EvaluationResult; error?: string } | null>(null);

  const analysis = useMemo(() => (leaf && scope ? analyseLeaf(leaf.text, scope) : null), [leaf, scope]);
  const resultType = analysis?.ast ? analysis.types.get(analysis.ast) : undefined;

  useEffect(() => {
    if (!leaf || !scenario || !analysis?.valid) { setReading(null); return; }
    const key = `${leaf.path}|${leaf.text}|${scenario.model}`;
    const timer = setTimeout(() => {
      let model: unknown;
      try { model = JSON.parse(scenario.model); } catch { setReading({ key, error: 'the selected scenario is not valid JSON' }); return; }
      client.evaluate({ modelType, document: leafDocument(document, leaf.path, leaf.text), model })
        .then((result) => setReading((current) => (current && current.key !== key && current.key > key ? current : { key, result })))
        .catch((error: unknown) => setReading({ key, error: error instanceof Error ? error.message : String(error) }));
    }, 300);
    return () => clearTimeout(timer);
  }, [leaf?.path, leaf?.text, scenario?.model, analysis?.valid, client, modelType]);

  return (
    <section className="leaf-inspector" role="region" aria-label="expression inspector">
      {!leaf ? (
        <p className="pane-hint">Put the caret inside a backtick to see what that expression is scoped to and reads as.</p>
      ) : (
        <>
          <div className="leaf-inspector-head">
            <span className="caption">expression under caret</span>
            {scope && <span className="leaf-scope">model: <b>{scope.modelName}</b></span>}
            {resultType && <span className="leaf-scope">type: <b>{resultType}</b></span>}
          </div>
          <div aria-live="polite">
            {!scenario && <p className="pane-hint">Open a scenario in the table to read this expression against it.</p>}
            {scenario && analysis && !analysis.valid && <p className="pane-hint">Fix the expression to read it.</p>}
            {reading?.error && <p role="alert" className="error">{reading.error}</p>}
            {reading?.result && (
              <div className="leaf-reading">
                <Verdict satisfied={reading.result.satisfied} text={reading.result.satisfied ? 'Satisfied' : 'Not satisfied'} label={`against ${scenario!.name}`} />
                <ul className="leaf-assertions">{reading.result.assertions.map((a) => <li key={a}>{a}</li>)}</ul>
              </div>
            )}
          </div>
        </>
      )}
    </section>
  );
}
```

Drop the stale-response guard's string comparison if it reads wrong: keep a `latest` ref of the last key issued and only accept a response whose key equals it. `DslEditor` tracks the caret in state from `update.selectionSet` and renders `<LeafInspector … />` after `.dsl-surface`; `EditorPane` passes `client` (it has it) and a new optional `ruleName`. CSS in `app.css`: `.leaf-inspector { border-top: 1px solid var(--border); padding: 10px 12px; display: flex; flex-direction: column; gap: 8px; min-height: 72px; }`, `.leaf-inspector-head { display: flex; gap: 10px; align-items: center; flex-wrap: wrap; }`, `.leaf-scope { font: 11.5px var(--mono); color: var(--muted); }`, `.leaf-assertions { margin: 0; padding-left: 18px; font: 12.5px var(--mono); }`.

- [ ] **Step 4: Run the suites and the accessibility sweep**

Run: `pnpm -C ui/apps/studio test && pnpm -C ui/apps/studio typecheck && pnpm -C ui/apps/studio a11y`
Expected: PASS. The a11y sweep needs no .NET host; if it flakes on a timeout under load, rerun it alone.

- [ ] **Step 5: Commit**

```bash
git add ui/apps/studio/src ui/apps/studio/test
git commit -m "Studio — the expression inspector: scope and reading under the DSL pane"
```

### Task 18: Builder rows and the placeholder

**Files:**
- Modify: `ui/apps/studio/src/builder/useInlineDslEditor.ts` (leaf scope for row completion), `ui/apps/studio/src/builder/NodeToolbar.tsx` (remove the placeholder button), `ui/apps/studio/src/builder/RuleNodeEditor.tsx` (the toolbar call site, if the toolbar becomes empty)
- Test: `ui/apps/studio/test/builder/NodeToolbar.test.tsx` (or wherever the placeholder is asserted — `grep -rn "expression — coming" ui/apps/studio/test`), `ui/apps/studio/test/builder/useInlineDslEditor.test.ts*`

**Interfaces:**
- `useInlineDslEditor`'s `scope()` gains `document: RuleDocument` and `path: string`, and the completion it installs passes `(p) => scopeAt(scope().document, p, scope().modelType, scope().catalog)`. A row's inline editor parses only its own node's text, whose paths are relative to that row; pass the row's `path` as a prefix when resolving (`scopeAt(document, path, …)` for the row itself, since a one-line inline buffer's `$.rule` *is* the row). The delete of `NodeToolbar` is complete if nothing else renders inside it: remove the component and its import, and the `.node-toolbar`/`.ext-point` styles.

- [ ] **Step 1: Update the tests**

Delete the assertion that the disabled "expression — coming" button exists; add to the inline editor test a case that, with a catalog carrying `modelTypes.customer`, typing `` `ag `` in a row's editor offers `age`.

- [ ] **Step 2: Run to see the new case fail, implement, run again**

Run: `pnpm -C ui/apps/studio exec vitest run test/builder`
Expected: PASS after the change.

- [ ] **Step 3: Full Studio verification**

Run: `pnpm -C ui/apps/studio test && pnpm -C ui/apps/studio typecheck && pnpm -C ui/apps/studio a11y && pnpm -C ui e2e`
`pnpm e2e` builds the bundle and drives the .NET host; in a worktree, first confirm port 5100 is *this* checkout's host or the e2e will test another checkout's build (check the asset hash in `src/Motiv.Studio/wwwroot/index.html` against what the served page loads).
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add ui/apps/studio
git commit -m "Studio — leaf completion in builder rows; the expression placeholder goes"
```

---

## Phase 4 — Corpus and documentation

### Task 19: The conformance corpus, run by both suites

**Files:**
- Create: `ui/packages/rules-core/test/expression/corpus.json`, `ui/packages/rules-core/test/expression/fixture-schema.ts`, `ui/packages/rules-core/test/expression-corpus.test.ts`
- Create: `src/Motiv.Serialization.Tests/Expressions/CorpusFixtures.cs`, `src/Motiv.Serialization.Tests/Expressions/LeafCorpusTests.cs`
- Modify: `src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj` (link the JSON as content)

**Corpus shape:**

```json
{
  "$comment": "Run by ui/packages/rules-core/test/expression-corpus.test.ts and src/Motiv.Serialization.Tests/Expressions/LeafCorpusTests.cs. A case passes on both sides or CI fails.",
  "parameters": { "minAge": "integer", "vip": "number", "region": "string" },
  "cases": [
    { "name": "literal typed by field", "scope": "customer", "leaf": "creditLimit > 1000",
      "facts": [{ "text": "1000", "type": "decimal", "from": "creditLimit" }], "result": "bool" },
    { "name": "subtree typed from the other side", "scope": "customer", "leaf": "@vip * 2 > orders.sum(o => o.total)",
      "facts": [{ "text": "@vip", "type": "decimal" }, { "text": "2", "type": "decimal", "from": "orders.sum(o => o.total)" }], "result": "bool" },
    { "name": "fractional literal widens int", "scope": "customer", "leaf": "age * 1.5 > 40",
      "facts": [{ "text": "1.5", "type": "decimal" }, { "text": "40", "type": "decimal" }], "result": "bool" },
    { "name": "decimal vs double refused", "scope": "customer", "leaf": "creditLimit > score",
      "problems": [{ "code": "ExpressionTypeMismatch", "contains": ["decimal", "double"], "range": [0, 19] }] },
    { "name": "number param vs int anchor refused", "scope": "customer", "leaf": "orders.count() >= @vip",
      "problems": [{ "code": "ExpressionTypeMismatch" }] },
    { "name": "unanchored defaults with warning", "scope": "customer", "leaf": "1 + 1 > 1",
      "facts": [{ "text": "1", "type": "int" }], "warnings": 1, "result": "bool" },
    { "name": "integer division warns", "scope": "customer", "leaf": "age / 2 > 10", "warnings": 1, "result": "bool" },
    { "name": "unknown field", "scope": "customer", "leaf": "nope > 1", "problems": [{ "code": "UnknownField", "contains": ["nope"], "range": [0, 4] }] },
    { "name": "member on collection", "scope": "customer", "leaf": "orders.total > 1", "problems": [{ "code": "UnknownMethod", "contains": ["collection"], "range": [7, 12] }] },
    { "name": "unknown lambda field", "scope": "customer", "leaf": "orders.sum(o => o.nope) > 1", "problems": [{ "code": "UnknownField", "range": [18, 22] }] },
    { "name": "unknown method", "scope": "customer", "leaf": "orders.first() != null", "problems": [{ "code": "UnknownMethod", "contains": ["first"] }] },
    { "name": "where needs a condition", "scope": "customer", "leaf": "orders.where(o => o.total) > 1", "problems": [{ "code": "ExpressionTypeMismatch", "contains": ["condition"] }] },
    { "name": "sum needs a number", "scope": "customer", "leaf": "orders.sum(o => o.status) > 1", "problems": [{ "code": "ExpressionTypeMismatch", "contains": ["number"] }] },
    { "name": "string vs number", "scope": "customer", "leaf": "country == 3", "problems": [{ "code": "ExpressionTypeMismatch", "contains": ["string"] }] },
    { "name": "leaf must be a condition", "scope": "customer", "leaf": "age + 1", "problems": [{ "code": "ExpressionTypeMismatch", "contains": ["condition"] }] },
    { "name": "unknown parameter", "scope": "customer", "leaf": "@nope > 1", "problems": [{ "code": "UnknownField", "contains": ["parameter"] }] },
    { "name": "null check on nullable", "scope": "customer", "leaf": "country != null", "result": "bool" },
    { "name": "null check on non-nullable warns", "scope": "customer", "leaf": "age == null", "warnings": 1, "result": "bool" },
    { "name": "equalsIgnoreCase", "scope": "customer", "leaf": "country.equalsIgnoreCase(\"se\")", "result": "bool" },
    { "name": "equalsIgnoreCase on a number", "scope": "customer", "leaf": "age.equalsIgnoreCase(\"1\")", "problems": [{ "code": "UnknownMethod" }] },
    { "name": "parent reachable in lambda", "scope": "customer", "leaf": "orders.all(o => o.total <= creditLimit)", "result": "bool" },
    { "name": "element scope", "scope": "order", "leaf": "daysSinceShipped < 30", "facts": [{ "text": "30", "type": "int", "from": "daysSinceShipped" }], "result": "bool" },
    { "name": "long vs int literal", "scope": "customer", "leaf": "points > 1000", "facts": [{ "text": "1000", "type": "long" }], "result": "bool" },
    { "name": "long vs double refused", "scope": "customer", "leaf": "points > score", "problems": [{ "code": "ExpressionTypeMismatch" }] },
    { "name": "syntax error", "scope": "customer", "leaf": "age >", "problems": [{ "code": "InvalidExpression", "range": [5, 5] }] },
    { "name": "boolean connectives", "scope": "customer", "leaf": "!isActive || age >= @minAge", "result": "bool" },
    { "name": "string parameter", "scope": "customer", "leaf": "country == @region", "result": "bool" }
  ]
}
```

The fixture schema (TypeScript `fixture-schema.ts` and the C# records) is: `customer { age: int32, points: int64, score: double, isActive: boolean, creditLimit: decimal, country: string|null, orders: array<order>|null, shippedAt: string(date-time)|null }`, `order { status: string enum[paid,pending,refunded], total: decimal, daysSinceShipped: int32 }`. In C#: `record Order(string Status, decimal Total, int DaysSinceShipped)`, `record Customer(int Age, long Points, double Score, bool IsActive, decimal CreditLimit, string? Country, IReadOnlyList<Order>? Orders, DateTime? ShippedAt)`.

**Semantics of a case:** `problems` lists the non-warning problems expected, in order; each entry's `code` must match, each `contains` fragment must appear in the message, and `range` when present must equal `[from, to]`. `warnings` is the expected count of warning problems (default 0). `facts` lists expected literal/parameter facts: matched by `text`, `type` must equal, `from` when present must equal. `result` is the leaf's result type when the case is valid.

- [ ] **Step 1: Write both runners**

```ts
// ui/packages/rules-core/test/expression-corpus.test.ts
import { describe, it, expect } from 'vitest';
import corpus from './expression/corpus.json' with { type: 'json' };
import { CUSTOMER, ORDER } from './expression/fixture-schema.js';
import { analyseLeaf, printLeaf, type LeafScope } from '../src/expression/index.js';

const parameters = Object.fromEntries(Object.entries(corpus.parameters).map(([n, t]) => [n, { type: t }])) as LeafScope['parameters'];
const scopes: Record<string, LeafScope> = {
  customer: { modelName: 'customer', model: CUSTOMER, parameters, vars: {} },
  order: { modelName: 'each of orders', model: ORDER, parameters, vars: {} },
};

describe('expression corpus', () => {
  for (const c of corpus.cases) {
    it(c.name, () => {
      const analysis = analyseLeaf(c.leaf, scopes[c.scope]!);
      const errors = analysis.problems.filter((p) => !p.warning);
      const warnings = analysis.problems.filter((p) => p.warning);
      expect(warnings).toHaveLength(c.warnings ?? 0);
      if (c.problems) {
        expect(errors.map((e) => e.code)).toEqual(c.problems.map((p) => p.code));
        c.problems.forEach((p, i) => {
          for (const fragment of p.contains ?? []) expect(errors[i]!.message).toContain(fragment);
          if (p.range) expect([errors[i]!.from, errors[i]!.to]).toEqual(p.range);
        });
        return;
      }
      expect(errors).toEqual([]);
      for (const f of c.facts ?? []) {
        const fact = analysis.facts.find((x) => x.node.kind !== 'binary' && printLeaf(x.node) === f.text);
        expect(fact, `fact for ${f.text}`).toBeDefined();
        expect(fact!.type).toBe(f.type);
        if (f.from !== undefined) expect(fact!.from).toBe(f.from);
      }
      if (c.result) expect(analysis.types.get(analysis.ast!)).toBe(c.result);
    });
  }
});
```

```csharp
// src/Motiv.Serialization.Tests/Expressions/LeafCorpusTests.cs
using System.Text.Json;
using Motiv.Serialization.Expressions;

namespace Motiv.Serialization.Tests.Expressions;

public class LeafCorpusTests
{
    private static readonly JsonDocument Corpus = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "expression", "corpus.json")));

    public static IEnumerable<object[]> Cases() =>
        Corpus.RootElement.GetProperty("cases").EnumerateArray().Select(c => new object[] { c.GetProperty("name").GetString()! });

    private static RuleParameterDeclaration[] Parameters() =>
        Corpus.RootElement.GetProperty("parameters").EnumerateObject()
            .Select(p => new RuleParameterDeclaration(p.Name, Enum.Parse<RuleParameterType>(p.Value.GetString()!, ignoreCase: true), false, null))
            .ToArray();

    [Theory]
    [MemberData(nameof(Cases))]
    public void Should_agree_with_the_corpus(string name)
    {
        var c = Corpus.RootElement.GetProperty("cases").EnumerateArray().Single(x => x.GetProperty("name").GetString() == name);
        var modelType = c.GetProperty("scope").GetString() == "order" ? typeof(CorpusFixtures.Order) : typeof(CorpusFixtures.Customer);
        var problems = new List<LeafProblem>();
        var root = LeafParser.Parse(c.GetProperty("leaf").GetString()!, problems);
        var analysis = root is null ? null : LeafChecker.Check(root, LeafScope.For(modelType, Parameters()));
        var all = analysis?.Problems ?? problems;
        var errors = all.Where(p => !p.IsWarning).ToList();
        var warnings = all.Where(p => p.IsWarning).ToList();

        warnings.Count.ShouldBe(c.TryGetProperty("warnings", out var w) ? w.GetInt32() : 0);
        if (c.TryGetProperty("problems", out var expected))
        {
            errors.Select(e => e.Code.ToString()).ShouldBe(expected.EnumerateArray().Select(p => p.GetProperty("code").GetString()));
            var i = 0;
            foreach (var p in expected.EnumerateArray())
            {
                if (p.TryGetProperty("contains", out var fragments))
                    foreach (var fragment in fragments.EnumerateArray()) errors[i].Message.ShouldContain(fragment.GetString()!);
                if (p.TryGetProperty("range", out var range))
                    (errors[i].Start, errors[i].End).ShouldBe((range[0].GetInt32(), range[1].GetInt32()));
                i++;
            }
            return;
        }

        errors.ShouldBeEmpty();
        if (c.TryGetProperty("facts", out var facts))
            foreach (var f in facts.EnumerateArray())
            {
                var fact = analysis!.Facts.Single(x => x.Node is not Binary && LeafChecker.Print(x.Node) == f.GetProperty("text").GetString());
                CorpusFixtures.TypeName(fact.Type).ShouldBe(f.GetProperty("type").GetString());
                if (f.TryGetProperty("from", out var from)) fact.From.ShouldBe(from.GetString());
            }
        if (c.TryGetProperty("result", out var result))
            CorpusFixtures.TypeName(analysis!.Types[root!]).ShouldBe(result.GetString());
    }
}
```

`CorpusFixtures.cs` holds the two records and `TypeName(Type)` (`int`, `long`, `float`, `double`, `decimal`, `bool`, `string`, with `?` for nullable — the same strings as the TypeScript `typeString`). In the test `.csproj`: `<Content Include="..\..\ui\packages\rules-core\test\expression\corpus.json" Link="expression\corpus.json" CopyToOutputDirectory="PreserveNewest" />`. Vitest needs `resolveJsonModule` (check `tsconfig`); if the `with { type: 'json' }` import attribute fails under the package's TS target, `readFileSync` + `JSON.parse` the file instead.

- [ ] **Step 2: Run both runners; reconcile until both agree**

Run: `pnpm -C ui/packages/rules-core exec vitest run test/expression-corpus.test.ts` and `env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests --framework net10.0 --filter LeafCorpusTests 2>&1 | grep -E "error CS|Passed!|Failed!|Failed "`
Expected: every case passes on both sides. A case that passes on one side only is a divergence between the two checkers: fix the checker whose behaviour contradicts the spec, never the corpus, unless the corpus itself misreads the spec.

- [ ] **Step 3: Commit**

```bash
git add ui/packages/rules-core/test src/Motiv.Serialization.Tests
git commit -m "Expressions — the conformance corpus, run by Vitest and xUnit"
```

### Task 20: Documentation, and the plan and design in the tree

**Files:**
- Create: `docs/live-rules/expressions.md`
- Modify: `README.md` (Core Features › Live Rules: a short leaf example), `docs/live-rules/toc.yml`, `docs/toc.yml` (only if the section listing needs it), `docs/Overview.md` (a row in the Live Rules table), `docs/propositions/index.md` (the "a predicate is C#" passage), `docs/live-rules/RuleDocuments.md` (the leaf node in the node listing), `CLAUDE.md` (an Architecture Note: the leaf language exists twice, and the corpus is the contract)

- [ ] **Step 1: Write `docs/live-rules/expressions.md`**

Sections, in this order: *What an expression leaf is* (one paragraph; the `{ "expression": "…" }` node and the backtick literal); *The language* (the grammar block from the design doc, the two closed method sets, a table of examples with what each binds to); *Scope* (root, quantifier body, lambda; the parent stays reachable); *Semantics* (strings, null, numerics — copy the design doc's rules; a table of allowed widenings; the `format` stamp); *What Studio shows* (completion, lint, hover facts, the inspector); *Errors* (a table of the five codes with one example each); *Limits* (no leaves on `netstandard2.0`; no casts; the closed sets); *Remarks* (why decimal is preferred, why null never throws, the corpus as the contract between editor and binder).

- [ ] **Step 2: README and the other pages**

Under `### Live Rules` in `README.md`, after the existing document example, add a five-line example whose rule holds one `expression` leaf and one `spec` reference, and one sentence pointing at `docs/live-rules/expressions.md`. `docs/live-rules/toc.yml`: `- name: Expression Leaves` / `href: expressions.md` after `RuleDocuments`. `docs/Overview.md`: a table row `[Expression Leaves](./live-rules/expressions.md) | A small owned language for a leaf's condition — fields, parameters, literals, a closed set of collection and string methods — parsed in the editor and bound through Spec.From.` `docs/propositions/index.md`: replace the two paragraphs that say leaves do not bind with one saying a leaf now binds on .NET 8 and later, and keep the point that a builder must still ask what a proposition starts *from* — a leaf is one of the two answers. `docs/live-rules/RuleDocuments.md`: where leaf kinds are listed, `expression` links to the new page. `CLAUDE.md` Architecture Notes, one bullet:

> - **The leaf language exists twice on purpose**: `ui/packages/rules-core/src/expression/` for the editor and `src/Motiv.Serialization/Expressions/` for the binder. `ui/packages/rules-core/test/expression/corpus.json` is the contract between them and is run by both suites; change the grammar, the lattice or a message in one without the corpus and CI tells you. Only the null-conditional node lives in `src/Motiv`.

- [ ] **Step 3: Verify the docs build references and run everything once more**

Run: `grep -rn "expressions.md" docs README.md | wc -l` (expect ≥ 4), then the full verification: `env -u MallocStackLogging -u MallocNanoZone dotnet test Motiv.sln --framework net10.0 2>&1 | grep -E "error CS|Passed!|Failed!"`, `pnpm -C ui -r test`, `pnpm -C ui -r typecheck`, `pnpm -C ui verify:publishable` (the core package's `exports` map is unchanged, but its surface grew — the tarball check must stay green), `pnpm -C ui/apps/studio a11y`. Also a bare `env -u MallocStackLogging -u MallocNanoZone dotnet build` for every target framework, since CI builds `netstandard2.0` and `net472` consumers.
Expected: all green; report any suite you could not run and why.

- [ ] **Step 4: Commit, with the plan and the design doc**

```bash
git add docs README.md CLAUDE.md
git commit -m "Docs — expression leaves: the language, its semantics, and what Studio shows

Lands docs/superpowers/plans/2026-09-18-studio-expression-leaves.md and the 2026-09-18 design alongside."
```

The design doc was committed on its own earlier (b1c0faae) and the plan is committed when written; this commit carries the docs pages and any edits to the two. Then the post-implementation `code-simplifier` review required by `CLAUDE.md`, applied and re-tested before the branch is finished.
