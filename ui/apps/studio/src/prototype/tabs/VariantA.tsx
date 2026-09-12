/*
 * PROTOTYPE — throwaway. Do not ship.
 *
 * Variant A — browser strip. A second row under the app bar holds one tab per open document.
 * Tabs are elastic like Chrome's: they share the row, shrink together as more open, and the
 * title truncates before the glyph does, so kind survives longer than name. Close (×) shows on
 * hover and on the active tab; a dirty tab shows ● in the same slot until hovered.
 */
import { useSyncExternalStore } from 'react';
import { useRuleEditor } from '@motiv-rules/react';
import { IconClose, IconNew } from '../../shell/icons.js';
import { KindGlyph, TabHoverCard, useHoverCard } from './TabCard.js';
import type { TabShellProps } from './TabsPrototype.js';
import { tabInteractions } from './tabEvents.js';
import type { OpenDoc } from './workspace.js';

function StripTab(props: TabShellProps & { doc: OpenDoc; hover: ReturnType<typeof useHoverCard> }) {
  const { doc } = props;
  const { dirty } = useRuleEditor(doc.store);
  const active = props.state.active === doc.id;
  const events = tabInteractions(doc.id, props.state.tabs, 'horizontal', props);
  return (
    <div
      role="tab"
      data-tab={doc.id}
      tabIndex={active ? 0 : -1}
      aria-selected={active}
      className={`btab kind-${doc.kind}${active ? ' active' : ''}${dirty ? ' dirty' : ''}`}
      {...events}
      {...props.hover.bind(doc.id)}
    >
      <KindGlyph kind={doc.kind} />
      <span className="btab-title">{doc.name}</span>
      <button
        type="button"
        className="btab-close"
        aria-label={dirty ? `Close ${doc.name} (unsaved changes)` : `Close ${doc.name}`}
        tabIndex={-1}
        onClick={(event) => { event.stopPropagation(); props.onRequestClose(doc.id); }}
      >
        <span className="btab-dot" aria-hidden="true" />
        <IconClose size={12} />
      </button>
    </div>
  );
}

export function VariantA(props: TabShellProps) {
  const hover = useHoverCard();
  // Re-render on every store change so dirty markers stay live even when the body does not change.
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

      <div className="tabstrip" role="tablist" aria-label="Open documents">
        {props.state.tabs.map((id) => {
          const doc = props.state.docs[id];
          return doc ? <StripTab key={id} {...props} doc={doc} hover={hover} /> : null;
        })}
        <button type="button" className="ghost tabstrip-new" aria-label="Open another" title="Open another (⌘K)" onClick={props.onOpenPalette}>
          <IconNew size={14} />
        </button>
      </div>

      {props.body}
      <TabHoverCard workspace={props.workspace} shown={hover.shown} side="below" />
    </>
  );
}
