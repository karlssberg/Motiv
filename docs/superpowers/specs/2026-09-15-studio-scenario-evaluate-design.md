# Studio scenarios: Evaluate as a table of samples, live beside draft

**Date:** 2026-09-15
**Status:** Implemented
**Primary source:** the prototype in PR #245 (`ui/apps/studio/src/panes/prototype/` on
`claude/checkout-section-redesign-9e673b`), seven variants switchable via `?variant=`; verdict
**C** in the first session (the scenario table) and **C2** in the second (what a row reveals).

## Problem

A rule tab's rail held two panes that ran the rule: **Evaluate**, the draft against one sample
model, and **Checkout**, the live server's decision for a hard-coded customer. Checkout did not
belong to the open rule — it called `POST /api/checkout`, which runs all three of Studio's rules as
one pinned decision, so a person editing `loyalty-discount` was shown verdicts for `can-checkout`
and `fraud-screening` too. The comparison it offered, *what the server decides now* against *what
my draft would decide*, was the useful part; the pane around it was not.

## What the prototype settled

Round one asked *how should before/after comparison look, and how does it belong to the open
rule?* Three answers were built over real evaluations:

- **A** — compare as a mode of Evaluate, one merged assertion list with diff marks;
- **B** — live and draft side by side under one sample, each with its own explanation tree;
- **C** — a table of named sample models, Live and Draft as columns, flipped rows highlighted.

**C won** — "it's like having unit tests". Round two added edit, clone, delete and reset, and asked
*what does a row's disclosure reveal before the row has been evaluated?*

- **C1** — no chevron until a run; the name opens the editor;
- **C2** — the chevron always present; the model editor stacked over the explanations;
- **C3** — the chevron always present; the detail tabbed Model | Why, Why disabled until a run.

**C2 won.**

| Question | Decision |
|---|---|
| Where it lives | **One pane, still named Evaluate**, in the rule rail. Checkout is gone. |
| Rows | **Named scenarios**: a name and a model JSON. Studio seeds four for its customer model; a tab's scenarios live in memory with the tab, like the old sample model did. |
| Columns | **Live v*N*** — what the server decides for *this rule* right now — and **Draft** — what the tab's document would decide. One tick or cross and the word per cell. |
| Disclosure | **Always present.** The detail is the scenario's editor (name, model) on top, and beneath it, once the row has run, the two explanation trees side by side, Live then Draft. |
| Editing | In place. **Editing the model drops the row back to unevaluated**: the last verdict no longer describes that input, so it is not shown against it. |
| Actions | **Run all**, **Add**, **Reset** (restore the seeded scenarios) in the pane's run row; **Clone** and **Delete** per row, revealed on hover and always reachable by keyboard. Clone inserts beneath its source, opened for editing. |
| Change signal | A row whose two verdicts, or whose assertion sets, differ is marked **flips**; the run row says **N of M scenarios change**, or **No scenario changes**. |
| Schemas | The catalog's model schema is enforced per scenario before it runs, as the old pane did for its one sample; violations show in the row's detail. |
| The live side | **`POST {basePath}/rules/{name}/evaluate`**, new in `Motiv.Serialization.AspNetCore`: evaluates the *live* rule by name against a model. It is the only way to reproduce a code-defined default, which has no document `POST /evaluate` could rebuild. |

## Not in scope

- **Persisting scenarios.** They are tab state. Keeping them with the rule document, or per user,
  is a later decision — the table's shape does not change either way.
- **Running one row.** Run all is the one gesture; the table is small.
- **Propositions.** A proposition tab keeps the single-sample Evaluate pane: a proposition is not
  a live rule, so there is no server side to compare a draft against.
- **`/api/checkout`.** The endpoint stays: it is Studio's demonstration of the consuming side and
  the source of the decision records the admin page reads. Only the pane that called it goes.

## Architecture

```
RuleDocument
└─ rail
   └─ ScenarioPane (src/panes/ScenarioPane.tsx)            region "Evaluate"
      ├─ run row: Run all · Add · Reset · change count
      └─ table: chevron | Scenario | Live vN | Draft | actions
         └─ detail row: ScenarioEditor (name, model) · SchemaViolations · Live | Draft explanations
      state: src/panes/scenarios.ts — Scenario[] with per-row Comparison { live, draft }
      live:  client.evaluateRule(name, model)   → POST /api/rules/rules/{name}/evaluate
      draft: client.evaluate({ modelType, document, model })
```

Server:

```
RuleBase.EvaluateBoxedAsync(object model, ResultSerializer, CancellationToken)   internal
  Rule<TModel,TMetadata>      → Evaluate((TModel) model), projected
  AsyncRule<TModel,TMetadata> → await EvaluateAsync((TModel) model), projected
ModelBinding.BindModel(JsonSerializerOptions, JsonElement) → object   (InvalidModelException)
POST {basePath}/rules/{name}/evaluate  { model }
  grant: same gate as /evaluate · 404 unknown rule · 400 missing or unbindable model
  · 400 when the rule's model type is not registered · runs inside the group's generation pin
```

An audited rule records a decision when evaluated this way, exactly as it did through checkout:
it *is* the live rule being evaluated.

## Accessibility

The region keeps its name, `Evaluate`, so the axe sweep and the e2e suites find it where it was.
Every chevron is named by its scenario (`details of <name>`), reports `aria-expanded`, and drops
`aria-controls` while the detail is unmounted. Row actions have visible text or an accessible name
naming the scenario, and are never hover-only for the keyboard. The explanation trees are the
`JustificationTree` the old pane rendered, unchanged.
