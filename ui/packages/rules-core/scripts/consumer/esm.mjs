/**
 * A framework-free consumer, in the shape a Vue, Svelte or vanilla adapter would take: subscribe
 * to the store, mutate it, read `getState()` back, and print. Nothing here is React-specific
 * because nothing in the package is — which is the point being tested.
 *
 * Runs from a scratch tree in which `@motiv-rules/core` is the only thing installed.
 */
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import {
  RuleEditorStore, accessibleExpression, getNode, parse, print, printInline, summarize,
} from '@motiv-rules/core';
import {
  PropositionWorkflowController, RuleWorkflowController,
  describePropositionFailure, describeUnexpectedFailure,
  whyPropositionSaveUnavailable, whyRuleSaveUnavailable,
} from '@motiv-rules/core/workflow';

// 1. The central claim: React is not here. If this ever resolves, the tree is not isolated and
//    every assertion below has been proving something weaker than it appears to.
assert.throws(
  () => createRequire(import.meta.url).resolve('react'),
  (error) => error.code === 'MODULE_NOT_FOUND',
  'react resolved from the isolated consumer — the scratch tree is not isolated',
);

// 2. The store is an observable an adapter binds to: subscribe, mutate, read back.
const store = new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
let notifications = 0;
const unsubscribe = store.subscribe(() => { notifications += 1; });

store.addOperand('$.rule', { spec: 'is-verified' });
assert.equal(notifications, 1, 'a mutation must notify subscribers');
assert.deepEqual(getNode(store.getState().document, '$.rule.and[2]'), { spec: 'is-verified' });

store.undo();
assert.equal(notifications, 2);
assert.equal(store.getState().canRedo, true);

unsubscribe();
store.redo();
assert.equal(notifications, 2, 'an unsubscribed listener must not be called');

// 3. The DSL: print what the store holds, parse it back, and land on the same document.
const text = print(store.getState().document);
const reparsed = parse(text);
assert.deepEqual(reparsed.errors, []);
assert.deepEqual(reparsed.document, store.getState().document);

// 4. The projections a UI renders from — the one-line expression, the accessible name, the badge.
assert.equal(printInline(store.getState().document.rule), 'is-active & is-adult & is-verified');
assert.equal(accessibleExpression(store.getState().document.rule), 'is-active & is-adult & is-verified');
assert.equal(summarize(store.getState().document.rule).badge, 'AND');

// 5. The workflow entry point resolves as its own subpath and carries its whole surface. Both
//    families, not one: the rules and propositions save loops are separate exports of the same
//    subpath, so a build that dropped either would still satisfy a check that named only the other.
//    Under ESM the named imports above are themselves the check — a missing export is a link-time
//    error before this file runs — and these assertions pin what those names must be.
const workflow = {
  RuleWorkflowController, whyRuleSaveUnavailable,
  PropositionWorkflowController, whyPropositionSaveUnavailable,
  describePropositionFailure, describeUnexpectedFailure,
};
for (const [name, value] of Object.entries(workflow)) {
  assert.equal(typeof value, 'function', `${name} is missing from @motiv-rules/core/workflow`);
}

console.log('  esm: store, DSL round trip, projections and /workflow — all with nothing else installed');
