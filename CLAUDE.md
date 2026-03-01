# Motiv Project Guidelines

## What Is Motiv?

Motiv is a .NET library that solves the **Boolean Blindness Problem** — when a boolean expression evaluates, you lose all context about *why* the value is true or false. Motiv preserves this reasoning by implementing the **Specification Pattern** with a fluent builder API, turning boolean expressions into composable, explainable propositions.

## Three Proposition Types

### 1. Minimal Proposition
The most concise form. Uses only a predicate and a propositional statement (name).

```csharp
var isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");
// true:  Assertions = ["is even"]
// false: Assertions = ["¬is even"]  (auto-negated)
```

- No explicit WhenTrue/WhenFalse — the name IS the assertion
- False assertion is auto-negated with `¬` prefix
- Creates a **Policy** (single assertion type)

### 2. Explanation Proposition
Provides explicit human-readable strings for both outcomes.

```csharp
var isActive = Spec
    .Build((User u) => u.IsActive)
    .WhenTrue("user is active")
    .WhenFalse("user is not active")
    .Create();
// true:  Assertions = ["user is active"]
// false: Assertions = ["user is not active"]
```

- WhenTrue/WhenFalse strings ARE the assertions — used directly
- `Create()` can be called without a name (first WhenTrue string becomes the implicit propositional statement)
- Creates a **Policy** when both WhenTrue and WhenFalse are used

### 3. Metadata Proposition
Provides arbitrary non-string objects as metadata (e.g., for multilingual support, error codes, structured data).

```csharp
var isAdmin = Spec
    .Build((User u) => u.IsAdmin)
    .WhenTrue(new Message { English = "admin" })
    .WhenFalse(new Message { English = "not admin" })
    .Create("is admin");
// true:  Metadata = [Message{...}], Assertions = ["is admin == true"]
// false: Metadata = [Message{...}], Assertions = ["is admin == false"]
```

- WhenTrue/WhenFalse provide non-string metadata objects
- **Requires** `Create("name")` with an explicit name (metadata can't serve as text)
- Textual assertions are derived from the name + `== true` / `== false` suffix

## The `== true` / `== false` Suffix Rule

This is how Motiv textually describes a boolean outcome when the **proposition name** (from `.Create("name")`) is the sole source of explanation text.

**Use `== true` / `== false`** when:
- **Minimal propositions** — `.Create("name")` with no WhenTrue/WhenFalse. The name is the only text available, so the suffix disambiguates the outcome.
- **Metadata propositions** — `.WhenTrue(nonStringValue)` / `.WhenFalse(nonStringValue)` with `.Create("name")`. The metadata is an object (bool, int, Uri, Regex, Guid, etc.) that can't serve as a textual explanation, so the name + suffix describes the outcome.

**Do NOT use `== true` / `== false`** when:
- **Explanation propositions** — `.WhenTrue("some string")` / `.WhenFalse("some string")`. The strings ARE the textual explanations. They already describe the outcome in human terms, so the suffix would be redundant.
- This applies to all string-returning overloads, including delegate forms like `.WhenTrue(model => "...")` and `.WhenTrueYield(model => ["..."])`.

**In short:** `== true` / `== false` bridges the gap between a proposition name and its boolean outcome. When WhenTrue/WhenFalse already provide that bridge as strings, the suffix is unnecessary.

## Result Properties

| Property | Purpose | Detail Level |
|---|---|---|
| `Satisfied` | Boolean outcome | `true` / `false` |
| `Reason` | Quick summary of what happened | Linear, operator-heavy: `"a & !b"` |
| `Justification` | Full breakdown of contributing causes | Hierarchical tree, prefix notation |
| `Assertions` | All contributing assertion strings | Flat collection of strings |
| `Values` | Custom metadata from the evaluation | Collection of `TMetadata` |
| `Description.Reason` | Same as Reason but via Description object | Same as Reason |

### Reason vs Justification
- **Reason** is a one-line summary: `"(left == true) & (right == false)"`
- **Justification** is a hierarchical tree:
  ```
  AND
      left == true
      right == false
  ```
- Both are de-noised — they only include assertions that influenced the final result

## Logical Operators

### Non-Short-Circuiting (always evaluate both operands)
- `&` / `.And()` — AND: satisfied when both are satisfied
- `|` / `.Or()` — OR: satisfied when at least one is satisfied
- `^` / `.XOr()` — XOR: satisfied when exactly one is satisfied (always shows both assertions)

### Short-Circuiting (may skip right operand)
- `.AndAlso()` — AND with short-circuit: skips right if left is false
- `.OrElse()` — OR with short-circuit: skips right if left is true
- `&&` / `||` operators only work on **results** (`BooleanResultBase`), not propositions - this is due to limitations in C# operator overloading. For propositions, use the method forms (`.AndAlso()`, `.OrElse()`) to get short-circuiting behavior.

### NOT
- `!` operator or `.Not()` method — negates the result
- Works on both propositions and results

### Operator Composition in Reasons
When a binary operation has:
- **2 causal operands**: each wrapped in parens: `"(left == true) & (right == true)"`
- **1 causal operand**: no parens: `"left == false"`

### Policy Preservation
- `!policy` returns a policy
- `policy.OrElse(policy)` returns a policy
- All other operations return a spec

## Builder Pattern

```
Spec.Build(input)
  ├── [Optional] .AsAllSatisfied() / .AsAnySatisfied() / etc. (higher-order)
  ├── [Optional] .WhenTrue(...) / .WhenTrueYield(...)
  ├── [Optional] .WhenFalse(...) / .WhenFalseYield(...)
  └── [Required] .Create() or .Create("name")
```

### Key Builder Paths
- **Minimal**: `Build(predicate).Create("statement")` — Policy with auto-negated WhenFalse
- **Explanation**: `Build(predicate).WhenTrue("t").WhenFalse("f").Create()` — Policy with explicit assertions
- **Metadata**: `Build(predicate).WhenTrue(obj).WhenFalse(obj).Create("name")` — non-string metadata requires a name
- **Expression Trees**: `Spec.From(expression).Create("name")` — auto-decomposed from lambda

### WhenTrue/WhenFalse vs WhenTrueYield/WhenFalseYield
- `WhenTrue()` / `WhenFalse()` — single value → creates a **Policy**
- `WhenTrueYield()` / `WhenFalseYield()` — multiple values → creates a **Spec** (not a Policy)

## Assertions Property Rules

1. **Explanation propositions**: Assertions come from WhenTrue/WhenFalse strings directly
2. **Metadata propositions**: Assertions are `"{name} == true"` or `"{name} == false"` (derived from Create name)
3. **Minimal propositions**: Same as metadata — `"{name}"` or `"¬{name}"`
4. **Compositions**: Aggregated from all contributing operands
5. **Mixed metadata types**: Falls back to string Assertions when TMetadata types differ across operands

## Policy vs Spec

A **Policy** resolves to a single value — one assertion or one metadata object per evaluation. It models a decision with exactly one outcome explanation. A **Spec** is an accumulation of values — it can yield multiple assertions or metadata objects from a single evaluation, aggregating results from underlying operands or yield functions.

This distinction is a meaningful type-level guarantee:
- **Policy** (`PolicyBase<TModel, TMetadata>`) — created by minimal propositions, or when both `WhenTrue()` and `WhenFalse()` (singular) are used. Guarantees exactly one value.
- **Spec** (`SpecBase<TModel, TMetadata>`) — created when `WhenTrueYield()` or `WhenFalseYield()` are used, or when composing propositions with operators. Can accumulate multiple values from underlying evaluations.

Policy is a subtype of Spec, so policies can be used anywhere a spec is expected. The reverse is not true — a spec that yields multiple values cannot be treated as a policy.

## Architecture Notes

- **Avoid over-DRYing**: The codebase intentionally has some duplication between proposition types. Each builder path has nuanced differences. Explicit code is preferred over complex abstractions with branching logic.
- **Results are composable**: `BooleanResultBase<TMetadata>` instances from different model types can be combined with operators, enabling cross-domain reasoning.
- **De-noising**: Results only surface assertions that influenced the final outcome, filtering out irrelevant branches.
- **Batch refactoring verification**: When refactoring multiple files with the same pattern, verify all files are modified before moving to the next phase — use `git status` or `git diff --stat` to confirm the expected set of changed files matches the plan.
- **Constructor signature changes**: When changing the signature of an `internal` type's constructor, search for all call sites across both production and test code before editing — test files often construct internal types directly via `[InternalsVisibleTo]` and will break if missed.
- **Documentation**: CLAUDE.md is for AI guidance and project conventions — not user-facing feature documentation. When asked to document a feature, add it to `README.md` (brief example under Core Features) and `docs/` (detailed pages following the existing structure: `docs/{feature}/index.md`, individual method pages, `toc.yml`, plus entries in `docs/toc.yml` and `docs/Overview.md`).

## Test-Driven Development

Follow TDD strictly when developing features or fixing bugs:

1. **Write a failing test first** — define the expected behavior before writing implementation code
2. **Run it to confirm it fails** — verify it fails for the right reason
3. **Write the minimum code to pass** — only enough to make the test green
4. **Run it to confirm it passes** — verify correctness
5. **Refactor if needed** — clean up while keeping tests green

Never write implementation code without a corresponding test. If fixing a bug, first write a test that reproduces it. Run the full test suite before considering work complete.

## Post-Implementation Code Review

After applying changes and confirming tests pass, **always** spawn a `code-simplifier` agent to review the changed code. The agent should focus on:

- **Code duplication** — identify semantically identical code that should be consolidated
- **Convoluted design** — simplify overly complex class hierarchies, unnecessary indirection, or tangled dependencies
- **Procedural code** — refactor imperative step-by-step logic into more declarative, composable patterns where appropriate
- **Long methods** — break down methods that do too much into smaller, well-named, single-responsibility methods
- **Other anti-patterns** — god classes, feature envy, primitive obsession, deep nesting, poor naming, etc.

This step is mandatory — do not skip it. If the agent identifies improvements, apply them and re-run the affected tests before considering the task complete.

## Roslyn CodeFix Conventions (Motiv.CodeFix)

- Use `ParseTypeName(typeName)` instead of `IdentifierName(typeName)` when constructing type syntax nodes — `IdentifierName("int")` creates an `IdentifierNameSyntax`, but the test framework expects `PredefinedTypeSyntax` for C# keyword types like `int`, `string`, `bool`
- When creating semicolon-terminated class declarations (no body), explicitly suppress braces with `.WithOpenBraceToken(Token(SyntaxKind.None)).WithCloseBraceToken(Token(SyntaxKind.None))`
- Do not rely on `node.Ancestors()` inside `CSharpSyntaxRewriter` overrides — ancestor context is unreliable during tree rewriting. Instead, create separate rewriter classes for structurally different cases (e.g., block-lambda vs expression-lambda)
- When targeting specific lambda expressions in a `CSharpSyntaxRewriter`, use structural properties (e.g., parameter count, body type) rather than parent-type checks, since multiple lambdas in a chain can share the same parent type
- In C# primary constructor inheritance, access forwarded parameters via the base class's properties (not the subclass constructor parameter) to avoid CS9107 dual-capture warnings
- `NormalizeWhitespace()` strips all custom trivia — do not add formatting trivia during syntax construction if a `NormalizeWhitespace` + rewriter pass will follow. Apply formatting exclusively in the rewriter to avoid duplicate or dead formatting logic.
