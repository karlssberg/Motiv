import { createContext, useContext } from 'react';
import { isHigherOrderNode, type Catalog } from '@motiv/rules-core';
import { useRuleNode } from '@motiv/rules-react';
import { NodeToolbar } from './NodeToolbar.js';
import { QuantifierNode } from './QuantifierNode.js';
import { DecorationEditor } from './DecorationEditor.js';
import { childPaths } from './childPaths.js';
import { summarize } from './nodeSummary.js';

/** Single-open accordion state shared by every {@link RuleNodeEditor} in the tree. */
export interface AccordionState {
  isExpanded: (path: string) => boolean;
  toggle: (path: string) => void;
  catalog: Catalog;
}

export const AccordionContext = createContext<AccordionState | null>(null);

function useAccordion(): AccordionState {
  const context = useContext(AccordionContext);
  if (!context) throw new Error('RuleNodeEditor must be used within an AccordionContext provider.');
  return context;
}

/** Recursively renders a rule node and its children as a single-open accordion. */
export function RuleNodeEditor(props: { path: string; modelType: string }) {
  const { path, modelType } = props;
  const { node, errors } = useRuleNode(path);
  const { isExpanded, toggle, catalog } = useAccordion();

  if (!node) return null;

  const expanded = isExpanded(path);
  const kids = childPaths(node, path);
  const hasChildren = kids.length > 0;
  const summary = summarize(node);

  // A quantifier's single child is scoped to the collection's element model type, not the parent's.
  const childModelType = isHigherOrderNode(node)
    ? (catalog.collections.find((c) => c.path === node.path)?.elementModelType ?? modelType)
    : modelType;

  return (
    <div className="node">
      <div className="node-row">
        {hasChildren && (
          <button
            type="button"
            className="node-chev"
            aria-label={`${expanded ? 'collapse' : 'expand'} ${path}`}
            onClick={() => toggle(path)}
          >
            {expanded ? '▾' : '▸'}
          </button>
        )}
        <span className={`node-badge node-badge-${summary.kind}`}>{summary.badge}</span>
        {summary.description && <span className="node-desc">{summary.description}</span>}
        {node.name && <span className="node-name">as &quot;{node.name}&quot;</span>}
      </div>
      {isHigherOrderNode(node) ? (
        <QuantifierNode path={path} node={node} catalog={catalog} modelType={modelType} />
      ) : (
        <NodeToolbar path={path} node={node} modelType={modelType} catalog={catalog} />
      )}
      {errors.length > 0 && (
        <span role="alert" className="error">{errors.map((e) => e.message).join('; ')}</span>
      )}
      {expanded && (
        <div className="node-detail">
          <DecorationEditor path={path} node={node} />
          {hasChildren && (
            <div className="node-kids">
              {kids.map((childPath) => (
                <RuleNodeEditor key={childPath} path={childPath} modelType={childModelType} />
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
