import { Decoration, EditorView, WidgetType, type DecorationSet } from '@codemirror/view';
import { StateEffect, StateField, type Extension } from '@codemirror/state';

/** A spec node in the text, and where its token sits. */
export interface PayloadTarget {
  path: string;
  spec: string;
  /** Document position of the token's first character. */
  from: number;
  /** Document position just past the token's last character. */
  to: number;
}

/** Replaces the set of spec nodes chips are drawn for. */
export const setPayloadTargets = StateEffect.define<readonly PayloadTarget[]>();

/**
 * The affordance that opens a spec node's payload card. It is drawn as an empty button with an
 * accessible name — its glyph comes from CSS — so nothing it renders lands in `doc`-shaped reads
 * of the content, which is the text the buffer round-trips through the parser.
 */
class PayloadChip extends WidgetType {
  constructor(
    private readonly target: PayloadTarget,
    private readonly onOpen: (target: PayloadTarget) => void,
  ) {
    super();
  }

  /**
   * A chip *is* its target, offsets included. Comparing on the node alone would be enough to
   * draw the right button — but CodeMirror reuses the DOM of any widget that compares equal, and
   * that DOM carries the listener below, which answers with the target it was built from. A chip
   * that ignored the offsets would go on opening its card anchored to where the token used to be.
   */
  override eq(other: PayloadChip): boolean {
    const mine = this.target;
    const theirs = other.target;
    return theirs.path === mine.path && theirs.spec === mine.spec
      && theirs.from === mine.from && theirs.to === mine.to;
  }

  override toDOM(): HTMLElement {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'dsl-chip';
    button.title = 'Edit payload';
    button.setAttribute('aria-label', `Edit ${this.target.spec} payload`);
    button.addEventListener('click', (event) => {
      event.preventDefault();
      this.onOpen(this.target);
    });
    return button;
  }

  /** The chip is chrome, not text: a click on it belongs to it, not to the editor's selection. */
  override ignoreEvent(): boolean {
    return true;
  }
}

/**
 * The span a chip is revealed by hovering — the node's own text, and nothing else's.
 *
 * `inclusiveEnd` is what draws the chip *inside* this span rather than after it: a widget on the
 * boundary falls within a mark whose end is inclusive. That nesting is the point, since the only
 * other thing the stylesheet could hang `:hover` on is the line, which holds every chip on it.
 */
const hoverTarget = Decoration.mark({ class: 'dsl-spec-hit', inclusiveEnd: true });

/**
 * Marks every spec node with a chip that opens its payload card.
 *
 * Which nodes those are is pushed in with `setPayloadTargets` rather than derived from the
 * document here: the spans come from the parse the host already runs, so re-deriving them would
 * be a second parser in the editor, one edit out of step with the first.
 */
export function payloadChips(onOpen: (target: PayloadTarget) => void): Extension {
  // Each node gets two decorations: its text as the hover target, and the chip at the end of
  // that text, drawn inside it.
  //
  // Targets are measured against the text that was parsed, which can lag the buffer by an edit,
  // so any that no longer fit the document are dropped rather than allowed to throw. An empty
  // span is dropped with them: CodeMirror rejects a mark decoration that covers no text.
  const chipsFor = (targets: readonly PayloadTarget[], docLength: number): DecorationSet =>
    Decoration.set(
      targets
        .filter((target) => target.from < target.to && target.to <= docLength)
        .flatMap((target) => [
          hoverTarget.range(target.from, target.to),
          Decoration.widget({ widget: new PayloadChip(target, onOpen), side: -1 }).range(target.to),
        ]),
      true,
    );

  return StateField.define<DecorationSet>({
    create: () => Decoration.none,
    update(chips, transaction) {
      for (const effect of transaction.effects) {
        if (effect.is(setPayloadTargets)) return chipsFor(effect.value, transaction.newDoc.length);
      }
      // Between pushes the chips ride along with the text they annotate, so an edit does not
      // leave them pointing at the wrong column until the next parse arrives.
      return chips.map(transaction.changes);
    },
    provide: (field) => EditorView.decorations.from(field),
  });
}
