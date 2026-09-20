# Decision reproduction and the Motiv MCP — Design

**Date:** 2026-09-20
**Status:** Draft, awaiting review
**Origin:** a brainstorm on the main criticisms of rules engines and how Studio, which bridges
compile-time and runtime rules, can answer them. Builds on issues #224 (proposition version log),
#239 (decision permalinks) and #241 (compiled ↔ live slider); it is the "reproduce" half of #241's
round trip and the server half of #239.
**Scope:** `Motiv.Serialization`, `Motiv.Serialization.Sql`, `Motiv.Serialization.EntityFrameworkCore`,
`Motiv.Serialization.AspNetCore`, a new `Motiv.Serialization.Testing`, the sample project, and the
Studio scenario pane's persistence.

## Problem

The standing criticisms of rules engines are that logic leaves the codebase, type safety and
refactoring tools stop working, rule interaction is opaque, explainability is poor, testing is hard
and reproduction of a production decision is close to impossible. Motiv's positioning is the
reverse: the C# boolean you already have becomes explainable, composable and hot-swappable. Studio
is meant to make the compile-time/runtime boundary a slider rather than a wall, so that a runtime
rule can be pulled into code and debugged with ordinary tools.

Today the pieces are unconnected. The decision log (spec 3B) records every audited evaluation with
three anchors — rule version, build id, and the transitive versions of every referenced
proposition — plus a captured input under a per-model-type posture. The rule store keeps an
append-only version log. The binder can turn any document into a `SpecBase`. Studio has a scenario
table that "is like having unit tests". But nothing joins a logged decision to a snapshot of the
documents that decided it, nothing prints a document as C#, scenarios live only in a browser tab,
and none of it is reachable by a coding agent.

## Goals

- A logged decision becomes a reproduction: the rule and every referenced proposition at the
  version that decided, the model as far as capture and a resolver allow, and a fidelity verdict
  naming any anchor that could not be honoured.
- A reproduction becomes a unit test in the adopter's own repository, bound against the compiled
  specs, runnable under a debugger, pinned to the logged versions.
- Any rule or proposition document can be printed as the equivalent C# builder chain, from one
  printer whose output is tested for fidelity against the binder.
- Scenarios are stored in the backend, attached to a rule, with an optional expected verdict, and
  a reproduction can be saved as one.
- A coding agent can do all of the above through an MCP server hosted inside the adopter's app,
  under the same grants as the rules API.

## Non-goals

- **Adopt** — replacing a compiled default with printed C# via a Roslyn action, and "register as
  live proposition". That is the other direction of #241 and a later slice over this printer.
- Any Studio UI beyond wiring the existing scenario pane to the store. The `#/decision/<id>`
  replay screen is #239; this design gives it its endpoint.
- Write tools on the MCP other than `save_scenario`. Drafting, change requests and publishing are
  the operations-agent loop, a later slice that reuses the governance endpoints and the approval
  gate.
- Round-tripping non-string metadata in the printer. Object payloads print as a commented literal.
- A stdio transport or standalone process. The server lives in the host.

## Decisions settled during brainstorming

| Axis | Decision |
|---|---|
| Primary MCP client | Both a coding agent and an operations agent, one server; the coding-agent loop is designed first |
| Entry point of the loop | A logged decision (id or correlation id) |
| C# generation | One printer in `Motiv.Serialization`; not a source generator, not a UI-side printer |
| Replace the default or test as a special case | Both, as separate acts: **reproduce** (this design) and **adopt** (later) |
| Model for a reference-only capture | Adopter-registered resolver seam, scaffold fallback |
| Where the server runs | Inside the adopter's host app, over Streamable HTTP, next to the rules API |
| First slice | Reproduce-only, sharing its primitive with Studio replay |
| Proposition versioning | Yes, as its own preceding slice — #224 is resolved as "replay is an obligation" |
| Scenarios | Stored in the backend, per rule; the MCP may list and add them |

## Design

### 1. What exists, what is new

Reused unchanged: `DecisionRecord` and `DecisionInput`; `IRuleStore.HistoryAsync`;
`RuleBinder.Bind`; `LayeredSpecSource`; `SqlDecisionSink.ReadAsync(DecisionQuery)`;
`InMemoryDecisionSink.Records`; the namespace grant model in `Motiv.Serialization.AspNetCore`.

New:

| Piece | Package | Purpose |
|---|---|---|
| `IDecisionSource` | `Motiv.Serialization` | read side of the decision log: by id, and the existing query |
| `StoredPropositionVersion`, history on `IPropositionStore` | `Motiv.Serialization` + EF | the proposition version log (#224) |
| `IScenarioStore`, `StoredScenario` | `Motiv.Serialization` + EF | persisted scenarios |
| `DecisionModelResolvers` | `Motiv.Serialization` | the resolver seam, on `DecisionLogOptions.Resolve` |
| `DecisionReproducer`, `Reproduction`, `ReproductionFidelity` | `Motiv.Serialization` | the primitive |
| `CSharpPrinter` | `Motiv.Serialization` | document → builder chain |
| `MotivMcpEndpoints`, scenario and reproduction HTTP endpoints | `Motiv.Serialization.AspNetCore` | the two front doors |
| `RuleSnapshot` | `Motiv.Serialization.Testing` (new) | bind a snapshot in a test |

Two facts the design states rather than hides:

- **The build id is a fingerprint, not a lookup.** Compiled delegates cannot be fetched at a past
  build. A mismatch is reported as a fidelity degradation; the generated test still pins the
  document versions, which is the half of the answer that can be pinned.
- **Propositions have no history today.** #224 is a prerequisite slice. Until it lands, every
  reproduction of a rule that references an authored proposition is `Degraded`.

### 2. The proposition version log (#224, prerequisite)

`IPropositionStore` gains the mirror of what `IRuleStore` has: `AppendAsync(versions)` with the
`(Name, Version)` primary key as the cross-replica compare-and-set, and `HistoryAsync(name)`
returning `StoredPropositionVersion(Name, Version, DocumentJson, Author, TimestampUtc, ChangeNote,
BuildId)`. Rows are kept forever, bounded only by the decision log's own retention if an adopter
ever configures pruning. The in-memory and EF stores implement it; the head projection stays
derived from the log, as for rules. The existing CAS on the head is preserved by the insert.

### 3. Persisted scenarios

```csharp
public sealed record StoredScenario(
    string RuleName,
    string Id,                    // stable, store-assigned
    string Name,
    string ModelJson,
    bool? ExpectedSatisfied,      // null: a sample; set: a test to hold
    string? SourceDecisionId,     // set when saved from a reproduction
    int Version,                  // compare-and-set on put
    RuleChangeProvenance Provenance);
```

- `IScenarioStore`: `LoadAsync()`, `ForRuleAsync(ruleName)`, `PutAsync(scenario, baseVersion)`,
  `DeleteAsync(ruleName, id, baseVersion)`. In-memory default; EF rows in the same DbContext.
- **Not a version log** and **not governed**. A scenario is test data and never changes live
  behaviour. Writes require `Author` on the rule's namespace, reads require `Read`. No new grant verb.
- **Expected verdict only.** Assertion expectations are out of scope; the verdict catches a flip.
- **Studio**: the scenario pane loads rows for the open rule from the store and saves on edit,
  clone and delete; Reset reloads from the store. The four seeds move into the sample project's
  store seeding. The pane's structure is unchanged.
- Endpoints: `GET /api/rules/{name}/scenarios`, `PUT /api/rules/{name}/scenarios/{id}`,
  `DELETE /api/rules/{name}/scenarios/{id}?baseVersion=`.

### 4. The resolver seam

```csharp
options.Resolve
    .Reference<Customer>((key, ct) => customers.LoadAsync(key, ct));
```

- One method, keyed by model type, registered at startup next to `Capture` on
  `DecisionLogOptions`, so the two postures for a type are declared and reviewed together.
- Only reference-only capture needs resolving; `Whole` is already a model and `Redacted` cannot
  be completed by anything.
- Returning null is the erasure case: the reproduction reports `ModelUnresolved` with the key. It
  is not an error.
- Nothing is registered by default. An adopter who registers nothing gets scaffolds.
- The resolver runs only inside the host, invoked by `DecisionReproducer`. It is never serialised
  and the MCP never receives a resolver, only its result.
- The seam does not claim the resolved model is the one that was evaluated; a reference-only log
  cannot prove that. Step 5 of the reproduction is what catches drift.

### 5. The `Reproduction` primitive

```csharp
public sealed record Reproduction(
    DecisionRecord Decision,
    StoredRuleVersion Rule,
    IReadOnlyList<StoredPropositionVersion> Propositions,   // transitive closure at pinned versions
    ReproducedModel Model,                                   // Resolved | Reference | Whole | Redacted
    ReproductionFidelity Fidelity,                           // Exact | Degraded(reasons)
    string CSharp);
```

`DecisionReproducer.ReproduceAsync(decisionId, ct)`:

1. Look the decision up through `IDecisionSource`. Not found is a typed error.
2. Fetch the rule at `Decision.RuleVersion` from `IRuleStore.HistoryAsync`; fetch each entry of
   `ReferencedPropositionVersions` from the proposition history.
3. Bind the snapshot with a **snapshot spec source**: a `LayeredSpecSource` whose top layer is
   the fetched proposition documents and whose bottom layer is the live compiled registry. The
   bound spec is transient. The live `RuleSet` and the evaluation hot path are untouched.
4. Obtain the model: `Whole` is used as-is; `Redacted` is used and marks `ModelRedacted`;
   `Reference` goes to the resolver, and `ModelUnresolved` if none is registered or it returns null.
5. With a model in hand, evaluate the bound spec and compare `Satisfied` with the logged outcome.
   Disagreement marks `OutcomeDiverged` — loud, never silent, per #239's rule.
6. Print the C# (section 6).

| Fidelity reason | Cause |
|---|---|
| `BuildMismatch` | the host's build id differs from `Decision.BuildId` |
| `PropositionVersionMissing` | a pinned version is absent from the history |
| `ModelRedacted` | the captured input was a projection |
| `ModelUnresolved` | reference-only capture and no resolver, or the resolver returned null |
| `OutcomeDiverged` | re-evaluation disagreed with the logged verdict |

`Exact` means none of these. The reasons are values the generated test quotes in a comment.

Expression leaves (`Spec.From` text nodes) are printed verbatim and not re-parsed; binding them
remains gated on #251.

### 6. The C# printer

`CSharpPrinter.Print(RuleDocument, PrintOptions)` walks the node tree and emits a builder chain.

| Node | Emits |
|---|---|
| `Spec` reference | the registered handle from a caller-supplied name map, else `registry.Get<TModel>("name")` |
| `Local` reference | the local variable its definition became |
| `Expression` | `Spec.From((TModel m) => <text>)`, verbatim, with a "not re-parsed" comment |
| `And` / `Or` / `XOr` | `left & right` / `left \| right` / `left ^ right` |
| `AndAlso` / `OrElse` | `.AndAlso(right)` / `.OrElse(right)` |
| `Not` | `!operand` |
| higher-order | `Spec.Build(inner).AsAllSatisfied()` etc., `N` inlined or as a parameter |
| decoration | `.WhenTrue("…").WhenFalse("…").Create("name")`; object payloads as a commented literal with a `TODO` |

- Definitions become `var` locals in declaration order before the root (#234).
- Parameters become arguments of a static method returning `SpecBase<TModel, string>`.
- `TModel` comes from the registered spec, never guessed.
- `PrintOptions` carries the name map, the namespace to emit, and whether to wrap in a class.

**Fidelity test as the contract.** Over the sample project's rule corpus and the serialization
tests' documents: print, compile with Roslyn against the sample's model assembly, bind the original
document, evaluate both over a scenario set. `Satisfied` must agree on every scenario and
`Assertions` as sets. A document feature landing without a printer case fails this test.

### 7. The MCP endpoint

`app.MapMotivMcp("/mcp")` in `Motiv.Serialization.AspNetCore`, on the official
`ModelContextProtocol.AspNetCore` package, Streamable HTTP. Opt-in: a host that never calls it
exposes nothing. `RequireAuthorization()` by default, as the rules API.

| Tool | Input | Returns | Grant |
|---|---|---|---|
| `get_decision` | decision id, or correlation id | the record as JSON, outcome and justification included | `Read` |
| `list_decisions` | rule, optional verdict, window, limit | `DecisionQuery` results, newest first | `Read` |
| `reproduce_decision` | decision id | the `Reproduction` | `Read` |
| `print_rule` | rule or proposition name, optional version | printed C# | `Read` |
| `get_rule` | name, optional version | document JSON and its provenance row | `Read` |
| `list_scenarios` | rule | the stored scenarios | `Read` |
| `save_scenario` | rule, name, model JSON, optional expected verdict, optional source decision id | the stored row | `Author` |

Rules:

- A rule the caller may not read is not found, not forbidden, so tool results do not leak which
  rules exist.
- Models cross the wire only as the resolver's result or the log's captured input, serialised with
  the host's `JsonSerializerOptions`. A `Reference` input returns the key and nothing else.
- `save_scenario` is the only write. Its description states that it writes test data and never
  behaviour, and that it is not governed by the approval gate.
- Tool descriptions carry the fidelity vocabulary and tell the agent to scaffold, not invent, when
  the model is unresolved, and to follow the target repository's test conventions.

The same `DecisionReproducer` call is exposed as `GET /api/decisions/{id}/reproduction` and the
printer as `GET /api/rules/{name}/csharp?version=` for Studio.

### 8. The testing package and the generated test

`Motiv.Serialization.Testing` ships `RuleSnapshot.FromJson(rule, propositions)` and
`Bind(SpecRegistry)`, layering the snapshot over the registry the test supplies — the same
`LayeredSpecSource` the reproducer uses.

```csharp
[Fact]
public void Decision_8f3c_loyalty_discount_v7_reproduces()
{
    // Reproduced from decision 8f3c… (correlation 2f1a…) at 2026-09-18T10:42Z.
    // Fidelity: Exact. Rule loyalty-discount v7, customer.is-active v3, customer.is-eligible v5.
    var snapshot = RuleSnapshot.FromJson(
        rule: """{ "name": "loyalty-discount", ... }""",
        propositions: [ """{ "name": "customer.is-active", ... }""" ]);

    var model = new Customer { Id = "4711", Tier = "gold", Orders = 3 };

    var result = snapshot.Bind(Specs.Registry).Evaluate(model);

    result.Satisfied.ShouldBeFalse();
    result.Assertions.ShouldBe(["customer is active", "fewer than five orders"]);
}
```

- The assertions are the logged outcome, so the test is green first when fidelity is `Exact`; the
  developer edits the expectation to what should have happened and works from red.
- Unresolved model: the model line throws `NotImplementedException` naming the key, the
  justification tree is in a comment, and the test is skipped with the fidelity reason.
- With scenarios, the agent writes a theory over every stored scenario plus the reproduced
  decision, asserting `ExpectedSatisfied` where set and the live verdict where not.
- The compiled specs are the test's dependency, not the store or the host.

### 9. What Studio inherits

#239 becomes UI-only: `#/decision/<id>` fetches the reproduction endpoint, renders the model as a
scenario row with Live pinned to the logged version and Draft as the open tab, shows the fidelity
as a chip, and offers "save as scenario". "Copy as C#" on a rule tab calls the csharp endpoint.

## Testing

- `PropositionHistoryTests`: contract suite over both stores; CAS on the insert; head derived.
- `ScenarioStoreTests`: contract suite; CAS; grant checks on the endpoints.
- `DecisionReproducerTests`: one test per fidelity reason, arranged by slipping that anchor. The
  `Exact` case publishes v2 of a referenced proposition after the decision and asserts v1 binds.
- `CSharpPrinterTests`: the corpus fidelity test plus one golden output per operator.
- `IDecisionSource` contract tests over both sinks.
- `MotivMcpEndpointsTests`: each tool through the MCP client; grant denial is not-found; a
  `Reference` input never carries more than the key; `save_scenario` refused without `Author`.
- `RuleSnapshotTests`: bind-and-evaluate agrees with the reproducer for the same snapshot.
- The full solution suite, including the example projects, since justification text is projected.
- Studio: the existing scenario Playwright tests run against the store-backed pane; the a11y
  sweep runs unchanged.

## Slices

Each lands as its own PR with its plan and design doc in the same commit.

1. Proposition version log (#224).
2. Persisted scenarios: store, EF rows, endpoints, Studio wiring, sample seeding.
3. `IDecisionSource`, resolver seam, `DecisionReproducer` with fidelity.
4. `CSharpPrinter` with the corpus fidelity test.
5. MCP endpoint, reproduction and csharp HTTP endpoints, `Motiv.Serialization.Testing`.

Later, not in this design: adopt (the Roslyn direction of #241) over the same printer; the
operations-agent write tools over the governance endpoints; #239's replay screen.

## Rejected alternatives

- **Roslyn source generator from checked-in JSON.** Makes JSON the source of truth and C# a build
  artefact, inverting the positioning; generated-at-build code is the code developers cannot edit.
- **A TypeScript printer in `@motiv-rules/core`** (#241's sketch). Untestable for fidelity where
  it lives; a second reader of the document to keep in sync.
- **Standalone `dotnet tool` MCP reading the store directly.** No compiled specs to bind, no
  decision sink, no resolver; reproduce degrades to a scaffold every time.
- **Reproduce by binding printed C# rather than the JSON snapshot.** A printer bug could mask a
  rule bug; the reproduction must run what production ran.
- **Whole-registry "resolve everything" switch.** The default-credentials trap the capture
  registry refused; the posture stays an explicit choice per type.
- **Scenarios attached to a model type rather than a rule.** Studio scopes them to the open rule
  and the expected verdict is meaningless without one.
