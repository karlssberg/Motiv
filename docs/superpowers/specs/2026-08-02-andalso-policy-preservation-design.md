# AndAlso Policy Preservation

**Date:** 2026-08-02
**Status:** Approved
**Builds on:** `docs/superpowers/specs/2026-08-02-policy-preservation-boundary-design.md`
**Branch:** `claude/andalso-policy-preservation`, stacked on `claude/orelse-policy-operators-a99626` (PR #90)

## Context

The preceding branch established the rule that governs this one:

> Policy preservation follows **short-circuiting**. A short-circuiting combinator always has a
> well-defined last-evaluated operand, so a single `Value` is a total function of the evaluation path.

`OrElse` and `Not` implement that rule today. `AndAlso` does not — `policy.AndAlso(policy)` returns a
spec. That branch recorded it as an outstanding gap rather than a decision. This branch closes it.

### Why the earlier rejection was wrong

The original reasoning rejected a policy-preserving `AndAlso` because "when satisfied, both operands
are causal and both are the point, so nominating one discards what the name emphasises."

That argument does not discriminate. It is equally true of an **unsatisfied `OrElse`**, where both
operands are causal and both are the point — and there the answer is the `Value`-is-a-selection /
`Values`-reaches-the-rest framing that the same branch introduced. The mitigation defuses the
objection. A property shared by the combinator we kept cannot be why we rejected the other.

The fallback discriminator — "`OrElse` is `??`, a universally understood single-value idiom, and
`AndAlso` has no equivalent" — does not hold either. `AndAlso` chaining is railway-oriented /
`Result.bind` semantics: **first failure wins, otherwise the final value**. That is as established an
idiom as `??`, in a different tradition.

With both objections removed, the short-circuiting rule stands unopposed, and `AndAlso` qualifies.

## Semantics

`AndAlsoPolicyResult.Value` is `(Right ?? Left).Value` — identical to `OrElsePolicyResult`. Read
through the short-circuit shape it produces:

```
a.AndAlso(b).AndAlso(c)
  a unsatisfied              -> Value = a's false value   (first failure)
  a satisfied, b unsatisfied -> Value = b's false value   (first failure)
  all satisfied              -> Value = c's true value    (last evaluated)
```

**A conceded weakness.** For `OrElse`, the last operand is conventionally the *designated fallback* —
chains are written that way deliberately, so "nothing matched → last" is meaningful. For `AndAlso`,
the last operand is just whichever gate happened to be written last, so "all satisfied → last" is
arbitrary. It is accepted because:

- any other choice would be less principled, not more — no operand is distinguished when all pass;
- the failure direction, which is the one callers actually ask about, is precisely meaningful;
- `Values` still reports every contributing value, so nothing is lost;
- arbitrary-but-deterministic is what `OrElse` already ships in its own degenerate direction.

## Surface

### New internal classes

| new file | mirrors | inversion |
|---|---|---|
| `AndAlso/AndAlsoPolicy.cs` | `OrElse/OrElsePolicy.cs` | short-circuits on an unsatisfied left |
| `AndAlso/AndAlsoPolicyResult.cs` | `OrElse/OrElsePolicyResult.cs` | `Satisfied = left && (right ?? true)` |
| `AndAlso/AsyncAndAlsoPolicy.cs` | `OrElse/AsyncOrElsePolicy.cs` | as above |
| `AndAlso/ExpressionAndAlsoPolicy.cs` | `OrElse/ExpressionOrElsePolicy.cs` | as above |

`Value` is `(Right ?? Left).Value` in all cases — no inversion. `GetCauses()` transfers unchanged: it
yields operands whose `Satisfied` matches the overall outcome, which de-noises correctly for
conjunction as well as disjunction.

### New public members

| type | member | returns |
|---|---|---|
| `PolicyBase<TModel,TMetadata>` | `AndAlso(PolicyBase<TModel,TMetadata>)` | `PolicyBase` |
| `PolicyBase<TModel,TMetadata>` | `AndAlso(AsyncPolicyBase<TModel,TMetadata>)` | `AsyncPolicyBase` |
| `PolicyResultBase<TMetadata>` | `AndAlso(PolicyResultBase<TMetadata>)` | `PolicyResultBase` |
| `AsyncPolicyBase<TModel,TMetadata>` | `AndAlso(AsyncPolicyBase<TModel,TMetadata>)` | `AsyncPolicyBase` |
| `AsyncPolicyBase<TModel,TMetadata>` | `AndAlso(PolicyBase<TModel,TMetadata>)` | `AsyncPolicyBase` |
| `ExpressionPolicyBase<TModel,TMetadata>` | `AndAlso(ExpressionPolicyBase<TModel,TMetadata>)` | `ExpressionPolicyBase` |
| `PolicyExtensions` | `AndAlsoTogether<TModel,TMetadata>` | `PolicyBase` |
| `PolicyResultExtensions` | `AndAlsoTogether<TMetadata>` | `PolicyResultBase` |

`ExpressionPolicyBase` additionally needs the `new` redeclarations of `AndAlso(SpecBase<TModel,TMetadata>)`
and `AndAlso(SpecBase<TModel>)` that give the inherited overloads equal declaring-type precedence.
`ExpressionPolicyBase.cs:218` documents why the `OrElse` equivalents exist; the same reasoning applies.

## Hazards

### 1. The collapsible-operand predicate must be widened in existing files

`BinarySpecDescription` takes a predicate identifying operands that collapse into the same operation
heading. The `OrElse` family's predicate is a **six-way union naming both spec and policy types**,
duplicated verbatim in `OrElseSpec.cs`, `OrElsePolicy.cs` and `ExpressionOrElseSpec.cs`:

```csharp
operand => operand is OrSpec<TModel, TMetadata> or OrElsePolicy<TModel, TMetadata>
    or OrElseSpec<TModel, TMetadata> or ExpressionOrSpec<TModel, TMetadata>
    or ExpressionOrElseSpec<TModel, TMetadata> or ExpressionOrElsePolicy<TModel, TMetadata>;
```

The `AndAlso` family's is four-way with no policy types:

```csharp
operand => operand is AndSpec<TModel, TMetadata> or AndAlsoSpec<TModel, TMetadata>
    or ExpressionAndSpec<TModel, TMetadata> or ExpressionAndAlsoSpec<TModel, TMetadata>;
```

Every existing `AndAlso*` file carrying this predicate must be widened to include
`AndAlsoPolicy<TModel, TMetadata>` and `ExpressionAndAlsoPolicy<TModel, TMetadata>`, matching the
`OrElse` shape. Missing one does not fail a build or a unit test in isolation — it silently stops a
mixed spec/policy conjunction chain from collapsing, which surfaces only as malformed multi-level
`Justification` output.

There is a concrete completion check. The predicate lives in the composition classes, not the result
classes (results use `OrElseBooleanResultDescription` / `AndAlsoBooleanResultDescription`), so:

```bash
grep -ln "BinarySpecDescription" src/Motiv/OrElse/*.cs   # 6: 3 spec + 3 policy
grep -ln "BinarySpecDescription" src/Motiv/AndAlso/*.cs  # 3 today, must end at 6
```

`AndAlso` must finish with the same six-carrier shape: `AndAlsoSpec`, `AsyncAndAlsoSpec` and
`ExpressionAndAlsoSpec` widened, plus `AndAlsoPolicy`, `AsyncAndAlsoPolicy` and
`ExpressionAndAlsoPolicy` created with the widened predicate from the start.
`AndAlsoPolicyResult` does **not** carry one.

Per CLAUDE.md's batch-refactoring note, confirm with `git diff --stat` that the expected set of files
changed before moving on. Enumerate carriers by grepping rather than working from any written list.

### 2. Overload resolution changes for existing callers

`policy.AndAlso(policy)` currently binds to `SpecBase<TModel,TMetadata>.AndAlso` and produces an
`AndAlsoSpec` / `AndAlsoBooleanResult`. After this change it binds to the new overload and produces
an `AndAlsoPolicy` / `AndAlsoPolicyResult`.

Source and binary compatibility hold — `PolicyBase : SpecBase`, so existing assignments still compile,
and the old method still exists for already-compiled callers. The risk is **behavioural**: if the two
result types render `Reason`, `Assertions`, `Justification` or `Values` differently, every existing
consumer who combined two policies sees changed output.

They are expected to match, since `OrElsePolicyResult` and `OrElseBooleanResult` share
`OrElseBooleanResultDescription`. Expected is not verified. The first task therefore pins the current
output *before* any production change, and that test must still pass afterwards, unmodified. The
example projects, which assert on justification strings, are the second net.

## Decisions

1. **Full parity in one branch.** All four classes and all eight members. Partial parity would
   recreate, in a new place, exactly the asymmetry the preceding branch existed to document.
2. **Mirror rather than abstract.** The `OrElse` and `AndAlso` families stay as parallel explicit
   implementations. CLAUDE.md's "Avoid over-DRYing" note governs: a shared generic short-circuit base
   would collapse the two into branching logic for no gain.
3. **No new operator overloads.** The `||`/`&&` prohibition from the preceding branch is unaffected
   and stands. This changes only the named methods.
4. **Two parked follow-ups are folded in**, since this branch touches production code anyway:
   - an XML `<remarks>` on `PolicyResultBase.Value` pointing at `Values` — the discoverability fix the
     preceding spec identified but its no-production-code invariant blocked;
   - a superseded note on the stale `"do not add a policy-preserving AndAlso"` line still quoted in
     `docs/superpowers/plans/2026-08-02-policy-preservation-boundary.md`.

## Scope of Change

| File | Change |
|---|---|
| `src/Motiv/AndAlso/AndAlsoPolicy.cs` | new |
| `src/Motiv/AndAlso/AndAlsoPolicyResult.cs` | new |
| `src/Motiv/AndAlso/AsyncAndAlsoPolicy.cs` | new |
| `src/Motiv/AndAlso/ExpressionAndAlsoPolicy.cs` | new |
| `src/Motiv/AndAlso/AndAlsoSpec.cs`, `AsyncAndAlsoSpec.cs`, `ExpressionAndAlsoSpec.cs` | widen the collapsible predicate |
| `src/Motiv/PolicyBase.cs` | two `AndAlso` overloads |
| `src/Motiv/PolicyResultBase.cs` | one `AndAlso` overload, plus the `<remarks>` on `Value` |
| `src/Motiv/AsyncPolicyBase.cs` | two `AndAlso` overloads |
| `src/Motiv/ExpressionPolicyBase.cs` | one `AndAlso` overload plus `new` redeclarations |
| `src/Motiv/PolicyExtensions.cs`, `PolicyResultExtensions.cs` | `AndAlsoTogether` |
| `src/Motiv.Tests/` | regression pin, then TDD coverage per task |
| `CLAUDE.md` | `AndAlso` now implements the rule; drop the outstanding-gap wording |
| `docs/operators/AndAlso.md` | Policies section mirroring `docs/operators/OrElse.md` |
| `docs/superpowers/plans/2026-08-02-policy-preservation-boundary.md` | superseded note |

## Non-Goals

- Operator overloads (`&&`, `&`) on policies. Ruled out on mechanism by the preceding spec.
- Any change to `OrElse`, `Or`, `And`, `XOr` or `Not` behaviour.
- A shared abstraction unifying the `OrElse` and `AndAlso` families.
- Higher-order propositions (`AsAllSatisfied` and friends) — untouched.

## Verification

Run the full solution suite, not just `Motiv.Tests` — the example projects assert on justification
strings and are the net for hazard 1.

```
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH"
dotnet test
```

`dotnet test` exits 1 on this host solely because net472 testhosts require mono; CI covers that
target. Judge success by the per-assembly `Failed: 0` lines, not the exit code.

Unlike the preceding branch, this is new behaviour, so TDD applies normally: write the failing test,
confirm it fails for the right reason, implement minimally, confirm green.
