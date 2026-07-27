import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { EMPTY_HIGHLIGHT, setHovered, setSelected } from '../../src/builder/highlight.js';
import { RuleDslStrip } from '../../src/builder/RuleDslStrip.js';

const rule = { and: [{ spec: 'a' }, { or: [{ spec: 'b' }, { spec: 'c' }] }] };

describe('RuleDslStrip', () => {
  it('renders the whole rule as one line of DSL', () => {
    render(<RuleDslStrip rule={rule} highlight={EMPTY_HIGHLIGHT} />);
    expect(screen.getByLabelText('rule expression').textContent).toBe('a & (b | c)');
  });

  it('marks nothing when nothing is hovered or selected', () => {
    const { container } = render(<RuleDslStrip rule={rule} highlight={EMPTY_HIGHLIGHT} />);
    expect(container.querySelectorAll('.dsl-strip-hover, .dsl-strip-selected')).toHaveLength(0);
  });

  it('fills the hovered subtree span, parentheses included', () => {
    const { container } = render(
      <RuleDslStrip rule={rule} highlight={setHovered(EMPTY_HIGHLIGHT, '$.rule.and[1]')} />,
    );
    const marked = [...container.querySelectorAll('.dsl-strip-hover')].map((el) => el.textContent).join('');
    expect(marked).toBe('(b | c)');
  });

  it('underlines the selected subtree span', () => {
    const { container } = render(
      <RuleDslStrip rule={rule} highlight={setSelected(EMPTY_HIGHLIGHT, '$.rule.and[0]')} />,
    );
    const marked = [...container.querySelectorAll('.dsl-strip-selected')].map((el) => el.textContent).join('');
    expect(marked).toBe('a');
  });

  it('renders both marks at once, nesting the hover inside the selection', () => {
    const highlight = setHovered(setSelected(EMPTY_HIGHLIGHT, '$.rule.and[1]'), '$.rule.and[1].or[0]');
    const { container } = render(<RuleDslStrip rule={rule} highlight={highlight} />);
    expect([...container.querySelectorAll('.dsl-strip-selected')].map((el) => el.textContent).join(''))
      .toBe('(b | c)');
    expect([...container.querySelectorAll('.dsl-strip-hover')].map((el) => el.textContent).join(''))
      .toBe('b');
  });

  it('never drops or duplicates text, whatever the marks', () => {
    const highlight = setHovered(setSelected(EMPTY_HIGHLIGHT, '$.rule.and[1]'), '$.rule.and[0]');
    render(<RuleDslStrip rule={rule} highlight={highlight} />);
    expect(screen.getByLabelText('rule expression').textContent).toBe('a & (b | c)');
  });

  it('marks a selected descendant nested inside its hovered ancestor', () => {
    // The mirror of the case above: here the hover is the wider range, so the selection
    // splits it into three segments. Covers the rendering; the scroll ref landing on
    // exactly one of those segments is a code property, not an observable one.
    const highlight = setHovered(setSelected(EMPTY_HIGHLIGHT, '$.rule.and[1].or[0]'), '$.rule.and[1]');
    const { container } = render(<RuleDslStrip rule={rule} highlight={highlight} />);

    expect([...container.querySelectorAll('.dsl-strip-hover')].map((el) => el.textContent).join(''))
      .toBe('(b | c)');
    expect([...container.querySelectorAll('.dsl-strip-selected')].map((el) => el.textContent).join(''))
      .toBe('b');
    expect(screen.getByLabelText('rule expression').textContent).toBe('a & (b | c)');
  });
  /**
   * A highlight path outlives the node it names: nothing reconciles the hover/selection model
   * against a document edit. These pin that a path with no span of its own marks *nothing*,
   * rather than resolving through `rangeOfPath`'s ancestor fallback — which is correct for the
   * linter it was written for (anchoring a `$.rule.whenTrue` diagnostic on its owning node) and
   * exactly wrong here, where a missing span means the node has ceased to exist.
   */
  describe('a path that no longer resolves', () => {
    it('marks nothing when the selected operand has been removed', () => {
      const shrunk = { and: [{ spec: 'a' }, { spec: 'b' }] };
      const highlight = setSelected(EMPTY_HIGHLIGHT, '$.rule.and[2]');

      const { container } = render(<RuleDslStrip rule={shrunk} highlight={highlight} />);

      expect(container.querySelectorAll('.dsl-strip-selected')).toHaveLength(0);
      expect(screen.getByLabelText('rule expression').textContent).toBe('a & b');
    });

    it('marks nothing when removal collapsed the parent to its survivor', () => {
      const collapsed = { spec: 'a' };
      const highlight = setHovered(EMPTY_HIGHLIGHT, '$.rule.and[1]');

      const { container } = render(<RuleDslStrip rule={collapsed} highlight={highlight} />);

      expect(container.querySelectorAll('.dsl-strip-hover')).toHaveLength(0);
      expect(screen.getByLabelText('rule expression').textContent).toBe('a');
    });
  });

  /**
   * The strip prints the rule and reparses it for spans, which rests on the printer's round-trip
   * guarantee. That guarantee has a documented hole: the DSL has no string escapes, so a name
   * carrying a double quote prints text the parser cannot read back. The spans that come out are
   * damaged rather than absent, so trusting them silently mis-marks.
   */
  describe('a rule the printer cannot round-trip', () => {
    const unquotable = { and: [{ spec: 'a', name: 'x"y' }, { spec: 'b' }] };

    it('marks nothing rather than trusting spans from a failed parse', () => {
      const highlight = setHovered(EMPTY_HIGHLIGHT, '$.rule.and[0]');

      const { container } = render(<RuleDslStrip rule={unquotable} highlight={highlight} />);

      expect(container.querySelectorAll('.dsl-strip-hover')).toHaveLength(0);
    });

    it('still renders the whole expression, so the strip degrades rather than blanking', () => {
      render(<RuleDslStrip rule={unquotable} highlight={EMPTY_HIGHLIGHT} />);

      expect(screen.getByLabelText('rule expression').textContent).toBe('a as "x\\"y" & b');
    });
  });
});
