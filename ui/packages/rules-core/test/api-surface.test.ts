import { describe, it, expect } from 'vitest';
import * as api from '../src/index.js';
import * as workflow from '../src/workflow/index.js';

/**
 * The approved runtime API of `@motiv-rules/core`, alphabetically. Types are enforced by the
 * explicit barrel itself (a type that is not re-exported does not resolve); this pins the
 * values, so widening or narrowing the published surface is a deliberate edit to this list —
 * never a side effect of an `export` keyword somewhere in the package.
 */
const APPROVED_API = [
  'ACCESSIBLE_NAME_LIMIT',
  'BINARY_OPERATORS',
  'DSL_KEYWORDS',
  'DSL_QUANTIFIERS',
  'DSL_TYPES',
  'DslSyncController',
  'EMPTY_ACCORDION',
  'EMPTY_HIGHLIGHT',
  'HIGHER_ORDER_KEYS',
  'N_QUANTIFIER_KINDS',
  'OPERATOR_LABELS',
  'PARAM_REST_CHARS',
  'RuleEditorStore',
  'RulesApiClient',
  'RulesApiError',
  'WORD_REST_CHARS',
  'WORD_START_CHARS',
  'accessibleExpression',
  'binaryOperator',
  'buildNamespaceTree',
  'childPaths',
  'closeAll',
  'completeDsl',
  'countLeaves',
  'createValidationController',
  'diagnosticsFor',
  'errorsForNode',
  'filterTree',
  'firstOperandTarget',
  'flattenExplanation',
  'focusedPath',
  'getNode',
  'higherOrderBody',
  'higherOrderKey',
  'insertTargetForRow',
  'isBinaryNode',
  'isCollapsed',
  'isExpressionNode',
  'isHigherOrderNode',
  'isNotNode',
  'isOpen',
  'isPinned',
  'isSpecNode',
  'joinSteps',
  'listPaths',
  'literalCountOf',
  'mergeDecorations',
  'nodeKind',
  'normalizeAt',
  'operandsOf',
  'parse',
  'planInsert',
  'print',
  'printInline',
  'rangeOfPath',
  'setBinaryOperator',
  'setHovered',
  'setNode',
  'setQuantifierCollection',
  'setQuantifierKind',
  'setQuantifierN',
  'setSelected',
  'splitLast',
  'summarize',
  'toExplanationView',
  'toggleCollapsed',
  'toggleOpen',
  'togglePin',
  'tokenSpans',
  'tokenize',
  'validateAgainstSchema',
];

/**
 * The approved runtime API of the `@motiv-rules/core/workflow` entry point — separate from the
 * root by design (ticket 07): document logic must be takeable without the session workflow or
 * its `RulesApiClient` coupling, so the workflow surface is its own deliberate list.
 */
const APPROVED_WORKFLOW_API = [
  'PropositionWorkflowController',
  'RuleWorkflowController',
  'describePropositionFailure',
  'describeUnexpectedFailure',
  'whyPropositionSaveUnavailable',
  'whyRuleSaveUnavailable',
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
