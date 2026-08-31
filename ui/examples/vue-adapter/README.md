# A worked Vue adapter for `@motiv-rules/core`

A complete Vue 3 adapter over the headless core: the same surface `@motiv-rules/react` offers,
symbol for symbol, bound to Vue's reactivity instead of React's.

It exists to make one claim checkable. [Runtimes and Support
Tiers](../../../docs/adoption/index.md) puts Vue, Svelte and vanilla JavaScript in a tier called
*enabled, not supported* — the core is framework-free, write your own bindings — and prices those
bindings. A price nobody has paid is an estimate. This is the payment, and the numbers on that page
are now measured from these files by `test/price.test.ts`, which fails when the source and the page
disagree.

## It is not published, and not supported

`private: true`, so `pnpm -r publish` and the publish-readiness gate both skip it, while `pnpm -r
build`, `typecheck` and `test` do not. Motiv maintains **one** adapter; a second on the release
train would say otherwise. Copy this one into your app, or read it and write your own — both are
the intended use, and neither comes with a support promise.

## What is here

| File | What it binds |
|---|---|
| `src/observe.ts` | The one binding. Follows any `subscribe`/`getState` object in core for the life of the calling scope, rebuilding it when a source changes |
| `src/context.ts` | `provide`/`inject` for the shared `RuleEditorStore` |
| `src/useRuleEditor.ts` | A scope's subscription to the editor state |
| `src/useRuleNode.ts` | One node's view over the store, and the errors anchored on it |
| `src/useCatalog.ts`, `src/useEvaluation.ts` | Async client state |
| `src/useDslSync.ts` | The DSL⇄tree sync controller, tied to the scope's lifetime |
| `src/workflow/` | The save loops — optimistic save, 409 recovery, blast-radius reporting |
| `src/JustificationTree.ts` | The one component: the explanation's accessible structure, with a scoped slot for the markup |

Everything else — path arithmetic, insertion planning, the accordion state machine, DSL parsing,
printing, completion, diagnostics, the accessible-name projection — is in `@motiv-rules/core` and
has no framework in it. `test/bindings-only.test.ts` reads that off the source: no file here
imports anything but `vue` and the core.

## Using it

The composables take their store or client as a value, a ref or a getter, and follow the calling
scope — a component's `setup`, or an `effectScope`.

```ts
import { provideRuleEditorStore, useRuleEditor, useDslSync } from './src/index.js';
import { RuleEditorStore } from '@motiv-rules/core';

const store = new RuleEditorStore({ rule: { spec: 'customer.is-active' } });

export default defineComponent({
  setup() {
    provideRuleEditorStore(store);
    const state = useRuleEditor(store);
    const dsl = useDslSync(store);

    return () => h('pre', [state.value.document, dsl.state.value.text]);
  },
});
```

## Running it

```sh
pnpm --filter @motiv-rules/vue-example test
pnpm --filter @motiv-rules/vue-example typecheck
```

Both run in CI as part of `pnpm -r`, alongside the published packages.
