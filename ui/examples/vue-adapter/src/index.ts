/**
 * The surface of the worked Vue adapter — deliberately the same one `@motiv-rules/react` offers,
 * so the two can be compared line for line and the price quoted in
 * `docs/adoption/index.md` is a price for the same goods.
 */

export { provideRuleEditorStore, useRuleEditorStore } from './context.js';
export { useRuleEditor } from './useRuleEditor.js';
export { useRuleNode, type RuleNodeView } from './useRuleNode.js';
export { useCatalog, type CatalogState } from './useCatalog.js';
export { useEvaluation, type Evaluation, type EvaluationState } from './useEvaluation.js';
export { useDslSync, type DslSync } from './useDslSync.js';
export { JustificationTree, type JustificationRow } from './JustificationTree.js';
