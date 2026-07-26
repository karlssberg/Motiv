import { afterEach, describe, expect, it } from 'vitest';
import { EditorView } from '@codemirror/view';
import { payloadChips, setPayloadTargets, type PayloadTarget } from '../../src/dsl/payloadChips.js';

const DOC = 'is-active && is-verified';
const IS_ACTIVE: PayloadTarget = { path: '$.rule.andAlso[0]', spec: 'is-active', from: 0, to: 9 };
const IS_VERIFIED: PayloadTarget = { path: '$.rule.andAlso[1]', spec: 'is-verified', from: 13, to: 24 };

let view: EditorView | null = null;

/** An editor carrying the chips extension, reporting whatever a chip opens. */
function mountEditor(doc = DOC) {
  const opened: PayloadTarget[] = [];
  view = new EditorView({
    doc,
    extensions: [payloadChips((target) => opened.push(target))],
    parent: document.body,
  });
  return { view, opened };
}

/** Clicks the nth chip, as a user pressing it would. */
function clickChip(instance: EditorView, index: number): void {
  const chip = instance.dom.querySelectorAll('.dsl-chip')[index];
  if (!chip) throw new Error(`No chip at index ${index}.`);
  chip.dispatchEvent(new MouseEvent('click', { bubbles: true }));
}

afterEach(() => {
  view?.destroy();
  view = null;
});

describe('payloadChips', () => {
  it('draws a chip for each target', () => {
    const { view: instance } = mountEditor();

    instance.dispatch({ effects: setPayloadTargets.of([IS_ACTIVE, IS_VERIFIED]) });

    const labels = [...instance.dom.querySelectorAll('.dsl-chip')]
      .map((chip) => chip.getAttribute('aria-label'));
    expect(labels).toEqual(['Edit is-active payload', 'Edit is-verified payload']);
  });

  // The chip is revealed by hovering the name it belongs to, so each spec token is marked as its
  // own hover target. Without that the only thing to hang `:hover` on is the whole line, which
  // lights up every chip on it at once.
  it('marks each spec token as its own hover target', () => {
    const { view: instance } = mountEditor();

    instance.dispatch({ effects: setPayloadTargets.of([IS_ACTIVE, IS_VERIFIED]) });

    const marked = [...instance.dom.querySelectorAll('.dsl-spec-hit')].map((el) => el.textContent);
    expect(marked).toEqual(['is-active', 'is-verified']);
  });

  // The stylesheet reveals a chip with `.dsl-spec-hit:hover .dsl-chip`, so a chip drawn outside
  // its token's span would never light up — a contract CSS cannot assert for itself.
  it('draws each chip inside its own spec token', () => {
    const { view: instance } = mountEditor();

    instance.dispatch({ effects: setPayloadTargets.of([IS_ACTIVE, IS_VERIFIED]) });

    const chips = [...instance.dom.querySelectorAll('.dsl-spec-hit')]
      .map((el) => el.querySelector('.dsl-chip')?.getAttribute('aria-label'));
    expect(chips).toEqual(['Edit is-active payload', 'Edit is-verified payload']);
  });

  it('opens the node its chip was drawn for', () => {
    const { view: instance, opened } = mountEditor();

    instance.dispatch({ effects: setPayloadTargets.of([IS_ACTIVE, IS_VERIFIED]) });
    clickChip(instance, 1);

    expect(opened).toEqual([IS_VERIFIED]);
  });

  // CodeMirror reuses a widget's DOM — the listener included — for any widget that compares equal
  // to it. A chip that answered to its node alone would therefore keep the offsets it was first
  // drawn with, and open its card anchored to wherever the token used to be.
  it('opens a moved node at where it has moved to', () => {
    const { view: instance, opened } = mountEditor();

    instance.dispatch({ effects: setPayloadTargets.of([IS_ACTIVE, IS_VERIFIED]) });
    instance.dispatch({
      changes: { from: 9, insert: '    ' },
      effects: setPayloadTargets.of([IS_ACTIVE, { ...IS_VERIFIED, from: 17, to: 28 }]),
    });
    clickChip(instance, 1);

    expect(opened).toEqual([{ ...IS_VERIFIED, from: 17, to: 28 }]);
  });

  // Targets are measured against the text that was parsed, which can lag the buffer by an edit.
  it('drops targets that no longer fit the document', () => {
    const { view: instance } = mountEditor('is-active');

    instance.dispatch({ effects: setPayloadTargets.of([IS_ACTIVE, IS_VERIFIED]) });

    expect(instance.dom.querySelectorAll('.dsl-chip')).toHaveLength(1);
  });
});
