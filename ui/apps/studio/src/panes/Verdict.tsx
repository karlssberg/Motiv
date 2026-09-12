import { IconCheck, IconCross } from '../shell/icons.js';

/**
 * A boolean outcome as a chip: a check or a cross, then the word. Both panes that run a rule —
 * Evaluate against a sample, Checkout against the live server — say their result this way, so a
 * reader learns one shape for "what happened" and finds it in the same place beside the button
 * that caused it.
 *
 * The glyph is hidden; the word carries the meaning, and the colour is a second channel rather
 * than the only one (WCAG 1.4.1).
 */
export function Verdict(props: { satisfied: boolean; text: string; label?: string }) {
  return (
    <p
      className={props.satisfied ? 'verdict' : 'verdict verdict-no'}
      {...(props.label !== undefined ? { 'aria-label': props.label } : {})}
    >
      {props.satisfied ? <IconCheck size={14} /> : <IconCross size={14} />}
      {props.text}
    </p>
  );
}

/**
 * The same outcome beside a single assertion: a check or a cross, and nothing to read. Decorative
 * by construction — the assertion it sits next to is the statement, so the glyph is hidden rather
 * than named twice (WCAG 1.4.1 again: colour is a second channel, not the only one).
 */
export function Tick(props: { satisfied: boolean }) {
  return (
    <span className={props.satisfied ? 'tick' : 'tick tick-no'} aria-hidden="true">
      {props.satisfied ? <IconCheck size={14} /> : <IconCross size={14} />}
    </span>
  );
}
