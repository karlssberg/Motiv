import {
  isBinaryNode, isSpecNode, nodeKind,
  type CatalogEntry, type RulesApiClient,
} from '@motiv/rules-core';
import { RuleTree, useCatalog, useRuleEditorStore } from '@motiv/rules-react';

function firstSpecName(catalog: CatalogEntry[]): string {
  return catalog[0]?.name ?? 'spec';
}

/** Minimal catalog-driven rule builder: pick specs, wrap in AND/OR, add/remove operands. */
export function BuilderPane(props: { client: RulesApiClient }) {
  const store = useRuleEditorStore();
  const catalogState = useCatalog(props.client);
  const catalog = catalogState.status === 'ready' ? catalogState.data.specs : [];

  return (
    <section aria-label="Builder">
      <h2>Builder</h2>
      {catalogState.status === 'loading' && <p>Loading catalog…</p>}
      <RuleTree>
        {({ path, node, level, errors }) => (
          <div style={{ paddingLeft: (level - 1) * 16 }}>
            {isSpecNode(node) ? (
              <>
                <label>
                  <span hidden>spec at {path}</span>
                  <select
                    aria-label={`spec at ${path}`}
                    value={node.spec}
                    onChange={(e) => store.replaceNode(path, { spec: e.target.value })}
                  >
                    {catalog.map((entry) => (
                      <option key={entry.name} value={entry.name}>{entry.name}</option>
                    ))}
                  </select>
                </label>
                <button
                  type="button"
                  aria-label={`AND at ${path}`}
                  onClick={() => store.wrapInOperator(path, 'and', { spec: firstSpecName(catalog) })}
                >AND</button>
                <button
                  type="button"
                  aria-label={`OR at ${path}`}
                  onClick={() => store.wrapInOperator(path, 'or', { spec: firstSpecName(catalog) })}
                >OR</button>
              </>
            ) : (
              <span>{nodeKind(node)}</span>
            )}
            {isBinaryNode(node) && (
              <button
                type="button"
                aria-label={`add operand at ${path}`}
                onClick={() => store.addOperand(path, { spec: firstSpecName(catalog) })}
              >+ operand</button>
            )}
            {path.endsWith(']') && (
              <button type="button" aria-label={`remove ${path}`} onClick={() => store.removeOperand(path)}>×</button>
            )}
            {errors.length > 0 && <span role="alert"> {errors.map((e) => e.message).join('; ')}</span>}
          </div>
        )}
      </RuleTree>
    </section>
  );
}
