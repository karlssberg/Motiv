import { hoverTooltip } from '@codemirror/view';
import type { Extension } from '@codemirror/state';
import type { HoverTooltipSource } from '@codemirror/view';
import type { Diagnostic } from '@codemirror/lint';
import { splitDiagnosticMessage, type PlacedFact } from './lint.js';

/** Appends a `tag` element carrying `text` under `className`, when there is text to show. */
function appendLine(
  root: HTMLElement,
  tag: string,
  className: string,
  text: string | undefined,
): void {
  if (!text) return;
  const element = document.createElement(tag);
  element.className = className;
  element.textContent = text;
  root.appendChild(element);
}

/** Renders a diagnostic as the tooltip's DOM: its code, its message, and its node path. */
export function renderDiagnostic(diagnostic: Diagnostic): HTMLElement {
  const root = document.createElement('div');
  root.className = 'dsl-hover';

  const { code, message } = splitDiagnosticMessage(diagnostic.message);
  appendLine(root, 'div', 'dsl-hover-code', code);
  appendLine(root, 'div', 'dsl-hover-message', message);
  appendLine(root, 'code', 'dsl-hover-path', diagnostic.source);

  return root;
}

/**
 * A hover source describing whichever diagnostic covers the hovered position. Diagnostics are
 * read through a getter so the tooltip always reflects the latest lint pass.
 */
export function diagnosticTooltipSource(
  getDiagnostics: () => readonly Diagnostic[],
): HoverTooltipSource {
  return (_view, pos) => {
    const diagnostic = getDiagnostics().find(({ from, to }) => pos >= from && pos <= to);
    if (!diagnostic) return null;

    return {
      pos: diagnostic.from,
      end: diagnostic.to,
      above: true,
      create: () => ({ dom: renderDiagnostic(diagnostic) }),
    };
  };
}

/** Renders a fact as the tooltip's DOM: a solved literal's type, or a warning's message. */
export function renderFact(fact: PlacedFact): HTMLElement {
  const root = document.createElement('div');
  root.className = 'dsl-hover';
  if (fact.isWarning) {
    appendLine(root, 'div', 'dsl-hover-code', 'warning');
    appendLine(root, 'div', 'dsl-hover-message', fact.message ?? '');
  } else {
    appendLine(root, 'div', 'dsl-hover-message', `${fact.text} as ${fact.type}`);
    appendLine(root, 'div', 'dsl-hover-path', fact.anchor ? `from ${fact.anchor}` : 'default — no model field fixed this type');
  }
  return root;
}

/**
 * A hover source describing whichever validation fact covers the hovered position. Facts are
 * read through a getter so the tooltip always reflects the latest validation pass.
 */
export function factTooltipSource(getFacts: () => readonly PlacedFact[]): HoverTooltipSource {
  return (_view, pos) => {
    // Prefer the narrowest fact, so a literal wins over the whole-leaf result fact that also covers it.
    const fact = [...getFacts()]
      .filter((f) => pos >= f.from && pos <= f.to)
      .sort((a, b) => (a.to - a.from) - (b.to - b.from))[0];
    if (!fact) return null;

    return {
      pos: fact.from,
      end: fact.to,
      above: true,
      create: () => ({ dom: renderFact(fact) }),
    };
  };
}

/** The editor extension showing diagnostic and validation-fact tooltips on hover. */
export function motivHover(
  getDiagnostics: () => readonly Diagnostic[],
  getFacts: () => readonly PlacedFact[] = () => [],
): Extension {
  return [hoverTooltip(diagnosticTooltipSource(getDiagnostics)), hoverTooltip(factTooltipSource(getFacts))];
}
