import { useEffect, useState } from 'react';

/**
 * What GET /api/admin/capabilities reports about the calling identity: whether the active grant
 * source can be administered at all (an immutable source, e.g. the dev identity's built-in grant,
 * answers false), whether the caller is themselves an administrator, and whether they are running
 * under the dev identity. An app endpoint, not part of `@motiv-rules/core`'s SDK client — talked to
 * with plain `fetch`, the same seam `CheckoutPane` uses for `/api/checkout`.
 */
export interface AdminCapabilities {
  grantAdministration: boolean;
  administrator: boolean;
  devIdentity: boolean;
}

/** Nothing administrable and nothing to administer with — the safe default before a fetch lands. */
const HIDDEN: AdminCapabilities = { grantAdministration: false, administrator: false, devIdentity: false };

/**
 * Fetches the caller's admin capabilities on mount. Starts (and stays, on any failure) at
 * `HIDDEN`, so a slow or failing request never flashes an admin affordance that then has to be
 * taken away — the grants surface and its nav link are opt-in once the server confirms them, never
 * shown by default and revoked on error.
 *
 * Called independently by `AppBar` and `AdminPage`, each fetching its own copy on mount rather than
 * sharing one in a context — the same duplicate-fetch shape every other pane in this demo uses (see
 * `CheckoutPane`'s comment on `getCatalog`), kept deliberately rather than DRYed into a shared
 * cache.
 */
export function useAdminCapabilities(): AdminCapabilities {
  const [capabilities, setCapabilities] = useState<AdminCapabilities>(HIDDEN);

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      try {
        const response = await fetch('/api/admin/capabilities');
        if (!response.ok) return;
        const body = (await response.json()) as AdminCapabilities;
        if (!cancelled) setCapabilities(body);
      } catch {
        // Network failure, or no fetch at all (e.g. an unrelated unit test): stay hidden.
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  return capabilities;
}
