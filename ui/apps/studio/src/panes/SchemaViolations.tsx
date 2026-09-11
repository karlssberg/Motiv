import type { SchemaViolation } from '@motiv-rules/core';

/**
 * What the catalog's model schema rejected, before anything was sent.
 *
 * Rendered identically wherever a pane enforces a model schema — Evaluate and Checkout both do —
 * so it lives here rather than as two copies that would have to be kept saying the same thing.
 * The list announces itself when it arrives — it is rendered only after a button was pressed, and
 * a rejection nobody is told about is a rejection nobody reads. The `role="alert"` sits on a
 * wrapper rather than on each `<li>`, because a role replaces the element's own: an `<li>` that is
 * an alert is no longer a list item, which leaves the `<ul>` directly containing something that is
 * not one and fails 1.3.1. Announcing the group also reads better — one alert naming every
 * violation, rather than one per row racing the others.
 *
 * Deliberately not shared with the document's own validation errors, despite the near-identical
 * markup: those are `ValidationError`s about the rule being authored, carry a code, and are
 * labelled as something else — one list of two kinds of thing would have to explain which is which.
 */
export function SchemaViolations(props: { violations: SchemaViolation[] }) {
  if (props.violations.length === 0) return null;

  return (
    <div role="alert">
      <ul aria-label="schema violations" className="errors">
        {props.violations.map((violation, index) => (
          <li key={`${violation.path}-${index}`} className="error">
            {violation.path}: {violation.message}
          </li>
        ))}
      </ul>
    </div>
  );
}
