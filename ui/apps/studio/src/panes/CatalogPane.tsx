import { useMemo, useState, type ReactNode } from 'react';
import type { PropositionListEntry, RuleListEntry } from '@motiv-rules/core';
import { IconNew, IconPropositions } from '../shell/icons.js';
import { KIND_LABEL, KindGlyph } from '../shell/TabCard.js';
import { tabIdOf, type DocKind, type TabId } from '../shell/workspace.js';

/** One row of the catalog: enough to say what a document is before it is opened. */
interface Row { id: TabId; kind: DocKind; name: string; modelType: string; version: number; description: string | null }

/**
 * What an empty tab shows: the catalog — every rule and every proposition, with its model type,
 * version and description, filterable — and the ways to author. Choosing a row opens that
 * document *in this tab*, which is what an empty tab is for; a browser's new-tab page, with the
 * catalog where the bookmarks would be.
 *
 * Authoring stays where it acts on the *set*: New opens the shell's dialog, and Manage the
 * explorer, whose namespace tree and Derive / Override / Delete are worth their own surface.
 */
export function CatalogPane(props: {
  rules: readonly RuleListEntry[];
  propositions: readonly PropositionListEntry[];
  /** The ids of the tabs already holding a document, so a row can say so. */
  open: ReadonlySet<TabId>;
  onOpen: (kind: DocKind, name: string) => void;
  onNew: () => void;
  onManage: () => void;
}) {
  const [query, setQuery] = useState('');
  const needle = query.trim().toLowerCase();
  const rows = useMemo((): Row[] => [
    ...props.rules.map((rule) => ({
      id: tabIdOf('rule', rule.name), kind: 'rule' as const, name: rule.name,
      modelType: rule.modelType, version: rule.version, description: rule.description ?? null,
    })),
    ...props.propositions.map((prop) => ({
      id: tabIdOf('proposition', prop.name), kind: 'proposition' as const, name: prop.name,
      modelType: prop.modelType, version: prop.version, description: prop.description,
    })),
  ], [props.rules, props.propositions]);
  const matching = (kind: DocKind): Row[] => rows.filter((row) =>
    row.kind === kind
    && (needle === '' || row.name.toLowerCase().includes(needle) || (row.description ?? '').toLowerCase().includes(needle)));

  const column = (kind: DocKind, actions?: ReactNode): ReactNode => {
    const found = matching(kind);
    return (
      <section className="catalog-column" aria-label={`${KIND_LABEL[kind]}s`}>
        <h3 className="catalog-heading">
          <KindGlyph kind={kind} />{KIND_LABEL[kind]}s<span className="catalog-count">{found.length}</span>{actions}
        </h3>
        {found.length === 0
          ? <p className="catalog-none">{needle === '' ? `No ${KIND_LABEL[kind].toLowerCase()}s.` : 'Nothing matches.'}</p>
          : (
            <ul className="catalog-rows">
              {found.map((row) => (
                <li key={row.id}>
                  <button type="button" className="catalog-row" onClick={() => props.onOpen(row.kind, row.name)}>
                    <span className="catalog-row-head">
                      <span className="catalog-row-name">{row.name}</span>
                      <span className="model-pill">{row.modelType}</span>
                      <span className="rule-version">v{row.version}</span>
                      {props.open.has(row.id) && <span className="origin-badge">open</span>}
                    </span>
                    {row.description && <span className="catalog-row-desc">{row.description}</span>}
                  </button>
                </li>
              ))}
            </ul>
          )}
      </section>
    );
  };

  return (
    <div className="catalog">
      <div className="catalog-top">
        <h2 className="catalog-title">Catalog</h2>
        <label className="catalog-search">
          <input
            type="search"
            aria-label="Filter the catalog"
            placeholder="Filter by name or description"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
          />
        </label>
      </div>
      <div className="catalog-columns">
        {column('rule')}
        {column('proposition', (
          <span className="catalog-actions">
            <button type="button" className="ghost ghost-labelled" onClick={props.onNew}><IconNew size={12} />New proposition</button>
            <button type="button" className="ghost ghost-labelled" onClick={props.onManage}><IconPropositions size={12} />Manage</button>
          </span>
        ))}
      </div>
    </div>
  );
}
