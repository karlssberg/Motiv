// PROTOTYPE — throwaway. Shared plumbing for the Evaluate-with-compare variants.
import type { EvaluationResult, RuleDocument, RulesApiClient } from '@motiv-rules/core';
import { MODEL_TYPE } from '../../App.js';

/**
 * Which field of the live checkout decision belongs to the open rule. The checkout endpoint runs
 * all three rules as one pinned decision; the prototype *selects* the open rule's part of it, so
 * "live" is what the server decides for THIS rule right now, including a code-defined default that
 * POST /evaluate could not reproduce. A production seam would be a per-rule live-evaluate endpoint.
 */
const LIVE_FIELD: Record<string, 'eligibility' | 'screening' | 'loyalty'> = {
  'can-checkout': 'eligibility',
  'fraud-screening': 'screening',
  'loyalty-discount': 'loyalty',
};

export type Side =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'ready'; result: EvaluationResult }
  | { status: 'error'; message: string }
  | { status: 'unavailable'; message: string };

export interface Comparison {
  live: Side;
  draft: Side;
}

export const idle: Comparison = { live: { status: 'idle' }, draft: { status: 'idle' } };

export async function runDraft(client: RulesApiClient, document: RuleDocument, model: unknown): Promise<Side> {
  try {
    return { status: 'ready', result: await client.evaluate({ modelType: MODEL_TYPE, document, model }) };
  } catch (cause) {
    return { status: 'error', message: cause instanceof Error ? cause.message : String(cause) };
  }
}

export async function runLive(ruleName: string, modelJson: string): Promise<Side> {
  const field = LIVE_FIELD[ruleName];
  if (!field) return { status: 'unavailable', message: `“${ruleName}” is not part of the live checkout decision.` };
  try {
    const response = await fetch('/api/checkout', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: modelJson,
    });
    if (!response.ok) return { status: 'error', message: `Live evaluation failed (${response.status}).` };
    const body = (await response.json()) as Record<string, EvaluationResult | undefined>;
    const result = body[field];
    if (!result) return { status: 'unavailable', message: 'The server did not return this rule.' };
    return { status: 'ready', result };
  } catch (cause) {
    return { status: 'error', message: cause instanceof Error ? cause.message : String(cause) };
  }
}

export async function runBoth(
  client: RulesApiClient, ruleName: string, document: RuleDocument, modelJson: string,
): Promise<Comparison> {
  const model = JSON.parse(modelJson) as unknown;
  const [live, draft] = await Promise.all([runLive(ruleName, modelJson), runDraft(client, document, model)]);
  return { live, draft };
}

/** Line-level diff of two flat assertion lists (order-insensitive). */
export function diffAssertions(before: string[], after: string[]) {
  const b = new Set(before);
  const a = new Set(after);
  return [
    ...before.filter((x) => !a.has(x)).map((text) => ({ kind: 'removed' as const, text })),
    ...after.filter((x) => b.has(x)).map((text) => ({ kind: 'same' as const, text })),
    ...after.filter((x) => !b.has(x)).map((text) => ({ kind: 'added' as const, text })),
  ];
}

export function outcomeChanged(c: Comparison): boolean | null {
  if (c.live.status !== 'ready' || c.draft.status !== 'ready') return null;
  return c.live.result.satisfied !== c.draft.result.satisfied
    || diffAssertions(c.live.result.assertions, c.draft.result.assertions).some((d) => d.kind !== 'same');
}
