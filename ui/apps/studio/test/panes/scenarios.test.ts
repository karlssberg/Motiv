import { describe, it, expect, vi } from 'vitest';
import type { EvaluationResult, RulesApiClient } from '@motiv-rules/core';
import {
  addScenario, cloneScenario, diffAssertions, editScenario, outcomeChanged, removeScenario,
  runScenario, seedScenarios, toggleScenario, withViolations, type Scenario,
} from '../../src/panes/scenarios.js';

const result = (satisfied: boolean, ...assertions: string[]): EvaluationResult => ({
  satisfied, reason: assertions.join(' & '), assertions, values: assertions,
  justification: assertions.join('\n'), explanation: { assertions, underlying: [] },
});

const client = (live: EvaluationResult, draft: EvaluationResult): RulesApiClient =>
  ({
    evaluateRule: vi.fn().mockResolvedValue(live),
    evaluate: vi.fn().mockResolvedValue(draft),
  }) as unknown as RulesApiClient;

describe('scenarios', () => {
  it('seeds named scenarios, each unevaluated', () => {
    const rows = seedScenarios();
    expect(rows.length).toBeGreaterThan(1);
    expect(new Set(rows.map((r) => r.id)).size).toBe(rows.length);
    for (const row of rows) {
      expect(row.name).not.toBe('');
      expect(() => JSON.parse(row.model)).not.toThrow();
      expect(row.comparison).toEqual({ live: { status: 'idle' }, draft: { status: 'idle' } });
    }
  });

  it('diffs two assertion lists into removed, same and added', () => {
    expect(diffAssertions(['a', 'b'], ['b', 'c'])).toEqual([
      { kind: 'removed', text: 'a' },
      { kind: 'same', text: 'b' },
      { kind: 'added', text: 'c' },
    ]);
  });

  it('reports a change when the verdicts or the assertions differ, and null before a run', () => {
    const idle: Scenario = seedScenarios()[0]!;
    expect(outcomeChanged(idle.comparison)).toBeNull();
    expect(outcomeChanged({ live: { status: 'ready', result: result(true, 'a') }, draft: { status: 'ready', result: result(true, 'a') } })).toBe(false);
    expect(outcomeChanged({ live: { status: 'ready', result: result(true, 'a') }, draft: { status: 'ready', result: result(false, 'a') } })).toBe(true);
    expect(outcomeChanged({ live: { status: 'ready', result: result(true, 'a') }, draft: { status: 'ready', result: result(true, 'b') } })).toBe(true);
  });

  it('runs a scenario against the live rule by name and the draft document', async () => {
    const api = client(result(true, 'live says yes'), result(false, 'draft says no'));
    const row = seedScenarios()[0]!;

    const comparison = await runScenario(api, {
      ruleName: 'can-checkout', modelType: 'customer', document: { rule: { spec: 'x' } },
    }, row);

    expect(api.evaluateRule).toHaveBeenCalledWith('can-checkout', JSON.parse(row.model));
    expect(api.evaluate).toHaveBeenCalledWith({ modelType: 'customer', document: { rule: { spec: 'x' } }, model: JSON.parse(row.model) });
    expect(comparison.live).toEqual({ status: 'ready', result: result(true, 'live says yes') });
    expect(comparison.draft).toEqual({ status: 'ready', result: result(false, 'draft says no') });
  });

  it('reports each side’s failure on its own, so one broken side does not hide the other', async () => {
    const api = {
      evaluateRule: vi.fn().mockRejectedValue(new Error('down')),
      evaluate: vi.fn().mockResolvedValue(result(true, 'fine')),
    } as unknown as RulesApiClient;
    const comparison = await runScenario(api, { ruleName: 'r', modelType: 'customer', document: { rule: { spec: 'x' } } }, seedScenarios()[0]!);
    expect(comparison.live).toEqual({ status: 'error', message: 'down' });
    expect(comparison.draft.status).toBe('ready');
  });

  it('refuses to run a scenario whose model is not JSON', async () => {
    const api = client(result(true), result(true));
    const row: Scenario = { ...seedScenarios()[0]!, model: '{ not json' };
    const comparison = await runScenario(api, { ruleName: 'r', modelType: 'customer', document: { rule: { spec: 'x' } } }, row);
    expect(comparison.live).toEqual({ status: 'error', message: 'Scenario model is not valid JSON.' });
    expect(comparison.draft).toEqual({ status: 'error', message: 'Scenario model is not valid JSON.' });
    expect(api.evaluate).not.toHaveBeenCalled();
  });

  it('editing the model drops the row back to unevaluated; renaming does not', () => {
    const evaluated: Scenario = {
      ...seedScenarios()[0]!,
      comparison: { live: { status: 'ready', result: result(true) }, draft: { status: 'ready', result: result(true) } },
    };
    const renamed = editScenario([evaluated], evaluated.id, { name: 'Other' })[0]!;
    expect(renamed.comparison.live.status).toBe('ready');
    const changed = editScenario([evaluated], evaluated.id, { model: '{}' })[0]!;
    expect(changed.comparison).toEqual({ live: { status: 'idle' }, draft: { status: 'idle' } });
  });

  it('clones beneath the source, unevaluated and named as a copy; adds at the end; removes by id', () => {
    const rows = seedScenarios();
    const [first, second] = rows;
    const cloned = cloneScenario(rows, first!.id);
    expect(cloned[1]!.name).toBe(`${first!.name} (copy)`);
    expect(cloned[1]!.model).toBe(first!.model);
    expect(cloned[1]!.id).not.toBe(first!.id);
    expect(cloned[2]!.id).toBe(second!.id);

    const added = addScenario(rows);
    expect(added.at(-1)!.name).toBe(`Scenario ${rows.length + 1}`);

    expect(removeScenario(rows, first!.id).map((r) => r.id)).not.toContain(first!.id);
  });

  it('derives ids from the list, so adding is a pure function of its input', () => {
    const rows = seedScenarios();
    const once = addScenario(rows);
    const twice = addScenario(rows);
    expect(once.at(-1)!.id).toBe(twice.at(-1)!.id);
    expect(new Set(addScenario(once).map((r) => r.id)).size).toBe(rows.length + 2);
  });

  it('opens a new or cloned row, toggles a row, and a deleted row takes its state with it', () => {
    const rows = seedScenarios();
    expect(rows.every((r) => !r.open)).toBe(true);
    expect(addScenario(rows).at(-1)!.open).toBe(true);
    expect(cloneScenario(rows, rows[0]!.id)[1]!.open).toBe(true);
    const toggled = toggleScenario(rows, rows[1]!.id);
    expect(toggled[1]!.open).toBe(true);
    expect(toggleScenario(toggled, rows[1]!.id)[1]!.open).toBe(false);
    expect(removeScenario(toggled, rows[1]!.id).some((r) => r.open)).toBe(false);
  });

  it('records violations against a row, and editing its model clears them', () => {
    const rows = seedScenarios();
    const violation = { path: '$.age', message: 'expected integer, got string' };
    const flagged = withViolations(rows, rows[0]!.id, [violation]);
    expect(flagged[0]!.violations).toEqual([violation]);
    expect(editScenario(flagged, rows[0]!.id, { name: 'Renamed' })[0]!.violations).toEqual([violation]);
    expect(editScenario(flagged, rows[0]!.id, { model: '{}' })[0]!.violations).toEqual([]);
  });
});
