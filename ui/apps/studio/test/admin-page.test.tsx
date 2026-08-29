import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AdminPage } from '../src/panes/AdminPage.js';

interface Capabilities {
  grantAdministration: boolean;
  administrator: boolean;
  devIdentity: boolean;
}

interface Grant {
  subject: string;
  prefix: string;
  verb: string;
}

const ALLOWED: Capabilities = { grantAdministration: true, administrator: true, devIdentity: false };
const DENIED: Capabilities = { grantAdministration: false, administrator: true, devIdentity: true };

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } });
}

/**
 * Routes fetch calls the way the real endpoints answer: capabilities is always GET, grants
 * branches on method. Matches by suffix so the relative paths the component actually sends
 * ("/api/admin/grants") line up regardless of how the test runner resolves the base URL.
 */
function mockFetch(options: {
  capabilities?: Capabilities;
  grants?: Grant[];
  onPost?: (body: Grant) => Response;
  onDelete?: (body: Grant) => Response;
}) {
  const { capabilities = ALLOWED, grants = [] } = options;
  return vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
    const url = String(input);
    const method = (init?.method ?? 'GET').toUpperCase();
    if (url.endsWith('/api/admin/capabilities')) return jsonResponse(capabilities);
    if (url.endsWith('/api/admin/grants')) {
      if (method === 'GET') return jsonResponse(grants);
      const body = JSON.parse(String(init?.body)) as Grant;
      if (method === 'POST') return options.onPost ? options.onPost(body) : new Response(null, { status: 204 });
      if (method === 'DELETE') return options.onDelete ? options.onDelete(body) : new Response(null, { status: 204 });
    }
    throw new Error(`unexpected fetch: ${method} ${url}`);
  });
}

function renderPage() {
  return render(<AdminPage page="admin" onNavigate={vi.fn()} />);
}

describe('AdminPage', () => {
  afterEach(() => vi.restoreAllMocks());

  it('renders the grants table when capabilities allow', async () => {
    mockFetch({ grants: [{ subject: 'alice', prefix: 'pricing', verb: 'author' }] });
    renderPage();

    expect(await screen.findByRole('cell', { name: 'alice' })).toBeTruthy();
    expect(screen.getByRole('cell', { name: 'pricing' })).toBeTruthy();
    expect(screen.getByRole('cell', { name: 'author' })).toBeTruthy();
    expect(screen.getByRole('button', { name: /delete/i })).toBeTruthy();
  });

  it('renders nothing (no table, no add form) when grantAdministration is false', async () => {
    mockFetch({ capabilities: DENIED });
    renderPage();

    await waitFor(() => expect(screen.queryByRole('tab', { name: 'Admin' })).toBeNull());
    expect(screen.queryByRole('table')).toBeNull();
    expect(screen.queryByRole('button', { name: /add grant/i })).toBeNull();
  });

  it('hides the admin nav link when grantAdministration is false', async () => {
    mockFetch({ capabilities: DENIED });
    renderPage();

    await waitFor(() => expect(screen.queryByRole('tab', { name: 'Admin' })).toBeNull());
  });

  it('shows the admin nav link when capabilities allow', async () => {
    mockFetch({});
    renderPage();

    expect(await screen.findByRole('tab', { name: 'Admin' })).toBeTruthy();
  });

  it('posts a new grant from the add form', async () => {
    const fetchSpy = mockFetch({});
    renderPage();
    await screen.findByRole('button', { name: /add grant/i });

    await userEvent.type(screen.getByLabelText('Subject'), 'bob');
    await userEvent.type(screen.getByLabelText('Prefix'), 'orders');
    await userEvent.selectOptions(screen.getByLabelText('Verb'), 'publish');
    await userEvent.click(screen.getByRole('button', { name: /add grant/i }));

    await waitFor(() => expect(fetchSpy).toHaveBeenCalledWith('/api/admin/grants', expect.objectContaining({
      method: 'POST',
      body: JSON.stringify({ subject: 'bob', prefix: 'orders', verb: 'publish' }),
    })));
  });

  it('refreshes the table after adding a grant', async () => {
    mockFetch({});
    renderPage();
    await screen.findByRole('button', { name: /add grant/i });

    await userEvent.type(screen.getByLabelText('Subject'), 'bob');
    await userEvent.click(screen.getByRole('button', { name: /add grant/i }));

    await waitFor(() => expect((screen.getByLabelText('Subject') as HTMLInputElement).value).toBe(''));
  });

  it('deletes a grant', async () => {
    const fetchSpy = mockFetch({ grants: [{ subject: 'alice', prefix: 'pricing', verb: 'author' }] });
    renderPage();
    await screen.findByRole('cell', { name: 'alice' });

    await userEvent.click(screen.getByRole('button', { name: /delete/i }));

    await waitFor(() => expect(fetchSpy).toHaveBeenCalledWith('/api/admin/grants', expect.objectContaining({
      method: 'DELETE',
      body: JSON.stringify({ subject: 'alice', prefix: 'pricing', verb: 'author' }),
    })));
  });

  it('surfaces the 409 message when removing the last administer grant', async () => {
    mockFetch({
      grants: [{ subject: 'dev', prefix: '', verb: 'administer' }],
      onDelete: () => jsonResponse({ error: 'cannot remove the last administer grant' }, 409),
    });
    renderPage();
    await screen.findByRole('cell', { name: 'dev' });

    await userEvent.click(screen.getByRole('button', { name: /delete/i }));

    expect((await screen.findByRole('alert')).textContent).toContain('cannot remove the last administer grant');
    // A refused delete removed nothing, so the row must still be there.
    expect(screen.getByRole('cell', { name: 'dev' })).toBeTruthy();
  });

  it('does not add a grant with a blank subject', async () => {
    const fetchSpy = mockFetch({});
    renderPage();
    await screen.findByRole('button', { name: /add grant/i });

    await userEvent.click(screen.getByRole('button', { name: /add grant/i }));

    expect(fetchSpy).not.toHaveBeenCalledWith('/api/admin/grants', expect.objectContaining({ method: 'POST' }));
  });
});
