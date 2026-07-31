import type { ReactNode } from 'react';
import type { Page } from '../routing/useHashRoute.js';

/** The pages, in the order they are offered. */
const PAGES: ReadonlyArray<{ id: Page; label: string }> = [
  { id: 'rules', label: 'Rules' },
  { id: 'propositions', label: 'Propositions' },
];

/**
 * The shell's top bar: brand, page tabs, then whatever breadcrumb trail the current page supplies,
 * and its controls on the right. Extracted from RuleHeader so both pages share one chrome rather
 * than growing two that drift apart.
 */
export function AppBar(props: {
  page: Page;
  onNavigate: (page: Page) => void;
  controls?: ReactNode;
  children?: ReactNode;
}) {
  return (
    <header className="appbar">
      <div className="appbar-brand">
        <span className="appbar-mark" aria-hidden="true">M</span>
        <span className="appbar-wordmark">Motiv</span>
      </div>
      <div className="page-tabs" role="tablist" aria-label="Page">
        {PAGES.map(({ id, label }) => (
          <button
            key={id}
            type="button"
            role="tab"
            aria-selected={props.page === id}
            className={props.page === id ? 'tab active' : 'tab'}
            onClick={() => props.onNavigate(id)}
          >
            {label}
          </button>
        ))}
      </div>
      {props.children}
      <div className="appbar-fill" />
      <div className="appbar-controls">{props.controls}</div>
    </header>
  );
}
