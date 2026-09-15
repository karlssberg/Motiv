import { useId, useState, type CSSProperties } from 'react';
import { validateAgainstSchema, type EvaluationResult, type RulesApiClient, type SchemaViolation } from '@motiv-rules/core';
import { JustificationTree, useCatalog, useRuleEditor, useRuleEditorStore } from '@motiv-rules/react';
import { MODEL_TYPE } from '../App.js';
import { SchemaViolations } from './SchemaViolations.js';
import { Tick } from './Verdict.js';
import { Caret, IconDelete, IconNew, IconPlay, IconRefresh } from '../shell/icons.js';
import {
  addScenario, cloneScenario, editScenario, outcomeChanged, removeScenario, runScenario, seedScenarios,
  toggleScenario, withComparison, withViolations, type Comparison, type Scenario, type Side,
} from './scenarios.js';

/**
 * Evaluate, as a table of scenarios: named sample models as rows, and for each what the *live*
 * rule decides right now and what the tab's *draft* would decide. A row whose two answers differ
 * is what saving would change — the table is the rule's unit tests, run against both versions.
 *
 * Scenarios are tab state, like the single sample model this pane replaced. Editing a model drops
 * its row back to unevaluated: the last verdict described the old input.
 */
export function ScenarioPane(props: { client: RulesApiClient; ruleName: string; version?: number | undefined }) {
  const store = useRuleEditorStore();
  const state = useRuleEditor(store);
  const catalogState = useCatalog(props.client);
  const [rows, setRows] = useState<Scenario[]>(seedScenarios);

  // Absent while loading or on older backends without modelTypes — then enforcement simply doesn't run.
  const modelSchema = catalogState.status === 'ready' ? catalogState.data.modelTypes?.[MODEL_TYPE] : undefined;

  // Every change to the table is a pure function of the committed rows, so two clicks before a
  // re-render compose rather than the second overwriting the first.
  const runAll = async (): Promise<void> => {
    // Enforce the catalog's model schema per scenario when we have one and the text parses; a row
    // that breaks it is held back with its violations, the rest run. Unparseable text is the run's
    // problem to report, not the schema's.
    const held = new Map<number, SchemaViolation[]>();
    const runnable: Scenario[] = [];
    for (const row of rows) {
      let model: unknown;
      try { model = JSON.parse(row.model); } catch { runnable.push(row); continue; }
      const broken = modelSchema ? validateAgainstSchema(model, modelSchema) : [];
      if (broken.length > 0) held.set(row.id, broken); else runnable.push(row);
    }
    const loading: Comparison = { live: { status: 'loading' }, draft: { status: 'loading' } };
    setRows((current) => {
      let next = current;
      for (const [id, violations] of held) next = withViolations(next, id, violations);
      return next.map((r) => (runnable.some((x) => x.id === r.id) ? { ...r, violations: [], comparison: loading } : r));
    });
    const rule = { ruleName: props.ruleName, modelType: MODEL_TYPE, document: state.document };
    await Promise.all(runnable.map(async (row) => {
      const comparison = await runScenario(props.client, rule, row);
      setRows((current) => withComparison(current, row.id, comparison));
    }));
  };

  const ran = rows.some((r) => r.comparison.draft.status !== 'idle');
  const flipped = rows.filter((r) => outcomeChanged(r.comparison) === true).length;

  return (
    <section aria-label="Evaluate" className="pane">
      <div className="pane-header">
        <h2>Evaluate</h2>
        <span className="pane-badge">scenarios</span>
        <span className="pane-hint truncate">every sample, live and draft</span>
      </div>
      <div className="pane-body">
        <div className="run-row">
          <button type="button" className="btn" onClick={() => void runAll()}><IconPlay size={14} />Run all</button>
          <button type="button" className="btn btn-secondary" onClick={() => setRows(addScenario)}>
            <IconNew size={14} />Add
          </button>
          <button type="button" className="btn btn-secondary" title="Restore the seeded scenarios" onClick={() => setRows(seedScenarios())}>
            <IconRefresh size={14} />Reset
          </button>
          {ran && (
            <span className={flipped > 0 ? 'change-count change-count-changed' : 'change-count'}>
              {flipped === 0 ? 'No scenario changes' : `${flipped} of ${rows.length} scenarios change`}
            </span>
          )}
        </div>
        <table className="scenarios">
          <thead>
            <tr>
              <th><span className="sr-only">details</span></th>
              <th>Scenario</th>
              <th>Live{props.version !== undefined ? ` v${props.version}` : ''}</th>
              <th>Draft</th>
              <th><span className="sr-only">actions</span></th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <ScenarioRow
                key={row.id}
                row={row}
                onToggle={() => setRows((current) => toggleScenario(current, row.id))}
                onEdit={(change) => setRows((current) => editScenario(current, row.id, change))}
                onClone={() => setRows((current) => cloneScenario(current, row.id))}
                onDelete={() => setRows((current) => removeScenario(current, row.id))}
              />
            ))}
          </tbody>
        </table>
        {rows.length === 0 && <p className="pane-hint">No scenarios. Add one, or reset to the seeds.</p>}
      </div>
    </section>
  );
}

function ScenarioRow(props: {
  row: Scenario;
  onToggle: () => void;
  onEdit: (change: { name?: string; model?: string }) => void;
  onClone: () => void;
  onDelete: () => void;
}) {
  const { row } = props;
  const { open } = row;
  const detailId = useId();
  const changed = outcomeChanged(row.comparison);
  return (
    <>
      <tr aria-label={row.name} className={changed ? 'scenario scenario-changed' : 'scenario'}>
        <td>
          {/* `aria-controls` is dropped while the detail row is unmounted, the rule every caret here follows. */}
          <button
            type="button"
            className="node-chev"
            aria-expanded={open}
            aria-controls={open ? detailId : undefined}
            aria-label={`details of ${row.name}`}
            onClick={props.onToggle}
          >
            <Caret open={open} size={12} />
          </button>
        </td>
        <td>
          <span className="scenario-name">{row.name}</span>
          {changed && <span className="pane-badge badge-changed">flips</span>}
        </td>
        <td aria-label="live"><Cell side={row.comparison.live} /></td>
        <td aria-label="draft"><Cell side={row.comparison.draft} /></td>
        <td className="scenario-actions">
          <button type="button" className="row-action" aria-label={`clone ${row.name}`} onClick={props.onClone}>Clone</button>
          <button type="button" className="row-action row-action-danger" aria-label={`delete ${row.name}`} onClick={props.onDelete}>
            <IconDelete size={12} />
          </button>
        </td>
      </tr>
      {open && (
        <tr id={detailId} aria-label={`${row.name}: details`} className="scenario-detail">
          <td />
          <td colSpan={4}>
            <div className="scenario-editor">
              <input aria-label="scenario name" className="control" value={row.name} onChange={(e) => props.onEdit({ name: e.target.value })} />
              <textarea aria-label="scenario model" className="control" rows={5} value={row.model} onChange={(e) => props.onEdit({ model: e.target.value })} />
            </div>
            <SchemaViolations violations={row.violations} />
            {row.comparison.draft.status !== 'idle' && (
              <div className="scenario-sides">
                <div className="scenario-side">
                  <h3>Live</h3>
                  <SideBody side={row.comparison.live} who="the live rule" />
                </div>
                <div className="scenario-side scenario-side-draft">
                  <h3>Draft</h3>
                  <SideBody side={row.comparison.draft} who="the draft" />
                </div>
              </div>
            )}
          </td>
        </tr>
      )}
    </>
  );
}

/** One verdict in the table: a tick or a cross and a word, or what stands in for one. */
function Cell(props: { side: Side }) {
  const { side } = props;
  if (side.status === 'ready') {
    return <><Tick satisfied={side.result.satisfied} />{side.result.satisfied ? 'yes' : 'no'}</>;
  }
  if (side.status === 'loading') return <span className="pane-hint">…</span>;
  if (side.status === 'error') return <span className="pane-hint" title={side.message}>n/a</span>;
  return <span className="pane-hint">–</span>;
}

function SideBody(props: { side: Side; who: string }) {
  const { side } = props;
  if (side.status === 'error') return <p role="alert" className="error">{side.message}</p>;
  if (side.status !== 'ready') return null;
  return <Explanation result={side.result} who={props.who} />;
}

/** The explanation tree, as the single-sample pane drew it: every row causal, so each carries the outcome's polarity. */
function Explanation(props: { result: EvaluationResult; who: string }) {
  const { result } = props;
  return (
    <JustificationTree
      explanation={result.explanation}
      label={`why ${props.who} was ${result.satisfied ? 'satisfied' : 'not satisfied'}`}
    >
      {({ row, toggle, groupId }) => {
        const causes = row.assertions.join(', ');
        return (
          <div className="assertion" style={{ '--depth': row.depth } as CSSProperties}>
            {row.hasChildren ? (
              <button
                type="button"
                aria-expanded={!row.collapsed}
                aria-controls={groupId ?? undefined}
                aria-label={`causes of ${causes}`}
                className="node-chev"
                onClick={() => toggle(row.id)}
              >
                <Caret open={!row.collapsed} size={12} />
              </button>
            ) : <Tick satisfied={result.satisfied} />}
            <span>{causes}</span>
          </div>
        );
      }}
    </JustificationTree>
  );
}
