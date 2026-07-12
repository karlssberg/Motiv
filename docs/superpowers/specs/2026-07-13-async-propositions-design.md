# Async Propositions — Design

**Date:** 2026-07-13
**Status:** Approved design, pending implementation plan

## Context

Motiv propositions are currently synchronous-only. Real-world rules frequently
depend on I/O — database lookups, HTTP APIs, feature-flag services — and today
users must pre-resolve those values before evaluation, losing composability and
short-circuiting across the async boundary.

This is the first of four planned capability initiatives, in order:

1. **Async propositions** (this spec) — foundational; changes the evaluation model
2. Production observability (OpenTelemetry integration)
3. Rule externalization (serialization of rules/results)
4. Compile-time power (source generators, AOT)

Async ships first because the later initiatives must be designed against the
final evaluation model, not retrofitted to it.

## Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Type model | Parallel `AsyncSpecBase` hierarchy | Purely additive; sync path and its perf work (#67–#69) untouched |
| Binary operators (`&`/`\|`/`^`) | Sequential left→right by default | Safe with non-thread-safe captured dependencies (EF `DbContext`, per-request services) |
| Concurrency | Explicit opt-in at composition/builder site | Parallel-safety is a property of the spec's captured dependencies — the spec author knows, the caller may not |
| Higher-order | Sequential by default, opt-in bounded concurrency via builder | Mirrors the operator decision |
| Packaging | Inside core `Motiv` package | `Task` needs no extra dependencies on any TFM; zero-dependency claim holds; sync/async mixing has no package seams |
| Task type | `Task<...>` everywhere (no `ValueTask`) | `ValueTask` would require `System.Threading.Tasks.Extensions` on net472; result allocation dominates anyway |

## API Surface

```csharp
var hasCredit = Spec
    .BuildAsync(async (User u, CancellationToken ct) =>
        await creditApi.CheckAsync(u.Id, ct))
    .WhenTrue("has credit")
    .WhenFalse("no credit")
    .Create();                                  // AsyncPolicyBase<User, string>

BooleanResultBase<string> result = await hasCredit.EvaluateAsync(user, ct);
bool ok = await hasCredit.MatchesAsync(user, ct);
```

- `Spec.BuildAsync(...)` overloads:
  - `Func<TModel, Task<bool>>`
  - `Func<TModel, CancellationToken, Task<bool>>`
  - async variants returning `BooleanResultBase<TMetadata>` / `PolicyResultBase<TMetadata>`
    (mirroring the sync `Build` overloads that wrap results)
- Downstream builder surface (`WhenTrue`, `WhenFalse`, `WhenTrueYield`,
  `WhenFalseYield`, `Create()` / `Create("name")`) is semantically identical to
  the sync builders, including v8 named-metadata semantics. Metadata factories
  remain synchronous.
- **Results are unchanged.** `EvaluateAsync` returns the existing immutable
  result types (`Task<BooleanResultBase<TMetadata>>`;
  `Task<PolicyResultBase<TMetadata>>` for policies). Async concerns only the
  *production* of a result — explanation, justification, and result composition
  need nothing new.
- No `IsSatisfiedByAsync` — the obsolete shim exists only for sync back-compat.

## Type Hierarchy

```
AsyncSpecBase<TModel>
└── AsyncSpecBase<TModel, TMetadata>       — EvaluateAsync / MatchesAsync
    └── AsyncPolicyBase<TModel, TMetadata>
```

- **Lifting:** operator overloads and composition methods on both hierarchies
  accept the other side; `syncSpec.AndAlso(asyncSpec)` and
  `asyncSpec & syncSpec` both yield async specs. Explicit `.ToAsyncSpec()`
  exists for when the type is needed directly.
- **No sync-over-async bridge.** There is deliberately no `.ToSyncSpec()` or
  blocking `Evaluate` on async specs.
- **Policy preservation mirrors sync:** `!asyncPolicy` is a policy;
  `asyncPolicy.OrElse(asyncPolicy)` is a policy; all other compositions return
  specs.

## Composition Semantics

- **`AndAlso` / `OrElse`** — await left; evaluate right only when the outcome
  requires it. Async short-circuiting means unnecessary I/O is never issued.
- **`&` / `|` / `^`** — always evaluate both, sequentially left→right.
- **Concurrent variants** — `AndConcurrently(right)` / `OrConcurrently(right)` /
  `XOrConcurrently(right)` evaluate both operands via `Task.WhenAll`. Results
  are indistinguishable from their sequential counterparts: same `Reason` text
  (`&`, `|`, `^` formatting), assertions in left-right order. Concurrency and
  asynchrony are evaluation details, never semantic ones. Exceptions propagate
  as `Task.WhenAll` propagates them.
- **`!` / `.Not()`** — async-preserving and policy-preserving as in sync.

## Higher-Order Propositions

The async builder supports the full higher-order surface (`AsAllSatisfied`,
`AsAnySatisfied`, `AsNSatisfied`, `AsAtLeastNSatisfied`, `AsAtMostNSatisfied`,
`AsNoneSatisfied`, …):

- **Default:** items evaluated sequentially in input order.
- **Opt-in bounded concurrency:** a builder step —
  `.AsAllSatisfied().WithMaxConcurrency(8)` — fans items out with a maximum
  degree of parallelism. Results are re-assembled in input order, so
  `AllModels`, `FalseModels`, assertions, and justifications are deterministic
  regardless of completion order.
- **`MatchesAsync` short-circuits across items** where the quantifier allows
  (e.g. `AsAllSatisfied` stops at the first false in sequential mode) — the
  async sibling of the sync higher-order `Matches` fast path.

## Cancellation & Errors

- `CancellationToken` threads from `EvaluateAsync` / `MatchesAsync` through
  every async predicate that accepts one; cancellation surfaces as standard
  `OperationCanceledException`.
- Predicate exceptions propagate out of `EvaluateAsync` exactly as sync
  predicate exceptions propagate out of `Evaluate` — no additional wrapping.

## Out of Scope (v1)

- Async metadata factories (`WhenTrue(async ...)`) — metadata generation stays
  synchronous.
- `Spec.From` with async lambdas — C# has no async expression trees.
- `IAsyncEnumerable<TModel>` collections for higher-order — natural follow-up.
- `ValueTask` overloads.
- Evaluation-site concurrency options — builder-site only; an evaluation-site
  cap can be added later if demanded.

## Testing Strategy

- **Equivalence matrix:** every async composition (lifted-sync and native-async
  operands, all operators) must produce results equal to the sync equivalent —
  `Satisfied`, `Reason`, `Assertions`, `Justification`, `Values`, metadata.
- **Short-circuit verification:** call-count spies prove `AndAlso`/`OrElse`
  skip the right operand and higher-order `MatchesAsync` stops early.
- **Concurrency determinism:** with deterministic gates (e.g.
  `TaskCompletionSource` ordering), concurrent variants must produce
  byte-identical explanations to sequential ones.
- **Cancellation:** a token cancelled mid-evaluation propagates
  `OperationCanceledException` and stops issuing further predicate calls.
- **Policy preservation:** type-level tests that `!`, `OrElse` keep
  `AsyncPolicyBase`.
- Full solution suite (including example projects) runs green; net472 target
  compiles without new dependencies.
