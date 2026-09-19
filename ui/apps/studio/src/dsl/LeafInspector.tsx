import { useEffect, useMemo, useRef, useState } from 'react';
import {
  analyseLeaf, getNode, isExpressionNode, isHigherOrderNode, isNodePath,
  type EvaluationResult, type LeafScope, type ParseResult, type RuleDocument, type RuleNode, type RulesApiClient,
} from '@motiv-rules/core';
import { Verdict } from '../panes/Verdict.js';
import { useSelectedScenario } from '../panes/scenarioSelection.js';

/** The expression node whose text covers `caret`, with its leaf-relative text range. */
function leafAtCaret(text: string, caret: number, result: ParseResult): { path: string; text: string } | null {
  if (!result.document) return null;
  for (const span of result.spans) {
    if (!isNodePath(span.path)) continue;
    const node = getNode(result.document, span.path);
    if (!node || !isExpressionNode(node)) continue;
    const from = span.from + 1;
    const to = text[span.to - 1] === '`' ? span.to - 1 : span.to;
    if (caret >= from && caret <= to) return { path: span.path, text: node.expression };
  }
  return null;
}

/** A document that evaluates just this leaf: bare at the root, or under its enclosing quantifier. */
function leafDocument(document: RuleDocument, path: string, expression: string): RuleDocument {
  const parentPath = path.slice(0, Math.max(path.lastIndexOf('.'), path.lastIndexOf('[')));
  const parent = parentPath.length > 1 ? getNode(document, parentPath) : undefined;
  const rule: RuleNode = parent && isHigherOrderNode(parent)
    ? ({ ...parent, [Object.keys(parent).find((k) => k.startsWith('as'))!]: { expression } } as RuleNode)
    : { expression };
  return { ...(document.parameters ? { parameters: document.parameters } : {}), rule };
}

/**
 * The strip under the DSL pane that names the scope of the expression leaf under the caret and
 * reads it, alone, against whichever scenario is open in the Evaluate table. A leaf's scope
 * differs from the document's model type whenever it sits inside a quantifier body — `scope`
 * carries that (`each of orders`, say) so the reading is legible without re-deriving it here.
 *
 * The reading is debounced 300ms after the caret or the leaf's text changes, and a response for a
 * since-superseded request is dropped: `latest` tracks the most recently *issued* key, and only a
 * response whose key still matches it is applied.
 */
export function LeafInspector(props: {
  text: string; caret: number; parseResult: ParseResult; document: RuleDocument;
  leafScope: (path: string) => LeafScope | null; client: RulesApiClient; modelType: string; ruleName?: string | undefined;
}) {
  const { text, caret, parseResult, document, leafScope, client, modelType } = props;
  const leaf = useMemo(() => leafAtCaret(text, caret, parseResult), [text, caret, parseResult]);
  const scope = leaf ? leafScope(leaf.path) : null;
  const scenario = useSelectedScenario(props.ruleName ?? '');
  const [reading, setReading] = useState<{ key: string; result?: EvaluationResult; error?: string } | null>(null);

  const analysis = useMemo(() => (leaf && scope ? analyseLeaf(leaf.text, scope) : null), [leaf, scope]);
  const resultType = analysis?.ast ? analysis.types.get(analysis.ast) : undefined;

  /** The key of the most recently *issued* request, so a stale response — one whose request was
   *  superseded by a later caret/text/scenario change before it returned — is dropped rather than
   *  overwriting a newer (possibly still-pending) reading. */
  const latest = useRef<string | null>(null);

  useEffect(() => {
    if (!leaf || !scenario || !analysis?.valid) { setReading(null); return; }
    const key = `${leaf.path}|${leaf.text}|${scenario.model}`;
    const timer = setTimeout(() => {
      latest.current = key;
      let model: unknown;
      try { model = JSON.parse(scenario.model); } catch { setReading({ key, error: 'the selected scenario is not valid JSON' }); return; }
      client.evaluate({ modelType, document: leafDocument(document, leaf.path, leaf.text), model })
        .then((result) => { if (latest.current === key) setReading({ key, result }); })
        .catch((error: unknown) => { if (latest.current === key) setReading({ key, error: error instanceof Error ? error.message : String(error) }); });
    }, 300);
    return () => clearTimeout(timer);
  }, [leaf?.path, leaf?.text, scenario?.model, analysis?.valid, client, modelType]);

  return (
    <section className="leaf-inspector" role="region" aria-label="expression inspector">
      {!leaf ? (
        <p className="pane-hint">Put the caret inside a backtick to see what that expression is scoped to and reads as.</p>
      ) : (
        <>
          <div className="leaf-inspector-head">
            <span className="caption">expression under caret</span>
            {scope && <span className="leaf-scope">model: <b>{scope.modelName}</b></span>}
            {resultType && <span className="leaf-scope">type: <b>{resultType}</b></span>}
          </div>
          <div aria-live="polite">
            {!scenario && <p className="pane-hint">Open a scenario in the table to read this expression against it.</p>}
            {scenario && analysis && !analysis.valid && <p className="pane-hint">Fix the expression to read it.</p>}
            {reading?.error && <p role="alert" className="error">{reading.error}</p>}
            {scenario && reading?.result && (
              <div className="leaf-reading">
                <Verdict satisfied={reading.result.satisfied} text={reading.result.satisfied ? 'Satisfied' : 'Not satisfied'} label={`against ${scenario.name}`} />
                <ul className="leaf-assertions">{reading.result.assertions.map((a) => <li key={a}>{a}</li>)}</ul>
              </div>
            )}
          </div>
        </>
      )}
    </section>
  );
}
