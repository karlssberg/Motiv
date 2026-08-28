import type { PropositionSaveResult } from '../contracts.js';

/**
 * Renders a save failure as something a person can act on, or `null` for a success — text, not
 * rendering, in the same spirit as `nodeSummary`: the consumer decides where the sentence goes.
 */
export function describePropositionFailure(result: PropositionSaveResult): string | null {
  switch (result.outcome) {
    case 'saved':
      return null;
    case 'conflict':
      return `Someone else saved version ${result.currentVersion}. Reload before saving again.`;
    case 'nameTaken':
      return 'A proposition is already authored under that name.';
    case 'referenced':
      return `Still referenced by ${result.referrers.join(', ')}. Change those first.`;
    case 'invalid': {
      // Broken dependents are reported apart from document errors, because a document error's path
      // points into *this* document and cannot address a break somewhere else.
      const broken = result.brokenDependents.map((dependent) =>
        `${dependent.kind} ${dependent.name} (${dependent.errors.map((error) => error.message).join('; ')})`);
      return broken.length > 0
        ? `This change would break ${broken.join(', ')}.`
        : result.errors.map((error) => error.message).join('; ');
    }
  }
}

/**
 * Renders a *thrown* failure — everything {@link describePropositionFailure} cannot see. The typed
 * outcomes cover the refusals the API models; a 500, a 404, or a body that will not parse arrives
 * as a thrown `RulesApiError` instead, and without this it would reach nobody: the surface would
 * simply do nothing, which is indistinguishable from the request never having been made.
 */
export function describeUnexpectedFailure(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}
