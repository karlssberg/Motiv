import type { SchemaViolation } from '@motiv/rules-core';

/**
 * What the catalog's model schema rejected, before anything was sent.
 *
 * Rendered identically wherever a pane enforces a model schema — Evaluate and Checkout both do —
 * so it lives here rather than as two copies that would have to be kept saying the same thing.
 * Each violation is its own `role="alert"`, which is what makes a list arriving after the button
 * was pressed announce itself rather than sit there silently.
 *
 * Deliberately not shared with the document's own validation errors, despite the near-identical
 * markup: those are `ValidationError`s about the rule being authored, carry a code, and are
 * labelled as something else — one list of two kinds of thing would have to explain which is which.
 */
export function SchemaViolations(props: { violations: SchemaViolation[] }) {
  if (props.violations.length === 0) return null;

  return (
    <ul aria-label="schema violations" className="errors">
      {props.violations.map((violation, index) => (
        <li key={`${violation.path}-${index}`} role="alert" className="error">
          {violation.path}: {violation.message}
        </li>
      ))}
    </ul>
  );
}
