import { describe, it, expect } from 'vitest';
import * as api from '../src/index.js';
import * as workflow from '../src/workflow/index.js';

/**
 * The surface of the worked adapter, alphabetically — held to the same pin the published packages
 * carry, and for a further reason here: the tier table prices *this* surface, so a symbol added
 * without a decision would change the price without changing the page.
 */
const SURFACE = [
  'JustificationTree',
  'provideRuleEditorStore',
  'useCatalog',
  'useDslSync',
  'useEvaluation',
  'useRuleEditor',
  'useRuleEditorStore',
  'useRuleNode',
];

const WORKFLOW_SURFACE = [
  'usePropositionWorkflow',
  'useRuleWorkflow',
];

describe('the adapter root', () => {
  it('exports exactly the approved surface', () => {
    expect(Object.keys(api).sort()).toEqual(SURFACE);
  });

  it('matches the React adapter one for one, allowing for each framework\'s own idiom', () => {
    // `@motiv-rules/react` exports `RuleEditorProvider` — a component, because that is how React
    // puts a value in context. Vue's equivalent is a call inside `setup`, so the name differs and
    // nothing else does. Every other symbol is the same symbol.
    const react = [
      'JustificationTree', 'RuleEditorProvider', 'useCatalog', 'useDslSync', 'useEvaluation',
      'useRuleEditor', 'useRuleEditorStore', 'useRuleNode',
    ];
    const rename = (name: string): string =>
      name === 'RuleEditorProvider' ? 'provideRuleEditorStore' : name;
    expect(react.map(rename).sort()).toEqual(SURFACE);
  });
});

describe('the workflow entry point', () => {
  it('exports exactly the approved surface', () => {
    expect(Object.keys(workflow).sort()).toEqual(WORKFLOW_SURFACE);
  });
});
