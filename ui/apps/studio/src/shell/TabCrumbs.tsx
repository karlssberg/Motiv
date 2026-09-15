import { IconChevronDown } from './icons.js';
import { KIND_LABEL, KindGlyph } from './TabCard.js';
import { MenuList, useMenu } from './Menu.js';
import type { DocKind } from './workspace.js';
import { Tooltip } from './Tooltip.js';

/**
 * Where a loaded tab is, as a browser's address would say it: *Catalog › Rules › name*. Each
 * segment is a way to change what the tab holds without leaving it — the first empties the tab
 * back to the catalog, and the kind drops the other documents of that kind to swap one in. The
 * name itself is text: it is where the tab already is.
 */
export function TabCrumbs(props: {
  kind: DocKind;
  name: string;
  /** The other documents of this kind, by name — what the kind segment offers. */
  siblings: readonly string[];
  onUnload: () => void;
  onReplace: (name: string) => void;
}) {
  const menu = useMenu();

  const kinds = `${KIND_LABEL[props.kind]}s`;
  return (
    <nav className="crumbs" aria-label="Where this tab is">
      <Tooltip text="Back to the catalog, in this tab"><button type="button" className="crumb" onClick={props.onUnload}>Catalog</button></Tooltip>
      <span className="crumb-sep" aria-hidden="true">›</span>
      <span className="overflow">
        <Tooltip text={`Open another of the ${kinds.toLowerCase()} in this tab`}><button
          type="button"
          className="crumb"
          aria-haspopup="menu"
          aria-expanded={menu.open}
          onClick={menu.toggle}
        >
          {kinds}<IconChevronDown size={11} />
        </button></Tooltip>
        {menu.open && (
          <MenuList label={`Other ${kinds.toLowerCase()}`} className="crumb-menu">
            {props.siblings.length === 0 && <li role="none" className="overflow-none">No other {kinds.toLowerCase()}</li>}
            {props.siblings.map((name) => (
              <li key={name} role="none">
                <button type="button" role="menuitem" className="overflow-item" onClick={() => { menu.close(); props.onReplace(name); }}>
                  <KindGlyph kind={props.kind} size={12} /><span className="overflow-name">{name}</span>
                </button>
              </li>
            ))}
          </MenuList>
        )}
      </span>
      <span className="crumb-sep" aria-hidden="true">›</span>
      <span className="crumb-here"><KindGlyph kind={props.kind} size={12} />{props.name}</span>
    </nav>
  );
}
