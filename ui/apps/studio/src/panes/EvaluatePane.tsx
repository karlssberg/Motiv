import { useState, type CSSProperties } from 'react';
import { validateAgainstSchema, type RulesApiClient, type SchemaViolation } from '@motiv-rules/core';
import { JustificationTree, useCatalog, useEvaluation, useRuleEditor, useRuleEditorStore } from '@motiv-rules/react';
import { MODEL_TYPE } from '../App.js';
import { SchemaViolations } from './SchemaViolations.js';

const SAMPLE_MODEL = '{\n  "age": 30,\n  "isActive": true,\n  "orderCount": 2\n}';

/** Evaluates the current document against a sample model and renders the explanation tree. */
export function EvaluatePane(props: { client: RulesApiClient }) {
  const store = useRuleEditorStore();
  const state = useRuleEditor(store);
  const evaluation = useEvaluation(props.client);
  const catalogState = useCatalog(props.client);
  const [modelText, setModelText] = useState(SAMPLE_MODEL);
  const [parseError, setParseError] = useState<string | null>(null);
  const [violations, setViolations] = useState<SchemaViolation[]>([]);

  // Absent while loading or on older backends without modelTypes — then enforcement simply doesn't run.
  const modelSchema =
    catalogState.status === 'ready' ? catalogState.data.modelTypes?.[MODEL_TYPE] : undefined;

  const run = (): void => {
    let model: unknown;
    try {
      model = JSON.parse(modelText);
    } catch {
      setParseError('Sample model is not valid JSON.');
      setViolations([]);
      return;
    }
    setParseError(null);
    const found = modelSchema ? validateAgainstSchema(model, modelSchema) : [];
    setViolations(found);
    if (found.length > 0) return;
    void evaluation.evaluate({ modelType: MODEL_TYPE, document: state.document, model });
  };

  return (
    <section aria-label="Evaluate" className="pane">
      <h2>Evaluate</h2>
      <div className="pane-body">
        <label className="field">
          <span>Sample model</span>
          <textarea
            aria-label="sample model"
            className="control"
            value={modelText}
            onChange={(e) => setModelText(e.target.value)}
            rows={5}
          />
        </label>
        <button type="button" className="btn" onClick={run}>Evaluate</button>
        {parseError && <p role="alert">{parseError}</p>}
        <SchemaViolations violations={violations} />
        {evaluation.status === 'error' && <p role="alert">Evaluation failed.</p>}
        {evaluation.status === 'ready' && (
          <>
            <p aria-label="outcome" className="outcome">{evaluation.result.satisfied ? 'Satisfied' : 'Not satisfied'}</p>
            {/*
              The explanation is the answer to "why?", so its disclosures have to say what they
              hide: the caret's accessible name was the glyph it is drawn as, which tells a reader
              that a control exists and nothing about what it does. `aria-controls` is dropped once
              the group is collapsed and unmounted, the same rule the builder's caret follows.
            */}
            <JustificationTree
              explanation={evaluation.result.explanation}
              label={`why this rule was ${evaluation.result.satisfied ? 'satisfied' : 'not satisfied'}`}
            >
              {({ row, toggle, groupId }) => {
                const causes = row.assertions.join(', ');
                return (
                  <div className="assertion" style={{ '--depth': row.depth } as CSSProperties}>
                    {row.hasChildren && (
                      <button
                        type="button"
                        aria-expanded={!row.collapsed}
                        aria-controls={groupId ?? undefined}
                        aria-label={`causes of ${causes}`}
                        onClick={() => toggle(row.id)}
                      >
                        {row.collapsed ? '▸' : '▾'}
                      </button>
                    )}
                    <span>{causes}</span>
                  </div>
                );
              }}
            </JustificationTree>
          </>
        )}
      </div>
    </section>
  );
}
