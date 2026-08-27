/**
 * The builder's two independent view concerns, which an earlier revision conflated into one
 * `expanded` set. They want opposite defaults — structure open, detail closed — so no single
 * flag can serve both.
 *
 * `collapsed` is structural: which subtrees are folded into a single line of DSL.
 * `openPath` + `pinned` are the detail accordion: at most one *transient* panel, plus any
 * number of pinned ones that opening another node does not displace.
 *
 * Paths are keys, and they shift when an operand is removed (`$.rule.and[1]` becomes
 * `$.rule.and[0]`). Stale entries address nodes that no longer exist and are inert, matching
 * how the set this replaces already behaved. Nothing prunes them.
 */
export interface AccordionModel {
  readonly collapsed: ReadonlySet<string>;
  readonly openPath: string | null;
  readonly pinned: ReadonlySet<string>;
}

/** Every subtree expanded, every detail panel closed, nothing pinned. */
export const EMPTY_ACCORDION: AccordionModel = {
  collapsed: new Set(),
  openPath: null,
  pinned: new Set(),
};

function added(set: ReadonlySet<string>, value: string): ReadonlySet<string> {
  return new Set(set).add(value);
}

function removed(set: ReadonlySet<string>, value: string): ReadonlySet<string> {
  const next = new Set(set);
  next.delete(value);
  return next;
}

export function isCollapsed(model: AccordionModel, path: string): boolean {
  return model.collapsed.has(path);
}

export function isPinned(model: AccordionModel, path: string): boolean {
  return model.pinned.has(path);
}

/** A panel is open when it is the transient one or has been pinned open. */
export function isOpen(model: AccordionModel, path: string): boolean {
  return model.openPath === path || model.pinned.has(path);
}

/** Folds a subtree into DSL text, or unfolds it. Never touches detail state. */
export function toggleCollapsed(model: AccordionModel, path: string): AccordionModel {
  const collapsed = isCollapsed(model, path)
    ? removed(model.collapsed, path)
    : added(model.collapsed, path);
  return { ...model, collapsed };
}

/**
 * Opens a node's detail panel, displacing the previous transient — but never a pinned panel.
 * Toggling a pinned node closes *and* unpins it, so a panel is never pinned-but-closed.
 */
export function toggleOpen(model: AccordionModel, path: string): AccordionModel {
  if (model.pinned.has(path)) {
    return {
      ...model,
      pinned: removed(model.pinned, path),
      openPath: model.openPath === path ? null : model.openPath,
    };
  }
  return { ...model, openPath: model.openPath === path ? null : path };
}

/**
 * Pinning moves a panel out of the transient slot, so the next node opened does not displace it.
 * Unpinning hands it back to that slot rather than closing it — clicking a pin should never make
 * content vanish.
 */
export function togglePin(model: AccordionModel, path: string): AccordionModel {
  if (model.pinned.has(path)) {
    return { ...model, pinned: removed(model.pinned, path), openPath: path };
  }
  return {
    ...model,
    pinned: added(model.pinned, path),
    openPath: model.openPath === path ? null : model.openPath,
  };
}

/** Closes every detail panel, pinned or not. Structure is left as it is. */
export function closeAll(model: AccordionModel): AccordionModel {
  return { ...model, openPath: null, pinned: new Set() };
}
