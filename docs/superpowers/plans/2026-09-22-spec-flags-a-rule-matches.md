# Flags A — `Matches` on the rule classes — Plan

**Source spec:** issue #236, section "Prerequisite inside the live-rules surface" (slice 1 of 6).
**Design:** `docs/superpowers/specs/2026-09-22-spec-flags-a-rule-matches-design.md`

**Goal:** give every live-rule handle a boolean-only entry point that agrees with `Evaluate`, reaches
the bound spec's fast path when the binding is unaudited, and still records when it is audited.

## Slicing #236

#236 is six PRs, not one. This slice is its stated prerequisite. The rest became tickets on #169:

| Ticket | Slice | Blocked by |
|---|---|---|
| #236 | A: `Matches`/`MatchesAsync` on the rule classes | none |
| #277 | B: `Motiv.OpenFeature`, boolean flags, `EvaluationContext` model, reason mapping, docs page | none |
| #278 | C: variant flags over `PolicyRule`, `PROVIDER_CONFIGURATION_CHANGED` | #277 |
| #279 | D: explain-when sampling | #277 |
| #280 | E: `Motiv.FeatureManagement` contextual filter | none |
| #281 | F: bucketing worked example | #277 |

## Steps

1. **Red.** Write `test/Motiv.Serialization.Tests/Rules/RuleMatchesTests.cs` (agreement, hot swap,
   pinned snapshot, unbound, short-circuit). Add `Matches` entry points to
   `RuleSpanCorrelationTests` (tag theory, no-core-span theory) and to `DecisionRecordingTests`
   (audited records per match with full outcome, unaudited records nothing). Build: CS1061 on
   every `Matches`/`MatchesAsync`, which is the right failure.
2. **Green, sync.** Extract `Rule.EvaluateIn(generation, state, model)` from `Evaluate`, then add
   `Matches`: read `Scope.Active` once; if audited, `EvaluateIn(...).Satisfied`; otherwise open the
   rule span and return `state.Spec.Matches(model)`.
3. **Green, async.** Extract `AsyncRule.EvaluateAsyncIn`, then add non-`async` `MatchesAsync`:
   audited → `SatisfiedAsync(EvaluateAsyncIn(...))`; unaudited → `state.Spec.MatchesAsync`, wrapped
   in `CloseAsync` only when a span was opened.
4. **Mutation check.** Route the unaudited path through `Evaluate`, confirm the short-circuit and
   no-core-span tests fail, then restore.
5. **Docs.** Add a "Matching" section to `docs/live-rules/Rules.md`, plus this plan and the design.
6. **Review.** Run the `code-simplifier` pass, then the full solution suite.
