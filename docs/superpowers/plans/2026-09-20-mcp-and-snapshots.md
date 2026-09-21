# MCP, Decision Endpoints and Rule Snapshots Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A coding agent reaches a logged decision, its reproduction, the printed C#, the rule documents and the scenarios through an MCP server hosted in the adopter's app; Studio reaches the same reproduction and C# over HTTP; and a generated test binds a rule-and-propositions snapshot against the compiled registry with `RuleSnapshot`.

**Architecture:** In `Motiv.Serialization.AspNetCore`, `MapMotivRules` maps `decisions`, `decisions/{id}`, `decisions/{id}/reproduction` and `rules/{name}/csharp` when an `IDecisionSource` is registered; `AddMcp()` on the builder plus `MapMotivMcp(path)` mount a Streamable HTTP MCP server on the official `ModelContextProtocol.AspNetCore` package whose seven tools call the same services the endpoints do and answer a rule the caller may not read as *not found*. A new package, `Motiv.Serialization.Snapshots`, ships `RuleSnapshot.FromJson(rule, propositions).Bind<TModel>(registry)`, layering the snapshot's proposition rows over the registry with the same pinned binder the reproducer uses, which this slice extracts into `PinnedPropositionBinder`. Over the wire a reproduction is a DTO: the model crosses as the host's JSON of the captured input or the resolver's result, and a `Reference` input as its key alone.

**Tech Stack:** C# / .NET 10; `ModelContextProtocol.AspNetCore` 2.2.0 (server) and `ModelContextProtocol` 2.2.0 (client, tests only) from nuget.org; xUnit + Shouldly; `Motiv.Serialization.Snapshots` targets `net8.0;net9.0;netstandard2.0;net10.0` like `Motiv.Serialization`.

**Spec:** `docs/superpowers/specs/2026-09-20-decision-reproduction-mcp-design.md`, sections 7 and 8. Slice 5 of 5.

## Global Constraints

- Every `dotnet` call needs `env -u MallocStackLogging -u MallocNanoZone` and the sandbox disabled; grep for `error CS`. The two packages are added to `Directory.Packages.props` (central package management) and restored with network access to `api.nuget.org`.
- The MCP surface is opt-in: a host that never calls `AddMcp()`/`MapMotivMcp` exposes nothing. `RequireAuthorization()` by default, as the rules API; `MapMotivMcp(path, anonymous: true)` is the escape hatch, mirroring `MotivRulesEndpointOptions.AllowAnonymous`.
- MCP grants: a rule the caller may not `Read` is answered *not found*, byte-identical to a rule that does not exist, so tool results do not leak names. The HTTP endpoints follow the HTTP surface's convention (`403`), because the catalog already reveals names there.
- A `Reference` input crosses the wire as its key and nothing else; a `Whole`/`Redacted` input and a resolved model cross as the host's JSON (`MotivRulesOptions.JsonSerializerOptions`).
- `save_scenario` is the only write; its description says it writes test data, never behaviour, and is not governed.
- The testing package is `Motiv.Serialization.Snapshots` (namespace `Motiv.Serialization.Snapshots`), not the spec's `Motiv.Serialization.Testing`: that namespace already names the conformance suites linked into four test projects. The parent spec is amended.
- `Motiv.Serialization.Snapshots` needs the internal parser, overlay and binder: add `<InternalsVisibleTo Include="Motiv.Serialization.Snapshots" />` to `Motiv.Serialization.csproj`.
- Branch: `claude/mcp-and-snapshots`, stacked on `claude/csharp-printer`. Plan and design doc (`docs/superpowers/specs/2026-09-20-mcp-and-snapshots-design.md`) land on the branch before the PR opens.

## Review Focus

1. A caller with `Read` on `pricing` asking `get_rule` for `billing.vat` must get the same not-found error text as asking for `billing.nonexistent`. Pinned by `Should_answer_not_found_for_a_rule_the_caller_may_not_read_exactly_as_for_a_missing_one` (Task 3).
2. `reproduce_decision` for a reference-only capture whose resolver failed must carry the key and no model fields; when the resolver succeeded, the model as the host's JSON. Pinned by `Should_return_only_the_key_for_an_unresolved_reference_capture` (Task 3) and `Should_project_a_reference_capture_as_its_key` (Task 1).
3. `save_scenario` without `Author` on the rule's namespace must be refused with the reason and never write. Pinned by `Should_refuse_save_scenario_without_author` (Task 3).
4. `RuleSnapshot.Bind` must shadow the registry's live heads with the snapshot's rows and bind them in dependency order, so it decides as the reproducer did even when the registry has moved on. Pinned by `Should_decide_as_the_reproducer_for_the_same_snapshot` (Task 4).
5. `GET rules/{name}/csharp?version=` for a version whose document is null (a revert) answers `204`, never `500`. Pinned by `Should_answer_no_content_for_a_reverted_version` (Task 2).

---

### Task 1: `PinnedPropositionBinder` and the wire contracts

**Files:**
- Create: `src/Motiv.Serialization/Decisions/PinnedPropositionBinder.cs`
- Modify: `src/Motiv.Serialization/Decisions/DecisionReproducer.cs` (delegate `BindIntoOverlay`/`ParsePending`/`Bind` to the binder; keep fetching and the head fallback)
- Create: `src/Motiv.Serialization.AspNetCore/DecisionsContracts.cs`
- Create: `src/Motiv.Serialization.AspNetCore.Tests/ReproductionContractsTests.cs`

**Interfaces:**
- Produces (internal, `Motiv.Serialization`):

```csharp
internal sealed record PinnedDocument(string Name, int Version, string? ModelType, string? DocumentJson, string? Description);

internal static class PinnedPropositionBinder
{
    /// Binds the documents in dependency order into an overlay over <paramref name="live"/>; a
    /// document that will not bind is noted and its dependents left unbound. Returns the layered
    /// source. <paramref name="propositions"/> supplies the model bindings and parser options.
    public static ISpecSource Bind(
        IReadOnlyList<PinnedDocument> documents, ISpecSource live, PropositionSet propositions, List<FidelityNote> notes);
}
```

- Produces (public, `Motiv.Serialization.AspNetCore`):

```csharp
public sealed record DecisionInputEntry(string Kind, JsonElement? Value, string? Key);
public sealed record DecisionEntry(Guid Id, string CorrelationId, DateTimeOffset TimestampUtc, string? Caller, string RuleName, int RuleVersion, string BuildId, IReadOnlyList<PropositionVersion> ReferencedPropositionVersions, DecisionInputEntry? Input, RuleEvaluationResult<object?> Outcome);
public sealed record FidelityNoteEntry(string Reason, string Detail);
public sealed record FidelityEntry(bool IsExact, IReadOnlyList<FidelityNoteEntry> Notes);
public sealed record ReproducedModelEntry(string Kind, JsonElement? Value, string? Key);
public sealed record PropositionRowEntry(string Name, int Version, string? ModelType, JsonElement? Document, string? Description, string Author, DateTimeOffset TimestampUtc);
public sealed record RuleRowEntry(string Name, int Version, JsonElement? Document, string Author, DateTimeOffset TimestampUtc, string? ChangeNote, string? ApprovalRef, string? BuildId);
public sealed record ReproductionEntry(DecisionEntry Decision, RuleRowEntry? Rule, IReadOnlyList<PropositionRowEntry> Propositions, ReproducedModelEntry Model, RuleEvaluationResult<object?>? Replayed, FidelityEntry Fidelity, string? CSharp, IReadOnlyList<string> CSharpWarnings);
public sealed record CSharpEntry(string Source, IReadOnlyList<string> Warnings);

internal static class DecisionsContracts
{
    public static DecisionEntry Entry(DecisionRecord record, JsonSerializerOptions json);
    public static ReproductionEntry Entry(Reproduction reproduction, JsonSerializerOptions json);
}
```

`DecisionInputEntry`/`ReproducedModelEntry` `Value` is `JsonSerializer.SerializeToElement(value, json)` for `Whole`/`Redacted`/`Resolved` and null otherwise; `Key` is the reference key for `Reference` (both records) and for `Resolved`. `Kind` is the enum name.

- [ ] **Step 1: Contracts tests (RED)** — `ReproductionContractsTests`: build a `Reproduction` by hand (a `DecisionRecord` with `DecisionInput.Reference("cust-42")`, a `ReproducedModel(Reference, null, "cust-42")`, no rule row, empty propositions, `Fidelity` with one `ModelUnresolved` note, `CSharp` null) and assert `Entry(...)`: `Decision.Input.Kind == "Reference"`, `.Value == null`, `.Key == "cust-42"`; `Model.Value == null`, `Model.Key == "cust-42"`; `Fidelity.IsExact == false`, `Notes[0].Reason == "ModelUnresolved"`. A second test with `DecisionInput.Whole(new { age = 30 })` and `ReproducedModel(Whole, new { age = 30 }, null)` asserts `Value!.Value.GetProperty("age").GetInt32() == 30` and `Key == null`.
- [ ] **Step 2: Implement the contracts; GREEN.**
- [ ] **Step 3: Extract the binder** — move `BindIntoOverlay`, `ParsePending`, `Bind` and `Pending` into `PinnedPropositionBinder` taking `PinnedDocument`s (the reproducer maps its `StoredPropositionVersion` rows to `PinnedDocument`s and passes `pinnedNames` derived from the rows as before — the binder derives `pinnedNames` from `documents` itself). `DecisionReproducerTests` stays green before and after. Commit.

```bash
git add src && git commit -m "Reproduction over the wire, and the pinned binder shared with the snapshot package

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 2: HTTP endpoints

**Files:**
- Create: `src/Motiv.Serialization.AspNetCore/MotivDecisionEndpoints.cs`
- Modify: `src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs` (after scenarios: `if (endpoints.ServiceProvider.GetService<IDecisionSource>() is { } decisions) MotivDecisionEndpoints.MapDecisionEndpoints(group, decisions, endpoints.ServiceProvider.GetService<DecisionReproducer>(), json);` and, inside `MapRuleEndpoints`, the csharp route)
- Create: `src/Motiv.Serialization.AspNetCore.Tests/DecisionEndpointTests.cs`
- Modify: `src/Motiv.Serialization.AspNetCore/RulesContracts.cs` if `CSharpEntry` is better placed there (keep it in `DecisionsContracts.cs`)

Routes under the rules group:

| Route | Grant | Answer |
|---|---|---|
| `GET decisions?correlationId&ruleName&satisfied&from&to&limit` | per record: `Read` on its rule; records the caller may not read are omitted | `{ records: DecisionEntry[] }`, newest first; `limit` clamped to `[1, 1000]` |
| `GET decisions/{id}` | `Read` on the record's rule, else `403` | `DecisionEntry`; unknown `404` |
| `GET decisions/{id}/reproduction` | `Read` on the record's rule, else `403` | `ReproductionEntry`; unknown `404`; `503` with the message when no `DecisionReproducer` is registered |
| `GET rules/{name}/csharp?version=` | `Read` on the rule | `CSharpEntry` printed exactly as the reproducer prints (registry async names, collections by element type, known names, class named after the rule); `204` when that version's document is null; `404` unknown rule or version; default version = the live one |

The csharp route needs the reproducer's print options: move `DecisionReproducer.Print`'s option-building into `internal static CSharpPrintOptions RulePrintOptions.For(RuleBase rule, SpecRegistry registry, RuleSerializerOptions serializerOptions)` in `Motiv.Serialization/Printing/` and call it from both; the route reads the version row from `IRuleStore.HistoryAsync` (`GetService<IRuleStore>()`; without a store only the live version prints, from `rules.FindEntry(name).DocumentJson`).

- [ ] **Step 1: Tests (RED)** — model on `ScenarioEndpointTests.StartAsync`: an `InMemoryDecisionSink` registered with `.AddRuleStore().AddPropositions().AddDecisionLog(sink, log => log.Capture.StoreWhole<Customer>()).AddDecisionSource(sink)` and an audited rule (`AddRule<ActiveRule>()`, then `PUT` its document `{ "audited": true, "rule": { "spec": "is-active" } }` through the rules endpoint as `RuleDiTests` does), then `POST rules/active-rule/evaluate` to log a decision and poll `GET decisions` until one record appears. Tests: list filters by rule and honours `limit`; `get` by id and `404`; reproduction `200` with `fidelity.isExact` true and `csharp` containing `Get<Customer>("is-active")`; a caller with `Read` on `other` only gets `403` on `decisions/{id}` and an empty list; `rules/active-rule/csharp` `200`, `?version=1` (the compiled default, null document) `204`, `?version=9` `404`, unknown rule `404`.
- [ ] **Step 2: Implement; GREEN; run `Motiv.Serialization.AspNetCore.Tests`; commit.**

---

### Task 3: The MCP server

**Files:**
- Modify: `Directory.Packages.props` (`<PackageVersion Include="ModelContextProtocol.AspNetCore" Version="2.2.0" />`, `<PackageVersion Include="ModelContextProtocol" Version="2.2.0" />`)
- Modify: `src/Motiv.Serialization.AspNetCore/Motiv.Serialization.AspNetCore.csproj` (`<PackageReference Include="ModelContextProtocol.AspNetCore" />`), `src/Motiv.Serialization.AspNetCore.Tests/Motiv.Serialization.AspNetCore.Tests.csproj` (`<PackageReference Include="ModelContextProtocol" />`)
- Create: `src/Motiv.Serialization.AspNetCore/Mcp/MotivMcpTools.cs`, `Mcp/MotivMcpEndpoints.cs`
- Modify: `MotivRulesServiceCollectionExtensions.cs` (`AddMcp()`)
- Create: `src/Motiv.Serialization.AspNetCore.Tests/MotivMcpEndpointsTests.cs`

**Interfaces:**
- `MotivRulesBuilder AddMcp()`: `Services.AddHttpContextAccessor(); Services.AddMcpServer(o => o.ServerInfo = new() { Name = "motiv", Version = <assembly version> }).WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.Stateless).WithTools<MotivMcpTools>(); return this;`
- `IEndpointConventionBuilder MapMotivMcp(this IEndpointRouteBuilder endpoints, string pattern = "/mcp", bool anonymous = false)`: `var mcp = endpoints.MapMcp(pattern); if (!anonymous) mcp.RequireAuthorization(); return mcp;`
- `MotivMcpTools` — `[McpServerToolType] public sealed class MotivMcpTools(IHttpContextAccessor http, MotivRulesOptions options, RuleSet rules, IRuleStore? ruleStore = null, PropositionSet? propositions = null, IPropositionStore? propositionStore = null, IDecisionSource? decisions = null, DecisionReproducer? reproducer = null, IScenarioStore? scenarios = null)`. Tools, each `[McpServerTool(Name = "...", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]` unless noted:

| Tool | Parameters | Returns | Refusal |
|---|---|---|---|
| `get_decision` | `id` (Guid) | `DecisionEntry` | `McpException("no decision '<id>' is in the log")` for unknown *or* unreadable |
| `list_decisions` | `ruleName?`, `satisfied?`, `fromUtc?`, `toUtc?`, `limit?` (default 20, max 200) | `DecisionEntry[]` filtered to readable rules | — |
| `reproduce_decision` | `id` | `ReproductionEntry` | as `get_decision` |
| `print_rule` | `name`, `version?` | `CSharpEntry` — a rule (through `RulePrintOptions.For`) or a proposition (`ModelType` from `propositions.Find(name)`'s model binding, `ClassName` PascalCase + `"Proposition"`) | `McpException("no rule or proposition '<name>' is known to this host")` for unknown or unreadable |
| `get_rule` | `name`, `version?` | `RuleRowEntry` for a rule or `PropositionRowEntry` for a proposition, at that version or the head | as `print_rule` |
| `list_scenarios` | `rule` | `ScenarioEntry[]` (ids starting `__` omitted) | not-found as above; `McpException("this host stores no scenarios")` when no store |
| `save_scenario` (`ReadOnly = false, Destructive = false, Idempotent = false`) | `rule`, `name`, `model` (string), `expectedSatisfied?`, `sourceDecisionId?` | `ScenarioEntry` | not-found without `Read`; `McpException("saving a scenario for '<rule>' requires the 'author' grant on its namespace")` without `Author` |

Descriptions (verbatim in `[Description]`): `reproduce_decision` — "Re-runs a logged decision under the rule and proposition versions that decided it. Read `fidelity` first: `isExact` means every anchor was honoured; otherwise each note names what slipped (RuleVersionMissing, BuildMismatch, PropositionVersionMissing, PropositionBindFailed, ModelRedacted, ModelUnresolved, OutcomeDiverged). When `model.kind` is `Reference` the model could not be resolved: scaffold the model from its key in the generated test, never invent field values. `csharp` is the rule printed as C# for adoption; `csharpWarnings` say where it needs a person. Follow the target repository's test conventions." `save_scenario` — "Saves a named sample model for a rule as test data. It never changes what the rule decides and is not subject to the approval gate."

Grant checks reuse `IGrantSource` from `HttpContext.RequestServices` exactly as `GrantGate` does (`GrantEvaluator.IsGranted(source.GrantsFor(user), verb, name)`); no `IGrantSource` registered means everything is granted, as the endpoints behave.

- [ ] **Step 1: Tests (RED)** — `MotivMcpEndpointsTests.StartAsync` builds the host as Task 2 does plus `.AddScenarios().AddMcp()`, `app.MapMotivMcp("/mcp")`; the client: `await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(client.BaseAddress!, "/mcp") }, client))` (read the 2.2.0 constructor: `HttpClientTransport(HttpClientTransportOptions options, HttpClient httpClient, ILoggerFactory? = null)` — adjust if the signature differs). Tests: `Should_list_the_seven_tools`; one call per tool asserting on `result.StructuredContent`; `Should_answer_not_found_for_a_rule_the_caller_may_not_read_exactly_as_for_a_missing_one` (two `print_rule` calls with a `FixedGrants` of `Read` on `other`, compare the `TextContentBlock.Text` of both `IsError` results); `Should_return_only_the_key_for_an_unresolved_reference_capture` (a host with `ReferenceOnly` capture and no resolver: `model.kind == "Reference"`, `model.value` absent, `model.key == "cust-42"`); `Should_refuse_save_scenario_without_author` (grants `Read` only → `IsError` with "author" in the text, then `list_scenarios` empty).
- [ ] **Step 2: Implement; GREEN; run `Motiv.Serialization.AspNetCore.Tests`; commit.**

---

### Task 4: `Motiv.Serialization.Snapshots`

**Files:**
- Create: `src/Motiv.Serialization.Snapshots/Motiv.Serialization.Snapshots.csproj` (copy `Motiv.Serialization.Sql.csproj`'s shape; `TargetFrameworks` as `Motiv.Serialization`; description "Bind a rule and its propositions, as a decision reproduction handed them back, against your compiled registry — the dependency a generated Motiv test has instead of a store or a host.")
- Create: `src/Motiv.Serialization.Snapshots/RuleSnapshot.cs`
- Create: `src/Motiv.Serialization.Snapshots.Tests/Motiv.Serialization.Snapshots.Tests.csproj`, `RuleSnapshotTests.cs`
- Modify: `Motiv.slnx` (both projects), `src/Motiv.Serialization/Motiv.Serialization.csproj` (`InternalsVisibleTo`)

**Interfaces:**

```csharp
namespace Motiv.Serialization.Snapshots;

/// A rule document and the proposition rows it resolved through, as a reproduction or get_rule handed them back.
public sealed class RuleSnapshot
{
    public static RuleSnapshot FromJson(string rule, IEnumerable<string> propositions);   // each proposition is a row: { "name", "version", "modelType", "document", "description"? }
    public IReadOnlyList<string> Warnings { get; }                                          // rows that did not bind, with why
    public SpecBase<TModel, string> Bind<TModel>(SpecRegistry registry, RuleSerializerOptions? options = null);
    public AsyncSpecBase<TModel, string> BindAsync<TModel>(SpecRegistry registry, RuleSerializerOptions? options = null);
}
```

`Bind`: `var propositions = new PropositionSet(registry, new InMemoryPropositionStore(), options)` with every model type the rows name registered through `AddModel` — the snapshot cannot know the CLR type from a model-type id, so `FromJson` takes an optional `Action<RuleSnapshot.Models>`? No: **the registry is enough** — `RuleSnapshot.Bind<TModel>` registers `TModel` under the rows' model-type ids when they all agree, and throws `InvalidOperationException` naming the ids when the rows span several model types (a snapshot over several model types needs the host, which is the reproducer's job). Then `PinnedPropositionBinder.Bind(rows, registry, propositions, notes)` → layered source → `new RuleSerializer(layered, options).Deserialize<TModel>(rule)`. Notes become `Warnings`; a rule that will not bind throws `RuleSerializationException` as `Deserialize` does.

- [ ] **Step 1: Tests (RED)** — `RuleSnapshotTests`: (a) `Should_decide_as_the_reproducer_for_the_same_snapshot`: the `DecisionReproducerTests` harness shape (copy `AHostAsync`; the snapshots test project references `Motiv.Serialization` too); decide under `customer.eligible` v1 = is-active for an active minor, publish v2 = is-adult, reproduce; build the snapshot from `reproduction.Rule.DocumentJson` and the `Propositions` rows serialised as `{ name, version, modelType, document }`; bind over a *fresh* registry that also has `customer.eligible` registered as a compiled is-adult spec (the moved-on head); evaluate the same customer; assert `Satisfied` and `Assertions` equal `reproduction.Replayed`'s. (b) `Should_refuse_rows_over_several_model_types`. (c) `Should_report_a_row_that_does_not_bind_as_a_warning_and_still_bind_the_rule` when the rule does not depend on it.
- [ ] **Step 2: Implement; GREEN on net10, net8, net9; netstandard2.0 build; commit.**

---

### Task 5: Docs, design doc, Studio, full verification, simplifier, PR

- [ ] **Studio:** `.AddMcp()` on the builder and `app.MapMotivMcp("/mcp")` after `MapMotivRules`; a `StudioHost` test that lists the tools through the MCP client (`Motiv.Studio.Tests` gains the `ModelContextProtocol` package).
- [ ] **Docs:** `docs/decision-log/mcp.md` (hosting, the tools table with grants, the not-found rule, the reference-key rule, an agent-loop sketch: decision → reproduce → print → scenarios → test), `docs/live-rules/AspNetCore.md` (the decision routes and `rules/{name}/csharp`), `docs/decision-log/replay.md` (the endpoint and the snapshot package), `docs/decision-log/snapshots.md` (`RuleSnapshot`, the generated-test example from spec §8 with the package name), toc/index rows, `docs/adoption/index.md` mention of the package, `CONTEXT.md` (**Snapshot**), parent spec §7–8 amended (package name; routes under the rules group; `FromJson` takes rows; `AddMcp`/`MapMotivMcp`).
- [ ] **Design doc:** `docs/superpowers/specs/2026-09-20-mcp-and-snapshots-design.md` — decisions: MCP in the host on the official package, stateless; tools over the same services; not-found for unreadable; DTOs and the key-only rule; `save_scenario` the one write; the snapshot package name; the shared binder; one model type per snapshot.
- [ ] **Full verification:** solution build, every test project, net8/net9 for `Motiv.Serialization.Tests` and the snapshots tests, Studio e2e and a11y (the Studio host gains a route).
- [ ] **Simplifier** over `git diff claude/csharp-printer...HEAD -- src/`; apply; re-run.
- [ ] **PR** against `claude/csharp-printer`.
