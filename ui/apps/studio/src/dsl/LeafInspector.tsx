import { useEffect, useMemo, useRef, useState } from 'react';
import {
  ancestors, analyseLeaf, getNode, higherOrderKey, isExpressionNode, isHigherOrderNode, isNodePath,
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

/**
 * A document that evaluates just this leaf: bare at the root, or — when the leaf sits inside a
 * quantifier body — wrapped back under the *nearest* enclosing quantifier, so the reading sees the
 * same collection-element scope the leaf is authored against.
 *
 * Only the nearest quantifier wraps the leaf. A leaf nested under two (`all in orders { all in
 * items { ... } }`) has no single-quantifier document that reproduces the outer one's scope too —
 * evaluating it in isolation can only ever mean "as read within the innermost body", against the
 * innermost element on its own.
 *
 * `ancestors` (from `@motiv-rules/core`) walks the same segment cuts `scopeAt` uses internally. A
 * single cut can land on an operand *array* rather than a node (e.g. `$.rule.asAllSatisfied.and[0]`
 * cuts first to `$.rule.asAllSatisfied.and`), which is why each ancestor is re-checked with
 * `getNode`/`isHigherOrderNode` rather than trusting the first cut to be a node at all.
 */
function leafDocument(document: RuleDocument, path: string, expression: string): RuleDocument {
  let quantifier: RuleNode | undefined;
  for (const ancestor of ancestors(path)) {
    if (ancestor === path) continue; // the leaf itself is not its own enclosing quantifier
    const node = getNode(document, ancestor);
    if (node && isHigherOrderNode(node)) { quantifier = node; break; }
  }
  const rule: RuleNode = quantifier && isHigherOrderNode(quantifier)
    ? ({ ...quantifier, [higherOrderKey(quantifier)]: { expression } } as RuleNode)
    : { expression };
  return { ...(document.parameters ? { parameters: document.parameters } : {}), rule };
}

/**
 * The strip under the DSL pane that names the scope of the expression leaf under the caret and
 * reads it, alone, against whichever scenario is open in the Evaluate table. A leaf's scope
 * differs from the document's model type whenever it sits inside a quantifier body — `scope`
 * carries that (`each of orders`, say) so the reading is legible without re-deriving it here.
 *
 * The reading is debounced 300ms after the caret, the leaf's text, the document or the scenario
 * changes, and a response for a since-superseded request is dropped: `latest` numbers requests as
 * they are scheduled, and only a response for the latest number is applied.
 */
export function LeafInspector(props: {
  text: string; caret: number; parseResult: ParseResult; document: RuleDocument;
  leafScope: (path: string) => LeafScope | null; client: RulesApiClient; modelType: string; ruleName?: string | undefined;
}) {
  const { text, caret, parseResult, document, leafScope, client, modelType } = props;
  const leaf = useMemo(() => leafAtCaret(text, caret, parseResult), [text, caret, parseResult]);
  const scope = leaf ? leafScope(leaf.path) : null;
  const scenario = useSelectedScenario(props.ruleName ?? '');
  const [reading, setReading] = useState<{ result?: EvaluationResult; error?: string } | null>(null);

  const analysis = useMemo(() => (leaf && scope ? analyseLeaf(leaf.text, scope) : null), [leaf, scope]);
  const resultType = analysis?.ast ? analysis.types.get(analysis.ast) : undefined;

  /** The number of the most recently *issued* request. It is taken synchronously, before the
   *  debounce, so a response is accepted only if nothing about the leaf, its document or the
   *  scenario changed after its request was scheduled — a change during the debounce window, or
   *  one outside the leaf text (a parameter default, a quantifier setting), both retire it. */
  const latest = useRef(0);

  useEffect(() => {
    const request = ++latest.current;
    if (!leaf || !scenario || !analysis?.valid) { setReading(null); return; }
    const timer = setTimeout(() => {
      let model: unknown;
      try { model = JSON.parse(scenario.model); } catch { setReading({ error: 'the selected scenario is not valid JSON' }); return; }
      client.evaluate({ modelType, document: leafDocument(document, leaf.path, leaf.text), model })
        .then((result) => { if (latest.current === request) setReading({ result }); })
        .catch((error: unknown) => { if (latest.current === request) setReading({ error: error instanceof Error ? error.message : String(error) }); });
    }, 300);
    return () => clearTimeout(timer);
  }, [leaf?.path, leaf?.text, scenario?.model, analysis?.valid, client, modelType, document]);

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
