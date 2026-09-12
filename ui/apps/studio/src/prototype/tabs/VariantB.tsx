/*
 * PROTOTYPE — throwaway. Do not ship.
 *
 * Variant B — open documents rail. No horizontal strip at all: a vertical list beside the editor,
 * grouped under Rules / Propositions headings, so kind is a heading rather than a glyph and width
 * pressure never touches the titles. The rail collapses to glyphs (a toggle, and automatically
 * under 700px), at which point the hover card is the only place the name appears.
 */
import { useState, useSyncExternalStore } from 'react';
import { useRuleEditor } from '@motiv-rules/react';
import { IconChevronRight, IconClose, IconNew } from '../../shell/icons.js';
import { KindGlyph, TabHoverCard, splitName, useHoverCard } from './TabCard.js';
import type { TabShellProps } from './TabsPrototype.js';
import { tabInteractions } from './tabEvents.js';
import type { DocKind, OpenDoc } from './workspace.js';

function RailRow(props: TabShellProps & { doc: OpenDoc; hover: ReturnType<typeof useHoverCard> }) {
  const { doc } = props;
  const { dirty } = useRuleEditor(doc.store);
  const active = props.state.active === doc.id;
  const { namespace, leaf } = splitName(doc.name);
  const events = tabInteractions(doc.id, props.state.tabs, 'vertical', props);
  return (
    <li
      role="tab"
      data-tab={doc.id}
      tabIndex={active ? 0 : -1}
      aria-selected={active}
      className={`rail-row${active ? ' active' : ''}${dirty ? ' dirty' : ''}`}
      {...events}
      {...props.hover.bind(doc.id)}
    >
      <KindGlyph kind={doc.kind} />
      <span className="rail-name"><span className="rail-ns">{namespace}</span><span className="rail-leaf">{leaf}</span></span>
      <span className="rail-dot" aria-hidden="true" />
      <button
        type="button"
        className="rail-close"
        aria-label={dirty ? `Close ${doc.name} (unsaved changes)` : `Close ${doc.name}`}
        tabIndex={-1}
        onClick={(event) => { event.stopPropagation(); props.onRequestClose(doc.id); }}
      >
        <IconClose size={12} />
      </button>
    </li>
  );
}

function Group(props: TabShellProps & { kind: DocKind; hover: ReturnType<typeof useHoverCard> }) {
  const docs = props.state.tabs.map((id) => props.state.docs[id]).filter((doc): doc is OpenDoc => doc?.kind === props.kind);
  return (
    <section className={`rail-group kind-${props.kind}`}>
      <h3 className="rail-heading"><KindGlyph kind={props.kind} size={12} /><span>{props.kind === 'rule' ? 'Rules' : 'Propositions'}</span><span className="rail-count">{docs.length}</span></h3>
      {docs.length === 0
        ? <p className="rail-empty">None open</p>
        : <ul role="tablist" aria-label={props.kind === 'rule' ? 'Open rules' : 'Open propositions'} aria-orientation="vertical">
            {docs.map((doc) => <RailRow key={doc.id} {...props} doc={doc} />)}
          </ul>}
    </section>
  );
}

export function VariantB(props: TabShellProps) {
  const hover = useHoverCard();
  const [collapsed, setCollapsed] = useState(false);
  useSyncExternalStore(props.workspace.subscribe, props.workspace.getState);

  return (
    <>
      <header className="appbar">
        <div className="appbar-brand">
          <span className="appbar-mark" aria-hidden="true">M</span>
          <span className="appbar-wordmark">Motiv</span>
        </div>
        <div className="appbar-fill" />
        <div className="appbar-controls">{props.actions}</div>
      </header>

      <div className="proto-b-body">
        <nav className={collapsed ? 'open-rail collapsed' : 'open-rail'} aria-label="Open documents">
          <div className="rail-head">
            <button type="button" className="ghost" aria-label={collapsed ? 'Expand open documents' : 'Collapse open documents'} aria-expanded={!collapsed} onClick={() => setCollapsed((value) => !value)}>
              <span className={collapsed ? 'rail-caret' : 'rail-caret open'}><IconChevronRight size={14} /></span>
            </button>
            <span className="rail-title">Open</span>
            <button type="button" className="ghost" aria-label="Open another" title="Open another (⌘K)" onClick={props.onOpenPalette}><IconNew size={14} /></button>
          </div>
          <Group {...props} kind="rule" hover={hover} />
          <Group {...props} kind="proposition" hover={hover} />
        </nav>
        <div className="proto-b-main">{props.body}</div>
      </div>
      <TabHoverCard workspace={props.workspace} shown={hover.shown} side="right" />
    </>
  );
}
