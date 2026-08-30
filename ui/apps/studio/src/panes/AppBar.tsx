import type { ReactNode } from 'react';
import { formatHash, type Page } from '../routing/useHashRoute.js';
import { useAdminCapabilities } from '../shell/useAdminCapabilities.js';
import { IconAdmin, IconPropositions, IconRules, type IconProps } from '../shell/icons.js';

/** A page, as the switcher offers it. */
interface PageLink { id: Page; label: string; icon: (props: IconProps) => JSX.Element }

/** The pages, in the order they are offered. */
const PAGES: ReadonlyArray<PageLink> = [
  { id: 'rules', label: 'Rules', icon: IconRules },
  { id: 'propositions', label: 'Propositions', icon: IconPropositions },
];

/** Admin is not one of the base pages: it is offered only once capabilities confirm it. */
const ADMIN_PAGE: PageLink = { id: 'admin', label: 'Admin', icon: IconAdmin };

/**
 * The shell's top bar: brand, page navigation, then whatever breadcrumb trail the current page
 * supplies, and its controls on the right. Extracted from RuleHeader so both pages share one
 * chrome rather than growing two that drift apart.
 */
export function AppBar(props: {
  page: Page;
  controls?: ReactNode;
  children?: ReactNode;
}) {
  // Self-fetched rather than threaded down from App: AppBar is mounted fresh from three different
  // parents (RuleHeader, PropositionsPage, AdminPage), and each already re-fetches its own static
  // data on mount (catalog, listings) rather than sharing a cache — see useAdminCapabilities.
  const capabilities = useAdminCapabilities();
  const pages = capabilities.grantAdministration && capabilities.administrator
    ? [...PAGES, ADMIN_PAGE]
    : PAGES;

  return (
    <header className="appbar">
      <div className="appbar-brand">
        <span className="appbar-mark" aria-hidden="true">M</span>
        <span className="appbar-wordmark">Motiv</span>
      </div>
      {/*
        Page *navigation*, so anchors in a landmark rather than the `role="tablist"` this used to
        declare: activating one changes the route, and there is no tabpanel here for a tab to
        control. The href is the navigation itself — no click handler intercepts it — which is what
        gives middle-click, open-in-new-tab and a visible destination in the status bar. It is
        minted by `formatHash`, the same function the router parses back, so a link and the route it
        leads to cannot drift; a bare page carries no name, which is how switching page drops the
        selection. EditorPane, one file over, keeps `role="tab"` because its tabs really do switch a
        panel: each carries `aria-controls` and the panel `aria-labelledby`, and nothing here ever
        had a counterpart to point at.
      */}
      <nav className="page-nav" aria-label="Pages">
        {pages.map(({ id, label, icon: Icon }) => (
          <a
            key={id}
            href={formatHash({ page: id, name: null })}
            // The state a link says its currentness with: `aria-selected` belongs to a tab, and a
            // link that is not current says nothing rather than saying `false`. The stylesheet
            // selects on it too, so there is no `active` class to keep in step with it.
            aria-current={props.page === id ? 'page' : undefined}
            className="tab"
          >
            <Icon size={13} />
            {label}
          </a>
        ))}
      </nav>
      {props.children}
      <div className="appbar-fill" />
      <div className="appbar-controls">{props.controls}</div>
    </header>
  );
}
