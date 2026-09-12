import type { ReactNode } from 'react';
import type { IconProps } from './icons.js';

/**
 * One toolbar action.
 *
 * `unavailable` carries the *reason* rather than a boolean, so the reason cannot be omitted:
 * there is no way to make an action unavailable without saying why.
 */
export interface ToolbarAction {
  id: string;
  /** The button's accessible name and its tooltip. A bare glyph teaches nothing on first sight. */
  label: string;
  icon: (props: IconProps) => JSX.Element;
  onActivate: () => void;
  /**
   * Why this action cannot be used right now. Absent — or explicitly `undefined`, which under
   * `exactOptionalPropertyTypes` is a separate thing to say and the natural end of a chain of
   * conditions — means it can.
   */
  unavailable?: string | undefined;
  /**
   * `'primary'` draws the action as a filled, worded button rather than a ghost glyph. One per
   * toolbar at most: it is for the action a page is *for* — Save — so the eye finds it without
   * reading the tooltips.
   */
  emphasis?: 'primary';
}

/**
 * How a toolbar button is drawn: filled for the action the page is *for*, a worded ghost where the
 * toolbar labels everything, and a bare glyph otherwise.
 */
function buttonClass(emphasis: 'primary' | undefined, worded: boolean): string {
  if (emphasis === 'primary') return 'btn';
  if (worded) return 'ghost ghost-labelled';
  return 'ghost';
}

/**
 * The shell's operations, as icons.
 *
 * Unavailable actions use `aria-disabled` and a handler that returns early, never the `disabled`
 * attribute — `disabled` removes a button from the tab order in every major browser, so a
 * keyboard screen-reader user cannot reach it and never hears the `aria-describedby` explaining
 * why it is unavailable.
 *
 * A labelled button carries its word as visible text and no `aria-label`: the name is stated once,
 * where it is seen, so it cannot drift from what is read out (WCAG 2.5.3, Label in Name). A bare
 * glyph has no visible word, so it carries the `aria-label` and a `title` instead.
 */
export function Toolbar(props: {
  actions: ToolbarAction[];
  /**
   * Show every action's word beside its glyph. For a surface met rarely — the palette's New /
   * Derive / Override / Delete — a label is recognition where a tooltip would be recall.
   */
  labelled?: boolean;
  /** Controls drawn after the actions, in the same row — the split Save button. */
  children?: ReactNode;
}) {
  return (
    <div className="toolbar">
      {props.actions.map((action) => {
        const Icon = action.icon;
        const unavailable = action.unavailable !== undefined;
        const reasonId = `toolbar-${action.id}-reason`;
        const worded = action.emphasis === 'primary' || props.labelled === true;
        return (
          <span key={action.id} className="toolbar-slot">
            <button
              type="button"
              className={buttonClass(action.emphasis, worded)}
              aria-label={worded ? undefined : action.label}
              title={worded ? undefined : action.label}
              aria-disabled={unavailable ? true : undefined}
              aria-describedby={unavailable ? reasonId : undefined}
              onClick={() => { if (!unavailable) action.onActivate(); }}
            >
              <Icon {...(worded ? { size: 14 } : {})} />
              {worded && action.label}
            </button>
            {unavailable && <span id={reasonId} className="sr-only">{action.unavailable}</span>}
          </span>
        );
      })}
      {props.children}
    </div>
  );
}
