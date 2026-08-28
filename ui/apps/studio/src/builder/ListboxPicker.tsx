import { useEffect, useId, type KeyboardEvent } from 'react';
import { usePopoverCard } from './usePopoverCard.js';

/** One entry on offer: the value it stands for, and the text that names it. */
export interface PickerOption<T extends string> {
  value: T;
  label: string;
}

/** Where the arrow keys land from `index`, or `null` for a key the list does not handle. */
function nextIndex(key: string, index: number, length: number): number | null {
  // The ends are stops rather than wrap-arounds, matching the native select this replaced.
  if (key === 'ArrowDown') return Math.min(index + 1, length - 1);
  if (key === 'ArrowUp') return Math.max(index - 1, 0);
  if (key === 'Home') return 0;
  if (key === 'End') return length - 1;
  return null;
}

/**
 * A value shown as text, doubling as the control that changes it.
 *
 * Wherever the value belongs to the prose around it — an operator inside an expression, a rule name
 * at the end of a breadcrumb — a native `<select>` is the wrong shape twice over: it reserves the
 * width of its longest option, leaving a hole after the short ones that reads as a missing word,
 * and it wears form-control chrome that shouts louder than the line it sits in. So the trigger is
 * drawn as the text it replaces, and is exactly as wide as the value it names.
 *
 * Losing the native control means owning what it gave away for free, which is the rest of this
 * file: the value is named as well as shown, focus opens onto the value in force, and the arrow
 * keys walk from there — since `listbox` promises that and a column of tab stops is not it.
 *
 * `listbox`/`option` rather than the `menu`/`menuitem` used for actions: this picks a value rather
 * than invoking something — and where the two live side by side they then stay distinguishable.
 *
 * `open` is owned by the caller so that a host holding several pickers can keep one popup at a time.
 */
export function ListboxPicker<T extends string>(props: {
  options: readonly PickerOption<T>[];
  /** The value in force: the list opens onto it, and marks it as the selected option. */
  value: T;
  onChoose: (value: T) => void;
  open: boolean;
  setOpen: (open: boolean) => void;
  /** What the trigger stands for, e.g. `rule`. The value in force is appended to it. */
  triggerName: string;
  listLabel: string;
  /** Typography for the trigger — the text it is drawn as. The reset and caret are this file's. */
  triggerClassName: string;
  /** Modifier on the popup card, on top of the shared `node-menu`. */
  listClassName: string;
}) {
  const { options, value, onChoose, open, setOpen } = props;
  const { trigger, card, style, placed, close } = usePopoverCard(open, setOpen);
  const current = options.findIndex((option) => option.value === value);
  const label = options[current]?.label ?? '';
  const listboxId = useId();

  const items = (): HTMLElement[] => Array.from(card.current?.querySelectorAll('[role="option"]') ?? []);

  // Opening puts the keyboard on the value in force, so the list starts where the value is.
  // `placed` is a dependency, not just a guard: until the card has been measured it is
  // `visibility: hidden`, and a hidden element refuses focus — silently, and only in a real
  // browser, since jsdom focuses it regardless. Without the re-run once it is placed, the one
  // attempt lands while it is still hidden and the keyboard never enters the list. Covered by
  // `e2e/operator.spec.ts`, which is the only harness that can see it.
  useEffect(() => {
    if (!open || !placed) return;
    items()[Math.max(current, 0)]?.focus();
  }, [open, placed]);

  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>): void => {
    const list = items();
    const target = nextIndex(event.key, list.indexOf(document.activeElement as HTMLElement), list.length);
    if (target === null) return;
    event.preventDefault();
    list[target]?.focus();
  };

  return (
    <>
      <button
        ref={trigger}
        type="button"
        role="combobox"
        className={`picker-trigger ${props.triggerClassName}`}
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-controls={listboxId}
        // The name restates the value, because it replaces the visible text for assistive tech —
        // name the trigger by its role alone and the one thing it exists to say is never heard.
        aria-label={`${props.triggerName}, ${label}`}
        onClick={() => setOpen(!open)}
      >
        {label}
      </button>
      {open && (
        <div
          ref={card}
          id={listboxId}
          role="listbox"
          className={`node-menu ${props.listClassName}`}
          style={style}
          aria-label={props.listLabel}
          onKeyDown={onKeyDown}
        >
          {options.map((option) => (
            <button
              key={option.value}
              type="button"
              role="option"
              aria-selected={option.value === value}
              className="node-menu-item"
              onClick={() => { onChoose(option.value); close(); }}
            >
              {option.label}
            </button>
          ))}
        </div>
      )}
    </>
  );
}
