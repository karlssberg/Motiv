/**
 * The same consumer through the CommonJS condition of the exports map. A package that publishes a
 * `require` condition owes a `require` that works: a Node script, a Jest suite or a build tool
 * that has not moved to ESM reaches this file, not the one beside it.
 */
const assert = require('node:assert/strict');
const { RuleEditorStore, parse, print, printInline } = require('@motiv-rules/core');
const workflow = require('@motiv-rules/core/workflow');

/** The whole of `@motiv-rules/core/workflow`, both save loops and the failure-text projections. */
const WORKFLOW_EXPORTS = [
  'RuleWorkflowController', 'whyRuleSaveUnavailable',
  'PropositionWorkflowController', 'whyPropositionSaveUnavailable',
  'describePropositionFailure', 'describeUnexpectedFailure',
];

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

// CommonJS resolves a missing export to `undefined` rather than failing to link, so the whole
// surface has to be named here: this is the condition where a partial `/workflow` build would
// otherwise go unnoticed until a consumer called the export that was not there.
for (const name of WORKFLOW_EXPORTS) {
  assert.equal(typeof workflow[name], 'function', `${name} is missing from @motiv-rules/core/workflow`);
}

console.log('  cjs: require() of both entry points, and the same round trip');
