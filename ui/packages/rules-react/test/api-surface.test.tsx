import { describe, it, expect } from 'vitest';
import * as api from '../src/index.js';
import * as workflow from '../src/workflow/index.js';

/**
 * The approved runtime API of `@motiv-rules/react`, alphabetically — the same pin
 * `@motiv-rules/core` carries: widening or narrowing the published surface is a deliberate edit
 * to these lists, never a side effect of an `export` keyword somewhere in the package.
 */
const APPROVED_API = [
  'JustificationTree',
  'RuleEditorProvider',
  'useCatalog',
  'useDslSync',
  'useEvaluation',
  'useRuleEditor',
  'useRuleEditorStore',
  'useRuleNode',
];

/**
 * The approved runtime API of the `@motiv-rules/react/workflow` entry point — separate from the
 * root for the same reason the core splits it out (ticket 07): the document bindings must be
 * takeable without the session workflow.
 */
const APPROVED_WORKFLOW_API = [
  'usePropositionWorkflow',
  'useRuleWorkflow',
];

describe('the package root', () => {
  it('exports exactly the approved API', () => {
    expect(Object.keys(api).sort()).toEqual(APPROVED_API);
  });
});

describe('the workflow entry point', () => {
  it('exports exactly the approved API', () => {
    expect(Object.keys(workflow).sort()).toEqual(APPROVED_WORKFLOW_API);
  });
});
