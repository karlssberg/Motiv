/*
 * PROTOTYPE — throwaway. Do not ship.
 *
 * What the variants share about a tab's *identity*: the kind glyph, and the hover card that
 * shows the full name plus the metadata a truncated tab cannot carry.
 */
import { useEffect, useRef, useState, useSyncExternalStore, type FocusEvent, type KeyboardEvent, type MouseEvent } from 'react';
import { useRuleEditor } from '@motiv-rules/react';
import { IconPropositions, IconRules } from '../../shell/icons.js';
import { referencesOf, tabIdOf, type DocKind, type OpenDoc, type TabId, type Workspace } from './workspace.js';

export const KIND_LABEL: Record<DocKind, string> = { rule: 'Rule', proposition: 'Proposition' };

export function KindGlyph(props: { kind: DocKind; size?: number }) {
  const Icon = props.kind === 'rule' ? IconRules : IconPropositions;
  return <span className={`kind-glyph kind-${props.kind}`}><Icon size={props.size ?? 13} /></span>;
}

/** The last dotted segment, with the namespace it sits in — what a narrow tab shows and mutes. */
export function splitName(name: string): { namespace: string; leaf: string } {
  const cut = name.lastIndexOf('.');
  return cut < 0 ? { namespace: '', leaf: name } : { namespace: name.slice(0, cut + 1), leaf: name.slice(cut + 1) };
}

const CARD_ID = 'proto-tab-card';

export interface HoverTarget { id: TabId; rect: DOMRect }

/**
 * A delayed hover/focus card. Returns spread-able handlers for each tab; the card itself is drawn
 * once by the variant, wherever it wants it.
 */
export function useHoverCard(delayMs = 350) {
  const [shown, setShown] = useState<HoverTarget | null>(null);
  const timer = useRef<number | null>(null);
  const clear = (): void => { if (timer.current !== null) { window.clearTimeout(timer.current); timer.current = null; } };
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
    'aria-describedby': shown?.id === id ? CARD_ID : undefined,
  });
  return { shown, bind, hide };
}

function CardBody(props: { workspace: Workspace; doc: OpenDoc }) {
  const state = useSyncExternalStore(props.workspace.subscribe, props.workspace.getState);
  const { dirty, document, errors } = useRuleEditor(props.doc.store);
  const { doc } = props;
  const references = doc.kind === 'rule' ? referencesOf(document) : [];
  const usedBy = doc.kind === 'proposition'
    ? state.tabs.filter((id) => {
        const other = state.docs[id];
        return other?.kind === 'rule' && referencesOf(other.store.getState().document).includes(doc.name);
      })
    : [];

  return (
    <>
      <div className="card-kind"><KindGlyph kind={doc.kind} size={12} />{KIND_LABEL[doc.kind]}{doc.origin ? ` · ${doc.origin.toLowerCase()}` : ''}</div>
      <div className="card-name">{doc.name}</div>
      <div className="card-meta">
        <span className="model-pill">{doc.modelType}</span>
        <span className="rule-version">v{doc.version}</span>
        {doc.isAsync && <span className="origin-badge">async</span>}
        {doc.isCodeDefault && <span className="origin-badge">code default</span>}
      </div>
      <div className={dirty ? 'card-state card-state-dirty' : 'card-state'}>
        {dirty ? 'Unsaved changes' : 'No unsaved changes'}
        {errors.length > 0 && ` · ${errors.length} validation ${errors.length === 1 ? 'error' : 'errors'}`}
      </div>
      {references.length > 0 && (
        <div className="card-refs">
          <span className="refs-label">Uses</span>
          {references.map((name) => {
            const open = state.docs[tabIdOf('proposition', name)];
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
      <div className="card-hint">Middle-click closes · ⌘W closes active</div>
    </>
  );
}

/** The card, fixed to the viewport beside its anchor. `side` is where the variant keeps its tabs. */
export function TabHoverCard(props: { workspace: Workspace; shown: HoverTarget | null; side: 'below' | 'right' }) {
  const state = useSyncExternalStore(props.workspace.subscribe, props.workspace.getState);
  if (!props.shown) return null;
  const doc = state.docs[props.shown.id];
  if (!doc) return null;
  const { rect } = props.shown;
  const style = props.side === 'below'
    ? { top: rect.bottom + 6, left: Math.min(rect.left, window.innerWidth - 336) }
    : { top: Math.min(rect.top, window.innerHeight - 220), left: rect.right + 8 };
  return (
    <div id={CARD_ID} role="tooltip" className="tab-card" style={style}>
      <CardBody workspace={props.workspace} doc={doc} />
    </div>
  );
}
