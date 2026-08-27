import { describe, it, expect } from 'vitest';
import * as api from '../src/index.js';

/**
 * The approved runtime API of `@motiv-rules/core`, alphabetically. Types are enforced by the
 * explicit barrel itself (a type that is not re-exported does not resolve); this pins the
 * values, so widening or narrowing the published surface is a deliberate edit to this list —
 * never a side effect of an `export` keyword somewhere in the package.
 */
const APPROVED_API = [
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

describe('the package root', () => {
  it('exports exactly the approved API', () => {
    expect(Object.keys(api).sort()).toEqual(APPROVED_API);
  });
});
