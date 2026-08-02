# Policy Preservation Boundary Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Document where policy preservation stops and why, and pin the `Value`-is-a-selection / `Values`-is-the-causal-set contract with tests.

**Architecture:** No production code changes. The shipped behaviour is correct; the documented contract overstates it. Two test files gain characterisation tests that lock the real contract, and two documentation files are corrected to describe it.

**Tech Stack:** C# / .NET (net8.0, net9.0, net10.0, net472), xUnit, Shouldly, AutoFixture.

## Global Constraints

- **No production code changes.** If a task appears to require editing anything under `src/Motiv/`, stop and escalate — the plan is wrong.
- **Test invocation requires the user-local .NET root.** Every `dotnet` command must be prefixed with `export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH"`. Without it, net8.0/net9.0 testhosts abort with "You must install or update .NET".
- **net472 cannot run on this machine** (vstest needs Windows for .NET Framework testhosts). It compiles only; CI covers it. Use `-f net10.0` when running a single filtered test.
- **The final verification runs the whole solution**, not just `Motiv.Tests` — the example projects (`src/examples/Motiv.Poker.Tests`, `Motiv.ECommerce.Tests`, `Motiv.SmartHome.Tests`) assert on justification strings.
- **These are characterisation tests, not TDD.** They describe behaviour that already ships, so they pass on first run. The red step is replaced by an explicit mutation check (Task 1, Step 3) proving the assertions are load-bearing.
- **Assert collections with Shouldly collection expressions**: `result.Values.ShouldBe(["a", "b"])`, matching existing style in `src/Motiv.Tests`.

**Source spec:** `docs/superpowers/specs/2026-08-02-policy-preservation-boundary-design.md`

---

## File Structure

| File | Responsibility |
|---|---|
| `src/Motiv.Tests/OrElsePolicyTests.cs` (modify) | Binary `OrElse` policy behaviour. Gains the `Value` vs `Values` divergence pin. |
| `src/Motiv.Tests/PolicyExtensionsTests.cs` (modify) | `OrElseTogether` chain behaviour. Gains the three `Values` guarantees: flattening, de-noising, metadata-agnosticism. |
| `CLAUDE.md` (modify) | AI-facing conventions. Gains the canonical-single-value rule, the static-type rule, the `||` mechanism, and the corrected Policy contract. |
| `docs/operators/OrElse.md` (modify) | User-facing operator reference. Gains a Policies section and a real explanation replacing "quirks". |

---

### Task 1: Pin the `Value` vs `Values` divergence on a binary chain

**Files:**
- Modify: `src/Motiv.Tests/OrElsePolicyTests.cs` (append inside the class, before the final closing brace on line 588)

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: the verified values `Value == "right-false"` and `Values == ["left-false", "right-false"]`, reused verbatim by Task 4's documentation example.

- [ ] **Step 1: Write the characterisation test**

Append this method inside the `OrElsePolicyTests` class, immediately before the class's closing brace:

```csharp
    [Fact]
    public void Should_select_the_last_evaluated_value_while_retaining_every_cause()
    {
        // Arrange
        var left =
            Spec.Build((object _) => false)
                .WhenTrue("left-true")
                .WhenFalse("left-false")
                .Create("left");

        var right =
            Spec.Build((object _) => false)
                .WhenTrue("right-true")
                .WhenFalse("right-false")
                .Create("right");

        var policy = left.OrElse(right);

        // Act
        var result = policy.Evaluate(new object());

        // Assert
        result.Satisfied.ShouldBeFalse();

        // Value is a *selection* — the last-evaluated operand, i.e. the `??` fallback.
        result.Value.ShouldBe("right-false");

        // Values is the full causal set. Value is deliberately NOT Values.Single().
        result.Values.ShouldBe(["left-false", "right-false"]);
    }
```

- [ ] **Step 2: Run the test to verify it passes**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Tests -f net10.0 --filter "FullyQualifiedName~Should_select_the_last_evaluated_value_while_retaining_every_cause"
```

Expected: PASS. This is a characterisation test — it describes shipped behaviour, so green on first run is correct, not a mistake.

- [ ] **Step 3: Prove the assertion is load-bearing**

Temporarily change line 11 of `src/Motiv/OrElse/OrElsePolicyResult.cs` from:

```csharp
    public override TMetadata Value => (Right ?? Left).Value;
```

to:

```csharp
    public override TMetadata Value => Left.Value;
```

Re-run the command from Step 2. Expected: FAIL with `Value` being `"left-false"` instead of `"right-false"`.

Then **revert the change**:

```bash
git checkout -- src/Motiv/OrElse/OrElsePolicyResult.cs
```

Re-run Step 2's command and confirm PASS again. Confirm `git status --short` shows no changes under `src/Motiv/`.

- [ ] **Step 4: Commit**

```bash
git add src/Motiv.Tests/OrElsePolicyTests.cs
git commit -m "test(orelse): pin Value as a selection, not Values.Single()"
```

---

### Task 2: Pin the three `Values` guarantees on a chain

**Files:**
- Modify: `src/Motiv.Tests/PolicyExtensionsTests.cs` (append inside the class, before the final closing brace on line 284)

**Interfaces:**
- Consumes: nothing from Task 1 — this task is independently testable.
- Produces: the verified chain values reused by Task 3's CLAUDE.md prose (3-policy chain yields 3 values; middle-satisfied yields 1; `int` metadata yields `[10, 20, 30]`).

Note on why three separate tests: each pins a distinct guarantee a consumer relies on, and a reviewer could reasonably accept one and reject another. Do not merge them into a `[Theory]`.

- [ ] **Step 1: Write the flattening test**

Append inside the `PolicyExtensionsTests` class, before its closing brace:

```csharp
    [Fact]
    public void Should_flatten_every_cause_of_an_unsatisfied_chain()
    {
        // Arrange
        var policies = new[]
        {
            Spec.Build<string>(_ => false).WhenTrue("a-true").WhenFalse("a-false").Create("a"),
            Spec.Build<string>(_ => false).WhenTrue("b-true").WhenFalse("b-false").Create("b"),
            Spec.Build<string>(_ => false).WhenTrue("c-true").WhenFalse("c-false").Create("c")
        };

        // Act
        var result = policies.OrElseTogether().Evaluate("model");

        // Assert
        result.Satisfied.ShouldBeFalse();
        result.Value.ShouldBe("c-false");

        // The chain is a left-nested tree of OrElsePolicyResult, but Values recurses:
        // three policies yield three values, not the root node's two.
        result.Values.ShouldBe(["a-false", "b-false", "c-false"]);
    }
```

- [ ] **Step 2: Write the de-noising test**

Append immediately after the previous method:

```csharp
    [Fact]
    public void Should_report_only_the_causal_value_when_a_chain_is_satisfied()
    {
        // Arrange
        var policies = new[]
        {
            Spec.Build<string>(_ => false).WhenTrue("x-true").WhenFalse("x-false").Create("x"),
            Spec.Build<string>(_ => true).WhenTrue("y-true").WhenFalse("y-false").Create("y"),
            Spec.Build<string>(_ => false).WhenTrue("z-true").WhenFalse("z-false").Create("z")
        };

        // Act
        var result = policies.OrElseTogether().Evaluate("model");

        // Assert
        result.Satisfied.ShouldBeTrue();
        result.Value.ShouldBe("y-true");

        // Only causal values appear: x did not cause the true result, and z was never evaluated.
        result.Values.ShouldBe(["y-true"]);
    }
```

- [ ] **Step 3: Write the non-string metadata test**

Append immediately after the previous method:

```csharp
    [Fact]
    public void Should_retain_every_cause_for_non_string_metadata()
    {
        // Arrange
        var policies = new[]
        {
            Spec.Build<string>(_ => false).WhenTrue(1).WhenFalse(10).Create("m1"),
            Spec.Build<string>(_ => false).WhenTrue(2).WhenFalse(20).Create("m2"),
            Spec.Build<string>(_ => false).WhenTrue(3).WhenFalse(30).Create("m3")
        };

        // Act
        var result = policies.OrElseTogether().Evaluate("model");

        // Assert
        result.Satisfied.ShouldBeFalse();
        result.Value.ShouldBe(30);

        // The guarantee is metadata-agnostic, not string-path-only.
        result.Values.ShouldBe([10, 20, 30]);
    }
```

- [ ] **Step 4: Run the three tests to verify they pass**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Tests -f net10.0 --filter "FullyQualifiedName~PolicyExtensionsTests"
```

Expected: PASS, including the pre-existing `PolicyExtensionsTests` methods.

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Tests/PolicyExtensionsTests.cs
git commit -m "test(orelse): pin Values as the flattened, de-noised causal set"
```

---

### Task 3: Correct the contract in CLAUDE.md

**Files:**
- Modify: `CLAUDE.md:125-128` (Policy Preservation section)
- Modify: `CLAUDE.md:163` (single line)
- Modify: `CLAUDE.md:166` (append a subsection after it)

**Interfaces:**
- Consumes: the values pinned in Tasks 1 and 2 — the prose below must not contradict them.
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Replace the Policy Preservation section**

Replace lines 125-128 in `CLAUDE.md`, which currently read:

```markdown
### Policy Preservation
- `!policy` returns a policy
- `policy.OrElse(policy)` returns a policy
- All other operations return a spec
```

with:

```markdown
### Policy Preservation
- `!policy` returns a policy
- `policy.OrElse(policy)` returns a policy
- All other operations return a spec

Policy status is **not** a property of the algebra. It is granted only where the combinator has a
canonical single value a caller would ask for:
- `OrElse` has one — it is `??`: the first operand that matched, or the final fallback. Its name advertises selection.
- `Not` has one trivially — one operand in, one out.
- `AndAlso` does **not**. "Also" advertises accumulation: when satisfied, both operands are causal and both are the point, so nominating one discards what the name emphasises. Its single-valued direction is the *unsatisfied* one ("which gate failed?"), which is a different operation wearing conjunction's name. If that is ever wanted, it gets its own name — do not add a policy-preserving `AndAlso`.
- Eager `Or` / `And` / `XOr` do not — both operands always evaluate and neither is distinguished.

Preservation is a **static-type property**. `policy.OrElse(spec)` returns a spec, and declaring
policies as `IEnumerable<SpecBase<TModel, TMetadata>>` before calling `OrElseTogether()` is the same
act — it returns an `OrElseSpec`, not an `OrElsePolicy`. Introduce a non-policy and you abandon
preservation. This is by design, not a covariance defect.

**Operator overloads cannot carry policy preservation — do not re-propose this.** C# cannot overload
`||` directly: `x || y` compiles to `T.false(x) ? x : T.|(x, y)`, and the selected `operator |` must
take *and* return exactly `T`. A policy-preserving `||` therefore forces a policy-preserving `|` —
but `|` is eager `Or` with no canonical operand, so `satisfiedPolicy | unsatisfiedPolicy` would
report `Satisfied == true` while returning the *unsatisfied* operand's value. Two further blockers:
`x || y` short-circuits by returning `x` itself, unwrapped, so no `OrElse` node appears in the
justification tree; and an `operator |` on `PolicyBase` that meant `OrElse` would make `|` eager on
specs and lazy on policies, so widening a variable's declared type would silently change evaluation
semantics.
```

- [ ] **Step 2: Correct the overstated guarantee**

In `CLAUDE.md`, find this line (line 163 before Step 1's edit; it will have shifted down):

```markdown
- **Policy** (`PolicyBase<TModel, TMetadata>`) — created by minimal propositions, or when both `WhenTrue()` and `WhenFalse()` (singular) are used. Guarantees exactly one value.
```

Replace that whole line with:

```markdown
- **Policy** (`PolicyBase<TModel, TMetadata>`) — created by minimal propositions, or when both `WhenTrue()` and `WhenFalse()` (singular) are used. Guarantees a single `Value` — but see below: that value is a *selection*, not a claim that only one cause exists.
```

- [ ] **Step 3: Add the Value-vs-Values subsection**

In `CLAUDE.md`, find this line, which ends the "Policy vs Spec" section:

```markdown
Policy is a subtype of Spec, so policies can be used anywhere a spec is expected. The reverse is not true — a spec that yields multiple values cannot be treated as a policy.
```

Append immediately after it (before the `## Architecture Notes` heading):

````markdown

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
````

- [ ] **Step 4: Verify the prose matches the pinned tests**

Confirm by inspection that the `Value`/`Values` examples in Steps 1-3 match the assertions committed in Tasks 1 and 2:
- `Value == "right-false"`, `Values == ["left-false", "right-false"]` (Task 1)
- three-policy chain yields three values (Task 2, Step 1)
- middle-satisfied yields one value (Task 2, Step 2)

Then confirm no source file was touched:

```bash
git status --short
```

Expected: only `CLAUDE.md` modified.

- [ ] **Step 5: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: state where policy preservation stops and why"
```

---

### Task 4: Correct the user-facing OrElse reference

**Files:**
- Modify: `docs/operators/OrElse.md:13-14` (replace the "quirks" sentence)
- Modify: `docs/operators/OrElse.md` (insert a Policies section after line 56, before `### [Boolean Results]`)

**Interfaces:**
- Consumes: the values pinned in Task 1 — the documented snippet output must match them.
- Produces: nothing.

- [ ] **Step 1: Replace the "quirks" explanation**

In `docs/operators/OrElse.md`, replace lines 13-14, which currently read:

```markdown
This is due to quirks regarding the overloading of the `||` operator, only the `OrElse()` method is
available for use with propositions.
```

with:

```markdown
Only the `OrElse()` method is available for propositions because C# cannot overload `||` directly.
The expression `x || y` compiles to `T.false(x) ? x : T.|(x, y)`, and the selected `operator |` must
take *and* return exactly `T` — so a short-circuiting operator on propositions would have to be built
out of the eager `|`, which always evaluates both operands. `x || y` also short-circuits by returning
`x` itself rather than a composed node, so it could not produce the `OrElse` node that appears in a
justification tree.
```

- [ ] **Step 2: Add the Policies section**

Insert the following immediately before the `### [Boolean Results](xref:Motiv.BooleanResultBase`1)` heading:

````markdown
### [Policies](xref:Motiv.PolicyBase`2)

`OrElse()` is the one composition that preserves a policy: `policy.OrElse(policy)` returns a
<xref:Motiv.PolicyBase`2>, so an `OrElse` chain behaves like `??` — it yields a single `Value` even
when nothing matched.

When the chain is satisfied, `Value` is the first operand that matched. When nothing matched, every
operand is a genuine cause and `Value` is the **last-evaluated** operand's — the fallback:

```csharp
var left = Spec
    .Build((object _) => false)
    .WhenTrue("left-true")
    .WhenFalse("left-false")
    .Create("left");

var right = Spec
    .Build((object _) => false)
    .WhenTrue("right-true")
    .WhenFalse("right-false")
    .Create("right");

var result = left.OrElse(right).Evaluate(new object());

result.Satisfied; // false
result.Value;     // "right-false"                    <- the selection: last evaluated
result.Values;    // ["left-false", "right-false"]    <- every contributing cause
```

`Value` is therefore a *selection*, not a guarantee that only one cause exists. Use `Values` to reach
everything it was selected from. `Values` flattens a nested chain — three policies combined with
`OrElseTogether()` yield three values, not the root node's two — reports only causal values, and
works for any metadata type.

Note that `Causes` and `Underlying` describe the **binary composition shape** rather than the
flattened causal set, so a three-policy chain reports two operands at its root.
````

- [ ] **Step 3: Verify the documented snippet's output is accurate**

The snippet in Step 2 is deliberately identical to the test committed in Task 1, so that test proves it. Confirm the correspondence:

```bash
grep -A20 "Should_select_the_last_evaluated_value_while_retaining_every_cause" src/Motiv.Tests/OrElsePolicyTests.cs
```

Expected: the test body asserts `result.Value.ShouldBe("right-false")` and
`result.Values.ShouldBe(["left-false", "right-false"])`, and builds `left`/`right` with the same
`WhenTrue`/`WhenFalse`/`Create` arguments as the documented snippet. If the strings differ in any
way, change the documentation to match the test — the test is the source of truth.

- [ ] **Step 4: Commit**

```bash
git add docs/operators/OrElse.md
git commit -m "docs(orelse): explain the || mechanism and document Value vs Values"
```

---

### Task 5: Full-suite verification and review

**Files:**
- No edits expected. If this task requires edits, an earlier task was wrong.

**Interfaces:**
- Consumes: all prior tasks.
- Produces: a verified, reviewed branch.

- [ ] **Step 1: Run the full solution test suite**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test
```

Expected: all tests pass on net8.0, net9.0 and net10.0. net472 compiles but does not execute on macOS — that is expected, not a failure to investigate.

If any example-project test fails (`Motiv.Poker.Tests`, `Motiv.ECommerce.Tests`, `Motiv.SmartHome.Tests`), stop and report — this plan changes no production code, so such a failure is pre-existing or environmental and must not be "fixed" by editing assertions.

- [ ] **Step 2: Confirm no production code was changed**

```bash
git diff --stat main...HEAD
```

Expected: only `CLAUDE.md`, `docs/operators/OrElse.md`, `src/Motiv.Tests/OrElsePolicyTests.cs`, `src/Motiv.Tests/PolicyExtensionsTests.cs`, and the two spec-document commits. **Nothing under `src/Motiv/`.**

- [ ] **Step 3: Run the mandatory code-simplifier review**

Per `CLAUDE.md`'s "Post-Implementation Code Review" convention, spawn a `code-simplifier` agent scoped to the two modified test files. Ask it to check for duplication across the four new tests and for naming that does not match the surrounding file's conventions.

Apply any improvements it identifies, then re-run:

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Tests -f net10.0 --filter "FullyQualifiedName~PolicyExtensionsTests|FullyQualifiedName~OrElsePolicyTests"
```

Expected: PASS.

- [ ] **Step 4: Commit any review fixes**

Only if Step 3 produced changes:

```bash
git add src/Motiv.Tests/OrElsePolicyTests.cs src/Motiv.Tests/PolicyExtensionsTests.cs
git commit -m "test(orelse): apply code-simplifier review"
```
