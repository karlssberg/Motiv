import { createContext, useContext, type CSSProperties } from 'react';
import type { Catalog } from '@motiv/rules-core';
import { useRuleNode } from '@motiv/rules-react';
import { NodeToolbar } from './NodeToolbar.js';
import { DecorationEditor } from './DecorationEditor.js';
import { childPaths } from './childPaths.js';

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
export function RuleNodeEditor(props: { path: string; depth: number; modelType: string }) {
  const { path, depth, modelType } = props;
  const { node, errors } = useRuleNode(path);
  const { isExpanded, toggle, catalog } = useAccordion();

  if (!node) return null;

  const expanded = isExpanded(path);
  const kids = childPaths(node, path);

  return (
    <div className="node" style={{ '--depth': depth } as CSSProperties}>
      <NodeToolbar
        path={path}
        node={node}
        modelType={modelType}
        catalog={catalog}
        expanded={expanded}
        onToggleExpand={() => toggle(path)}
      />
      {errors.length > 0 && (
        <span role="alert" className="error">{errors.map((e) => e.message).join('; ')}</span>
      )}
      {expanded && (
        <>
          <DecorationEditor path={path} node={node} />
          {kids.length > 0 && (
            <div className="node-kids">
              {kids.map((childPath) => (
                <RuleNodeEditor key={childPath} path={childPath} depth={depth + 1} modelType={modelType} />
              ))}
            </div>
          )}
        </>
      )}
    </div>
  );
}
