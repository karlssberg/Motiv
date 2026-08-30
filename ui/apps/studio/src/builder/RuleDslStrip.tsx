import { useEffect, useMemo, useRef } from 'react';
import {
  focusedPath, parse, printInline,
  type HighlightModel, type RuleNode, type SourceRange,
} from '@motiv-rules/core';

/** One run of text that carries the same set of marks throughout. */
interface Segment {
  key: string;
  value: string;
  hover: boolean;
  selected: boolean;
}

/**
 * Cuts `text` at every mark boundary, so each resulting run is uniformly inside or outside each
 * mark. Doing it this way — rather than nesting elements — is what lets a hover mark sit inside a
 * selection mark without either element having to contain the other.
 */
function segmentize(
  text: string, hover: SourceRange | null, selected: SourceRange | null,
): Segment[] {
  const cuts = new Set<number>([0, text.length]);
  for (const range of [hover, selected]) {
    if (!range) continue;
    // Clamp: keeps this function total against any range, in bounds or not, rather than relying
    // on every caller to have already validated one.
    cuts.add(Math.max(0, Math.min(range.from, text.length)));
    cuts.add(Math.max(0, Math.min(range.to, text.length)));
  }
  const bounds = [...cuts].sort((a, b) => a - b);

  const covers = (range: SourceRange | null, from: number): boolean =>
    range !== null && from >= range.from && from < range.to;

  const segments: Segment[] = [];
  for (let i = 0; i < bounds.length - 1; i += 1) {
    const from = bounds[i]!;
    const to = bounds[i + 1]!;
    // No emptiness check needed: bounds comes from a sorted Set, so consecutive values strictly increase.
    segments.push({
      key: `seg-${from}`,
      value: text.slice(from, to),
      hover: covers(hover, from),
      selected: covers(selected, from),
    });
  }
  return segments;
}

/**
 * The permanent one-line DSL rendering of the whole rule, marking the span of the hovered and
 * selected nodes.
 *
 * The tree shows structure but destroys reading order and precedence; this line shows reading order
 * but hides structure. Neither alone says why a rule means what it means, so the correspondence
 * between them is the point — hovering a grouped subtree lights up its parentheses, which is
 * exactly what the indented tree cannot express.
 *
 * Spans are obtained by printing the rule and reparsing it. That is sound rather than expedient:
 * the printer guarantees `parse(printInline(node))` deep-equals `node`, so the reparse recovers the
 * same tree, and Studio's DSL pane derives its own spans the same way. Memoised on rule identity,
 * so a hover costs no work at all.
 */
export function RuleDslStrip(props: {
  rule: RuleNode;
  highlight: HighlightModel;
  /**
   * Names the generated text so something else can point at it. The builder does: this line *is*
   * the composition's accessible description, so the tree describes itself by it rather than
   * duplicating the string into an `aria-description` that could then disagree with what is shown.
   */
  textId?: string;
}) {
  const { rule, highlight } = props;

  const { text, spans } = useMemo(() => {
    const printed = printInline(rule);
    const result = parse(printed);
    // The round-trip that licenses this reparse has a documented hole: the DSL has no string
    // escapes, so a `name` carrying a double quote prints text the parser cannot read back
    // (`printer.ts`). What comes out then is not *no* spans but *damaged* ones — for
    // `{and: [{spec: 'a', name: 'x"y'}, {spec: 'b'}]}` the two operand spans vanish and only a
    // truncated `$.rule` survives, so both operands would resolve to the same wrong range.
    // Dropping the lot degrades honestly: the expression still renders, nothing is marked.
    return { text: printed, spans: result.errors.length > 0 ? [] : result.spans };
  }, [rule]);

  /**
   * The span recorded for exactly this path, or `null`.
   *
   * Deliberately not `rangeOfPath`. Its ancestor fallback is right for the linter it was written
   * for — a diagnostic on `$.rule.whenTrue` should anchor on the node that owns it — and exactly
   * wrong here, because the two callers mean opposite things by a missing span. Nothing reconciles
   * the highlight model against a document edit, so a selection can outlive the node it names:
   * select the last operand of an `and`, remove it, and `$.rule.and[2]` no longer exists. Walking
   * up to `$.rule` would then underline the *entire rule* as selected, while no row shows as
   * selected — the strip claiming something the tree contradicts.
   */
  const range = (path: string | null): SourceRange | null => {
    if (path === null) return null;
    const span = spans.find((candidate) => candidate.path === path);
    return span ? { from: span.from, to: span.to } : null;
  };

  const segments = segmentize(text, range(highlight.hoveredPath), range(highlight.selectedPath));

  // Keep the mark the user just moved to in view. Scrolling to the *most recently changed* of the
  // two needs no tie-break: `focus` already records which that was.
  const scrollTarget = useRef<HTMLSpanElement | null>(null);
  const focus = focusedPath(highlight);
  useEffect(() => {
    scrollTarget.current?.scrollIntoView({ block: 'nearest', inline: 'nearest' });
  }, [focus, text]);

  const focusIsHover = highlight.focus === 'hover';
  // The focused mark can span several segments: a selected descendant splits its hovered
  // ancestor's range into three, all carrying the hover flag. One ref object assigned to
  // several elements leaves whichever React commits last in `.current`, which is arbitrary —
  // so the ref goes on the first segment of the mark, deliberately.
  const scrollIndex = segments.findIndex((segment) => (focusIsHover ? segment.hover : segment.selected));

  return (
    <div className="dsl-strip">
      <span className="dsl-strip-label">rule</span>
      {/* A bare `<span>` has no implicit ARIA role — it computes to `generic`, and `generic` is
          one of the roles ARIA prohibits from having an accessible name, so `aria-label` on a
          roleless span is silently dropped from the accessibility tree rather than exposed.
          `role="group"` gives the element a role that does support naming, so the label the
          existing `getByLabelText('rule expression')` queries actually resolves. */}
      <span className="dsl-strip-text" id={props.textId} role="group" aria-label="rule expression">
        {segments.map((segment, index) => {
          const marks = [
            segment.selected ? 'dsl-strip-selected' : null,
            segment.hover ? 'dsl-strip-hover' : null,
          ].filter(Boolean).join(' ');
          return (
            <span
              key={segment.key}
              className={marks || undefined}
              ref={index === scrollIndex ? scrollTarget : undefined}
            >
              {segment.value}
            </span>
          );
        })}
      </span>
    </div>
  );
}
