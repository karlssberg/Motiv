import {
  useId, useMemo, useRef, useState,
  type CSSProperties, type KeyboardEvent, type MouseEvent,
} from 'react';
import {
  buildNamespaceTree, countLeaves, filterTree,
  type NamespaceNode, type PropositionListEntry,
} from '@motiv-rules/core';
import { CommandPalette } from '../shell/CommandPalette.js';
import { Toolbar, type ToolbarAction } from '../shell/Toolbar.js';
import { IconDelete, IconDerive, IconNew, IconOverride } from '../shell/icons.js';

/** What the explorer can ask the page to do. */
export interface ExplorerActions {
  onSelect: (name: string) => void;
  onDerive: (name: string) => void;
  onOverride: (name: string) => void;
  onNew: () => void;
  onDelete: (entry: PropositionListEntry) => void;
  /** Dismiss without choosing. */
  onClose: () => void;
}

const ORIGIN_LABEL: Record<PropositionListEntry['origin'], string> = {
  Compiled: 'compiled',
  Overridden: 'overridden',
  Authored: 'authored',
};

/**
 * A listing entry addressed by its name. The palette keys and identifies rows by `id`, and a
 * proposition's name already is its identity — so this is a rename at the type level rather than
 * a second notion of which row is which.
 */
type PaletteEntry = PropositionListEntry & { id: string };

/** Why an action that needs a proposition cannot run when none is in hand. */
const NO_TARGET = 'Pick a proposition first.';

/**
 * The proposition chooser: a command palette that browses the namespace tree while the query is
 * empty and flattens to matches once anything is typed. The hierarchy is a pure projection of the
 * dotted names — there is no stored folder structure — so a rename moves a proposition and nothing
 * else needs to know.
 */
export function PropositionExplorer(props: {
  entries: PropositionListEntry[];
  selected: string | null;
  actions: ExplorerActions;
}) {
  const { entries, selected, actions } = props;
  // The model chips filter the browse view only, and are held here rather than inside it: the
  // browse view unmounts the moment anything is typed, and a chip that forgot itself over a
  // search would have to be set again on the way back. The palette owns the *query* instead —
  // reopening with the last search still in the box is a palette that has to be cleared before it
  // can be used — because a search is a passing act and a chip is a standing narrowing.
  const [models, setModels] = useState<string[]>([]);

  const items = useMemo(
    () => entries.map((entry): PaletteEntry => ({ ...entry, id: entry.name })),
    [entries],
  );

  const selectedEntry = entries.find((entry) => entry.name === selected) ?? null;

  const toggleModel = (model: string): void =>
    setModels((current) =>
      current.includes(model) ? current.filter((kept) => kept !== model) : [...current, model]);

  // Choosing is the palette's whole purpose, so it closes on the way out rather than leaving the
  // user to dismiss a modal they are finished with.
  const choose = (name: string): void => { actions.onSelect(name); actions.onClose(); };

  return (
    <CommandPalette<PaletteEntry>
      label="Propositions"
      placeholder="Filter propositions"
      items={items}
      match={(entry, needle) => entry.name.toLowerCase().includes(needle.toLowerCase())}
      renderItem={(entry) => <MatchedRow entry={entry} />}
      renderBrowse={() => (
        <NamespaceBrowse
          entries={entries}
          models={models}
          onToggleModel={toggleModel}
          selected={selected}
          onChoose={choose}
        />
      )}
      renderEmpty={(needle) => (
        <p className="explorer-empty">No propositions match “{needle}”.</p>
      )}
      onChoose={(entry) => choose(entry.name)}
      onClose={actions.onClose}
      footer={(highlighted) => (
        // The highlighted row is what the user is pointing at *now*; the selection is what they
        // pointed at last. Acting on the highlight while one exists is what makes the footer read
        // as belonging to the row above it.
        <ExplorerFooter entry={highlighted ?? selectedEntry} entries={entries} actions={actions} />
      )}
    />
  );
}

/**
 * One matched row, once a query has flattened the tree away.
 *
 * The namespace is de-emphasised rather than dropped: two leaves can share a name, and which
 * `is-active` this is only legible from the path it sits under.
 */
function MatchedRow(props: { entry: PropositionListEntry }) {
  const { name, origin } = props.entry;
  const cut = name.lastIndexOf('.');
  return (
    <span className="palette-name">
      {cut > 0 && <span className="palette-ns">{name.slice(0, cut + 1)}</span>}
      {name.slice(cut + 1)}
      <span className="palette-badge">{ORIGIN_LABEL[origin]}</span>
    </span>
  );
}

/**
 * The namespace tree, as browsed while the query is empty. Since that is the only state it renders
 * in, the model chips are the whole filter — hence the empty needle handed to `filterTree`.
 */
function NamespaceBrowse(props: {
  entries: PropositionListEntry[];
  models: string[];
  onToggleModel: (model: string) => void;
  selected: string | null;
  onChoose: (name: string) => void;
}) {
  const { entries, models, onToggleModel, selected, onChoose } = props;

  const modelTypes = useMemo(
    () => [...new Set(entries.map((entry) => entry.modelType))].sort(),
    [entries],
  );

  const tree = useMemo(() => buildNamespaceTree(entries), [entries]);
  const filtered = useMemo(() => filterTree(tree, '', models), [tree, models]);
  const shown = countLeaves(filtered);

  return (
    <>
      <div className="explorer-chips">
        {modelTypes.map((model) => (
          <button
            key={model}
            type="button"
            className="model-pill"
            // `aria-pressed` is the single source of truth for the toggled state — the stylesheet
            // selects on it, as it already does for the builder's toggles, so there is no parallel
            // class to keep in step with it.
            aria-pressed={models.includes(model)}
            onClick={() => onToggleModel(model)}
          >
            {model}
          </button>
        ))}
        <span className="explorer-count">{shown} of {entries.length}</span>
      </div>

      {shown === 0
        // Browsing happens only with an empty query, so there is no query to blame for this —
        // which is also the state of the very first paint, before the listing has arrived, and of
        // a catalog that is genuinely empty. A failed *search* is said elsewhere.
        ? <p className="explorer-empty">No propositions yet.</p>
        : <NamespaceTree nodes={filtered} selected={selected} onSelect={onChoose} />}
    </>
  );
}

/**
 * Why Override cannot run against `entry`, or `undefined` when it can.
 *
 * Override composes from another spec over the same model — UI propositions are composition-only,
 * so a compiled spec whose model has no *other* spec offers nothing to build an override from, and
 * the action must not be offered as if it did. The reason is the only form the rule takes: there
 * is no separate boolean to fall out of step with what the button says.
 */
function whyNotOverride(
  entry: PropositionListEntry | null,
  entries: PropositionListEntry[],
): string | undefined {
  if (entry === null) return NO_TARGET;
  if (entry.origin !== 'Compiled') return 'Only a name served solely by a compiled spec can be overridden.';
  const composable = entries.some((other) =>
    other.modelType === entry.modelType && other.name !== entry.name);
  if (!composable) return `Nothing else of model ${entry.modelType} to compose an override from.`;
  return undefined;
}

/**
 * The palette's actions, aimed at whichever entry is in hand.
 *
 * Every one of them stays reachable with `aria-disabled` and a reason rather than being hidden or
 * `disabled` — a control that vanishes teaches nothing, and `disabled` takes the explanation out
 * of the tab order along with the button. Delete is the single exception: on a compiled entry
 * there is no authored document to delete or revert, so there is no action to explain.
 */
function ExplorerFooter(props: {
  entry: PropositionListEntry | null;
  entries: PropositionListEntry[];
  actions: ExplorerActions;
}) {
  const { entry, entries, actions } = props;

  const noTarget = entry === null ? NO_TARGET : undefined;
  const overrideUnavailable = whyNotOverride(entry, entries);
  // With nothing in hand Delete is still shown, unavailable: what is missing is the target, not
  // the action. It is only a *compiled* entry that has no delete to offer at all.
  const deletable = entry === null || entry.origin !== 'Compiled';

  const deleteAction: ToolbarAction = {
    id: 'delete',
    // Read off the entry rather than the response: a revert and an outright delete are the same call.
    label: entry?.origin === 'Overridden' ? 'Revert to compiled' : 'Delete',
    icon: IconDelete,
    onActivate: () => { if (entry !== null) actions.onDelete(entry); },
    unavailable: noTarget,
  };

  const toolbar: ToolbarAction[] = [
    { id: 'new', label: 'New', icon: IconNew, onActivate: actions.onNew },
    {
      id: 'derive',
      label: 'Derive',
      icon: IconDerive,
      onActivate: () => { if (entry !== null) actions.onDerive(entry.name); },
      unavailable: noTarget,
    },
    {
      id: 'override',
      label: 'Override',
      icon: IconOverride,
      // Only the null check is made here, as with Derive and Delete: `unavailable` is what stops
      // an activation, and Toolbar returns early on it. Restating the reason here as well would
      // read as though this one action did not trust that, and could fall out of step with it.
      onActivate: () => { if (entry !== null) actions.onOverride(entry.name); },
      unavailable: overrideUnavailable,
    },
    ...(deletable ? [deleteAction] : []),
  ];

  return <Toolbar actions={toolbar} />;
}

/**
 * One row of the tree as the keyboard walks it — the rendered rows, flattened into the order they
 * appear on screen, with the two links (parent, first child) the sideways keys need.
 *
 * Flattened rather than walked live because every movement key is an index arithmetic on this
 * order: down and up are ±1, Home and End are its ends, and type-ahead is a scan of it. Nothing is
 * ever collapsed, so this really is the whole tree — a row hidden inside a closed branch would have
 * to be excluded here, and there is no such thing to exclude.
 */
interface TreeRow {
  path: string;
  /** What type-ahead matches on: the row's own segment, not the dotted name it sits under. */
  segment: string;
  parent: string | null;
  firstChild: string | null;
  /** The proposition this row *is*, or null for a bare namespace — which is not activatable. */
  name: string | null;
}

function flattenTree(nodes: NamespaceNode[], parent: string | null = null): TreeRow[] {
  return nodes.flatMap((node) => [
    {
      path: node.path,
      segment: node.segment,
      parent,
      firstChild: node.children[0]?.path ?? null,
      name: node.entry?.name ?? null,
    },
    ...flattenTree(node.children, node.path),
  ]);
}

/** How long a type-ahead keeps accumulating before the next keystroke starts a new search. */
const TYPE_AHEAD_MS = 500;

/**
 * The first row at or after `from` whose segment starts with `prefix`, wrapping round the end —
 * or null when nothing does, which leaves the focus where it is.
 *
 * `includeCurrent` is what makes an accumulating search behave: the first character of a search
 * must move *off* the current row (pressing `i` twice walks the rows starting with `i`), while
 * every character after it refines the search the current row may well still satisfy.
 */
function rowStartingWith(
  rows: TreeRow[],
  from: number,
  prefix: string,
  includeCurrent: boolean,
): string | null {
  const first = includeCurrent ? 0 : 1;
  for (let step = first; step < first + rows.length; step += 1) {
    const row = rows[(from + step) % rows.length]!;
    if (row.segment.toLowerCase().startsWith(prefix)) return row.path;
  }
  return null;
}

/** The path of the treeitem an event came from, or null when it came from anywhere else. */
function pathOf(target: EventTarget): string | null {
  return target instanceof HTMLElement ? target.dataset['path'] ?? null : null;
}

/**
 * The namespace tree, navigated the way `role="tree"` says it is: **one tab stop**, arrow keys to
 * move between rows, Home/End to the ends, type-ahead to jump, and Enter/Space to choose. The role
 * used to be worn without any of that — every row a tab stop, no arrow keys — which told a screen
 * reader to switch into a navigation mode that then did nothing.
 *
 * **Focus is the state.** The keys move it with `focus()` and the roving `tabindex` follows,
 * because focus is what the user experiences and a second copy of "where am I" could disagree with
 * it. `focused` is only a mirror, and it is *read* through the rows rather than corrected: the
 * model chips can filter the focused row away, and a stale path then falls back rather than leaving
 * the tree with no tab stop at all.
 *
 * There is nothing to expand or collapse — the tree renders fully expanded, which is why
 * `aria-expanded` is always `true` — so ArrowRight and ArrowLeft carry only their movement
 * meanings: into the subtree, and back out to the parent.
 */
function NamespaceTree(props: {
  nodes: NamespaceNode[];
  selected: string | null;
  onSelect: (name: string) => void;
}) {
  const { nodes, selected, onSelect } = props;
  const treeId = useId();
  const rows = useMemo(() => flattenTree(nodes), [nodes]);
  const [focused, setFocused] = useState<string | null>(null);
  // A ref rather than state: a search in progress changes nothing on screen, and re-rendering the
  // whole tree on every keystroke of a type-ahead would be a render per character for no paint.
  const typed = useRef({ prefix: '', at: 0 });

  // Rows are addressed by id and reached with `getElementById`, deliberately: a `useId` prefix
  // contains colons, which are legal in an id and not in a selector — a `querySelector` for the
  // same string would throw.
  const rowId = (path: string): string => `${treeId}-${path}`;

  // Where the tab stop sits. The row last focused, else the selected proposition — so tabbing in
  // lands where the user left off rather than at the top of a list they have already moved past —
  // else the first row.
  const stop = rows.find((row) => row.path === focused)?.path
    ?? rows.find((row) => row.name !== null && row.name === selected)?.path
    ?? rows[0]?.path
    ?? null;

  const focusRow = (path: string | null | undefined): void => {
    if (path != null) document.getElementById(rowId(path))?.focus();
  };

  const onKeyDown = (event: KeyboardEvent<HTMLUListElement>): void => {
    const from = rows.findIndex((row) => row.path === pathOf(event.target));
    const row = rows[from];
    if (row === undefined) return;

    // Every movement key is claimed whether or not it has somewhere to go: at the last row
    // ArrowDown must still not scroll the palette out from under the row that has focus.
    const move = (path: string | null | undefined): void => {
      event.preventDefault();
      focusRow(path);
    };

    switch (event.key) {
      case 'ArrowDown': return move(rows[from + 1]?.path);
      case 'ArrowUp': return move(rows[from - 1]?.path);
      case 'ArrowRight': return move(row.firstChild);
      case 'ArrowLeft': return move(row.parent);
      case 'Home': return move(rows[0]?.path);
      case 'End': return move(rows[rows.length - 1]?.path);
      case 'Enter':
      case ' ':
        event.preventDefault(); // Space must not scroll the page
        if (row.name !== null) onSelect(row.name);
        return;
      default:
    }

    // Type-ahead. A chord belongs to the shell (⌘K opens this palette) or to the platform, so only
    // a bare character is taken — and a key that names itself with a word rather than a character
    // (Tab, Escape, F1) is not a character to search for.
    if (event.key.length !== 1 || event.metaKey || event.ctrlKey || event.altKey) return;
    const now = Date.now();
    const continuing = now - typed.current.at < TYPE_AHEAD_MS;
    const prefix = (continuing ? typed.current.prefix : '') + event.key.toLowerCase();
    typed.current = { prefix, at: now };
    focusRow(rowStartingWith(rows, from, prefix, continuing));
  };

  return (
    <ul
      className="explorer-tree"
      role="tree"
      aria-label="Proposition namespaces"
      onKeyDown={onKeyDown}
      // Focus events bubble as `focusin`, so one handler on the tree keeps the roving tabindex in
      // step however focus arrived — arrow key, Tab, or a click on a row.
      onFocus={(event) => {
        const path = pathOf(event.target);
        if (path !== null) setFocused(path);
      }}
    >
      {nodes.map((node) => (
        <TreeNode
          key={node.path}
          node={node}
          depth={0}
          selected={selected}
          stop={stop}
          rowId={rowId}
          onSelect={onSelect}
        />
      ))}
    </ul>
  );
}

/**
 * One node. A node can be both a namespace and a proposition — `customer` may be a name in its own
 * right — so the entry and the children are rendered independently rather than as an either/or.
 *
 * The keyboard lives one level up, on the tree: with a roving tabindex the focused row is the only
 * one a key can reach, so a handler per row would be the same handler mounted once per row.
 */
function TreeNode(props: {
  node: NamespaceNode;
  depth: number;
  selected: string | null;
  /** The path of the tree's one tab stop. */
  stop: string | null;
  rowId: (path: string) => string;
  onSelect: (name: string) => void;
}) {
  const { node, depth, selected, stop, rowId, onSelect } = props;
  const entry = node.entry;
  const quarantine = entry?.quarantine ?? [];
  const quarantined = quarantine.length > 0;

  // Only a node that *is* a proposition is selectable; a bare namespace is scaffolding.
  const activate = (): void => {
    if (entry) onSelect(entry.name);
  };

  // The click handler always lives on the treeitem itself (not a descendant span) and always
  // stops propagation, even when this node has no entry. A click event bubbles from its target up
  // through ancestors, never down into children, so a handler placed only on a descendant span
  // would never fire when the treeitem element itself is the click target. And if a bare-namespace
  // row simply carried no handler at all, an unhandled click would keep bubbling past it into
  // whichever ancestor *does* have one — e.g. a dual-role namespace/proposition further up the
  // tree — silently selecting that ancestor instead of doing nothing. Always stopping propagation
  // here, then deciding separately whether to call `onSelect`, preserves both: nothing is selected
  // for a bare namespace, and no click leaks past it either.
  const handleClick = (event: MouseEvent<HTMLLIElement>): void => {
    event.stopPropagation();
    activate();
  };

  // The accessible name is composed from this node's own segment plus its origin/quarantine state
  // — not the segment alone — because those badges (compiled/overridden/authored, quarantined)
  // are this task's whole deliverable, and an explicit `aria-label` overrides element content
  // entirely in ARIA name computation. Without this, a screen reader announces only the segment
  // and never the state the visible badges carry. (Scoping the name to this node at all — rather
  // than leaving it to default content-based computation — is still necessary: without it, a
  // treeitem's accessible name aggregates its whole subtree, so a namespace's name would include
  // every leaf nested beneath it and collide with queries for those leaves.)
  const accessibleName = entry
    ? [node.segment, ORIGIN_LABEL[entry.origin], quarantined ? 'quarantined' : null]
        .filter((part): part is string => part !== null)
        .join(' ')
    : node.segment;

  return (
    <li
      id={rowId(node.path)}
      data-path={node.path}
      role="treeitem"
      aria-label={accessibleName}
      // `aria-selected="false"` means "selectable, but not selected" — a claim a bare namespace
      // must not make. Omitting it is what says "not selectable"; being reachable by the arrow keys
      // is a different question, which is why every row carries a tabindex and only some of these.
      aria-selected={entry ? entry.name === selected : undefined}
      // Always expanded, and honestly so: nothing here collapses, so there is no state to toggle.
      aria-expanded={node.children.length > 0 ? true : undefined}
      // The roving tabindex: the tree is one stop in the tab sequence, and every other row is
      // reachable only from inside it. A bare namespace is included — a tree navigates its
      // structure, and a namespace the arrow keys could not reach would be a hole in it.
      tabIndex={node.path === stop ? 0 : -1}
      className="explorer-node"
      style={{ '--depth': depth } as CSSProperties}
      title={quarantined ? quarantine.map((error) => error.message).join('\n') : undefined}
      onClick={handleClick}
    >
      <span className={entry ? 'explorer-leaf' : 'explorer-namespace'}>
        <span className="explorer-segment">{node.segment}</span>
        {entry && (
          <>
            <span className="model-pill">{entry.modelType}</span>
            <span className="origin-badge">{ORIGIN_LABEL[entry.origin]}</span>
            {quarantined && <span className="quarantine-badge">quarantined</span>}
          </>
        )}
      </span>

      {node.children.length > 0 && (
        <ul role="group">
          {node.children.map((child) => (
            <TreeNode
              key={child.path}
              node={child}
              depth={depth + 1}
              selected={selected}
              stop={stop}
              rowId={rowId}
              onSelect={onSelect}
            />
          ))}
        </ul>
      )}
    </li>
  );
}
