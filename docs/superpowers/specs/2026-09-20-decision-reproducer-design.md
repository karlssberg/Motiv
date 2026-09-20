# Decision reproducer — Design

**Date:** 2026-09-20
**Status:** Implemented
**Parent:** `2026-09-20-decision-reproduction-mcp-design.md`, sections 4 and 5. Slice 3 of 5.
**Plan:** `docs/superpowers/plans/2026-09-20-decision-reproducer.md`

## Problem

A decision record pins the rule version, the build and every referenced proposition version, and
keeps as much of the input as its capture posture allows. Since slice 1 the proposition store is a
log, so every pinned version names a document that still exists. Nothing yet turned those anchors
back into a re-run. Without that, the log answers "what was decided" but not "would it decide the
same today, and if not, why".

## Decisions

1. **Reading stays off `IDecisionSink`.** `IDecisionSource` is a separate interface — `FindAsync(id)`
   and `QueryAsync(query)` — implemented by the sinks that keep records (`InMemoryDecisionSink`,
   `SqlDecisionSink`) and registered on its own with `AddDecisionSource`. A forwarding sink keeps
   nothing and must not be made to lie. `DecisionQuery` moves to `Motiv.Serialization` with it.
2. **The resolver sits beside the capture posture.** `DecisionLogOptions.Resolve` mirrors
   `Capture`: one `Reference<TModel>` resolver per model type, nothing registered by default. The
   mirror makes the pairing visible at the one place a posture is chosen.
3. **The rule replays itself.** `RuleBase.ReplayAsync(serializer, documentJson, model)` binds the
   pinned document (null: the compiled default) through the existing `BindStoredState` and evaluates
   the bound spec directly. Never `Evaluate`, so a replay records nothing, pins nothing and opens no
   span. The four rule flavours each implement it once. Because it records nothing, it binds an
   `audited` document without the capture gate that guards a live publish: a read-only reproduction
   host needs no decision log.
4. **A transient overlay over the live source.** Pinned proposition documents bind into a fresh
   `PropositionOverlay` layered by `LayeredSpecSource` over `BindingScope.Source` — the live source,
   not the bare registry — so a pinned version shadows today's head for exactly the pinned names
   while unpinned names resolve as production does. `LayeredSpecSource` now takes any `ISpecSource`
   beneath; the live generations still pass the registry.
5. **Dependency-ordered binding, dependents of a failure left unbound.** Pinned documents bind in
   passes: a document binds once every pinned name it references is bound. What is left when a pass
   binds nothing waits on a pin that failed (or on a cycle) and is noted `PropositionBindFailed`
   rather than resolved through the live head, which would silently substitute today's logic. The
   rule is the last dependent: after any `PropositionBindFailed` nothing replays, because the rule's
   own document would resolve the failed name through today's head.
6. **A missing pin falls back to the head and says so.** A version the log no longer holds — a
   pre-log store, or a record older than the log — binds the name's live head with
   `PropositionVersionMissing`; a name with no head at all binds nothing.
7. **Rehydration goes through the host's JSON options.** A `Whole` or `Redacted` capture from a
   durable sink is a `JsonElement`; from an in-memory sink it may already be the model. Either
   rehydrates to the rule's model type through `MotivRulesOptions.JsonSerializerOptions`, so
   converters match; a failure is `ModelUnresolved`, never a crash.
8. **`RuleVersionMissing` and `PropositionBindFailed` join the reasons.** The parent spec listed five
   reasons; the two cases it did not name — the rule log lacking the pinned version, and a pinned
   document that no longer binds — are real and distinct from the five.
9. **No HTTP surface.** The endpoint, the printer and the MCP are slices 4 and 5; this slice is the
   primitive and its DI registration.

## Rejected

- **A query method on `IDecisionSink`.** Every forwarding sink would have to throw.
- **Resolving through `IRuleStore` heads instead of history.** The head is today's rule; the record
  pinned a version.
- **Binding pinned documents through `PropositionSet.CreateAsync` into a scratch set.** That writes
  a store and takes the scope gate; a reproduction must touch neither.
- **Quarantining a pinned document that will not bind, as `Load` does.** Quarantine leaves the
  compiled spec beneath the name to resolve, which for a reproduction is a silent substitution.
- **Recording replays as decisions.** A replay is evidence about a decision, not a decision.

## Outcome

`DecisionSourceConformance` runs on both sinks (3 tests each). `DecisionReproducerTests` pins the
five review-focus inputs and ten more: an exact reproduction, a pinned version bound instead of the
head, a missing pin's head fallback, a reverted version replaying the compiled default, a missing
rule version, a build mismatch, a resolved reference, an erased subject, a reference with no
resolver, a diverged outcome, a redacted capture, a `JsonElement` rehydration, pins that reference
each other, an unknown id, and a replay that leaves the log untouched. `DecisionSourceDiTests`
covers the registration and the message naming a missing store. Studio registers the SQL sink as
its source and resolves seed customers by id.
