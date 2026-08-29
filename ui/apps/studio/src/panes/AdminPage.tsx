import { useCallback, useEffect, useState } from 'react';
import type { Page } from '../routing/useHashRoute.js';
import { useAdminCapabilities } from '../shell/useAdminCapabilities.js';
import { IconDelete } from '../shell/icons.js';
import { AppBar } from './AppBar.js';

type Verb = 'read' | 'author' | 'publish' | 'administer';

const VERBS: readonly Verb[] = ['read', 'author', 'publish', 'administer'];

/** One row of GET /api/admin/grants: who can do what, and how far the name prefix reaches. */
interface GrantRecord {
  subject: string;
  prefix: string;
  verb: Verb;
}

/** A blank row for the add form. Prefix defaults to "" — every name — same as a fresh grant would. */
const EMPTY_DRAFT: { subject: string; prefix: string; verb: Verb } = { subject: '', prefix: '', verb: 'read' };

/** Renders a *thrown* failure — a non-2xx status this page has no typed outcome for. */
function describeThrown(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}

/**
 * The grant administration surface: who can read/author/publish/administer which name prefixes.
 * Talks to `/api/admin/*` with plain `fetch` — these are app endpoints, not part of
 * `@motiv-rules/core`'s SDK client, the same seam `CheckoutPane` uses for `/api/checkout`.
 *
 * The table and add form render only when the fetched capabilities allow it: `grantAdministration`
 * (the active grant source can be administered at all — an immutable source, e.g. the dev
 * identity's built-in grant, answers 404 from the grants endpoints) and `administrator` (the caller
 * is themselves one). Neither being true leaves nothing this page can show, so it renders just its
 * `AppBar` — the same chrome every other page carries, so navigating here and back is not a dead
 * end even for a caller this page has nothing for.
 */
export function AdminPage(props: { page: Page; onNavigate: (page: Page) => void }) {
  const capabilities = useAdminCapabilities();
  const canAdminister = capabilities.grantAdministration && capabilities.administrator;

  const [grants, setGrants] = useState<GrantRecord[]>([]);
  const [failure, setFailure] = useState<string | null>(null);
  const [draft, setDraft] = useState(EMPTY_DRAFT);
  const [saving, setSaving] = useState(false);

  const loadGrants = useCallback(async (): Promise<void> => {
    try {
      const response = await fetch('/api/admin/grants');
      if (!response.ok) throw new Error(`Could not load grants (${response.status}).`);
      setGrants((await response.json()) as GrantRecord[]);
    } catch (error: unknown) {
      setFailure(describeThrown(error));
    }
  }, []);

  useEffect(() => {
    if (!canAdminister) return;
    void loadGrants();
  }, [canAdminister, loadGrants]);

  const trimmedSubject = draft.subject.trim();
  const canAdd = trimmedSubject !== '' && !saving;

  // Guards itself rather than relying on the caller, so it is safe as the Add button's handler:
  // `aria-disabled` does not stop a click reaching it — see PropositionDialog's Create button.
  const addGrant = async (): Promise<void> => {
    if (!canAdd) return;
    const grant: GrantRecord = { subject: trimmedSubject, prefix: draft.prefix.trim(), verb: draft.verb };
    setSaving(true);
    setFailure(null);
    try {
      const response = await fetch('/api/admin/grants', {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify(grant),
      });
      if (!response.ok) throw new Error(`Could not add grant (${response.status}).`);
      setDraft(EMPTY_DRAFT);
      await loadGrants();
    } catch (error: unknown) {
      setFailure(describeThrown(error));
    } finally {
      setSaving(false);
    }
  };

  const removeGrant = async (grant: GrantRecord): Promise<void> => {
    setFailure(null);
    try {
      const response = await fetch('/api/admin/grants', {
        method: 'DELETE',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify(grant),
      });
      // 409 is the one typed refusal this surface has: removing the last administer grant would
      // lock everyone out, so it is reported inline rather than thrown as an opaque failure.
      if (response.status === 409) {
        const body = (await response.json()) as { error: string };
        setFailure(body.error);
        return;
      }
      if (!response.ok) throw new Error(`Could not remove grant (${response.status}).`);
      await loadGrants();
    } catch (error: unknown) {
      setFailure(describeThrown(error));
    }
  };

  return (
    <>
      <AppBar page={props.page} onNavigate={props.onNavigate}>
        <span className="breadcrumb-sep">/</span>
        <span className="breadcrumb-item">Admin</span>
      </AppBar>

      {canAdminister && (
        <section aria-label="Grant administration" className="pane">
          <h2>Grants</h2>
          <div className="pane-body">
            {failure !== null && <p role="alert" className="dialog-error">{failure}</p>}

            <table className="grants-table">
              <thead>
                <tr>
                  <th>Subject</th>
                  <th>Prefix</th>
                  <th>Verb</th>
                  <th aria-hidden="true" />
                </tr>
              </thead>
              <tbody>
                {grants.map((grant) => (
                  <tr key={`${grant.subject}:${grant.prefix}:${grant.verb}`}>
                    <td>{grant.subject}</td>
                    <td>{grant.prefix}</td>
                    <td>{grant.verb}</td>
                    <td>
                      <button
                        type="button"
                        className="btn btn-danger"
                        aria-label={`Delete grant: ${grant.subject} ${grant.prefix} ${grant.verb}`}
                        onClick={() => void removeGrant(grant)}
                      >
                        <IconDelete size={13} />
                        Delete
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>

            <div className="admin-add-form">
              <label className="field">
                <span>Subject</span>
                <input
                  type="text"
                  className="control"
                  value={draft.subject}
                  onChange={(event) => setDraft({ ...draft, subject: event.target.value })}
                />
              </label>
              <label className="field">
                <span>Prefix</span>
                <input
                  type="text"
                  className="control"
                  value={draft.prefix}
                  onChange={(event) => setDraft({ ...draft, prefix: event.target.value })}
                />
              </label>
              <label className="field">
                <span>Verb</span>
                <select
                  className="control"
                  value={draft.verb}
                  onChange={(event) => setDraft({ ...draft, verb: event.target.value as Verb })}
                >
                  {VERBS.map((verb) => <option key={verb} value={verb}>{verb}</option>)}
                </select>
              </label>
              <button
                type="button"
                className="btn"
                aria-disabled={canAdd ? undefined : true}
                onClick={() => void addGrant()}
              >
                Add grant
              </button>
            </div>
          </div>
        </section>
      )}
    </>
  );
}
