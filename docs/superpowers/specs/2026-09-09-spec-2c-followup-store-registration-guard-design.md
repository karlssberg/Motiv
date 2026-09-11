# Spec 2C follow-up — The registration that could be called twice — Design

**Date:** 2026-09-09
**Ticket:** [#132](https://github.com/karlssberg/Motiv/issues/132)
**Plan:** [`2026-09-09-spec-2c-followup-store-registration-guard.md`](../plans/2026-09-09-spec-2c-followup-store-registration-guard.md)
**Source:** bundle spec
[2 — Durability & Data](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/2-durability-and-data.md),
§3 App surface (16) — the EF reference store, §6 step 5.
**Lineage:** Spec 2C ([#128](https://github.com/karlssberg/Motiv/pull/128)) → the scoped re-review of
that fix wave, which raised this as non-blocking → here.

## The defect

`AddMotivEntityFrameworkStore<TContext>` registered the invariance-bridging adapter with
`AddSingleton`:

```csharp
services.AddSingleton<IDbContextFactory<MotivStoreDbContext>>(provider =>
    new DerivedContextFactory<TContext>(provider.GetRequiredService<IDbContextFactory<TContext>>()));
```

`AddSingleton` appends. `IServiceProvider` resolves a single service from the *last* descriptor
registered for its type. So a container that reached this line twice held two live registrations for
one slot, and the two stores — `EfRuleStore` and `EfPropositionStore`, which both resolve exactly
`IDbContextFactory<MotivStoreDbContext>` — opened every context against whichever database was
configured last. Nothing said so. The first call's `configure`, its connection string and its whole
database were simply inert.

Four call shapes reach it:

| Shape | What collides |
|---|---|
| non-generic, then generic | `AddDbContextFactory<MotivStoreDbContext>` owns the slot; the adapter takes it |
| generic, then non-generic | the adapter owns it; `AddDbContextFactory<MotivStoreDbContext>` takes it |
| either overload twice | two identical registrations, two `DbContextOptions` |
| generic with two different derived contexts | two adapters over two adopter factories |

Not a live defect, and the ticket says so: no documented usage calls it twice, and the self-wrap
guard that keeps `DerivedContextFactory` from wrapping itself into infinite recursion is sound. This
is latent robustness.

## Why a refusal rather than `TryAddSingleton`

Both close the hole. They disagree about what the second caller *meant*.

`TryAddSingleton` makes the second call a silent no-op: first-wins instead of last-wins. The adopter
who wrote two calls with two connection strings still gets one database, still learns nothing, and
now the surviving one is the one they wrote first — arguably the more surprising of the two, since
the later line reads like the intended override.

The refusal says the call is not meaningful. Registering two contexts is not layering: they contend
for one slot, and there is no composition of them that would be right. That is the reasoning
`MotivRulesBuilder` already committed to in four places —

> an argument quietly ignored is worse than a refusal

— for `AddPropositions`, `AddRuleStore`, `AddGovernance` and `AddRefresh`. The ticket names this
convention explicitly and applies it here. It is a locked choice, not a fresh one.

`TryAdd` does still appear in that file, for `BreakGlass.Off` in `AddGovernance` — and the contrast
sharpens the rule rather than muddying it. There, `TryAdd` is *last*-wins by construction: a host
registers its own `BreakGlass` after `AddGovernance`, and `AddSingleton` overrides `TryAdd`. It fills
a default nobody claimed. It is not resolving a contention between two deliberate arguments.

## The sentinel

The rules builder needs a distinct sentinel type per feature (`PropositionSet`, `RuleStoreOptions`,
`ChangeRequestSet`, `MotivRefreshOptions`), because each of those registers several services and none
of the individual slots identifies the feature.

Here no new type is needed, because **every successful call fills exactly one common slot**:

- on the zero-config path, `AddDbContextFactory<MotivStoreDbContext>` registers
  `IDbContextFactory<MotivStoreDbContext>` directly;
- on the derived path, the adapter registers that same closed type.

So `IDbContextFactory<MotivStoreDbContext>` *is* the sentinel, and a single probe catches all four
shapes above — including the mixed-overload ones, which a per-`TContext` sentinel would miss by
construction. One check, one site: the non-generic overload delegates to the generic one, so the
guard is written once.

```csharp
if (services.Any(descriptor =>
        descriptor.ServiceType == typeof(IDbContextFactory<MotivStoreDbContext>)))
    throw new InvalidOperationException(…);
```

### Position is part of the contract

The probe runs **before `AddDbContextFactory`**, and one test —
`Should_leave_the_container_as_the_first_call_left_it` — exists solely to pin that: it counts
descriptors either side of the refusal and asserts the losing context's factory was never registered.
A guard that threw *after* the first registration line would corrupt the very state it exists to
protect — the container would carry a half-registered second context past an exception the host may
well catch and log. Every other test in the file passes under that mistake.

Argument validation stays ahead of the probe, so a first call with a null `configure` is still an
`ArgumentNullException` rather than the misleading "already called".

## What this does not do

- **No escape hatch is preserved for a host that pre-registered its own
  `IDbContextFactory<MotivStoreDbContext>` and then calls this.** That host is refused. This is
  deliberate and is the same contention: the extension's whole job is to fill that slot, so a host
  that has already filled it should not be calling the extension. `AddRuleStore`'s tolerance of a
  directly-registered `IRuleStore` is not the analogous case — there, the direct registration is a
  documented alternative *instead of* the builder method, not a prelude to it.
- **`DerivedContextFactory` is untouched.** Its forwarding of `CreateDbContextAsync` rather than
  inheriting the interface's blocking default remains as Spec 2C left it.
- **Nothing about the store contract changes.** No invariant in §4 of the bundle spec is touched: the
  two stores are still never in one transaction, head is still a projection, and the generation is
  still a scalar read. This is a wiring refusal, above all of that.
