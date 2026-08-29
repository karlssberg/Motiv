import { describe, it, expect } from 'vitest';
import type { EditorView, Tooltip } from '@codemirror/view';
import type { Diagnostic } from '@codemirror/lint';
import { diagnosticTooltipSource, renderDiagnostic } from '../../src/dsl/hover.js';

/** The tooltip source never touches the view, so a bare stand-in suffices. */
const VIEW = {} as EditorView;

const DIAGNOSTIC: Diagnostic = {
  from: 13,
  to: 24,
  severity: 'error',
  source: '$.rule.andAlso[1]',
  message: "UnknownSpec: 'is-nonsense' is not a registered spec",
};

/** Runs the source at `pos`, asserting it produced a single tooltip. */
function tooltipAt(diagnostics: Diagnostic[], pos: number): Tooltip | null {
  const result = diagnosticTooltipSource(() => diagnostics)(VIEW, pos, 1);
  expect(result).not.toBeInstanceOf(Promise);
  expect(Array.isArray(result)).toBe(false);
  return result as Tooltip | null;
}

/** The rendered DOM for a diagnostic's tooltip. */
function domFor(diagnostic: Diagnostic): HTMLElement {
  return renderDiagnostic(diagnostic);
}

describe('diagnosticTooltipSource', () => {
  it('returns null when no diagnostic covers the position', () => {
    expect(tooltipAt([DIAGNOSTIC], 4)).toBeNull();
  });

  it('returns null when there are no diagnostics at all', () => {
    expect(tooltipAt([], 13)).toBeNull();
  });

  it('anchors the tooltip on the covering diagnostic', () => {
    const tooltip = tooltipAt([DIAGNOSTIC], 18);
    expect(tooltip).toMatchObject({ pos: 13, end: 24 });
  });

  it('covers both ends of the diagnostic range', () => {
    expect(tooltipAt([DIAGNOSTIC], 13)).not.toBeNull();
    expect(tooltipAt([DIAGNOSTIC], 24)).not.toBeNull();
    expect(tooltipAt([DIAGNOSTIC], 25)).toBeNull();
  });

  it('reads the diagnostics afresh on every hover', () => {
    let diagnostics: Diagnostic[] = [];
    const source = diagnosticTooltipSource(() => diagnostics);
    expect(source(VIEW, 18, 1)).toBeNull();

    diagnostics = [DIAGNOSTIC];
    expect(source(VIEW, 18, 1)).not.toBeNull();
  });

  it('builds its DOM from the diagnostic', () => {
    const tooltip = tooltipAt([DIAGNOSTIC], 18)!;
    const dom = tooltip.create(VIEW).dom;
    expect(dom.querySelector('.dsl-hover-code')?.textContent).toBe('UnknownSpec');
  });
});

describe('renderDiagnostic', () => {
  it('renders the code, the message and the path', () => {
    const dom = domFor(DIAGNOSTIC);
    expect(dom.className).toBe('dsl-hover');
    expect(dom.querySelector('.dsl-hover-code')?.textContent).toBe('UnknownSpec');
    expect(dom.querySelector('.dsl-hover-message')?.textContent).toBe(
      "'is-nonsense' is not a registered spec",
    );
    expect(dom.querySelector('code.dsl-hover-path')?.textContent).toBe('$.rule.andAlso[1]');
  });

  it('omits the path when the diagnostic has no source', () => {
    const dom = domFor({ from: 0, to: 1, severity: 'error', message: 'UnclosedGroup: nope' });
    expect(dom.querySelector('.dsl-hover-path')).toBeNull();
    expect(dom.querySelector('.dsl-hover-message')?.textContent).toBe('nope');
  });

  it('renders an uncoded message as-is', () => {
    const dom = domFor({ from: 0, to: 1, severity: 'error', message: 'something odd' });
    expect(dom.querySelector('.dsl-hover-code')).toBeNull();
    expect(dom.querySelector('.dsl-hover-message')?.textContent).toBe('something odd');
  });
});
