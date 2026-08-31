---
title: Runtimes and Support Tiers
description: Which runtimes the Motiv rules stack supports — React as the maintained JavaScript adapter, other JS frameworks over the framework-free core, and .NET including Blazor through Motiv.Serialization — what each tier costs, and what enforces the claim.
---

The rules stack ships **two cores, one per runtime that needs one**: `@motiv-rules/core` in
TypeScript for a browser authoring UI, and `Motiv.Serialization` in C# for the server that binds
and evaluates what was authored. Both speak the same JSON rule document, pinned by one schema file
in this repository.

So the question an adopter actually asks — *we are a Vue shop / a Blazor shop, can we use this?* —
is answered **per runtime, not per framework**. This page is the answer, stated rather than left to
be discovered.

## The tiers

| Runtime | Tier | What you take | What Motiv maintains |
|---|---|---|---|
| **React** | Supported | `@motiv-rules/core` + `@motiv-rules/react` | The adapter, its tests, and Motiv Studio built on top of it |
| **Vue, Svelte, Solid, vanilla** | Enabled, not supported | `@motiv-rules/core` + your own bindings | The framework-free core, a check that keeps it framework-free, and a worked Vue adapter you copy rather than install |
| **.NET, including Blazor** | Enabled | `Motiv.Serialization` | The C# parser, validator, binder and evaluator — no JavaScript involved |
| **Web components** | Declined | — | — |

The two words carry their full weight:

- **Supported** — Motiv writes it, tests it, and its own CI goes red when it regresses.
- **Enabled** — the seam is real, verified and documented, and the code on your side of it is
  yours. Nobody here will notice if your bindings break.

## React — the supported adapter

`@motiv-rules/react` is bindings only. Every hook adapts one of the core package's
`subscribe`/`getState` stores to React's subscription primitives; the authoring logic itself lives
in `@motiv-rules/core`, and everything rendered is delegated back to the consumer. The single
exception is `JustificationTree`, a render-prop projection that owns the accessibility semantics of
an explanation and none of its markup.

## Other JavaScript frameworks

An adapter is the whole of what React's costs, and both packages are small enough to price honestly.
`@motiv-rules/react` is **439 lines**, and they divide like this:

<!-- react-adapter-price -->
| Part | Lines | Code lines | What it adapts |
|---|---:|---:|---|
| `context`, `useRuleEditor`, `useRuleNode`, `useCatalog`, `useEvaluation`, `useDslSync`, the barrel | 179 | 127 | The editor store, one node's view of it, catalogue and evaluation calls, and the DSL⇄tree sync controller |
| `@motiv-rules/react/workflow` | 162 | 93 | The save loops — optimistic save, 409 recovery, blast-radius reporting |
| `JustificationTree` | 98 | 59 | The one component, and the only accessibility the packages carry |

Everything else — path arithmetic, insertion planning, the accordion state machine, DSL parsing,
printing, completion, diagnostics, the accessible-name projection — is already in the core and has
no framework in it, so a second adapter re-pays only the table above.

### The second adapter, written

That price used to be an estimate. It is now a measurement, because the adapter it describes has
been written: [`ui/examples/vue-adapter`](https://github.com/karlssberg/Motiv/tree/main/ui/examples/vue-adapter)
is a complete Vue 3 adapter over `@motiv-rules/core`, offering the same surface as
`@motiv-rules/react` symbol for symbol, tested on every CI run against the same behaviours.

<!-- vue-adapter-price -->
| Part | Lines | Code lines | What it adapts |
|---|---:|---:|---|
| `observe`, the context pair, `useRuleEditor`, `useRuleNode`, `useCatalog`, `useEvaluation`, `useDslSync`, the barrel | 250 | 140 | The same stores and client calls, bound to Vue's reactivity instead of React's |
| the `workflow` entry point | 115 | 71 | The same save loops |
| `JustificationTree` | 94 | 59 | The same nested-groups structure, rendered through a scoped slot |

The two adapters land within 5% of each other — 459 lines against 439, and 270 lines of code
against 279 — so the headline claim holds: a second runtime costs what React's adapter costs. Three
things the estimate got wrong, all of them visible only once someone paid it:

- **The document bindings cost more, not less: 250 lines against React's 179.** Almost all of the
  difference is one file, `observe.ts`. React re-runs a hook on every render, so the store an action
  closes over is always the current one and swapping stores needs no code at all; Vue's `setup` runs
  once, so following a store that can change is an explicit `watch`, and reaching the current
  controller is an explicit indirection. React's rebinding is not free, it is *prepaid* — and it was
  the one part of the price nobody had counted.
- **The workflow entry point costs less: 115 against 162.** React keeps the consumer's `onSelect` in
  a ref written from an effect, because the controller is built once and the callback is rebuilt
  every render; Vue reads the options object late instead, and the whole ref-and-effect dance —
  along with the window in which an in-flight completion still reaches a superseded callback — does
  not exist.
- **A non-React adopter loses `JustificationTree`, and no page said so.** It is the only
  accessibility the packages carry, and it ships in the React adapter alone. Porting it is 94 lines
  and costs the markup, not the decision — nested labelled groups rather than `role="tree"`, each
  named by the assertion it explains, `aria-controls` dropped rather than left dangling — but a
  price list that omitted it was quoting for less than the goods.

The numbers in both tables above are measured from the two source trees by
`ui/examples/vue-adapter/test/price.test.ts`, and drift between the source and this page is a test
failure. A page that prices a decision should not be able to go quietly out of date.

### It is evidence, not a package

The Vue adapter is **not published, and not supported**. Motiv maintains one adapter, and shipping a
second on the release train would say otherwise. What it is for is the sentence above it: the tier
table makes a claim about a runtime nobody here maintains, and the claim is now backed by an
artefact CI keeps green rather than by an estimate. Copy it, or read it and write your own — both
are the intended use.

What you are binding to is one contract, the universal one:

```ts
const store = new RuleEditorStore({ rule: { spec: 'customer.is-active' } });

const unsubscribe = store.subscribe(() => render(store.getState()));
store.addOperand('$.rule', { spec: 'customer.is-adult' });
```

`subscribe(listener) => unsubscribe` and a `getState()` that composes the current fields fresh on
every call. There is deliberately no cached snapshot in it: caching a referentially stable snapshot
is React's tearing-avoidance requirement, and baking it in would have made the core React-shaped
without importing React. In Vue that store is a `shallowRef` plus an `onScopeDispose` — and writing
the adapter confirmed the omission was right in a way the reasoning had not reached: a Vue binding
that cached by comparing snapshot fields would go *stale*, because the store mutates behind a fresh
wrapper. In Svelte the same store is a `readable`; in plain JavaScript it is the two lines above.

### Framework-free is enforced, not asserted

"Enabled" is a claim about the artefact, so it is checked like one:

- **Nothing to install alongside it.** `@motiv-rules/core` declares no `dependencies` and no
  `peerDependencies`, and not one import in its source names anything outside the package — no bare
  specifier at all, type-only imports included. `test/framework-free.test.ts` fails on the import
  that breaks either property.
- **No DOM.** The package drops `DOM` from its TypeScript `lib`, so `document`, `window` and
  `localStorage` do not resolve in it. A core that renders nothing has no business reaching for
  them, and the compiler now says so.
- **It runs with no React present.** `scripts/isolated-consumer.mjs` packs the package the way a
  publish would, extracts the tarball into a scratch tree where *nothing else is installed*, and
  drives it from plain Node — the store, a DSL round trip, the projections, and the `/workflow`
  entry point, through both the `import` and the `require` conditions of its exports map — while
  asserting that `react` does not resolve anywhere in that tree. It runs as its own CI job. Inside
  the monorepo React is one `node_modules` away from every file, so this is the only place the
  property can actually be observed.

## .NET and Blazor

A .NET consumer — Blazor WebAssembly included — uses `Motiv.Serialization` **directly and needs no
JavaScript package at all**. It is the same C# stack the server runs: the parser, the schema, the
binder, the evaluator and the governance model, in the runtime the app is already written in. For a
.NET buyer this fits better than any JavaScript adapter would.

What that gives you today, precisely:

| You want to… | With `Motiv.Serialization` alone |
|---|---|
| Check a document is well-formed and binds | `new RuleSerializer(registry).Validate<TModel>(json)` → `IReadOnlyList<RuleError>`, each with a `$.rule…` path |
| Turn one into a live proposition | `Deserialize<TModel>(json)` → a `SpecBase<TModel, string>`, then `Evaluate(model)` |
| Check it against the published schema | `schemas/rule.v1.json` — the same file the TypeScript core validates against |
| Compose a document programmatically | Compose the JSON yourself |

That last row is the honest boundary. The document model (`RuleDocument`, `RuleNode`) and the
document parser are `internal`, and there is no C# equivalent of the TypeScript mutations, the path
arithmetic or the DSL printer. A .NET authoring UI therefore builds the JSON — from its own model,
or by driving the same HTTP API Studio drives — and leans on `Validate` to tell it what is wrong
and where. That is enough to author a valid rule document with `Motiv.Serialization` alone; it is
not a port of the TypeScript authoring core, and this page will say so until it is one.

```csharp
// A Blazor WebAssembly component authoring a rule, with no @motiv-rules/core anywhere.
var registry = new SpecRegistry()
    .Register("customer.is-active", Spec.Build((Customer c) => c.IsActive).Create("is active"))
    .Register("customer.is-adult", Spec.Build((Customer c) => c.Age >= 18).Create("is adult"));

var serializer = new RuleSerializer(registry);

const string json =
    """
    {
      "name": "customer.can-checkout",
      "rule": { "andAlso": [{ "spec": "customer.is-active" }, { "spec": "customer.is-adult" }] }
    }
    """;

var errors = serializer.Validate<Customer>(json);
if (errors.Count == 0)
{
    var rule = serializer.Deserialize<Customer>(json);
    var result = rule.Evaluate(customer);
    Console.WriteLine(result.Reason);
}
```

## One document, both cores

The two runtimes stay a single story because they validate against the same artefact:
[`schemas/rule.v1.json`](https://github.com/karlssberg/Motiv/blob/main/schemas/rule.v1.json). Both
sides test against that one file rather than a copy of it — `test/schema.test.ts` in
`@motiv-rules/core` compiles it with ajv and validates the TypeScript document shapes;
`RuleSchemaTests` in `Motiv.Serialization.Tests` copies it into its test output and evaluates the
C# ones. A change to one core that drifts from the schema fails on both sides.

The schema's own `$id` still resolves to the copy on `main`, which is a moving target for a file
named `v1`; pinning it is outstanding work on the versioning policy. It does not weaken the
guarantee above, because neither drift test resolves the URL — both read the file in the
repository.

## Web components — declined

The packages are headless: they own the logic of authoring and render nothing, so there are no
components to ship as custom elements. Manufacturing some in order to serve every framework at once
would tax the React consumer, who is the actual current user, to serve a hypothetical one. A neutral
core plus a per-runtime adapter already reaches everybody, without that cost.

## What the packages do not carry

Accessibility. The headless packages ship no components, so an adopter building their own UI
inherits none of Motiv Studio's accessibility work — the one exception being `JustificationTree`,
which is read-only and therefore tractable, and which ships in the React adapter alone. A second
runtime re-pays it: that is the third row of the Vue table above, and it is why the row is there.
See [Accessibility](../accessibility/index.md) for the full statement.

## Publishing

`Motiv` is published on NuGet. `Motiv.Serialization` and the rest of the .NET rules stack are not
yet — they are on their own maturity curve, and a deliberate sequence of breaking changes is cheaper
before a first release than after it.

`@motiv-rules/core` and `@motiv-rules/react` have not been published yet either, but the pipeline
that publishes them is in place and gated. The `@motiv-rules` npm scope is held and the names above
are final.

### Two trains, two tag prefixes

`Motiv` releases on a `v*` tag and the npm packages on a `motiv-rules-v*` one, and the prefixes
cannot match each other. The reason is the same one that gave the .NET rules stack its own version
line: sharing a train would number the packages' *first* release after `Motiv`'s majors, and would
drag `Motiv` to a new major every time the rules stack took a deliberate break — signalling a
migration to adopters whose package did not change.

Both npm packages release together, at one version. `@motiv-rules/react` is an adapter for
`@motiv-rules/core` and exists for nothing else; independent numbering would buy a pair like that
nothing, and `pnpm publish -r` already orders them so the core lands first.

### Cutting a release

1. Bump `version` in both `ui/packages/rules-core/package.json` and
   `ui/packages/rules-react/package.json` to the same value, and merge that commit. It is the
   release's reviewable artefact.
2. Tag it `motiv-rules-v<version>` and push the tag.

`.github/workflows/release-npm.yml` does the rest: it refuses immediately if the tag and the two
manifests disagree, runs the build, the typecheck, the full test suite, the isolated consumer and
the publish-readiness gate on the tagged commit, publishes both packages with
[npm provenance](https://docs.npmjs.com/generating-provenance-statements), and opens a GitHub
Release. A hyphenated version (`0.2.0-rc.1`) is marked a prerelease. The tag is the authority on the
version but does not set it — nothing is rewritten in CI, so `git show <tag>` is an honest record of
what shipped.

### What a publish is checked against

The packages are checked as a *workspace* by every other job, and a publish ships a tarball. The two
are not the same artefact, and the gaps between them are where the defects live: in the workspace
`@motiv-rules/react` reaches `@motiv-rules/core` through a symlink, so its `workspace:*` range is
never read; TypeScript resolves both packages through their `src`, so the `exports` map is never
read either; and `files` decides nothing because nothing is copied. All three are read for the first
time by a consumer, after the version on the registry is immutable.

So `pnpm verify:publishable` packs each package the way a publish would and asserts against the
tarball — that the entry points the `exports` map advertises exist and resolve from an ESM consumer
*and* a CommonJS one under `node16` resolution, that no `workspace:` range survived packing, that
the scope publishes publicly, that the licence text ships, and that the two packages agree on one
version. It runs as its own CI job on every push, and again on the tagged commit before anything
reaches the registry.

The CommonJS half of that is not hypothetical. Both packages declare `"type": "module"`, which makes
a `.d.ts` an ESM declaration, so a `types` condition shared between `import` and `require` leaves a
CommonJS consumer unable to import the package at all — `TS1479`, at compile time, with a working
`dist/index.cjs` sitting right beside it. Each entry point therefore names its declarations per
condition, and the gate is what keeps it that way.
