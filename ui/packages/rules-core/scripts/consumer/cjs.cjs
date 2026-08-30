/**
 * The same consumer through the CommonJS condition of the exports map. A package that publishes a
 * `require` condition owes a `require` that works: a Node script, a Jest suite or a build tool
 * that has not moved to ESM reaches this file, not the one beside it.
 */
const assert = require('node:assert/strict');
const { RuleEditorStore, parse, print, printInline } = require('@motiv-rules/core');
const { PropositionWorkflowController } = require('@motiv-rules/core/workflow');

assert.throws(
  () => require.resolve('react'),
  (error) => error.code === 'MODULE_NOT_FOUND',
  'react resolved from the isolated consumer — the scratch tree is not isolated',
);

const store = new RuleEditorStore({ rule: { orElse: [{ spec: 'is-vip' }, { spec: 'is-active' }] } });
store.setName('$.rule', 'eligible');

const text = print(store.getState().document);
assert.deepEqual(parse(text).document, store.getState().document);
assert.equal(printInline(store.getState().document.rule), '(is-vip || is-active) as "eligible"');

assert.equal(typeof PropositionWorkflowController, 'function');

console.log('  cjs: require() of both entry points, and the same round trip');
