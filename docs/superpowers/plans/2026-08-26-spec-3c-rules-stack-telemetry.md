# Spec 3C — The Rules-Stack Telemetry Surface — Implementation Plan

**Design:** [2026-08-26-spec-3c-rules-stack-telemetry-design.md](../specs/2026-08-26-spec-3c-rules-stack-telemetry-design.md)
**Ticket:** [04](https://github.com/karlssberg/Motiv/issues/104), step 3 of bundle spec 3's §6
**Source:** bundle spec [3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
§2 (telemetry, PII control) and §3 (readiness, correlation)

> Reconstructed from the shipped diff after the merge, per the docs backlog on
> [#169](https://github.com/karlssberg/Motiv/issues/169). The sequence below is what the build actually
> did, including the four rounds CI and review added after the first green run.

## Global constraints

- **TDD throughout.** Failing test → confirm it fails for the right reason → minimum code → green.
- **Core `Motiv`'s signal names are frozen contract.** Nothing here may rename or repurpose a
  `motiv.*` name; the one permitted change reaching published `Motiv` is additive — the explanation-detail
  control spec 3 §2 authorises by name.
- **Nothing is emitted unsubscribed.** The surface must stay zero-cost until a listener attaches, which
  is what makes registering the readiness check and the break-glass gauge unconditionally safe.
- **A reading must not keep its subject alive.** Observable instruments need a live object to read from,
  and the obvious registry is a leak — see design decision 3. This constrains the shape before the first
  observable instrument is written.
- **Run the whole solution.** This slice touches evaluation-path types (`Rule`, `PolicyRule`, and their
  async siblings) and process-wide state, so per-project runs are not sufficient. Both of the failures
  that got past the first green run were cross-class or cross-framework.

## File structure

```
src/Motiv.Serialization/Diagnostics/MotivRulesTelemetry.cs   (new — source, meter, 13 instruments, spans)
src/Motiv.Serialization/Diagnostics/TelemetrySubjects.cs     (new — the weak registry readings come from)
src/Motiv.Serialization/Diagnostics/NodeSpanWriter.cs        (new — the iterative result-tree walk)
src/Motiv.Serialization/Rules/{Rule,AsyncRule}.cs            (the evaluate span, two entry points)
src/Motiv.Serialization/Rules/{PolicyRule,AsyncPolicyRule}.cs (…and two more, both `new` shadows)
src/Motiv.Serialization/Rules/RuleSet.cs                     (bind failures, store timing, refresh outcomes)
src/Motiv.Serialization/Propositions/{PropositionSet,BindingScope}.cs   (the same, proposition-side)
src/Motiv.Serialization/Governance/{BreakGlass,ChangeRequestSet}.cs     (the gauge; publishes under it)
src/Motiv.Serialization/Decisions/{DecisionLog,DecisionCaptureRegistry}.cs  (readings; the PII ceiling)
src/Motiv.Serialization.AspNetCore/MotivStoreHealthCheck.cs  (new — readiness)
src/Motiv.Serialization.AspNetCore/MotivRulesServiceCollectionExtensions.cs  (registers it)
src/Motiv.Serialization.Tests/Diagnostics/*.cs               (new — 10 files incl. the harness)
src/examples/Motiv.RulesEngine.Sample/Program.cs             (maps /health/ready, anonymous)
docs/observability/rules-stack.md                            (new — the user-facing surface)
```

## Sequence

1. **The source, the meter, and the authoring instruments.** `bind_failures`, `publish_conflicts`,
   `store.duration` — the three with real call sites, so the harness and the naming test are exercised
   before anything observable exists.
2. **The weak subject registry**, then the readings that need it: `catalog.size`, `generation`,
   `replica_lag`, and the decision-log trio Spec 3B left waiting. `QueueDepth` is new on `DecisionLog`.
   Assert that a collected subject stops being reported — the property the weakness exists for.
3. **Break-glass.** The gauge, `Off` registering too, and the counter of publishes made under it. This
   forces the positional record out longhand; the clone's reporting obligation comes with it.
4. **The PII ceiling.** `ExplanationCeiling` on the capture registry as a pure read first, then
   `DecisionLog`'s constructor applying it. Tests for the two properties that make it safe: it only
   tightens, and it never derives `ReasonOnly`.
5. **The evaluate span.** `motiv.rules.evaluate` carrying name and version, parenting core's span, from
   all four entry points — `PolicyRule.Evaluate` and `AsyncPolicyRule.EvaluateAsync` are `new` shadows, so
   they are the two a sweep over base declarations would miss.
6. **Per-node spans.** Opt-in, riding `audited`, iterative walk, truncation tagged on the evaluation span.
7. **Readiness.** The `motiv-store` check, registered by `AddMotivRules`; the sample maps
   `/health/ready` filtered to the `ready` tag, anonymous, since a load balancer holds no token.
8. **The user-facing page** — `docs/observability/rules-stack.md`, cross-linked from the observability
   index, `Overview.md` and `README.md`.
9. **The simplification pass**, by hand: the timed-store-call wrapper, and `NodeSpanWriter` extracted.

## Rounds after the first green run

Recorded because four of the nine commits are here, and three of them found real defects.

10. **A pre-existing flake, fixed in passing.** `DecisionLogTests` enqueued into a possibly-full queue
    under `Drop` and shed the record it then looked for — reproduced on unmodified main, 2 of 6 runs.
    `QueueDepth` made it deterministic.
11. **The Windows CI failure**, which is design decision 6 seen from a test suite: constructing a
    `DecisionLog` tightens `ExplanationDetail` process-wide and permanently, and four existing classes
    were doing that in parallel with two new tests asserting on explanation text. Serialize every class
    that constructs a `DecisionLog` — a blunt rule, on purpose, because it is easier to keep true than
    one about postures. Reproduce the mechanism with a throwaway test before fixing.
12. **A CI verbosity change, to make the failure readable at all.** `--verbosity minimal`: 977 bytes
    where `normal` emits 146 KB per project per framework, against a log GitHub serves only the tail of.
    Verify a failing test still prints name, assertion and stack trace by deliberately breaking one.
13. **Node-span parenting**, once the log was readable: `Activity.Id`, not `Context`, or every node is a
    root on net472. Rewrite the test that could not have caught it — it compared two all-zero defaults.
14. **The break-glass record's hand-written members**, pinned after Codecov put the file at 50% patch
    coverage: deconstruction, value equality, `with`-copying, and the clone's duty to report. Verify the
    last has teeth by deleting the `Report()` call and watching it fail.
