import { useEffect, useRef, useState, useSyncExternalStore, type FocusEvent, type KeyboardEvent, type MouseEvent } from 'react';
import { useRuleEditor } from '@motiv-rules/react';
import { IconPropositions, IconRules } from './icons.js';
import { referencesOf, tabIdOf, type DocKind, type OpenDoc, type TabId, type Workspace } from './workspace.js';

export const KIND_LABEL: Record<DocKind, string> = { rule: 'Rule', proposition: 'Proposition' };

/** The kind, as a lettered tile: outlined, so the filled brand mark stays the one solid tile in the bar. */
export function KindTile(props: { kind: DocKind }) {
  return <span className={`kind-tile kind-${props.kind}`} aria-hidden="true">{props.kind === 'rule' ? 'R' : 'P'}</span>;
}

export function KindGlyph(props: { kind: DocKind; size?: number }) {
  const Icon = props.kind === 'rule' ? IconRules : IconPropositions;
  return <span className={`kind-glyph kind-${props.kind}`} aria-hidden="true"><Icon size={props.size ?? 13} /></span>;
}

/** The last dotted segment, and the namespace before it — what a chip shows, and mutes. */
export function splitName(name: string): { namespace: string; leaf: string } {
  const cut = name.lastIndexOf('.');
  return cut < 0 ? { namespace: '', leaf: name } : { namespace: name.slice(0, cut + 1), leaf: name.slice(cut + 1) };
}

const CARD_ID = 'tab-card';

export interface HoverTarget { id: TabId; rect: DOMRect }

/**
 * A delayed hover / focus card. Returns spread-able handlers for each tab, and the target the
 * strip draws the card for. The delay keeps a pass of the pointer across the strip from flashing
 * a card per chip; focus shows it after the same delay, so keyboard users get the same card.
 */
export function useHoverCard(delayMs = 350) {
  const [shown, setShown] = useState<HoverTarget | null>(null);
  const timer = useRef<number | null>(null);
  const clear = (): void => {
    if (timer.current !== null) { window.clearTimeout(timer.current); timer.current = null; }
  };
  const show = (id: TabId, el: HTMLElement): void => {
    clear();
    timer.current = window.setTimeout(() => setShown({ id, rect: el.getBoundingClientRect() }), delayMs);
  };
  const hide = (): void => { clear(); setShown(null); };
  useEffect(() => clear, []);

  const bind = (id: TabId) => ({
    onMouseEnter: (event: MouseEvent<HTMLElement>) => show(id, event.currentTarget),
    onMouseLeave: hide,
    onFocus: (event: FocusEvent<HTMLElement>) => show(id, event.currentTarget),
    onBlur: hide,
    onKeyDown: (event: KeyboardEvent<HTMLElement>) => { if (event.key === 'Escape') hide(); },
    // Dropped while the card is unmounted: an IDREF to an absent element is invalid, not harmless.
    'aria-describedby': shown?.id === id ? CARD_ID : undefined,
  });
  return { shown, bind };
}

function CardBody(props: { workspace: Workspace; doc: OpenDoc }) {
  const state = useSyncExternalStore(props.workspace.subscribe, props.workspace.getState);
  const { dirty, document, errors } = useRuleEditor(props.doc.store);
  const { doc } = props;
  const latest = state.latest[doc.name];
  const references = doc.kind === 'rule' ? referencesOf(document) : [];
  const usedBy = doc.kind === 'proposition'
    ? state.tabs.filter((id) => {
        const other = state.docs[id];
        return other?.kind === 'rule' && referencesOf(other.store.getState().document).includes(doc.name);
      })
    : [];

  return (
    <>
      <div className="card-kind">
        <KindGlyph kind={doc.kind} size={12} />
        {KIND_LABEL[doc.kind]}{latest?.origin ? ` · ${latest.origin.toLowerCase()}` : ''}
      </div>
      <div className="card-name">{doc.name}</div>
      <div className="card-meta">
        {latest?.modelType && <span className="model-pill">{latest.modelType}</span>}
        {latest && <span className="rule-version">v{latest.version}</span>}
        {latest?.isAsync && <span className="origin-badge">async</span>}
      </div>
      <div className={dirty ? 'card-state card-state-dirty' : 'card-state'}>
        {dirty ? 'Unsaved changes' : 'No unsaved changes'}
        {errors.length > 0 && ` · ${errors.length} validation ${errors.length === 1 ? 'error' : 'errors'}`}
      </div>
      {references.length > 0 && (
        <div className="card-refs">
          <span className="refs-label">Uses</span>
          {references.map((name) => {
            const open = state.docs[tabIdOf('proposition', name)] !== undefined;
            return <span key={name} className={open ? 'card-ref card-ref-open' : 'card-ref'}>{name}{open ? ' (open)' : ''}</span>;
          })}
        </div>
      )}
      {usedBy.length > 0 && (
        <div className="card-refs">
          <span className="refs-label">Used by open</span>
          {usedBy.map((id) => <span key={id} className="card-ref card-ref-open">{state.docs[id]!.name}</span>)}
        </div>
      )}
    </>
  );
}

/** The card, fixed to the viewport beneath its anchor and kept inside the window's right edge. */
export function TabHoverCard(props: { workspace: Workspace; shown: HoverTarget | null }) {
  const state = useSyncExternalStore(props.workspace.subscribe, props.workspace.getState);
  if (!props.shown) return null;
  const doc = state.docs[props.shown.id];
  if (!doc) return null;
  const { rect } = props.shown;
  const style = { top: rect.bottom + 6, left: Math.max(8, Math.min(rect.left, window.innerWidth - 336)) };
  return (
    <div id={CARD_ID} role="tooltip" className="tab-card" style={style}>
      <CardBody workspace={props.workspace} doc={doc} />
    </div>
  );
}
