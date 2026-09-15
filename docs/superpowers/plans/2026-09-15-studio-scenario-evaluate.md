# Studio scenarios — implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or
> superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax.
> Written in the same change as the implementation, per `CLAUDE.md`.

**Goal:** Replace the rule rail's Evaluate and Checkout panes with one Evaluate pane that runs a
table of named scenarios against both the live rule and the draft, scoped to the open rule.

**Architecture:** A per-rule live evaluation endpoint in `Motiv.Serialization.AspNetCore`, backed
by a boxed evaluate on `RuleBase`; a client method for it in `@motiv-rules/core`; a `ScenarioPane`
in Studio over a small scenario state module. Checkout's pane and test go; its endpoint stays.

**Tech stack:** ASP.NET Core minimal APIs, xunit + Shouldly, TypeScript, React 18, Vitest +
Testing Library, Playwright.

**Spec:** `docs/superpowers/specs/2026-09-15-studio-scenario-evaluate-design.md`

## Global constraints

- The `Evaluate` region name is pinned by the e2e and axe suites; `Checkout` is removed from both.
- No new runtime dependency in `ui/`.
- `pnpm -C ui/apps/studio a11y` stays green; an `aria-controls` IDREF is dropped whenever its
  target is unmounted.

## Tasks

- [x] **1. Boxed evaluate on rules.** `RuleBase.EvaluateBoxedAsync(object, ResultSerializer,
  CancellationToken)` (internal), overridden by `Rule<,>` and `AsyncRule<,>`. Tests in
  `Motiv.Serialization.Tests`: sync and async rules project the same `RuleEvaluationResult`
  their typed entry points do.
- [x] **2. Model binding.** `ModelBinding.BindModel` deserialises a `JsonElement` to `TModel`,
  throwing `InvalidModelException` on a shape mismatch or null; `Evaluate` uses it.
- [x] **3. Endpoint.** `POST {basePath}/rules/{name}/evaluate` with `RuleEvaluateRequest(Model)`.
  Tests in `Motiv.Serialization.AspNetCore.Tests/RuleEvaluateEndpointTests.cs`: evaluates a live
  sync rule and an async one; reflects a published document; 404 unknown rule; 400 missing model;
  400 unbindable model; refused without an author grant where `/evaluate` is.
- [x] **4. Docs.** Row in `docs/live-rules/AspNetCore.md`'s endpoint table.
- [x] **5. Client.** `RulesApiClient.evaluateRule(name, model)`; contract `RuleEvaluateRequest`;
  test in `rules-core/test/client.test.ts`.
- [x] **6. Scenario state.** `ui/apps/studio/src/panes/scenarios.ts`: `Scenario`, `Comparison`,
  `seedScenarios`, `diffAssertions`, `outcomeChanged`, `runScenario`. Vitest.
- [x] **7. ScenarioPane.** Replaces `EvaluatePane` and `CheckoutPane` in `RuleDocument`'s rail;
  region `Evaluate`. Vitest covers: seeded rows; Run all fills both columns and counts changes;
  a flipped row is marked; editing invalidates; clone/delete/reset; schema violations block a
  row; the explanation's disclosure semantics carried from the old pane's tests.
- [x] **8. Remove Checkout.** Delete `CheckoutPane.tsx`, its test and CSS; update the two
  comments that name it as the raw-fetch seam; `RuleDocument.test.tsx` no longer expects it.
- [x] **9. e2e.** `live-rules.spec.ts` and `propositions.spec.ts` read the scenario table's Live
  column instead of pressing *Try checkout*.
- [x] **10. Verify.** `dotnet test` (full solution), `pnpm -r build && pnpm -r typecheck && pnpm -r
  test`, `pnpm e2e`, `pnpm -C ui/apps/studio a11y`.
