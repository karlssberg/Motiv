---
name: motiv-semantics
description: Motiv's proposition semantics — the three proposition types (minimal, explanation, metadata), the `== true` / `== false` suffix rule, the operator surface, the builder paths, and what Assertions / Values / Reason / Justification each resolve to. Load before writing or changing a proposition, an assertion or metadata payload, a `Create()` call, an operator composition, or anything that alters justification output or result formatting.
---

# Motiv proposition semantics

Reference for the library's public semantics. `CLAUDE.md` carries the summary and the rules that must
hold without being looked up; this is the worked detail behind them.

## Three Proposition Types

### 1. Minimal Proposition
The most concise form. Uses only a predicate and a propositional statement (name).

```csharp
var isEven = Spec.Build((int n) => n % 2 == 0).Create("is even");
// true:  Assertions = ["is even == true"]
// false: Assertions = ["is even == false"]
```

- No explicit WhenTrue/WhenFalse — the name is suffixed with `== true` / `== false` to form the assertion
- The outcome is disambiguated by the `== true` / `== false` suffix
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

Supplying an explicit name via `Create("name")` changes the semantics — the strings become metadata (`Values`) rather than assertions, and the name + `== true`/`== false` suffix takes over as the assertion text:

```csharp
var isActive = Spec
    .Build((User u) => u.IsActive)
    .WhenTrue("user is active")
    .WhenFalse("user is not active")
    .Create("is active");
// true:  Assertions = ["is active == true"], Values = ["user is active"]
// false: Assertions = ["is active == false"], Values = ["user is not active"]
```

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

This is how Motiv textually describes a boolean outcome whenever an explicit name is supplied via `.Create("name")`. The name is the sole authority for explanation text — `Reason`, `Assertions`, and `Justification` all resolve to `"name == true"` / `"name == false"`, regardless of what WhenTrue/WhenFalse were given. WhenTrue/WhenFalse payloads — strings included — are metadata (`Values`) whenever a name exists; a supplied name always outranks them as the source of explanation text. **Exception:** ExpressionTree propositions (`Spec.From(...)`) operate differently — when named, only `Reason` takes the `name == true` / `name == false` form; `Assertions` and `Justification` surface the underlying decomposed expression-clause assertions (e.g., `["n > 0 == true"]` rather than `["name == true"]`).

**Use `== true` / `== false`** when:
- **Minimal propositions** — `.Create("name")` with no WhenTrue/WhenFalse. The name is the only text available, so the suffix disambiguates the outcome.
- **Metadata propositions** — `.WhenTrue(nonStringValue)` / `.WhenFalse(nonStringValue)` with `.Create("name")`. The metadata is an object (bool, int, Uri, Regex, Guid, etc.) that can't serve as a textual explanation, so the name + suffix describes the outcome.
- **Named explanation propositions** — `.WhenTrue("some string").WhenFalse("some string").Create("name")`. Even though the strings are textual, supplying a name means they are demoted to metadata (`Values`); the name + suffix becomes the assertion text. This also applies to delegate forms (`.WhenTrue(model => "...")`) and yield forms (`.WhenTrueYield(model => [...])`) when named.

**Do NOT use `== true` / `== false`** when:
- **Unnamed explanation propositions** — `.WhenTrue("some string").WhenFalse("some string").Create()` with no name. The WhenTrue string doubles as the propositional statement, so the strings ARE the textual explanations directly. `Create()` guards the WhenTrue string as non-whitespace; a degenerate resolved string (null/empty/whitespace, e.g. from a delegate at runtime) falls back to `"statement == true"` / `"statement == false"`.
- **Exception**: the two `Spec.From(expr).WhenTrue("...").WhenFalse("...")` expression-tree WithName factories (single-value, not yield) derive their unnamed statement from the expression itself, so they have no `trueBecause` guard — a degenerate string simply falls back at evaluation time.

**In short:** `== true` / `== false` bridges the gap between a proposition name and its boolean outcome. It appears whenever a name is the source of the statement — whether that name was supplied explicitly via `Create("name")`, or implicitly derived from an unguarded expression-tree statement.

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

See **Policy Preservation** in `CLAUDE.md` for which combinators preserve policy-ness, and why
operator overloads cannot.

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

1. **Explanation propositions**: Assertions come from WhenTrue/WhenFalse strings directly only when `Create()` is parameterless (unnamed); with `Create("name")`, Assertions are `"{name} == true"` / `"{name} == false"` and the strings surface instead via `Values`. ExpressionTree propositions are the exception — named `Assertions` remain the underlying decomposed clause assertions (e.g., `["n > 0 == true"]`), while only `Reason` takes the suffix
2. **Metadata propositions**: Assertions are `"{name} == true"` or `"{name} == false"` (derived from Create name)
3. **Minimal propositions**: Same as metadata — `"{name} == true"` or `"{name} == false"`
4. **Compositions**: Aggregated from all contributing operands
5. **Mixed metadata types**: Falls back to string Assertions when TMetadata types differ across operands

## Policy vs Spec

A **Policy** resolves to a single value — one assertion or one metadata object per evaluation. It models a decision with exactly one outcome explanation. A **Spec** is an accumulation of values — it can yield multiple assertions or metadata objects from a single evaluation, aggregating results from underlying operands or yield functions.

This distinction is a meaningful type-level guarantee:
- **Policy** (`PolicyBase<TModel, TMetadata>`) — created by minimal propositions, or when both `WhenTrue()` and `WhenFalse()` (singular) are used. Guarantees a single `Value` — but see below: that value is a *selection*, not a claim that only one cause exists.
- **Spec** (`SpecBase<TModel, TMetadata>`) — created when `WhenTrueYield()` or `WhenFalseYield()` are used, or when composing propositions with operators. Can accumulate multiple values from underlying evaluations.

Policy is a subtype of Spec, so policies can be used anywhere a spec is expected. The reverse is not true — a spec that yields multiple values cannot be treated as a policy.

### `Value` is a selection; `Values` is the full causal set

A Policy guarantees a single `Value`, but for an `OrElse` composition that value is the
**last-evaluated operand's** — the `??` fallback. When a chain is unsatisfied, every operand is a
genuine cause, so `Value` is *not* necessarily `Values.Single()`:

```csharp
left.OrElse(right).Evaluate(model);          // both unsatisfied
// Value  == "right-false"                   <- the selection
// Values == ["left-false", "right-false"]   <- everything it was selected from
```

`Values` is how a consumer reaches the unselected causes. It:
- **flattens** — an `OrElseTogether` chain is a left-nested tree of `OrElsePolicyResult`, but a three-policy chain yields three values, not the root's two;
- **de-noises** — only causal values appear, so when a middle policy is satisfied `Values` holds just that one;
- is **metadata-agnostic** — it works for arbitrary `TMetadata`, not only the string path.

Contrast `Causes` and `Underlying`, which expose the **binary composition shape** (two operands at
the root of a three-policy chain) rather than the flattened causal set.
