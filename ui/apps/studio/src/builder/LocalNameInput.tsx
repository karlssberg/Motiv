import { useLayoutEffect, useRef, type ChangeEvent, type FocusEvent } from 'react';
import { finishLocalName, isValidLocalName, normalizeLocalName } from '@motiv-rules/core';

/**
 * A definition-name text field: normalises as the user types, finishes on blur, and reports
 * whether the current value is a name that could actually be used.
 *
 * Normalising in `onChange` rather than validating and rejecting keystrokes keeps the field always
 * showing something the user typed — a space becomes the `-` it will end up as, and a character
 * outside the grammar just never appears, rather than the field going blank or beeping at them.
 * That has one cost: dropping characters shortens the string out from under the caret the browser
 * already placed, so the caret has to be recomputed from how much of the *prefix* up to the old
 * caret was itself dropped, and reapplied after React commits the shorter value — a plain
 * `setSelectionRange` during the change handler would be undone by the DOM update that follows it.
 */
export function LocalNameInput(props: {
  value: string;
  onChange: (next: string) => void;
  onCommit: () => void;
  ariaLabel: string;
  taken: ReadonlySet<string>;
}) {
  const { value, onChange, onCommit, ariaLabel, taken } = props;
  const inputRef = useRef<HTMLInputElement | null>(null);
  const pendingCaret = useRef<number | null>(null);

  // Applied after React has committed the normalised value, so it lands on top of the browser's
  // own (now-stale) caret placement rather than being overwritten by it.
  useLayoutEffect(() => {
    const caret = pendingCaret.current;
    if (caret === null) return;
    pendingCaret.current = null;
    inputRef.current?.setSelectionRange(caret, caret);
  });

  const handleChange = (event: ChangeEvent<HTMLInputElement>): void => {
    const raw = event.target.value;
    const caret = event.target.selectionStart ?? raw.length;
    const next = normalizeLocalName(raw);
    // The space case is length-preserving, so the caret is unchanged; every other rejection
    // shortens the string by however much of the *typed prefix up to the caret* was dropped.
    pendingCaret.current = normalizeLocalName(raw.slice(0, caret)).length;
    onChange(next);
  };

  const handleBlur = (event: FocusEvent<HTMLInputElement>): void => {
    const finished = finishLocalName(event.target.value);
    if (finished !== event.target.value) onChange(finished);
    onCommit();
  };

  const invalid = taken.has(value) || !isValidLocalName(value);
  const errorId = `${ariaLabel}-error`;

  return (
    <>
      <input
        ref={inputRef}
        className="control"
        type="text"
        aria-label={ariaLabel}
        aria-invalid={invalid}
        aria-describedby={invalid ? errorId : undefined}
        value={value}
        onChange={handleChange}
        onBlur={handleBlur}
      />
      {invalid && (
        <p id={errorId} className="field-error" role="alert">
          {taken.has(value) ? 'That name is already in use.' : 'Not a valid definition name.'}
        </p>
      )}
    </>
  );
}
