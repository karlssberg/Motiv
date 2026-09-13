import { useEffect, useLayoutEffect, useRef, useState, useSyncExternalStore, type MouseEvent, type ReactNode } from 'react';
import { useRuleEditor } from '@motiv-rules/react';
import { IconChevronDown, IconClose, IconNew } from './icons.js';
import { KIND_LABEL, KindTile, TabHoverCard, splitName, useHoverCard } from './TabCard.js';
import { tabInteractions } from './tabEvents.js';
import type { OpenDoc, TabId, Workspace } from './workspace.js';

/** A chip's width including its gap, so how many fit is arithmetic on the strip's width. */
const CHIP_WIDTH = 176;
/** Room kept for the "+N" button. */
const MENU_WIDTH = 64;
/** Below this window width the strip is a dropdown: a phone, or a narrow split. */
export const COMPACT_BELOW = 640;

interface StripHandlers {
  onActivate: (id: TabId) => void;
  /** Close as the old toolbar's Close did it: the shell asks first when the tab is dirty. */
  onRequestClose: (id: TabId) => void;
  /** Opens the palette. */
  onOpen: () => void;
}

function isCompact(): boolean {
  return window.innerWidth < COMPACT_BELOW;
}

/** A close control names the document it closes, and says when closing it would lose work. */
function closeLabel(name: string, dirty: boolean): string {
  return dirty ? `Close ${name} (unsaved changes)` : `Close ${name}`;
}

/** The menu both the dropdown and the "+N" overflow drop: a click inside it is not a click out. */
function TabMenu(props: { label: string; children: ReactNode }) {
  return (
    <ul role="menu" className="overflow-menu" aria-label={props.label} onClick={(event) => event.stopPropagation()}>
      {props.children}
    </ul>
  );
}

/**
 * One chip: the kind tile, the leaf with its namespace muted before it, and — beside the tab
 * rather than inside it, so the tab has no focusable descendant — its ×. A dirty tab shows ● in
 * the × slot until hovered. The tab is a button named "<Kind> <name>", so its kind is read out
 * and the tile can stay decorative.
 */
function Chip(props: StripHandlers & { doc: OpenDoc; active: boolean; visible: readonly TabId[]; bind: ReturnType<typeof useHoverCard>['bind'] }) {
  const { doc } = props;
  const { dirty } = useRuleEditor(doc.store);
  const { namespace, leaf } = splitName(doc.name);
  // Both bindings listen for keys — the card for Escape, the tab for Delete and the arrows — so
  // they are merged rather than one spread over the other.
  const events = tabInteractions(doc.id, props.visible, 'horizontal', props);
  const card = props.bind(doc.id);
  return (
    <div className={`chip kind-${doc.kind}${props.active ? ' active' : ''}${dirty ? ' dirty' : ''}`} role="presentation">
      <button
        type="button"
        role="tab"
        data-tab={doc.id}
        tabIndex={props.active ? 0 : -1}
        aria-selected={props.active}
        aria-label={`${KIND_LABEL[doc.kind]} ${doc.name}`}
        className="chip-tab"
        {...events}
        {...card}
        onKeyDown={(event) => { card.onKeyDown(event); events.onKeyDown(event); }}
      >
        <KindTile kind={doc.kind} />
        <span className="chip-title"><span className="chip-ns">{namespace}</span><span className="chip-leaf">{leaf}</span></span>
      </button>
      {/*
        A pointer affordance, outside the accessibility tree: a `tablist` may own only tabs, and
        the keyboard closes with Delete on the tab or ⌘W. The label still names it for anyone
        reading the DOM, and the focused tab's card says "Unsaved changes".
      */}
      <button
        type="button"
        className="chip-close"
        aria-label={closeLabel(doc.name, dirty)}
        aria-hidden="true"
        tabIndex={-1}
        onClick={() => props.onRequestClose(doc.id)}
      >
        <span className="chip-dot" aria-hidden="true" />
        <IconClose size={11} />
      </button>
    </div>
  );
}

/** One row of a menu: the tab, and — in the dropdown — its ×. */
function MenuRow(props: StripHandlers & { doc: OpenDoc; active: boolean; closable: boolean; onPick: () => void }) {
  const { dirty } = useRuleEditor(props.doc.store);
  return (
    <li role="none" className="overflow-row">
      <button
        type="button"
        role="menuitem"
        aria-current={props.active ? 'true' : undefined}
        className={`overflow-item${dirty ? ' dirty' : ''}${props.active ? ' active' : ''}`}
        onClick={props.onPick}
      >
        <KindTile kind={props.doc.kind} />
        <span className="overflow-name">{props.doc.name}</span>
        <span className="chip-dot" aria-hidden="true" />
      </button>
      {props.closable && (
        <button
          type="button"
          className="chip-close overflow-close"
          aria-label={closeLabel(props.doc.name, dirty)}
          aria-hidden="true"
          tabIndex={-1}
          onClick={() => props.onRequestClose(props.doc.id)}
        >
          <IconClose size={11} />
        </button>
      )}
    </li>
  );
}

/**
 * The open documents, in the app bar. Chips at a fixed width; whatever does not fit collapses
 * into a "+N" menu, and the active tab is always kept visible — an overflowed tab that is
 * activated swaps into the last slot. On a narrow window the strip is one dropdown naming the
 * active document and listing every tab with a × each.
 *
 * Reports `onCompactChange` so the shell can move the document's actions into the bar's free
 * space while the strip is a dropdown.
 */
export function TabStrip(props: StripHandlers & { workspace: Workspace; onCompactChange?: (compact: boolean) => void }) {
  const state = useSyncExternalStore(props.workspace.subscribe, props.workspace.getState);
  const hover = useHoverCard();
  const strip = useRef<HTMLDivElement | null>(null);
  const [capacity, setCapacity] = useState(4);
  const [compact, setCompact] = useState(isCompact);
  const [menuOpen, setMenuOpen] = useState(false);

  useLayoutEffect(() => {
    const el = strip.current;
    if (!el) return;
    const measure = (): void => {
      setCompact(isCompact());
      setCapacity(Math.max(1, Math.floor((el.clientWidth - MENU_WIDTH) / CHIP_WIDTH)));
    };
    measure();
    window.addEventListener('resize', measure);
    const observer = typeof ResizeObserver === 'undefined' ? null : new ResizeObserver(measure);
    observer?.observe(el);
    return () => { window.removeEventListener('resize', measure); observer?.disconnect(); };
  }, []);

  const { onCompactChange } = props;
  useEffect(() => { onCompactChange?.(compact); }, [compact, onCompactChange]);

  // A menu left open across the compact / chips switch would reappear at the other anchor.
  useEffect(() => { setMenuOpen(false); }, [compact]);
  useEffect(() => {
    if (!menuOpen) return;
    const close = (): void => setMenuOpen(false);
    const onKey = (event: KeyboardEvent): void => { if (event.key === 'Escape') close(); };
    window.addEventListener('click', close);
    window.addEventListener('keydown', onKey);
    return () => { window.removeEventListener('click', close); window.removeEventListener('keydown', onKey); };
  }, [menuOpen]);

  const { tabs, active, docs } = state;
  let visible = tabs.slice(0, capacity);
  let hidden = tabs.slice(capacity);
  if (active !== null && hidden.includes(active)) {
    const bumped = visible[visible.length - 1]!;
    visible = [...visible.slice(0, -1), active];
    hidden = [bumped, ...hidden.filter((id) => id !== active)];
  }
  const activeDoc = active === null ? undefined : docs[active];

  const toggleMenu = (event: MouseEvent<HTMLElement>): void => {
    event.stopPropagation();
    setMenuOpen((open) => !open);
  };

  const openButton = (
    <button type="button" className="ghost chip-new" aria-label="Open" title="Open (⌘K)" onClick={props.onOpen}>
      <IconNew size={14} />
    </button>
  );

  if (compact && tabs.length > 0) {
    return (
      <div className="chipstrip" ref={strip}>
        <div className="overflow tab-dropdown">
          <button
            type="button"
            className={`chip active tab-dropdown-button${activeDoc && activeDoc.store.getState().dirty ? ' dirty' : ''}`}
            aria-haspopup="menu"
            aria-expanded={menuOpen}
            aria-label={`Open documents: ${activeDoc ? `${KIND_LABEL[activeDoc.kind]} ${activeDoc.name}` : 'none active'}, ${tabs.length} open`}
            onClick={toggleMenu}
          >
            {activeDoc && <KindTile kind={activeDoc.kind} />}
            <span className="chip-title"><span className="chip-leaf">{activeDoc?.name ?? 'Nothing open'}</span></span>
            <span className="chip-dot" aria-hidden="true" />
            <span className="tab-dropdown-count" aria-hidden="true">{tabs.length}</span>
            <IconChevronDown size={12} />
          </button>
          {menuOpen && (
            <TabMenu label="Open documents">
              {tabs.map((id) => {
                const doc = docs[id];
                return doc ? (
                  <MenuRow key={id} {...props} doc={doc} active={id === active} closable onPick={() => { props.onActivate(id); setMenuOpen(false); }} />
                ) : null;
              })}
              <li role="none" className="overflow-row overflow-open">
                <button type="button" role="menuitem" className="overflow-item" onClick={() => { setMenuOpen(false); props.onOpen(); }}>
                  <IconNew size={13} /><span className="overflow-name">Open another…</span><kbd aria-hidden="true">⌘K</kbd>
                </button>
              </li>
            </TabMenu>
          )}
        </div>
      </div>
    );
  }

  return (
    <div className="chipstrip" ref={strip}>
      <div role="tablist" aria-label="Open documents" className="chips">
        {visible.map((id) => {
          const doc = docs[id];
          return doc ? <Chip key={id} {...props} doc={doc} active={id === active} visible={visible} bind={hover.bind} /> : null;
        })}
      </div>
      {/* Beside the list, not in it: a tablist may own only tabs. */}
      {openButton}
      {hidden.length > 0 && (
        <div className="overflow">
          <button
            type="button"
            className="ghost ghost-labelled overflow-button"
            aria-haspopup="menu"
            aria-expanded={menuOpen}
            aria-label={`+${hidden.length} more open documents`}
            onClick={toggleMenu}
          >
            <span aria-hidden="true">+{hidden.length}</span><IconChevronDown size={12} />
          </button>
          {menuOpen && (
            <TabMenu label="More open documents">
              {hidden.map((id) => {
                const doc = docs[id];
                return doc ? (
                  <MenuRow key={id} {...props} doc={doc} active={false} closable={false} onPick={() => { props.onActivate(id); setMenuOpen(false); }} />
                ) : null;
              })}
            </TabMenu>
          )}
        </div>
      )}
      <TabHoverCard workspace={props.workspace} shown={hover.shown} />
    </div>
  );
}
