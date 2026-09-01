# Spec 4K — The replacement hosted example for `src/examples/` — Plan

**Date:** 2026-09-01
**Status:** Shipped
**Source:** bundle spec [4 — Surface Quality](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/4-surface-quality.md),
§2 (ticket [#108](https://github.com/karlssberg/Motiv/issues/108)), §3 and §7. Tracked as
[#171](https://github.com/karlssberg/Motiv/issues/171) under the build map
[#169](https://github.com/karlssberg/Motiv/issues/169).

## The debt being paid

Spec 4C rehomed the demo as `Motiv.Studio`, out of `src/examples/`. Ticket #108 priced that move in
advance and accepted it:

> Gap to close deliberately: `src/examples/` loses its only hosted rules-engine example.

The gap is real and still open: `src/examples/Motiv.RulesEngine.Sample` is an empty directory with
zero tracked files, and the four surviving examples (`Motiv.Benchmark`, `Motiv.ECommerce`,
`Motiv.Poker`, `Motiv.SmartHome`) are all console or library projects. Nothing under `src/examples/`
is hosted, and nothing there exercises `Motiv.Serialization` at all.

Spec 4 §7 states the verification this slice is judged against:

> The Blazor sample authors a valid rule document through `Motiv.Serialization` alone (no `rules-core`).

## Why this is more than a sample

Spec 4E published a support-tier table in `docs/adoption/index.md`. Its third row is:

| **.NET, including Blazor** | Enabled | `Motiv.Serialization` | The C# parser, validator, binder and evaluator — no JavaScript involved |

That row is currently backed by a code block on a documentation page. 4I established the standard
this repository now holds itself to — *a claim about a runtime nobody here exercises is an estimate
until someone pays it* — and paid it for Vue. This slice pays it for .NET.

It is also the load-bearing half of the two-runtime story: the C# core is the one an enterprise .NET
buyer actually adopts, and it is the only tier with no worked artefact.

## What is being demonstrated, precisely

The adoption page already states the honest boundary, and the sample must live inside it rather than
around it:

> The document model (`RuleDocument`, `RuleNode`) and the document parser are `internal`, and there
> is no C# equivalent of the TypeScript mutations, the path arithmetic or the DSL printer. A .NET
> authoring UI therefore builds the JSON — from its own model, or by driving the same HTTP API Studio
> drives — and leans on `Validate` to tell it what is wrong and where.

Verified against the tree: `RuleDocument` and `RuleNode` are both `internal sealed`, and
`Motiv.Serialization`'s `InternalsVisibleTo` names only its own test assembly and
`Motiv.Serialization.AspNetCore`. So the sample's real work is the three things that boundary leaves
to the consumer:

1. **Its own authoring model**, and a writer that emits schema-valid JSON from it.
2. **Path arithmetic of its own** — because `RuleError.Path` is a JSON path (`$.rule.and[1]`) and an
   authoring UI has to put the error next to the control the user is editing.
3. **Nothing else.** Validation, binding, evaluation and the explanation all come from
   `Motiv.Serialization`.

Point 2 is the interesting one. It is the part the page says a .NET adopter has to build, and no
artefact has ever shown how much it costs.

## Shape

A **standalone Blazor WebAssembly** app, not Blazor Server. The spec says "Blazor WASM consumer"
twice and "Blazor WebAssembly included" once; a server-rendered app would prove the weaker claim,
since C# on a server was never in doubt. WASM puts the whole rules stack in the browser with no
JavaScript package, which is the actual assertion in the tier table.

### Projects

- `src/examples/Motiv.RuleAuthoring.Blazor` — `Microsoft.NET.Sdk.BlazorWebAssembly`, `net10.0`,
  one `ProjectReference` to `Motiv.Serialization`.
- `src/examples/Motiv.RuleAuthoring.Blazor.Tests` — xunit + Shouldly, matching the other example
  test projects.

Both added to `Motiv.slnx` under `/Examples/`.

### The authoring core (framework-free, and therefore testable without bUnit)

> Held for the core, and the core alone. The components themselves ended up under bUnit after the
> review round — see the design doc's coverage section for why.

- `Authoring/DraftNode` — the mutable authoring tree: a kind (spec / not / and / or / xor / andAlso
  / orElse), an optional registry name, an optional node name, children.
- `Authoring/RuleDocumentWriter` — draft → `{ "name": …, "rule": … }` JSON via `Utf8JsonWriter`,
  returning the JSON **and** the path→node map it computed while writing. One walk, one source of
  truth for paths; a second walk that re-derived them could disagree with the JSON.
- `Authoring/AuthoringSession` — composes, calls `Validate<Customer>`, and on a clean document calls
  `Deserialize<Customer>` and `Evaluate`, surfacing `Satisfied`, `Reason` and `Justification`.
- `Authoring/AuthoringOutcome` / `LocatedError` — an error paired with the deepest draft node whose
  path is a prefix of the error's path, so `$.rule.and[1].whenTrue` still lands on `and[1]`.

### The UI

One page: the draft tree as nested controls, the live JSON, the located errors, and the evaluation
of a chosen sample customer showing Motiv's own generated `Reason` and `Justification`.

### Domain

`Customer` (`IsActive`, `Age`, `OrderCount`), a `SpecRegistry` of four propositions, and a couple of
sample customers. Deliberately the same vocabulary as the adoption page's snippet and Studio's
`loyalty-discount.json`, so a reader moving between them recognises the names.

## Gates

Per 4I's lesson — *a gate is only worth what it refuses* — two tests exist to refuse a specific
regression, not to decorate:

- **`SampleDependenciesTest`** reads the sample's `.csproj` and refuses any `ProjectReference` or
  `PackageReference` outside the allowed set. §7 says "through `Motiv.Serialization` alone"; the
  cheap ways to break that are a reference to `Motiv.Serialization.AspNetCore` (which would give the
  sample access to internals a real adopter does not have) or a reach into `Motiv.Studio`.
- **`NoJavaScriptPayloadTest`** refuses any `.js` file under the sample's `wwwroot`. "No
  `rules-core`" is the §7 clause with the weakest natural enforcement — a C# project cannot
  accidentally `npm install`, but a sample *can* quietly acquire a script tag, and then the artefact
  stops demonstrating the claim while every test stays green.

Both mirror `ui/examples/vue-adapter/test/bindings-only.test.ts`, which is the same idea on the
JavaScript side.

## TDD order

1. `RuleDocumentWriter` emits a spec node → assert JSON and the `$.rule` path.
2. Nested `and` → assert `$.rule.and[0]` / `$.rule.and[1]` paths.
3. `not` → assert `$.rule.not`.
4. `AuthoringSession` validates a good document clean.
5. Unknown spec name → one `UnknownSpec` error located on the right draft node.
6. Property-suffixed error path resolves to its owning node (prefix fallback).
7. Clean document evaluates, and `Reason` is Motiv's generated text.
8. An `and` with a single child is refused by `Validate` (schema `minItems: 2`) — the sample must
   surface that rather than crash.
9. The two gates.

## Expected fallout

- **Central package management.** `Microsoft.AspNetCore.Components.WebAssembly` and its `.DevServer`
  are not in `Directory.Packages.props`; both need adding.
- **CI builds this on `windows-latest` with no workloads.** Verified locally that
  `Microsoft.NET.Sdk.BlazorWebAssembly` restores and builds on the bare 10.0.302 SDK with an empty
  `dotnet workload list` — `wasm-tools` is needed only for AOT and native relinking, neither of which
  a debug build or a default publish does. If that were wrong, adding this project would turn every
  CI run red rather than fail in isolation.
- **`TreatWarningsAsErrors` is on repo-wide**, so Razor-generated code has to be warning-clean.
- **Trimming.** A default Blazor WASM publish trims. The binder resolves registry entries by name and
  reflects over the model type, so the sample pins `PublishTrimmed=false` rather than ship an example
  that works in `dotnet run` and breaks on publish.

## Out of scope, deliberately

- Any change to `Motiv.Serialization`'s public surface. If authoring in C# wants a document model,
  that is a spec decision and a different slice; the `internal` boundary is what the adoption page
  documents today and what this sample is evidence *of*.
- Playwright/e2e coverage. The existing e2e suite drives Studio; pointing it at a second host is its
  own slice.
- The manual screen-reader pass (#172) — that is spec 4L and assigned to a human by design.

## Docs to update

- `docs/adoption/index.md` — the .NET section gains a pointer to the worked sample, in the shape 4E's
  Vue section gained one from 4I.
- `README.md` — the examples list.
- The design doc, in the same commit as the implementation.
