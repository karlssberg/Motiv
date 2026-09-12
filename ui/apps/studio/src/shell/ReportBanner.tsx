import type { ReactNode } from 'react';
import { IconRefresh, IconWarn } from './icons.js';

/**
 * A report raised against whatever the page currently holds: a conflicting version, or a failure
 * that only this banner would ever say. It sits below the app bar because both pages raise it and
 * the propositions page's own strip sits in the same place.
 *
 * One implementation for both channels and both pages, because they differ in what they say and
 * not in how they are read — a second copy is a second chance for one of them to stop being an
 * `alert`, and a report nobody hears is the defect this banner exists to fix.
 */
export function ReportBanner(props: {
  children: ReactNode;
  /** The way back, where the page has an identity to reload; omitted when there is nothing to. */
  onReload?: () => void;
}) {
  return (
    <div role="alert" className="report-banner">
      <span className="report-icon"><IconWarn size={15} /></span>
      <span className="report-text">{props.children}</span>
      {props.onReload && (
        <button type="button" className="btn btn-secondary" onClick={props.onReload}>
          <IconRefresh size={14} />Reload latest
        </button>
      )}
    </div>
  );
}
