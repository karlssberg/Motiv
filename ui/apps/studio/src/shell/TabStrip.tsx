import { useEffect, useLayoutEffect, useRef, useState, useSyncExternalStore, type ReactNode } from 'react';
import { useRuleEditor } from '@motiv-rules/react';
import { IconChevronDown, IconClose, IconNew, IconSearch } from './icons.js';
import { MenuList, useMenu, type MenuState } from './Menu.js';
import { KIND_LABEL, KindTile, TabHoverCard, splitName, useHoverCard } from './TabCard.js';
import { tabInteractions } from './tabEvents.js';
import { isEmptyTab, type OpenDoc, type TabId, type Workspace } from './workspace.js';

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
  /** Opens a new, empty tab — the "+", as a browser's. */
  onNewTab: () => void;
  /** Opens the palette — the search beside the "+", and ⌘K. */
  onOpen: () => void;
}

/** What the strip is drawing: the tabs in order, which is in front, and what each one holds. */
interface StripTabs {
  tabs: readonly TabId[];
  active: TabId | null;
  docs: Readonly<Record<TabId, OpenDoc>>;
}

function isCompact(): boolean {
  return window.innerWidth < COMPACT_BELOW;
}

/** A close control names the document it closes, and says when closing it would lose work. */
function closeLabel(name: string, dirty: boolean): string {
  return dirty ? `Close ${name} (unsaved changes)` : `Close ${name}`;
}

/** What an empty tab is called: it shows the catalog, so that is its name. */
const EMPTY_LABEL = 'Catalog';

/** The hollow tile an empty tab wears where a document's kind would be. */
function EmptyTile() {
  return <span className="kind-tile kind-empty" aria-hidden="true" />;
}

/**
 * A tab holding nothing: the same chip shape, with a hollow tile where the kind would be and the
 * catalog's name in place of a document's. Nothing to describe, so no hover card — a card would
 * only restate the label.
 */
function EmptyChip(props: StripHandlers & { id: TabId; active: boolean; visible: readonly TabId[] }) {
  const events = tabInteractions(props.id, props.visible, 'horizontal', props);
  return (
    <div className={`chip chip-empty${props.active ? ' active' : ''}`} role="presentation">
      <button
        type="button"
        role="tab"
        data-tab={props.id}
        tabIndex={props.active ? 0 : -1}
        aria-selected={props.active}
        aria-label={EMPTY_LABEL}
        className="chip-tab"
        {...events}
      >
        <EmptyTile />
        <span className="chip-title chip-title-empty">{EMPTY_LABEL}</span>
      </button>
      <button
        type="button"
        className="chip-close"
        aria-label="Close empty tab"
        aria-hidden="true"
        tabIndex={-1}
        onClick={() => props.onRequestClose(props.id)}
      >
        <IconClose size={11} />
      </button>
    </div>
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

/**
 * One row of a menu: the tab, and — in the dropdown — its ×. Drawn the same whether the tab holds
 * a document or nothing; an empty tab's row is muted and has no dirty dot, having no draft.
 */
function MenuRow(props: {
  tile: ReactNode;
  name: string;
  /** The row stands for an empty tab: the catalog's name in place of a document's. */
  empty: boolean;
  dirty: boolean;
  active: boolean;
  closable: boolean;
  closeLabel: string;
  onPick: () => void;
  onClose: () => void;
}) {
  return (
    <li role="none" className="overflow-row">
      <button
        type="button"
        role="menuitem"
        aria-current={props.active ? 'true' : undefined}
        className={`overflow-item${props.dirty ? ' dirty' : ''}${props.active ? ' active' : ''}`}
        onClick={props.onPick}
      >
        {props.tile}
        <span className={`overflow-name${props.empty ? ' overflow-name-empty' : ''}`}>{props.name}</span>
        {!props.empty && <span className="chip-dot" aria-hidden="true" />}
      </button>
      {props.closable && (
        <button
          type="button"
          className="chip-close overflow-close"
          aria-label={props.closeLabel}
          aria-hidden="true"
          tabIndex={-1}
          onClick={props.onClose}
        >
          <IconClose size={11} />
        </button>
      )}
    </li>
  );
}

/** A row for a tab holding a document: its kind, its name, and whether its draft is unsaved. */
function DocMenuRow(props: StripHandlers & { doc: OpenDoc; active: boolean; closable: boolean; onPick: () => void }) {
  const { doc } = props;
  const { dirty } = useRuleEditor(doc.store);
  return (
    <MenuRow
      tile={<KindTile kind={doc.kind} />}
      name={doc.name}
      empty={false}
      dirty={dirty}
      active={props.active}
      closable={props.closable}
      closeLabel={closeLabel(doc.name, dirty)}
      onPick={props.onPick}
      onClose={() => props.onRequestClose(doc.id)}
    />
  );
}

/** A row for one tab, whatever it holds. */
function TabMenuRow(props: StripHandlers & { id: TabId; doc: OpenDoc | undefined; active: boolean; closable: boolean; onPick: () => void }) {
  const { doc } = props;
  if (doc) return <DocMenuRow {...props} doc={doc} />;
  return (
    <MenuRow
      tile={<EmptyTile />}
      name={EMPTY_LABEL}
      empty
      dirty={false}
      active={props.active}
      closable={props.closable}
      closeLabel="Close empty tab"
      onPick={props.onPick}
      onClose={() => props.onRequestClose(props.id)}
    />
  );
}

/** What the dropdown's button is called: the document in front, or that an empty tab is. */
function frontTabName(strip: StripTabs): string {
  const doc = strip.active === null ? undefined : strip.docs[strip.active];
  if (doc) return `${KIND_LABEL[doc.kind]} ${doc.name}`;
  return strip.active === null ? 'none active' : EMPTY_LABEL;
}

/**
 * The strip on a narrow window: one dropdown naming the tab in front and listing every tab with a
 * × each, followed by the two ways to open another.
 */
/** "+" for a new empty tab, and the search for the palette — in both layouts. */
function StripButtons(props: Pick<StripHandlers, 'onNewTab' | 'onOpen'>) {
  return (
    <>
      <button type="button" className="ghost chip-new" aria-label="New tab" title="New tab" onClick={props.onNewTab}>
        <IconNew size={14} />
      </button>
      <button type="button" className="ghost chip-new" aria-label="Open" title="Open (⌘K)" onClick={props.onOpen}>
        <IconSearch size={14} />
      </button>
    </>
  );
}

function TabDropdown(props: StripHandlers & StripTabs & { menu: MenuState }) {
  const { tabs, active, docs, menu } = props;
  const activeDoc = active === null ? undefined : docs[active];
  return (
    <div className="overflow tab-dropdown">
      <button
        type="button"
        className={`chip active tab-dropdown-button${activeDoc && activeDoc.store.getState().dirty ? ' dirty' : ''}`}
        aria-haspopup="menu"
        aria-expanded={menu.open}
        aria-label={`Open documents: ${frontTabName(props)}, ${tabs.length} open`}
        onClick={menu.toggle}
      >
        {activeDoc ? <KindTile kind={activeDoc.kind} /> : <EmptyTile />}
        <span className="chip-title"><span className="chip-leaf">{activeDoc?.name ?? EMPTY_LABEL}</span></span>
        <span className="chip-dot" aria-hidden="true" />
        <span className="tab-dropdown-count" aria-hidden="true">{tabs.length}</span>
        <IconChevronDown size={12} />
      </button>
      {menu.open && (
        <MenuList label="Open documents">
          {tabs.map((id) => (
            <TabMenuRow key={id} {...props} id={id} doc={docs[id]} active={id === active} closable onPick={() => { props.onActivate(id); menu.close(); }} />
          ))}
          <li role="none" className="overflow-row overflow-open">
            <button type="button" role="menuitem" className="overflow-item" onClick={() => { menu.close(); props.onNewTab(); }}>
              <IconNew size={13} /><span className="overflow-name">New tab</span>
            </button>
          </li>
          <li role="none" className="overflow-row">
            <button type="button" role="menuitem" className="overflow-item" onClick={() => { menu.close(); props.onOpen(); }}>
              <IconSearch size={13} /><span className="overflow-name">Open…</span><kbd aria-hidden="true">⌘K</kbd>
            </button>
          </li>
        </MenuList>
      )}
    </div>
  );
}

/**
 * Which tabs get a chip, and which collapse into the "+N" menu: the first `capacity` of them,
 * except that the tab in front is always drawn — an overflowed tab that is activated swaps into
 * the last slot, and the tab it displaced joins the menu.
 */
function splitByCapacity(tabs: readonly TabId[], active: TabId | null, capacity: number): { visible: TabId[]; hidden: TabId[] } {
  const visible = tabs.slice(0, capacity);
  const hidden = tabs.slice(capacity);
  if (active === null || !hidden.includes(active)) return { visible, hidden };
  const bumped = visible[visible.length - 1]!;
  return {
    visible: [...visible.slice(0, -1), active],
    hidden: [bumped, ...hidden.filter((id) => id !== active)],
  };
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
  const menu = useMenu();

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
  const { close: closeMenu } = menu;
  useEffect(() => { closeMenu(); }, [compact, closeMenu]);

  const { tabs, active, docs } = state;

  if (compact && tabs.length > 0) {
    return (
      <div className="chipstrip" ref={strip}>
        <TabDropdown {...props} tabs={tabs} active={active} docs={docs} menu={menu} />
        {/* Still beside the dropdown: with an empty tab always open, the dropdown is the only strip a phone ever sees. */}
        <StripButtons {...props} />
      </div>
    );
  }

  const { visible, hidden } = splitByCapacity(tabs, active, capacity);
  return (
    <div className="chipstrip" ref={strip}>
      <div role="tablist" aria-label="Open documents" className="chips">
        {visible.map((id) => {
          const doc = docs[id];
          if (doc) return <Chip key={id} {...props} doc={doc} active={id === active} visible={visible} bind={hover.bind} />;
          return isEmptyTab(id) ? <EmptyChip key={id} {...props} id={id} active={id === active} visible={visible} /> : null;
        })}
      </div>
      {/* Beside the list, not in it: a tablist may own only tabs. */}
      <StripButtons {...props} />
      {hidden.length > 0 && (
        <div className="overflow">
          <button
            type="button"
            className="ghost ghost-labelled overflow-button"
            aria-haspopup="menu"
            aria-expanded={menu.open}
            aria-label={`+${hidden.length} more open documents`}
            onClick={menu.toggle}
          >
            <span aria-hidden="true">+{hidden.length}</span><IconChevronDown size={12} />
          </button>
          {menu.open && (
            <MenuList label="More open documents">
              {hidden.map((id) => (
                <TabMenuRow key={id} {...props} id={id} doc={docs[id]} active={false} closable={false} onPick={() => { props.onActivate(id); menu.close(); }} />
              ))}
            </MenuList>
          )}
        </div>
      )}
      <TabHoverCard workspace={props.workspace} shown={hover.shown} />
    </div>
  );
}
