import type { ReactNode } from 'react';

/**
 * The document, named where it is edited: the editor pane's header rather than the top bar.
 *
 * The top bar used to carry a breadcrumb trail, a model pill and the loaded version beside the
 * brand and the page switch, and the two kinds of thing — *where am I in the app* and *what am I
 * editing* — competed for one row. Splitting them leaves the bar with navigation and actions,
 * and puts the document's identity on the pane that holds its content, which is where a reader
 * looks for it.
 *
 * Presentational: both pages own a different workflow and pass what it knows.
 */
export function DocumentTitle(props: {
  /** The name, already shaped: a plain string, or a breadcrumb of segments for a dotted name. */
  name: ReactNode;
  /** The model type the document is validated and evaluated against. */
  modelType: string;
  /** The loaded version, or absent for a document that has none yet. */
  version?: number | undefined;
  /** A short aside on the version — "code-defined default", say. */
  note?: string | undefined;
}) {
  return (
    <div className="doc-title">
      <span className="doc-name">{props.name}</span>
      <span className="kbd" aria-hidden="true">⌘K</span>
      <span className="model-pill" title="Model type the rule is validated and evaluated against">
        {props.modelType}
      </span>
      {props.version !== undefined && <span className="rule-version">v{props.version}</span>}
      {props.note !== undefined && <span className="doc-note">{props.note}</span>}
    </div>
  );
}
