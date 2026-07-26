import { createContext, useContext } from 'react';
import { isHigherOrderNode, type Catalog } from '@motiv/rules-core';
import { useRuleNode } from '@motiv/rules-react';
import { NodeToolbar } from './NodeToolbar.js';
import { QuantifierNode } from './QuantifierNode.js';
import { DecorationEditor } from './DecorationEditor.js';
import { childPaths } from './childPaths.js';
import { summarize } from './nodeSummary.js';
import { NodeDsl } from './NodeDsl.js';
import { isCollapsed, isOpen, isPinned, type AccordionModel } from './accordion.js';

/** The accordion state and its transitions, shared by every {@link RuleNodeEditor} in the tree. */
export interface AccordionState {
  model: AccordionModel;
  toggleCollapsed: (path: string) => void;
  toggleOpen: (path: string) => void;
  togglePin: (path: string) => void;
  closeAll: () => void;
  catalog: Catalog;
}

export const AccordionContext = createContext<AccordionState | null>(null);

export function useAccordion(): AccordionState {
  const context = useContext(AccordionContext);
  if (!context) throw new Error('RuleNodeEditor must be used within an AccordionContext provider.');
  return context;
}

/** The id tying a node's detail toggle to the panel it opens. */
const panelId = (path: string): string => `detail-${path}`;

/**
 * Recursively renders a rule node.
 *
 * Two view concerns, deliberately independent. The **caret** is structural: it folds a subtree
 * into a single line of DSL and back, and starts expanded. The **detail** panel holds the node's
 * decoration fields and edit controls, starts closed, and is displaced when another node is
 * opened unless it has been pinned. A node can be collapsed with its panel open, or the reverse.
 *
 * The row body carries no interactive role of its own. It has to host a text editor once the
 * subtree is collapsed, and interactive content nested inside a button is invalid HTML that
 * swallows events — so the detail toggle is a sibling control rather than the row itself.
 */
export function RuleNodeEditor(props: { path: string; modelType: string }) {
  const { path, modelType } = props;
  const { node, errors } = useRuleNode(path);
  const { model, toggleCollapsed, toggleOpen, togglePin, catalog } = useAccordion();

  if (!node) return null;

  const kids = childPaths(node, path);
  const hasChildren = kids.length > 0;
  const collapsed = isCollapsed(model, path);
  const open = isOpen(model, path);
  const pinned = isPinned(model, path);
  const summary = summarize(node);
  // A leaf's tree form and its text form are the same string, so it has nothing to toggle
  // between and is always shown as DSL.
  const inDslView = !hasChildren || collapsed;

  // A quantifier's single child is scoped to the collection's element model type, not the parent's.
  const childModelType = isHigherOrderNode(node)
    ? (catalog.collections.find((c) => c.path === node.path)?.elementModelType ?? modelType)
    : modelType;

  return (
    <div className="node">
      <div className="node-row">
        {hasChildren ? (
          <button
            type="button"
            className="node-chev"
            aria-expanded={!collapsed}
            aria-label={`${collapsed ? 'expand' : 'collapse'} ${path}`}
            onClick={() => toggleCollapsed(path)}
          >
            {collapsed ? '▸' : '▾'}
          </button>
        ) : (
          <span className="node-bullet" aria-hidden="true">•</span>
        )}
        <span className="node-body">
          {inDslView ? (
            <NodeDsl path={path} node={node} modelType={modelType} catalog={catalog} />
          ) : (
            <>
              <span className={`node-badge node-badge-${summary.kind}`}>{summary.badge}</span>
              {summary.description && <span className="node-desc">{summary.description}</span>}
              {node.name && <span className="node-name">as &quot;{node.name}&quot;</span>}
            </>
          )}
        </span>
        <button
          type="button"
          className={open ? 'node-detail-toggle open' : 'node-detail-toggle'}
          aria-expanded={open}
          aria-controls={panelId(path)}
          aria-label={`details for ${path}`}
          onClick={() => toggleOpen(path)}
        >
          ⋯
        </button>
        <button
          type="button"
          className={pinned ? 'node-pin pinned' : 'node-pin'}
          aria-pressed={pinned}
          aria-label={`${pinned ? 'unpin' : 'pin'} ${path}`}
          onClick={() => togglePin(path)}
        >
          📌
        </button>
      </div>
      {errors.length > 0 && (
        <span role="alert" className="error">{errors.map((e) => e.message).join('; ')}</span>
      )}
      {open && (
        <div className="node-detail" id={panelId(path)}>
          {isHigherOrderNode(node) ? (
            <QuantifierNode path={path} node={node} catalog={catalog} modelType={modelType} />
          ) : (
            <NodeToolbar path={path} node={node} modelType={modelType} catalog={catalog} />
          )}
          <DecorationEditor path={path} node={node} />
        </div>
      )}
      {hasChildren && !collapsed && (
        <div className="node-kids">
          {kids.map((childPath) => (
            <RuleNodeEditor key={childPath} path={childPath} modelType={childModelType} />
          ))}
        </div>
      )}
    </div>
  );
}
