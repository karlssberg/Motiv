/**
 * The public surface of `@motiv-rules/core/workflow` — the authoring session's workflow logic
 * (optimistic save, 409 recovery, blast-radius reporting), behind its own entry point so taking
 * the document logic from the package root never drags in session opinions or the
 * `RulesApiClient` coupling. Everything here is chosen, pinned by the approved-API snapshot in
 * `test/api-surface.test.ts`.
 */

// The rules save loop.
export {
  RuleWorkflowController, whyRuleSaveUnavailable,
  type LoadedRule, type RuleWorkflowState,
} from './ruleWorkflow.js';

// The propositions save loop.
export {
  PropositionWorkflowController, whyPropositionSaveUnavailable,
  type LoadedProposition, type PropositionWorkflowState, type PropositionWorkflowOptions,
} from './propositionWorkflow.js';

// Failure text projections.
export { describePropositionFailure, describeUnexpectedFailure } from './failureText.js';
