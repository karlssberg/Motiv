import { test, expect, request as newRequestContext, type APIRequestContext } from '@playwright/test';

/**
 * End-to-end coverage of the real OIDC path: grants enforced over actual Keycloak tokens, not the
 * zero-config dev identity every other spec in this directory runs against. Requires the
 * `--profile auth` compose stack (`docker compose --profile auth up -d --build`), which brings up
 * a second app instance (studio-auth, :5101) wired to a real Keycloak (:8081) instead of the dev
 * identity, plus the `motiv-demo` realm import (`keycloak/motiv-realm.json`) that seeds the four
 * users this suite drives: alice-author, paula-publisher, petra-publisher, root-admin (all
 * password "password", client_id motiv-demo, password grant).
 *
 * Self-skips whenever MOTIV_E2E_AUTH_URL is unset, so the default `pnpm e2e` run (which starts
 * only the local dev-identity server via playwright.config.ts's webServer, on MOTIV_E2E_PORT) is
 * completely unaffected — this spec never touches that server or that port.
 */
const APP_URL = process.env.MOTIV_E2E_AUTH_URL ?? '';
const KEYCLOAK_URL = process.env.MOTIV_E2E_KEYCLOAK_URL ?? 'http://localhost:8081';

/** The gate document the "maker-checker" cases install: >=1 approval, and the author cannot be
 * the approver. Same shape asserted against directly in ApprovalGateTests/LockoutPreCheckTests. */
const MAKER_CHECKER_GATE = {
  rule: {
    and: [
      { spec: 'change.approver-count-at-least', args: { n: 1 } },
      { not: { spec: 'change.author-is-approver' } },
    ],
  },
};

/** A minimal valid proposition document: a single reference to a compiled spec over `customer`. */
function customerIsActiveDocument() {
  return { rule: { spec: 'customer.is-active' } };
}

test.describe.serial('authenticated path against real Keycloak grants (--profile auth stack)', () => {
  test.skip(
    !process.env.MOTIV_E2E_AUTH_URL,
    'requires MOTIV_E2E_AUTH_URL (e.g. http://localhost:5101) — run ' +
      '`docker compose --profile auth up -d --build` first',
  );

  /** Password-grant against the realm's token endpoint, form-encoded per the OIDC spec. */
  async function token(request: APIRequestContext, username: string): Promise<string> {
    const response = await request.post(`${KEYCLOAK_URL}/realms/motiv/protocol/openid-connect/token`, {
      form: {
        grant_type: 'password',
        client_id: 'motiv-demo',
        username,
        password: 'password',
      },
    });
    expect(response.ok(), `token request for ${username} failed: ${response.status()} ${await response.text()}`).toBe(
      true,
    );
    return ((await response.json()) as { access_token: string }).access_token;
  }

  function bearer(accessToken: string) {
    return { Authorization: `Bearer ${accessToken}` };
  }

  // Shared across the serial run: the proposition paula creates in case 4 is updated by the
  // maker-checker workflow in case 6 and torn down in case 8, each step needing the version the
  // previous step left behind.
  let e2eFlagVersion: number;
  let changeRequestId: string;

  test.afterAll(async () => {
    // Safety-net teardown. Serial mode aborts the remaining tests — including case 8's own
    // cleanup — the moment any test in this file fails, which would otherwise leave the
    // maker-checker gate installed and/or pricing.e2e-flag behind in the container's file-backed
    // stores. A subsequent run's case 4 (create) or case 5 (gate PUT) would then fail confusingly
    // against state left by a *previous* failure rather than starting clean. This hook mirrors
    // case 8, but every step is independently best-effort (never throws) and re-derives what to
    // delete rather than trusting this run's local variables, so it is idempotent whether case 8
    // already ran or not. `request` (the fixture) cannot be used from afterAll, hence the
    // standalone context via the `request` *module export* imported as `newRequestContext`.
    //
    // Residual limitation: a hard process kill (not a test failure/throw) before this hook gets
    // to run still leaks state. `docker compose --profile auth down` (or a fresh `up`) resets it.
    if (!process.env.MOTIV_E2E_AUTH_URL) return;

    const context = await newRequestContext.newContext();
    try {
      const rootToken = await token(context, 'root-admin').catch(() => undefined);
      if (rootToken) {
        await context.delete(`${APP_URL}/api/rules/gate`, { headers: bearer(rootToken) }).catch(() => {});
      }

      const paulaToken = await token(context, 'paula-publisher').catch(() => undefined);
      if (paulaToken) {
        const current = await context
          .get(`${APP_URL}/api/rules/propositions/pricing.e2e-flag`, { headers: bearer(paulaToken) })
          .catch(() => undefined);
        if (current?.ok()) {
          const version = ((await current.json()) as { version: number }).version;
          await context
            .delete(`${APP_URL}/api/rules/propositions/pricing.e2e-flag?baseVersion=${version}`, {
              headers: bearer(paulaToken),
            })
            .catch(() => {});
        }
      }
    } finally {
      await context.dispose();
    }
  });

  test('1: an unauthenticated catalog read is refused', async ({ request }) => {
    const response = await request.get(`${APP_URL}/api/rules/catalog`);
    expect(response.status()).toBe(401);
  });

  test('2: alice-author reads the catalog (an unfiltered read, no grant needed beyond auth)', async ({ request }) => {
    const aliceToken = await token(request, 'alice-author');
    const response = await request.get(`${APP_URL}/api/rules/catalog`, { headers: bearer(aliceToken) });
    expect(response.status()).toBe(200);
  });

  test('3: alice-author (author only) cannot PUT a pricing proposition (needs publish)', async ({ request }) => {
    const aliceToken = await token(request, 'alice-author');
    const response = await request.put(`${APP_URL}/api/rules/propositions/pricing.alice-attempt`, {
      headers: bearer(aliceToken),
      data: { document: customerIsActiveDocument(), baseVersion: 1 },
    });
    expect(response.status()).toBe(403);
  });

  test('4: paula-publisher creates pricing.e2e-flag directly, but cannot write under fraud.', async ({
    request,
  }) => {
    const paulaToken = await token(request, 'paula-publisher');

    const created = await request.post(`${APP_URL}/api/rules/propositions`, {
      headers: bearer(paulaToken),
      data: {
        name: 'pricing.e2e-flag',
        modelType: 'customer',
        document: customerIsActiveDocument(),
        description: 'e2e coverage flag (task 24)',
      },
    });
    expect(created.status(), await created.text()).toBe(201);
    e2eFlagVersion = ((await created.json()) as { version: number }).version;

    const fraudAttempt = await request.put(`${APP_URL}/api/rules/propositions/fraud.e2e-flag`, {
      headers: bearer(paulaToken),
      data: { document: customerIsActiveDocument(), baseVersion: 1 },
    });
    expect(fraudAttempt.status()).toBe(403);
  });

  test('5: root-admin installs the maker-checker gate; alice-author cannot configure the gate', async ({
    request,
  }) => {
    const rootToken = await token(request, 'root-admin');
    const gatePut = await request.put(`${APP_URL}/api/rules/gate`, {
      headers: bearer(rootToken),
      data: { document: MAKER_CHECKER_GATE },
    });
    expect(gatePut.status(), await gatePut.text()).toBe(200);

    const aliceToken = await token(request, 'alice-author');
    const aliceAttempt = await request.put(`${APP_URL}/api/rules/gate`, {
      headers: bearer(aliceToken),
      data: { document: MAKER_CHECKER_GATE },
    });
    expect(aliceAttempt.status()).toBe(403);
  });

  test('6: maker-checker workflow — paula cannot self-publish, petra approves, paula publishes', async ({
    request,
  }) => {
    const paulaToken = await token(request, 'paula-publisher');
    const petraToken = await token(request, 'petra-publisher');

    const created = await request.post(`${APP_URL}/api/rules/change-requests`, {
      headers: bearer(paulaToken),
      data: {
        changeNote: 'e2e maker-checker workflow (task 24)',
        changes: [
          {
            kind: 'proposition',
            name: 'pricing.e2e-flag',
            document: customerIsActiveDocument(),
            baseVersion: e2eFlagVersion,
            rollbackOfVersion: null,
            modelTypeId: null,
            description: null,
          },
        ],
      },
    });
    expect(created.status(), await created.text()).toBe(201);
    changeRequestId = ((await created.json()) as { id: string }).id;

    // De-noising surfaces the causal operand ("fewer than 1 approvals"), not a distinct
    // "no self-approval" assertion — the &-composed gate only reports what actually failed.
    const selfPublish = await request.post(`${APP_URL}/api/rules/change-requests/${changeRequestId}/publish`, {
      headers: bearer(paulaToken),
    });
    expect(selfPublish.status()).toBe(403);
    const refusal = (await selfPublish.json()) as { assertions: string[] };
    expect(refusal.assertions).toContain('change has fewer than 1 approvals');

    const approval = await request.post(`${APP_URL}/api/rules/change-requests/${changeRequestId}/approvals`, {
      headers: bearer(petraToken),
    });
    expect(approval.status(), await approval.text()).toBe(200);

    const publish = await request.post(`${APP_URL}/api/rules/change-requests/${changeRequestId}/publish`, {
      headers: bearer(paulaToken),
    });
    expect(publish.status(), await publish.text()).toBe(200);
    const published = (await publish.json()) as { publishedVersions: Record<string, number> };
    const publishedVersion = published.publishedVersions['pricing.e2e-flag'];
    expect(publishedVersion, 'publish response must report the new version of pricing.e2e-flag').toBeDefined();
    e2eFlagVersion = publishedVersion as number;
  });

  test('7: root-admin cannot install a gate requiring an unknown role (lockout pre-check)', async ({ request }) => {
    const rootToken = await token(request, 'root-admin');
    const ghostGate = await request.put(`${APP_URL}/api/rules/gate`, {
      headers: bearer(rootToken),
      data: { document: { rule: { spec: 'change.approver-has-role', args: { role: 'ghost' } } } },
    });
    expect(ghostGate.status()).toBe(422);
    const refusal = (await ghostGate.json()) as { assertions: string[] };
    expect(refusal.assertions).toContain("no approver holds role 'ghost'");
  });

  test('8: cleanup — root-admin removes the gate, paula removes the proposition she created', async ({
    request,
  }) => {
    const rootToken = await token(request, 'root-admin');
    const gateDelete = await request.delete(`${APP_URL}/api/rules/gate`, { headers: bearer(rootToken) });
    expect(gateDelete.status()).toBe(204);

    const paulaToken = await token(request, 'paula-publisher');
    const propositionDelete = await request.delete(
      `${APP_URL}/api/rules/propositions/pricing.e2e-flag?baseVersion=${e2eFlagVersion}`,
      { headers: bearer(paulaToken) },
    );
    expect(propositionDelete.status(), await propositionDelete.text()).toBe(200);
  });
});
