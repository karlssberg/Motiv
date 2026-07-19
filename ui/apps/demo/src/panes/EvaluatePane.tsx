import { useState, type CSSProperties } from 'react';
import type { RulesApiClient } from '@motiv/rules-core';
import { JustificationTree, useEvaluation, useRuleEditor, useRuleEditorStore } from '@motiv/rules-react';
import { MODEL_TYPE } from '../App.js';

const SAMPLE_MODEL = '{\n  "age": 30,\n  "isActive": true,\n  "orderCount": 2\n}';

/** Evaluates the current document against a sample model and renders the explanation tree. */
export function EvaluatePane(props: { client: RulesApiClient }) {
  const store = useRuleEditorStore();
  const state = useRuleEditor(store);
  const evaluation = useEvaluation(props.client);
  const [modelText, setModelText] = useState(SAMPLE_MODEL);
  const [parseError, setParseError] = useState<string | null>(null);

  const run = (): void => {
    let model: unknown;
    try {
      model = JSON.parse(modelText);
    } catch {
      setParseError('Sample model is not valid JSON.');
      return;
    }
    setParseError(null);
    void evaluation.evaluate({ modelType: MODEL_TYPE, document: state.document, model });
  };

  return (
    <section aria-label="Evaluate" className="pane">
      <h2>Evaluate</h2>
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
      {evaluation.status === 'error' && <p role="alert">Evaluation failed.</p>}
      {evaluation.status === 'ready' && (
        <>
          <p aria-label="outcome" className="outcome">{evaluation.result.satisfied ? 'Satisfied' : 'Not satisfied'}</p>
          <JustificationTree explanation={evaluation.result.explanation}>
            {({ row, toggle }) => (
              <div className="assertion" style={{ '--depth': row.depth } as CSSProperties}>
                {row.hasChildren && (
                  <button type="button" onClick={() => toggle(row.id)}>{row.collapsed ? '▸' : '▾'}</button>
                )}
                <span>{row.assertions.join(', ')}</span>
              </div>
            )}
          </JustificationTree>
        </>
      )}
    </section>
  );
}
