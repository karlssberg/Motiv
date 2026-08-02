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
      {/*
        These are page *navigation*, not tabs: activating one changes the route, and there is no
        tabpanel here for a tab to control. `role="tablist"` is a knowing approximation — the
        honest markup is a <nav> of anchors, whose hrefs would also give middle-click, open-in-new-
        tab, and a visible destination for free. Recorded rather than done because the swap changes
        behaviour the e2e suite and App.test.tsx both assert on. Note the divergence from
        EditorPane, one file over, which does implement the full pattern — its tabs really do
        switch a panel, so each carries `aria-controls` and the panel carries `aria-labelledby`;
        neither has any counterpart to point at from here.
      */}
      <div className="page-tabs" role="tablist" aria-label="Page">
        {PAGES.map(({ id, label }) => {
          const active = props.page === id;
          return (
            <button
              key={id}
              type="button"
              role="tab"
              aria-selected={active}
              className={active ? 'tab active' : 'tab'}
              onClick={() => props.onNavigate(id)}
            >
              {label}
            </button>
          );
        })}
      </div>
      {props.children}
      <div className="appbar-fill" />
      <div className="appbar-controls">{props.controls}</div>
    </header>
  );
}
