import { hoverTooltip } from '@codemirror/view';
import type { Extension } from '@codemirror/state';
import type { HoverTooltipSource } from '@codemirror/view';
import type { Diagnostic } from '@codemirror/lint';
import { splitDiagnosticMessage } from './lint.js';

/** Appends a `<div>` carrying `text` under `className`, when there is text to show. */
function appendLine(root: HTMLElement, className: string, text: string): void {
  if (!text) return;
  const element = document.createElement('div');
  element.className = className;
  element.textContent = text;
  root.appendChild(element);
}

/** Renders a diagnostic as the tooltip's DOM: its code, its message, and its node path. */
export function renderDiagnostic(diagnostic: Diagnostic): HTMLElement {
  const root = document.createElement('div');
  root.className = 'dsl-hover';

  const { code, message } = splitDiagnosticMessage(diagnostic.message);
  appendLine(root, 'dsl-hover-code', code);
  appendLine(root, 'dsl-hover-message', message);

  if (diagnostic.source) {
    const path = document.createElement('code');
    path.className = 'dsl-hover-path';
    path.textContent = diagnostic.source;
    root.appendChild(path);
  }

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
    const diagnostic = getDiagnostics().find((candidate) => pos >= candidate.from && pos <= candidate.to);
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
