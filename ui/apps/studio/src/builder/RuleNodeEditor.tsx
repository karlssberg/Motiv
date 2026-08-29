import { createContext, useContext, useMemo, useRef, type MouseEvent } from 'react';
import {
  accessibleExpression, childPaths, firstOperandTarget, insertTargetForRow, isBinaryNode,
  isCollapsed, isHigherOrderNode, isOpen, isPinned, planInsert, summarize,
  type AccordionModel, type Catalog, type HighlightModel,
} from '@motiv-rules/core';
import { useRuleEditorStore, useRuleNode } from '@motiv-rules/react';
import { NodeToolbar } from './NodeToolbar.js';
import { OperatorPicker } from './OperatorPicker.js';
import { QuantifierNode } from './QuantifierNode.js';
import { DecorationEditor } from './DecorationEditor.js';
import { NodeDsl } from './NodeDsl.js';
import { NodeMenu } from './NodeMenu.js';
import { NodeInsertButton } from './NodeInsertButton.js';
import { PendingSlot } from './PendingSlot.js';

/**
 * The tree-wide state shared by every {@link RuleNodeEditor} in the tree: accordion state (its
 * original scope) plus the catalog, hover/selection highlight, popover slot, and pending
 * insertion slot that have since joined it.
 */
export interface BuilderTreeState {
  model: AccordionModel;
  toggleCollapsed: (path: string) => void;
  toggleOpen: (path: string) => void;
  togglePin: (path: string) => void;
  /**
   * Which popup in the tree is open, if any, as a {@link popoverKey}. Held centrally so that
   * opening one closes the last — two at once is otherwise reachable by keyboard alone.
   */
  openPopover: string | null;
  setOpenPopover: (key: string | null) => void;
  catalog: Catalog;
  /** Which node the DSL strip marks, and which mark it scrolls to. */
  highlight: HighlightModel;
  setHovered: (path: string | null) => void;
  setSelected: (path: string | null) => void;
  /** The open insertion slot, if any: a row path plus which of that row's two positions. */
  pending: { path: string; where: 'after' | 'first' } | null;
  setPending: (pending: { path: string; where: 'after' | 'first' } | null) => void;
}

/** The popups a row can open. */
type PopoverKind = 'menu' | 'operator';

/** The two controls the caret is, which follows from whether its node has children. */
type CaretKind = 'children' | 'detail';

/** Identifies one row's popup, since a row has more than one. */
const popoverKey = (kind: PopoverKind, path: string): string => `${kind}:${path}`;

export const BuilderTreeContext = createContext<BuilderTreeState | null>(null);

export function useBuilderTree(): BuilderTreeState {
  const context = useContext(BuilderTreeContext);
  if (!context) throw new Error('RuleNodeEditor must be used within a BuilderTreeContext provider.');
  return context;
}

/** The id tying a node's detail toggle to the panel it opens. */
const panelId = (path: string): string => `detail-${path}`;

/** The id tying a parent's caret to the group of children it opens. */
const kidsId = (path: string): string => `operands-${path}`;

/**
 * Recursively renders a rule node.
 *
 * Two view concerns, deliberately independent. Structure folds a subtree into a single line of
 * DSL and back, and starts expanded. The **detail** panel holds the node's decoration fields and
 * edit controls, starts closed, and is displaced when another node is opened unless it has been
 * pinned. A node can be collapsed with its panel open, or the reverse.
 *
 * The caret reveals whatever is *inside* a node, so which concern it drives follows from the node
 * itself: a parent's insides are its children, and a leaf — having none — discloses its metadata
 * instead. That leaves the caret slot meaningful on every row rather than an inert bullet on
 * leaves, and spares a leaf the second control it would otherwise need. Only a parent, whose
 * caret is spoken for, carries the separate `⋯` toggle.
 *
 * The row body carries no interactive role of its own. It has to host a text editor once the
 * subtree is collapsed, and interactive content nested inside a button is invalid HTML that
 * swallows events — so the detail toggle is a sibling control rather than the row itself.
 */
export function RuleNodeEditor(props: { path: string; modelType: string }) {
  const { path, modelType } = props;
  const { node, errors } = useRuleNode(path);
  const {
    model, toggleCollapsed, toggleOpen, togglePin, openPopover, setOpenPopover, catalog,
    highlight, setHovered, setSelected, pending, setPending,
  } = useBuilderTree();
  const store = useRuleEditorStore();

  /**
   * Which of the caret's two roles was under the pointer when it was pressed — see
   * {@link releaseMatchesPress}. Declared up here with the other hooks rather than beside the two
   * closures that use it, which sit below the `!node` guard: a hook called after an early return
   * is a hook this component sometimes skips.
   */
  const pressedKind = useRef<CaretKind | null>(null);

  /**
   * The name of this row's operand group — see the group itself, below.
   *
   * Memoised because `printInline` walks the whole subtree, and every expanded parent in the tree
   * does it: unmemoised, one render of a fully-expanded tree prints every node once per ancestor.
   * `node` comes straight out of the document (`getNode`), so it changes identity only when the
   * document does, which is exactly when the printed text can differ.
   */
  const expression = useMemo(() => (node ? accessibleExpression(node) : ''), [node]);

  /** Binds one of this row's popups to the tree's single open slot. */
  const popover = (kind: PopoverKind): { open: boolean; setOpen: (next: boolean) => void } => ({
    open: openPopover === popoverKey(kind, path),
    setOpen: (next: boolean) => setOpenPopover(next ? popoverKey(kind, path) : null),
  });

  if (!node) return null;

  const kids = childPaths(node, path);
  const hasChildren = kids.length > 0;
  const collapsed = isCollapsed(model, path);
  /**
   * Whether this row's children are on screen — and so whether `.node-kids` exists to render into.
   * Named because the two sites that need it would otherwise spell it differently (`collapsed` in
   * one, `hasChildren && !collapsed` in the other) and be equivalent only via an invariant the
   * reader has to reconstruct: only a binary node can host a `'first'` slot, and a binary node
   * always has at least two operands.
   */
  const kidsMounted = hasChildren && !collapsed;
  const open = isOpen(model, path);
  const pinned = isPinned(model, path);
  const selected = highlight.selectedPath === path;
  // A leaf's tree form and its text form are the same string, so it has nothing to toggle
  // between and is always shown as DSL. Only the other case has a summary to render.
  const inDslView = !hasChildren || collapsed;
  const summary = inDslView ? null : summarize(node);

  /** Which of its two roles the caret is playing on this row, as one name rather than three. */
  const caretKind: CaretKind = hasChildren ? 'children' : 'detail';

  const pressCaret = (event: MouseEvent): void => {
    // Secondary buttons keep their usual meaning, as they do on the row's DSL text. Recording one
    // would also strand the kind, since no click follows to consume it.
    if (event.button !== 0) return;
    pressedKind.current = caretKind;
  };

  /**
   * Whether the click now arriving belongs to the press that began it, on the control it began on.
   *
   * The caret is two controls sharing one slot — a parent's subtree collapse, a leaf's metadata
   * disclosure — and a row can change from one to the other *while a gesture is in flight*.
   * Pressing a leaf's caret blurs an open editor, and that commit-on-blur can give the row children
   * before the press has become a click. The click then ran a control the user never aimed at,
   * collapsing a subtree the same gesture had only just created: the tree flashed open and shut,
   * and only ever on the first click, since a committed row's caret no longer re-kinds under the
   * pointer.
   *
   * So the press settles which control was meant, and a release that finds a different one does
   * nothing. Letting React's reconciliation drop the click instead — by keying the two buttons
   * apart so the swap becomes a remount — was tried and rejected: whether the click survives then
   * depends on how long the button is held, because a slow release finds the replacement sitting
   * in the same place and the browser retargets onto it. This does not depend on timing.
   */
  const releaseMatchesPress = (event: MouseEvent): boolean => {
    const pressed = pressedKind.current;
    pressedKind.current = null;
    // Keyboard activation is identified by what it *is*, not by the absence of a press: Enter and
    // Space report no click count, where every pointer click reports at least one. Testing for
    // absence instead would wave through any click that reached us without a press of its own —
    // including one retargeted onto a row that mounted mid-gesture, which is the very hazard this
    // guard exists for.
    return event.detail === 0 || pressed === caretKind;
  };

  // A quantifier's single child is scoped to the collection's element model type, not the parent's.
  const childModelType = isHigherOrderNode(node)
    ? (catalog.collections.find((c) => c.path === node.path)?.elementModelType ?? modelType)
    : modelType;

  // Renders the slot for `where`, targeting the position it names. A 'first' slot is scoped to
  // this row's children, since it becomes one of them; an 'after' slot is scoped to this row's
  // own siblings.
  const slotFor = (where: 'after' | 'first') => (
    <PendingSlot
      modelType={where === 'first' ? childModelType : modelType}
      catalog={catalog}
      onCommit={(inserted) => {
        const target = where === 'first' ? firstOperandTarget(path) : insertTargetForRow(path);
        store.applyPlan(planInsert(store.getState().document, target, inserted));
        setPending(null);
      }}
      onCancel={() => setPending(null)}
    />
  );

  return (
    <div className="node">
      <div
        className={selected ? 'node-row selected' : 'node-row'}
        onMouseEnter={() => setHovered(path)}
        onMouseLeave={() => setHovered(null)}
      >
        {caretKind === 'children' ? (
          <button
            type="button"
            className="node-chev"
            aria-expanded={!collapsed}
            // Dropped while collapsed, because the group is unmounted then and an IDREF to an
            // absent element is an invalid relationship rather than a harmless one — the same
            // rule the palette's `aria-controls` follows while it browses.
            aria-controls={kidsMounted ? kidsId(path) : undefined}
            aria-label={`${collapsed ? 'expand' : 'collapse'} ${path}`}
            onMouseDown={pressCaret}
            onClick={(event) => { if (releaseMatchesPress(event)) toggleCollapsed(path); }}
          >
            {collapsed ? '▸' : '▾'}
          </button>
        ) : (
          <button
            type="button"
            className="node-chev"
            aria-expanded={open}
            aria-controls={panelId(path)}
            aria-label={`details for ${path}`}
            onMouseDown={pressCaret}
            onClick={(event) => { if (releaseMatchesPress(event)) toggleOpen(path); }}
          >
            {open ? '▾' : '▸'}
          </button>
        )}
        <span className="node-body">
          {summary === null ? (
            <NodeDsl path={path} node={node} modelType={modelType} catalog={catalog} />
          ) : (
            <>
              {isBinaryNode(node) ? (
                <OperatorPicker path={path} node={node} {...popover('operator')} />
              ) : (
                <span className={`node-badge node-badge-${summary.kind}`}>{summary.badge}</span>
              )}
              {summary.description && <span className="node-desc">{summary.description}</span>}
              {node.name && <span className="node-name">as &quot;{node.name}&quot;</span>}
            </>
          )}
        </span>
        {/* Selection is its own control rather than a click on the row: the row body is a DSL
            editor that takes focus, and `.node-dsl` already claims click to start editing. A
            separate button also gives selection a tab stop and an accessible name, which is what
            the armed-move in Milestone 2 will need. */}
        <button
          type="button"
          className="node-select"
          aria-pressed={selected}
          aria-label={`select ${path}`}
          onClick={() => setSelected(selected ? null : path)}
        >
          ◈
        </button>
        <NodeInsertButton path={path} onOpen={() => setPending({ path, where: 'after' })} />
        <NodeMenu
          path={path}
          // Only an operand of an n-ary operator can be removed; a NOT's child or a quantifier's
          // body is the node's whole content, so removing it would leave the parent malformed.
          canRemove={path.endsWith(']')}
          {...popover('menu')}
          onDetails={() => toggleOpen(path)}
          {...(isBinaryNode(node) ? { onInsertFirst: () => setPending({ path, where: 'first' }) } : {})}
        />
        <button
          type="button"
          className={pinned ? 'node-pin pinned' : 'node-pin'}
          aria-pressed={pinned}
          aria-label={`${pinned ? 'unpin' : 'pin'} ${path}`}
          onClick={() => togglePin(path)}
        >
          📌
        </button>
      </div>
      {errors.length > 0 && (
        <span role="alert" className="error">{errors.map((e) => e.message).join('; ')}</span>
      )}
      {pending?.path === path && pending.where === 'after' && slotFor('after')}
      {/* A collapsed parent has no mounted `.node-kids` for the 'first' slot to join, so it
          renders here instead — the same fallback spot the 'after' slot always uses. */}
      {pending?.path === path && pending.where === 'first' && !kidsMounted && slotFor('first')}
      {open && (
        <div className="node-detail" id={panelId(path)}>
          {isHigherOrderNode(node) ? (
            <QuantifierNode path={path} node={node} catalog={catalog} modelType={modelType} />
          ) : (
            <NodeToolbar path={path} node={node} />
          )}
          <DecorationEditor path={path} node={node} />
        </div>
      )}
      {kidsMounted && (
        /*
          Ticket 18: nested labelled `group`s plus disclosure, never `role="tree"` — `tree` is a
          navigation pattern with a roving tabindex and single-focusable items, and every row here
          holds editable fields, a popover and a toolbar.

          The name is the node's own generated DSL text, which is the ticket's key move: the
          indentation and connecting lines a sighted reader takes the structure from convey nothing
          at all to a screen reader, and Motiv's whole thesis is that boolean structure linearises
          into readable text. So entering a group announces the composition it holds.
        */
        <div className="node-kids" role="group" id={kidsId(path)} aria-label={expression}>
          {pending?.path === path && pending.where === 'first' && slotFor('first')}
          {kids.map((childPath) => (
            <RuleNodeEditor key={childPath} path={childPath} modelType={childModelType} />
          ))}
        </div>
      )}
    </div>
  );
}
