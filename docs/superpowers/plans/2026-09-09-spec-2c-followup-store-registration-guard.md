# Spec 2C follow-up — The registration that could be called twice — Implementation Plan

**Design:** [`2026-09-09-spec-2c-followup-store-registration-guard-design.md`](../specs/2026-09-09-spec-2c-followup-store-registration-guard-design.md)
**Ticket:** [#132](https://github.com/karlssberg/Motiv/issues/132)
**Source:** bundle spec
[2 — Durability & Data](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/2-durability-and-data.md),
§3 App surface (16) — the EF reference store built for §6 step 5.

## What this is

`AddMotivEntityFrameworkStore` becomes once-only. A second call of either overload throws
`InvalidOperationException` instead of leaving two competing
`IDbContextFactory<MotivStoreDbContext>` registrations for DI to resolve last-wins.

Latent robustness, not a live defect: no documented usage reaches it, and the existing self-wrap
guard (`typeof(TContext) != typeof(MotivStoreDbContext)`) is sound. The ticket names the fix and the
convention to match, and neither is reopened here.

## Global constraints

- **The convention is already written down.** `MotivRulesBuilder.AddPropositions`, `AddRuleStore`,
  `AddGovernance` and `AddRefresh` all refuse a second call with the same shape — a
  `Services.Any(descriptor => descriptor.ServiceType == typeof(…))` probe and an
  `InvalidOperationException` naming the method. This follows it exactly rather than inventing a
  fifth spelling; the ticket picks the refusal over `TryAddSingleton` for that reason.
- **No new type.** The rules builder needed a sentinel type per feature because each registers
  several services. Here one slot is filled by every successful call, so it is its own sentinel.
- **The guard runs before any registration.** A refusal must leave the container as the first call
  left it.
- **Argument validation stays first.** A null `configure` on a *first* call is still an
  `ArgumentNullException`, not a confusing "already called".

## Sequence

1. **Red.** `StoreRegistrationTests` — the collision shapes the ticket names (either overload
   twice; the two overloads in either order; two different derived contexts), plus the two properties that must survive:
   a first call still works, and a null argument is still an `ArgumentNullException`. Run and confirm
   the refusals fail on "should throw … but did not" and two pass as pins.
2. **Green.** The guard, in the generic overload only — the non-generic one delegates to it, so
   there is one guard site, not two.
3. **The adopter-facing doc.** `docs/live-rules/entity-framework-store.md` gains a "Call it once"
   note under the provider snippets, where an adopter choosing a provider would meet the rule.
4. **Full solution suite**, reading the exit code and grepping `error CS` — a filtered `dotnet test`
   cannot tell a compile failure from a clean run.
5. **`code-simplifier` pass**, per `CLAUDE.md`.

## Verification

- The EF store suite green (51 tests), and the full solution suite green on every runnable target
  framework: 17 assembly/TFM legs, including the `net8.0` and `net9.0` legs of `Motiv.Tests` and
  `Motiv.Serialization.Tests`, which only launch under the user-local `~/.dotnet/dotnet` muxer.
- **The `net472` legs did not run**, and cannot on this machine: vstest needs `mono` to host them.
  They *build*, which is what CI compiles them for. A `dotnet test` over the whole solution therefore
  exits non-zero here even when everything runnable passed — the exit code has to be read against
  that, not taken at face value in either direction.
- `Should_leave_the_container_as_the_first_call_left_it` is the one that proves the guard's
  *position*, not merely its existence: it counts descriptors before and after and asserts the losing
  context's factory was never registered. A guard placed after `AddDbContextFactory` would pass every
  other test in the file and fail this one.
