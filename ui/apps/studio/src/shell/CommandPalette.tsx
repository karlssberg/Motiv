import { useId, useMemo, useState, type KeyboardEvent, type ReactNode } from 'react';
import { Modal } from './Modal.js';
import { IconSearch } from './icons.js';

/** The least a palette needs to know about a row: something stable to key and address it by. */
export interface PaletteItem { id: string; }

/**
 * Where the highlight sits, or `-1` for nowhere when nothing matched.
 *
 * Derived from the cursor rather than corrected in an effect: when the query changes the list
 * changes under the highlight, and the row at index N is no longer the row the user was looking
 * at — so a cursor left past the end of a shrunken list falls back to the top of it.
 */
function highlightIndexOf(cursor: number, count: number): number {
  if (count === 0) return -1;
  if (cursor < count) return cursor;
  return 0;
}

/**
 * Search-first chooser. Opens with the caret in the search box and reopens fresh every time —
 * a palette still holding the previous query is a palette that has to be cleared before use.
 *
 * With no query it renders `renderBrowse` if given, so a namespaced set stays browsable; once
 * anything is typed the results flatten to one row per match, because hierarchy is noise in a
 * result list.
 */
export function CommandPalette<T extends PaletteItem>(props: {
  label: string;
  placeholder: string;
  items: T[];
  match: (item: T, query: string) => boolean;
  renderItem: (item: T, highlighted: boolean) => ReactNode;
  renderBrowse?: () => ReactNode;
  /** Rendered instead of an empty list, handed the query that matched nothing. */
  renderEmpty?: (query: string) => ReactNode;
  onChoose: (item: T) => void;
  onClose: () => void;
  footer?: (highlighted: T | null) => ReactNode;
}) {
  const [query, setQuery] = useState('');
  const [cursor, setCursor] = useState(0);
  const listId = useId();

  const trimmed = query.trim();
  const browsing = trimmed === '' && props.renderBrowse !== undefined;

  const matches = useMemo(
    () => (trimmed === '' ? props.items : props.items.filter((item) => props.match(item, trimmed))),
    // `props.match` is intentionally not a dependency: callers pass an inline arrow, so including
    // it would recompute on every render and defeat the memo entirely.
    [props.items, trimmed],
  );

  // Browsing renders the browse view *instead of* the list, so there is no row on screen under the
  // highlight. Reporting one anyway would let Enter choose something nobody was shown, hand a
  // footer a target the user never picked, and point aria-activedescendant at an id that is not in
  // the document at all.
  const highlightIndex = browsing ? -1 : highlightIndexOf(cursor, matches.length);
  const highlighted = matches[highlightIndex] ?? null;

  const optionId = (index: number): string => `${listId}-option-${index}`;

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>): void => {
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      setCursor(Math.min(highlightIndex + 1, matches.length - 1));
      return;
    }
    if (event.key === 'ArrowUp') {
      event.preventDefault();
      setCursor(Math.max(highlightIndex - 1, 0));
      return;
    }
    if (event.key === 'Enter' && highlighted !== null) {
      event.preventDefault();
      props.onChoose(highlighted);
    }
  };

  return (
    <Modal label={props.label} onClose={props.onClose} className="palette" fullscreenOnMobile>
      <div className="palette-search">
        <IconSearch size={15} />
        <input
          type="text"
          role="combobox"
          autoFocus
          className="palette-input"
          aria-label={props.placeholder}
          aria-expanded={!browsing}
          // Dropped while browsing, because the browse view renders *instead of* the list — see
          // below — so the id names nothing in the document. An IDREF to an absent element is an
          // invalid relationship, and this is the palette's default state: every open of the
          // Propositions palette started there.
          aria-controls={browsing ? undefined : listId}
          aria-activedescendant={highlightIndex >= 0 ? optionId(highlightIndex) : undefined}
          placeholder={props.placeholder}
          value={query}
          onChange={(event) => { setQuery(event.target.value); setCursor(0); }}
          onKeyDown={onKeyDown}
        />
        {/*
          A live region, not just a caption. Typing into the box is the only thing that narrows the
          list, and the list is exactly what a screen-reader user cannot see — so without an
          announcement, filtering to one row and filtering to none sound identical: silence. `status`
          is polite by construction, so a fast typist is not interrupted mid-keystroke, and the count
          it carries is already the one on screen rather than a second phrasing that could drift.
        */}
        {!browsing && (
          <span className="palette-count" role="status">{matches.length} of {props.items.length}</span>
        )}
      </div>

      {browsing
        ? <div className="palette-browse">{props.renderBrowse?.()}</div>
        : (
          <>
            {/* The listbox stays in the document even while empty: the input's `aria-controls`
                names it, and a control pointing at nothing is worse than a control pointing at an
                empty list. The explanation sits beside it rather than inside, because a listbox's
                children are its options. */}
            <ul id={listId} role="listbox" aria-label={props.label} className="palette-list">
              {matches.map((item, index) => {
                const isHighlighted = index === highlightIndex;
                return (
                  <li
                    key={item.id}
                    id={optionId(index)}
                    role="option"
                    aria-selected={isHighlighted}
                    className={isHighlighted ? 'palette-row highlighted' : 'palette-row'}
                    onClick={() => props.onChoose(item)}
                  >
                    {props.renderItem(item, isHighlighted)}
                  </li>
                );
              })}
            </ul>
            {matches.length === 0 && props.renderEmpty?.(trimmed)}
          </>
        )}

      {props.footer !== undefined && (
        <div className="palette-footer">{props.footer(highlighted)}</div>
      )}
    </Modal>
  );
}
