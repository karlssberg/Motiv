/**
 * The public surface of `@motiv-rules/react/workflow` — bindings-only wrappers over
 * `@motiv-rules/core/workflow`'s controllers, behind their own entry point for the same reason
 * the core splits them out: taking the document bindings from the package root never drags in
 * the session workflow. Pinned by the approved-API snapshot in `test/api-surface.test.tsx`.
 */

export { useRuleWorkflow, type RuleWorkflow } from './useRuleWorkflow.js';
export {
  usePropositionWorkflow,
  type PropositionWorkflow, type UsePropositionWorkflowOptions,
} from './usePropositionWorkflow.js';
