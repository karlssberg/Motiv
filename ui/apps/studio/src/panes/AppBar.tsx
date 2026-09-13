import type { ReactNode } from 'react';
import { formatHash } from '../routing/useHashRoute.js';
import { useAdminCapabilities } from '../shell/useAdminCapabilities.js';
import { IconAdmin, IconRules } from '../shell/icons.js';

/**
 * The shell's top bar: the brand, then whatever the page puts beside it — the tab strip, for the
 * documents — and its controls on the right, with Admin as a link at the far end once
 * capabilities confirm it. Shared by the documents shell and the admin page so there is one
 * chrome rather than two that drift apart.
 *
 * The page switch that used to sit here is gone: with tabs there are no pages to switch between,
 * only documents to open. Admin keeps its link because it is still a route of its own, and from it
 * the way back is a link too — anchors in a navigation landmark, whose href is the navigation
 * itself, minted by `formatHash` so a link and the route it leads to cannot drift.
 */
export function AppBar(props: {
  /** The current route, when it is the admin page; the documents shell passes nothing. */
  current?: 'admin';
  controls?: ReactNode;
  children?: ReactNode;
}) {
  // Self-fetched rather than threaded down: the bar is mounted by two parents, and each already
  // fetches its own data on mount rather than sharing a cache — see useAdminCapabilities.
  const capabilities = useAdminCapabilities();
  const admin = capabilities.grantAdministration && capabilities.administrator;
  const onAdmin = props.current === 'admin';

  return (
    <header className="appbar">
      <div className="appbar-brand">
        <span className="appbar-mark" aria-hidden="true">M</span>
        <span className="appbar-wordmark">Motiv</span>
      </div>
      <span className="appbar-divider" aria-hidden="true" />
      {props.children}
      <div className="appbar-fill" />
      <div className="appbar-controls">{props.controls}</div>
      {(admin || onAdmin) && (
        <nav className="page-nav" aria-label="Pages">
          {onAdmin && (
            <a href={formatHash({ page: 'rules', name: null })} className="tab">
              <IconRules size={13} />Documents
            </a>
          )}
          {admin && (
            <a
              href={formatHash({ page: 'admin', name: null })}
              aria-current={onAdmin ? 'page' : undefined}
              className="tab"
            >
              <IconAdmin size={13} />Admin
            </a>
          )}
        </nav>
      )}
    </header>
  );
}
