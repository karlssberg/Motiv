# MCP, decision endpoints and rule snapshots — Design

**Date:** 2026-09-20
**Status:** Implemented
**Parent:** `2026-09-20-decision-reproduction-mcp-design.md`, sections 7 and 8. Slice 5 of 5.
**Plan:** `docs/superpowers/plans/2026-09-20-mcp-and-snapshots.md`

## Problem

Slices 1–4 built the primitives: a version log for propositions, stored scenarios, a reproducer
with a fidelity verdict, a C# printer. None of it was reachable by the coding agent the whole design
exists for, nor by Studio; and a test generated from a reproduction had nothing to bind its
snapshot with short of a host.

## Decisions

1. **The MCP server lives in the adopter's host, on the official package, stateless.** `AddMcp()`
   registers `ModelContextProtocol.AspNetCore` with `HttpServerSessionMode.Stateless` and
   `MapMotivMcp(path)` mounts it behind `RequireAuthorization()`. The host already holds the
   compiled specs, the sink, the resolver and the grants; a standalone tool would have none.
2. **Tools call the services the endpoints call, and check the same grants.** `MotivMcpTools` is
   constructed per call from DI (`RuleSet`, the stores, the source, the reproducer, the scenario
   store, all optional beyond the rule set) and reads the caller through `IHttpContextAccessor`.
3. **Unreadable is not found.** A tool answers a rule or decision the caller may not `Read` with the
   message a missing one gets, so results never say which rules exist. The HTTP endpoints keep the
   HTTP surface's `403`, because the catalog reveals names there already.
4. **A model crosses the wire only as the host's JSON of what was captured or resolved**, and a
   reference capture as its key alone. `DecisionsContracts` is the one projection, used by the
   endpoints and the tools.
5. **Tools return `JsonElement`.** `WithTools<T>` takes serializer options at registration, where
   the host's `MotivRulesOptions` is not yet resolvable; serialising in the tool with the host's
   options keeps the wire shape identical to HTTP's. The price is no output schema on the tools;
   the descriptions carry the shape.
6. **`save_scenario` is the only write.** Its annotations say non-destructive, not idempotent,
   not read-only; its description says test data, never behaviour, not governed.
7. **The decision routes sit under `MapMotivRules`'s base path**, mapped only when an
   `IDecisionSource` is registered; `rules/{name}/csharp` when a rule set is. Version 1 of a rule
   with no stored row is the compiled default and answers `204`.
8. **`Motiv.Serialization.Snapshots`, not `.Testing`.** The spec's name already names the
   conformance suites linked into four test projects; a public package must not collide.
9. **The pinned binder is shared, through a resolver seam.** `PinnedPropositionBinder` takes a
   model-binding resolver and parser options rather than a `PropositionSet`: a `PropositionSet`
   claims the registry's scope, and a second snapshot bound over the same test registry would
   throw. `PropositionModelBinding.For<TModel>` is the factory `AddModel` and the snapshot share.
10. **One model type per snapshot.** The rows name model types by the host's ids and the only CLR
    type a test bind knows is `TModel`; rows spanning ids throw naming them.

## Rejected

- **A standalone `dotnet tool` MCP over the store.** No compiled specs, no sink, no resolver.
- **`403` from the tools.** A forbidden answer tells the agent the name exists.
- **A `get_model` tool.** The posture the capture registry refused; a model reaches the wire only
  through the log or the resolver.
- **Building a `PropositionSet` inside `RuleSnapshot.Bind`.** It claims the registry; the second
  test in a class would throw.

## Outcome

`ReproductionContractsTests` (2), `DecisionEndpointTests` (5), `MotivMcpEndpointsTests` (6, through
the MCP client), `RuleSnapshotTests` (3, on net8/net9/net10) and Studio's `McpEndpointTests` (1)
are green; every existing suite is unchanged. The parent spec is amended for the package name, the
route placement and `AddMcp`.
