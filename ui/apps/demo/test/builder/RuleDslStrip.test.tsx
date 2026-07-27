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
});
