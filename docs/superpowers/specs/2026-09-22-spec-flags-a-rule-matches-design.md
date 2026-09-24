# Flags A — `Matches` on the rule classes — Design

**Date:** 2026-09-22
**Status:** Implemented
**Source spec:** issue #236, *Feature flags — OpenFeature provider and FeatureManagement filter over
live rules*, section "Prerequisite inside the live-rules surface". #236 came out of the 2026-09-14
positioning conversation, not a bundle spec, so its body is the source spec. Slice 1 of 6.
**Plan:** `docs/superpowers/plans/2026-09-22-spec-flags-a-rule-matches.md`
**Follow-ups:** #277 (OpenFeature boolean provider), #278 (variant flags and events), #279
(explain-when sampling), #280 (FeatureManagement filter), #281 (bucketing example).

## Problem

A flag call site wants a `bool`. The live-rule handles (`Rule`, `PolicyRule`, `AsyncRule`,
`AsyncPolicyRule`) exposed only `Evaluate`/`EvaluateAsync`. Every flag check would therefore build a
full result tree (assertions, justification, values) and throw it away, and it could not reach the
boolean fast path that core specs already have (`SpecBase.Matches`, plus the short-circuiting
higher-order `Matches` from `2026-07-12-higher-order-matches-fast-path.md`).

## Decisions

1. **`Matches` on `Rule`, `MatchesAsync` on `AsyncRule`, inherited by the policy flavours.** The
   policy flavours *shadow* `Evaluate` because the return type narrows to `PolicyResultBase`. A
   `bool` has nothing to narrow, so a shadow would be pure duplication. The tests still cover all
   four flavours, because an inheritance that evaluated the wrong spec would still compile.
2. **Unaudited: the bound spec's own `Matches`.** No result tree is built, and a higher-order spec
   may stop at its first counterexample. This is observable, and tested, as fewer predicate calls
   and as the absence of core's `motiv.evaluate` span.
3. **Audited: evaluate and record anyway.** Spec 3B made `audited` mean *every* decision is recorded
   ("audited means total, not sampled", `DecisionRecordingTests`). A fast path that skipped the log
   would be an unannounced way around the audit, because the caller's choice of method would decide
   whether a governed rule is recorded. Therefore `Matches` on an audited binding runs the full
   evaluation, records it with the full outcome, and returns `Satisfied`. The cost follows the flag
   the operator set, which is the right owner for it. Sampled explanation for *unaudited* rules is a
   separate channel, explain-when (#279).
4. **One read of `Scope.Active`, handed to the fallback.** `Evaluate`'s body became
   `EvaluateIn(generation, state, model)` (async: `EvaluateAsyncIn`). `Matches` inspects
   `state.Audited` and passes that same state on, so a publish landing between two reads cannot make
   it record a decision against a version other than the one it checked. Pinned snapshots
   (`PinSnapshot`) are honoured the same way `Evaluate` honours them.
5. **Telemetry: the rule span, no node spans.** `Matches` opens `motiv.rules.evaluate`, tagged with
   name and version, so a flag decision on the fast path is still findable from a publish. Node spans
   ride the audited flag and need a result tree. An unaudited match has neither, and an audited one
   gets them through the fallback.
6. **`MatchesAsync` is not an `async` method**, for the reasons `EvaluateAsync` gives. An unbound
   rule throws at the call. An unobserved, unaudited match forwards the spec's `ValueTask` untouched,
   and only a listener (`CloseAsync`) or an audit (`SatisfiedAsync` over the observed evaluation)
   pays for a state machine.

## Invariant

`rule.Matches(m) == rule.Evaluate(m).Satisfied` holds for every flavour and outcome
(`RuleMatchesTests.Should_agree_with_evaluate`). #279 depends on it: a sampled explanation is a
faithful account of the decision the fast path made only because the two cannot disagree.

## Verification

- `RuleMatchesTests`: agreement across the four flavours × {satisfied, unsatisfied}, hot swap, pinned
  snapshot, unbound (async throws synchronously), and short-circuit through `AsAllSatisfied`.
- `RuleSpanCorrelationTests`: the name and version tag on a match, and no core `motiv.evaluate` span
  on an unaudited match, for all four flavours.
- `DecisionRecordingTests`: an audited match records one decision per call with the full outcome,
  and an unaudited match records nothing, for all four flavours.
- Mutation check: routing unaudited `Matches`/`MatchesAsync` through `Evaluate` fails the
  short-circuit test and all four no-core-span theories.
