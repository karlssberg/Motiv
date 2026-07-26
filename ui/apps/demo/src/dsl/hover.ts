import { hoverTooltip } from '@codemirror/view';
import type { Extension } from '@codemirror/state';
import type { HoverTooltipSource } from '@codemirror/view';
import type { Diagnostic } from '@codemirror/lint';
import { splitDiagnosticMessage } from './lint.js';

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

/** The editor extension showing diagnostic tooltips on hover. */
export function motivHover(getDiagnostics: () => readonly Diagnostic[]): Extension {
  return hoverTooltip(diagnosticTooltipSource(getDiagnostics));
}
