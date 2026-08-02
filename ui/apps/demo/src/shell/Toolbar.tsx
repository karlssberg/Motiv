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
}

/**
 * The shell's operations, as icons.
 *
 * Unavailable actions use `aria-disabled` and a handler that returns early, never the `disabled`
 * attribute — `disabled` removes a button from the tab order in every major browser, so a
 * keyboard screen-reader user cannot reach it and never hears the `aria-describedby` explaining
 * why it is unavailable.
 */
export function Toolbar(props: { actions: ToolbarAction[] }) {
  return (
    <div className="toolbar">
      {props.actions.map((action) => {
        const Icon = action.icon;
        const unavailable = action.unavailable !== undefined;
        const reasonId = `toolbar-${action.id}-reason`;
        return (
          <span key={action.id} className="toolbar-slot">
            <button
              type="button"
              className="ghost"
              aria-label={action.label}
              title={action.label}
              aria-disabled={unavailable ? true : undefined}
              aria-describedby={unavailable ? reasonId : undefined}
              onClick={() => { if (!unavailable) action.onActivate(); }}
            >
              <Icon />
            </button>
            {unavailable && <span id={reasonId} className="sr-only">{action.unavailable}</span>}
          </span>
        );
      })}
    </div>
  );
}
