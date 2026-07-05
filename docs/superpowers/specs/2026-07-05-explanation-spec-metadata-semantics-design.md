# Explanation Spec Guard Policy and Metadata Semantics

**Date:** 2026-07-05
**Status:** Approved design, pending implementation plan
**Release target:** v8.0.0 (expression-trees line, PR #57)

## Problem

Two related inconsistencies exist in how explanation propositions
(`.WhenTrue(string)` / `.WhenFalse(string)`) treat their strings:

1. **Validation is misplaced.** `Create(string statement)` guards the
   `statement` with `ThrowIfNullOrWhitespace` everywhere (~70 factories), but
   the parameterless `Create()` — where the `WhenTrue` string *becomes* the
   statement — has no guard at all. `.WhenTrue(" ").WhenFalse("x").Create()`
   silently builds a proposition whose statement is a single space. One stray
   factory (`ExplanationFromPolicyResultWithNamePropositionFactory.cs:43`)
   guards `trueBecause` inside `Create(string)`, where the string is *not* the
   statement.

2. **Evaluation semantics are split across families.** When an explicit name
   is supplied, most families (`BooleanResultPredicateProposition`,
   `PolicyResultPredicateProposition`, most `HigherOrder*` classes) resolve
   `Reason` as `"name == true/false"` via a
   `string s when !Description.HasExplicitStatement => s` guard, while
   `Assertions` keep the strings. Three classes (`Proposition`,
   `SpecDecoratorProposition`, `PolicyDecoratorProposition`) use the string
   verbatim for both, with no `HasExplicitStatement` check.

## Decision

An explicit statement from `Create("name")` is the **sole authority** for
explanation text. WhenTrue/WhenFalse payloads — string or otherwise — are
metadata whenever a name exists. Strings serve as explanation text only when
they are also the statement source (parameterless `Create()`).

Named explanation specs therefore become observably identical to metadata
specs. This resolves the family split by going one step further than the
majority pattern: `Assertions` and `Justification` change too, not just
`Reason`.

### Behavioral specification

For explanation-shaped propositions (`TMetadata == string`):

| Build path | Statement | Reason / Assertions / Justification | Values (metadata) |
|---|---|---|---|
| `.WhenTrue("t").WhenFalse("f").Create()` | `"t"` | `"t"` / `"f"` | `["t"]` / `["f"]` |
| `.WhenTrue("t").WhenFalse("f").Create("name")` | `"name"` | `"name == true"` / `"name == false"` | `["t"]` / `["f"]` |
| `.WhenTrue(m => ...).WhenFalse(...).Create("name")` | `"name"` | `"name == true/false"` | resolved strings |
| `.WhenTrueYield(...).Create("name")` | `"name"` | `"name == true/false"` | yielded strings |

Delegate and yield rows have no parameterless form: a statement cannot be
derived from a delegate, and delegate-form explanation specs are already
`MetadataPropositionFactory<TModel, string>` instances.

### Guard policy

- `Create(string statement)`: keeps `statement.ThrowIfNullOrWhitespace`
  everywhere (unchanged). **No** guards on WhenTrue/WhenFalse values; the
  stray `trueBecause` guard at
  `ExplanationFromPolicyResultWithNamePropositionFactory.cs:43` is deleted.
- Parameterless `Create()`: **adds**
  `trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause))` to the ~28
  "WithName" factories, because the string becomes the statement and inherits
  the statement's contract.
- `falseBecause` gets no eager guard — mechanically impossible, because the
  code-gen fluent overloads wrap `WhenFalse("...")` into a closure
  (`_ => whenFalse`) before `Create()` runs. Instead, a uniform
  evaluation-time fallback applies in unnamed specs: if a resolved assertion
  string is null/empty/whitespace, explanation text falls back to
  `Description.ToReason(satisfied)` (e.g. `"trueBecause == false"`). The same
  fallback covers whitespace produced by delegates at runtime.

**Invariant:** every result always carries meaningful explanation text —
guaranteed eagerly where a raw string is available, and by fallback everywhere
else.

## Implementation shape

Factory-level rerouting (chosen over an evaluation-time
`HasExplicitStatement` check and over collapsing factory types via codegen):
the factory, not a runtime flag, decides the semantics. This matches the
project convention of explicit code over branching abstractions.

Metadata and explanation factories currently construct the same proposition
classes — which is why the `string because => because` runtime switch exists.
The split:

1. **Shared (named/metadata) classes lose the string special case.**
   `Proposition<TModel, TMetadata>` and siblings resolve assertions as
   `Description.ToReason(satisfied)` unconditionally. No
   `metadata.Value is string` branching. These serve every `Create(string)`
   path: metadata, delegate-explanation, yield-explanation, and named
   string-explanation alike.
2. **Each family gains a dedicated explanation-proposition class** for the
   parameterless path, bound to `TMetadata = string`, used only by the
   ~28 WithName factories' `Create()`. Single rule: assertion = the resolved
   string, falling back to `Description.ToReason(satisfied)` when
   null/empty/whitespace. `Create()` constructs the explanation class;
   `Create(string)` constructs the metadata-style class. The type encodes the
   semantics.
3. **Guard edits** as specified above.
4. **Cleanup fallout** (verify during implementation, do not assume):
   - The `!Description.HasExplicitStatement` branches in
     `BooleanResultPredicateProposition`, `PolicyResultPredicateProposition`,
     and higher-order classes become dead once the class split encodes the
     distinction — remove them.
   - If `HasExplicitStatement` retains no consumers (audit
     `NotSpecDescription`, `HigherOrderFromBooleanResultProposition:47`,
     `HigherOrderFromExpressionTreeExplanationProposition:46`), delete the
     flag.
   - Affected proposition classes are internal; test code constructs internal
     types directly via `[InternalsVisibleTo]`, so every constructor change
     needs a call-site sweep across production and test code.

**Scale estimate:** ~10–15 internal proposition classes across the five
families (BooleanPredicate, BooleanResultPredicate, Decorator, HigherOrder,
ExpressionTree) currently string-special-case and get the split treatment.
The implementation plan enumerates them precisely.

## Testing

TDD throughout. New tests first, per family:

- Named explanation specs yield `"name == true/false"` in
  `Reason`/`Assertions`/`Justification` while `Values` retains the strings.
- Unnamed specs keep string explanations.
- Whitespace-string fallback to `ToReason` (including delegate-produced
  whitespace at runtime).
- Parameterless `Create()` throws `ArgumentException` on whitespace
  `trueBecause`.
- `Create(string)` no longer throws on whitespace `trueBecause` (regression
  test pinning the outlier's removal).
- `statement` guards still throw.

Existing fallout: a large share of the ~13,267 tests assert explanation
strings from named explanation specs — expect broad mechanical updates in
`Motiv.Tests` and the example projects (`Motiv.Poker.Tests`,
`Motiv.ECommerce.Tests`, `Motiv.SmartHome.Tests`), which assert justification
output at integration level. Run the full solution suite across
net472/net8.0/net9.0/net10.0. The codecov patch gate requires the new
guard/fallback lines to be covered; the new tests satisfy it.

Audit `Motiv.CodeFix`/`Motiv.Analyzer`: the CodeFix generates spec code and
its tests assert generated output; generated `.WhenTrue(...).Create("name")`
chains now carry different runtime semantics, worth reflecting in CodeAction
descriptions if mentioned.

## Documentation

- `CLAUDE.md`: the "`== true/false` Suffix Rule" section inverts — the suffix
  applies whenever an explicit name is supplied, regardless of
  WhenTrue/WhenFalse types. Update the Explanation Proposition and Assertions
  Property Rules sections to match.
- `docs/`: `builder/WhenTrue.md`, `builder/Create.md`, and any page showing
  named explanation output; README examples.
- XML docs: update `Create()`/`Create(string)` summaries and add
  `<exception>` tags documenting the guards (currently none exist).

## Migration / release

Ships in v8.0.0 on the expression-trees line (PR #57). Release notes get a
migration entry:

> Named explanation specs now report `name == true/false`; to keep string
> assertions, use parameterless `Create()`, or read the strings from
> `Values`.

No obsolete-shim period — the change is behavioral, not API-shaped, so there
is nothing to mark `[Obsolete]`.
