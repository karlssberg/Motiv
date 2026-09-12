/*
 * PROTOTYPE — throwaway. Do not ship.
 *
 * Variant C — in-bar chips with an overflow menu. The tabs sit *in* the app bar where the page
 * switch used to be, so no vertical space is spent on them. Chips never shrink below a readable
 * width: whatever does not fit collapses into a "+N" menu at the end, and the active tab is
 * always kept visible (an overflowed tab that is activated swaps into the last slot). Kind is a
 * lettered tile (R / P) rather than a glyph, and the title shows the last dotted segment with
 * the namespace muted before it.
 *
 * Chosen direction, second pass: the app bar carries nothing but the brand and the strip. The
 * document's own actions (JSON, Save) live in its panel — see TabDocument — so Open and Close
 * have no bar buttons: Open is "+" / ⌘K, Close is the chip's ×. On a narrow window the strip
 * becomes a single dropdown naming the active document and listing the rest — and since that
 * leaves the bar mostly empty, the document's actions move up into it; narrower still and they
 * wrap onto a second row of the bar rather than squeezing.
 */
import { useEffect, useLayoutEffect, useRef, useState, useSyncExternalStore } from 'react';
import { useRuleEditor } from '@motiv-rules/react';
import { IconChevronDown, IconClose, IconNew } from '../../shell/icons.js';
import { TabHoverCard, splitName, useHoverCard } from './TabCard.js';
import type { TabShellProps } from './TabsPrototype.js';
import { tabInteractions } from './tabEvents.js';
import type { OpenDoc, TabId } from './workspace.js';

const CHIP_WIDTH = 176; // px, including gap
const MENU_WIDTH = 64;
/** Below this window width the strip becomes a dropdown: a phone, or a narrow split. */
const COMPACT_BELOW = 640;

function KindTile(props: { kind: OpenDoc['kind'] }) {
  return <span className={`kind-tile kind-${props.kind}`} aria-hidden="true">{props.kind === 'rule' ? 'R' : 'P'}</span>;
}

function Chip(props: TabShellProps & { doc: OpenDoc; visible: TabId[]; hover: ReturnType<typeof useHoverCard> }) {
  const { doc } = props;
  const { dirty } = useRuleEditor(doc.store);
  const active = props.state.active === doc.id;
  const { namespace, leaf } = splitName(doc.name);
  const events = tabInteractions(doc.id, props.visible, 'horizontal', props);
  return (
    <div
      role="tab"
      data-tab={doc.id}
      tabIndex={active ? 0 : -1}
      aria-selected={active}
      className={`chip kind-${doc.kind}${active ? ' active' : ''}${dirty ? ' dirty' : ''}`}
      {...events}
      {...props.hover.bind(doc.id)}
    >
      <KindTile kind={doc.kind} />
      <span className="chip-title"><span className="chip-ns">{namespace}</span><span className="chip-leaf">{leaf}</span></span>
      <button
        type="button"
        className="chip-close"
        aria-label={dirty ? `Close ${doc.name} (unsaved changes)` : `Close ${doc.name}`}
        tabIndex={-1}
        onClick={(event) => { event.stopPropagation(); props.onRequestClose(doc.id); }}
      >
        <span className="chip-dot" aria-hidden="true" />
        <IconClose size={11} />
      </button>
    </div>
  );
}

function OverflowRow(props: TabShellProps & { doc: OpenDoc; onPick: () => void; closable?: boolean }) {
  const { dirty } = useRuleEditor(props.doc.store);
  const active = props.state.active === props.doc.id;
  return (
    <li role="none" className="overflow-row">
      <button type="button" role="menuitem" aria-current={active ? 'true' : undefined} className={`overflow-item${dirty ? ' dirty' : ''}${active ? ' active' : ''}`} onClick={props.onPick}>
        <KindTile kind={props.doc.kind} />
        <span className="overflow-name">{props.doc.name}</span>
        <span className="chip-dot" aria-hidden="true" />
      </button>
      {props.closable && (
        <button type="button" className="chip-close overflow-close" aria-label={`Close ${props.doc.name}`} onClick={() => props.onRequestClose(props.doc.id)}>
          <IconClose size={11} />
        </button>
      )}
    </li>
  );
}

/** The compact strip: one button naming the active document, opening the list of every open tab. */
function TabDropdown(props: TabShellProps & { open: boolean; setOpen: (open: boolean) => void }) {
  const active = props.state.active ? props.state.docs[props.state.active] : undefined;
  const { dirty } = useRuleEditor(active?.store ?? props.state.docs[props.state.tabs[0]!]!.store);
  return (
    <div className="overflow tab-dropdown">
      <button
        type="button"
        className={`chip active tab-dropdown-button${dirty ? ' dirty' : ''}`}
        aria-haspopup="menu"
        aria-expanded={props.open}
        onClick={(event) => { event.stopPropagation(); props.setOpen(!props.open); }}
      >
        {active ? <KindTile kind={active.kind} /> : null}
        <span className="chip-title"><span className="chip-leaf">{active?.name ?? 'Nothing open'}</span></span>
        <span className="chip-dot" aria-hidden="true" />
        <span className="tab-dropdown-count">{props.state.tabs.length}</span>
        <IconChevronDown size={12} />
      </button>
      {props.open && (
        <ul role="menu" className="overflow-menu" aria-label="Open documents" onClick={(event) => event.stopPropagation()}>
          {props.state.tabs.map((id) => {
            const doc = props.state.docs[id];
            return doc ? <OverflowRow key={id} {...props} doc={doc} closable onPick={() => { props.onActivate(id); props.setOpen(false); }} /> : null;
          })}
          <li role="none" className="overflow-row overflow-open">
            <button type="button" role="menuitem" className="overflow-item" onClick={() => { props.setOpen(false); props.onOpenPalette(); }}>
              <IconNew size={13} /><span className="overflow-name">Open another…</span><kbd aria-hidden="true">⌘K</kbd>
            </button>
          </li>
        </ul>
      )}
    </div>
  );
}

export function VariantC(props: TabShellProps) {
  const hover = useHoverCard();
  useSyncExternalStore(props.workspace.subscribe, props.workspace.getState);
  const strip = useRef<HTMLDivElement | null>(null);
  const [capacity, setCapacity] = useState(4);
  const [compact, setCompact] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);

  useLayoutEffect(() => {
    const el = strip.current;
    if (!el) return;
    const measure = (): void => {
      setCompact(window.innerWidth < COMPACT_BELOW);
      setCapacity(Math.max(1, Math.floor((el.clientWidth - MENU_WIDTH) / CHIP_WIDTH)));
    };
    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(el);
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    if (!menuOpen) return;
    const close = (): void => setMenuOpen(false);
    const onKey = (event: KeyboardEvent): void => { if (event.key === 'Escape') close(); };
    window.addEventListener('click', close);
    window.addEventListener('keydown', onKey);
    return () => { window.removeEventListener('click', close); window.removeEventListener('keydown', onKey); };
  }, [menuOpen]);

  // A menu left open across the compact/chips switch would reappear at the other anchor.
  useEffect(() => { setMenuOpen(false); }, [compact]);

  const { tabs, active } = props.state;
  let visible = tabs.slice(0, capacity);
  let hidden = tabs.slice(capacity);
  if (active && hidden.includes(active)) {
    // Keep the active tab on screen: it takes the last visible slot, whose occupant overflows.
    const bumped = visible[visible.length - 1];
    visible = [...visible.slice(0, -1), active];
    hidden = [bumped!, ...hidden.filter((id) => id !== active)];
  }

  return (
    <>
      <header className={compact ? 'appbar proto-c-bar compact' : 'appbar proto-c-bar'}>
        <div className="appbar-brand">
          <span className="appbar-mark" aria-hidden="true">M</span>
          <span className="appbar-wordmark">Motiv</span>
        </div>
        <span className="appbar-divider" aria-hidden="true" />
        <div className="chipstrip" ref={strip}>
          {compact && tabs.length > 0 ? <TabDropdown {...props} open={menuOpen} setOpen={setMenuOpen} /> : (<>
          <div role="tablist" aria-label="Open documents" className="chips">
            {visible.map((id) => {
              const doc = props.state.docs[id];
              return doc ? <Chip key={id} {...props} doc={doc} visible={visible} hover={hover} /> : null;
            })}
            <button type="button" className="ghost chip-new" aria-label="Open another" title="Open another (⌘K)" onClick={props.onOpenPalette}><IconNew size={14} /></button>
          </div>
          {hidden.length > 0 && (
            <div className="overflow">
              <button
                type="button"
                className="ghost ghost-labelled overflow-button"
                aria-haspopup="menu"
                aria-expanded={menuOpen}
                onClick={(event) => { event.stopPropagation(); setMenuOpen((value) => !value); }}
              >
                +{hidden.length}<IconChevronDown size={12} />
              </button>
              {menuOpen && (
                <ul role="menu" className="overflow-menu" aria-label="More open documents" onClick={(event) => event.stopPropagation()}>
                  {hidden.map((id) => {
                    const doc = props.state.docs[id];
                    return doc ? <OverflowRow key={id} {...props} doc={doc} onPick={() => { props.onActivate(id); setMenuOpen(false); }} /> : null;
                  })}
                </ul>
              )}
            </div>
          )}
          </>)}
        </div>
        {compact && <div className="appbar-controls">{props.docActions}</div>}
      </header>

      {props.bodyWith(!compact)}
      <TabHoverCard workspace={props.workspace} shown={hover.shown} side="below" />
    </>
  );
}
