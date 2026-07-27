import { useEffect, useMemo, useRef } from 'react';
import { parse, printInline, rangeOfPath, type RuleNode, type SourceRange } from '@motiv/rules-core';
import { focusedPath, type HighlightModel } from './highlight.js';

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
    // Clamp: a stale path can resolve past the end of a freshly reprinted expression.
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
    if (from === to) continue;
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
 * same tree, and the demo's DSL pane derives its own spans the same way. Memoised on rule identity,
 * so a hover costs no work at all.
 */
export function RuleDslStrip(props: { rule: RuleNode; highlight: HighlightModel }) {
  const { rule, highlight } = props;

  const { text, spans } = useMemo(() => {
    const printed = printInline(rule);
    return { text: printed, spans: parse(printed).spans };
  }, [rule]);

  const range = (path: string | null): SourceRange | null =>
    (path === null ? null : rangeOfPath(path, spans, text.length));

  const segments = segmentize(text, range(highlight.hoveredPath), range(highlight.selectedPath));

  // Keep the mark the user just moved to in view. Scrolling to the *most recently changed* of the
  // two needs no tie-break: `focus` already records which that was.
  const scrollTarget = useRef<HTMLSpanElement | null>(null);
  const focus = focusedPath(highlight);
  useEffect(() => {
    scrollTarget.current?.scrollIntoView({ block: 'nearest', inline: 'nearest' });
  }, [focus, text]);

  const focusIsHover = highlight.focus === 'hover';

  return (
    <div className="dsl-strip">
      <span className="dsl-strip-label">rule</span>
      <span className="dsl-strip-text" aria-label="rule expression">
        {segments.map((segment) => {
          const marks = [
            segment.selected ? 'dsl-strip-selected' : null,
            segment.hover ? 'dsl-strip-hover' : null,
          ].filter(Boolean).join(' ');
          const isTarget = focusIsHover ? segment.hover : segment.selected;
          return (
            <span
              key={segment.key}
              className={marks || undefined}
              ref={isTarget ? scrollTarget : undefined}
            >
              {segment.value}
            </span>
          );
        })}
      </span>
    </div>
  );
}
