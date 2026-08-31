/**
 * The `workflow` entry point of the worked Vue adapter — the authoring session's save loops,
 * split from the root for the same reason `@motiv-rules/core` splits them: taking the document
 * bindings must never drag in session opinions or the `RulesApiClient` coupling.
 */

export { useRuleWorkflow, type RuleWorkflow } from './useRuleWorkflow.js';
export {
  usePropositionWorkflow,
  type PropositionWorkflow, type UsePropositionWorkflowOptions,
} from './usePropositionWorkflow.js';
