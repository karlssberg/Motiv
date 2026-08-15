import type {
  BrokenDependent, Catalog, DependentEntry, ErrorResponse, EvaluateRequest, EvaluationResult,
  PropositionCreateRequest, PropositionGetResponse, PropositionListEntry, PropositionSaveResult,
  RuleError, RuleGetResponse, RuleListEntry, RuleSaveResult,
  ValidateRequest, ValidationResponse,
} from './contracts.js';
import type { RuleDocument } from './document.js';

/** Options for constructing a {@link RulesApiClient}. */
export interface RulesApiClientOptions {
  /** Base path the API is mounted under, e.g. "/api/rules". No trailing slash. */
  baseUrl: string;
  /** Injectable fetch implementation; defaults to the global fetch. */
  fetch?: typeof fetch;
  /**
   * Called when a response reports a generation behind the one this client last accepted (see
   * {@link RulesApiClient.generation}) — the client was routed to a replica that has not caught up
   * yet. Detection, not policy: the caller decides whether to retry, warn, or ignore.
   */
  onStaleGeneration?: (observed: StoreGeneration, accepted: StoreGeneration) => void;
}

/** Where both stores stood in the world a response was served from. */
export interface StoreGeneration {
  rules: number;
  propositions: number;
}

/** Reads the `Motiv-Generation` header. Anything the server did not write is refused. */
export function parseGeneration(token: string | null | undefined): StoreGeneration | undefined {
  if (!token) return undefined;
  const match = /^r(\d+)\.p(\d+)$/.exec(token);
  return match ? { rules: Number(match[1]), propositions: Number(match[2]) } : undefined;
}

/** The union of shapes a failed proposition write's body can take (see `#readPropositionResult`). */
interface PropositionErrorBody {
  currentVersion?: number;
  referrers?: string[];
  errors?: RuleError[];
  brokenDependents?: BrokenDependent[];
  error?: string;
}

/** Thrown when the API returns a non-2xx response. */
export class RulesApiError extends Error {
  readonly status: number;
  /** Present when the failure body was a ValidationResponse. */
  readonly errors?: RuleError[];

  constructor(status: number, message: string, errors?: RuleError[]) {
    super(message);
    this.name = 'RulesApiError';
    this.status = status;
    if (errors) this.errors = errors;
  }
}

/** A transport-agnostic client for the Motiv rules API. */
export class RulesApiClient {
  readonly #baseUrl: string;
  readonly #fetch: typeof fetch;
  readonly #onStaleGeneration: ((observed: StoreGeneration, accepted: StoreGeneration) => void) | undefined;
  #generation: StoreGeneration | undefined;

  constructor(options: RulesApiClientOptions) {
    this.#baseUrl = options.baseUrl.replace(/\/$/, '');
    this.#fetch = options.fetch ?? globalThis.fetch.bind(globalThis);
    this.#onStaleGeneration = options.onStaleGeneration;
  }

  /**
   * The most recent `Motiv-Generation` that was not behind the one before it — the newest world this
   * client accepts as having really been served to it. Undefined until a response carries one.
   *
   * Not a component-wise maximum, deliberately. A response is kept whole or discarded whole, so a
   * mixed-direction observation (`r5.p9` after `r7.p3`) alarms and is dropped entire, and its `p9` is
   * never recorded. Combining the two halves would synthesise a world no replica ever served, and
   * every subsequent honest response would then look like a regression against it. The cost is
   * under-detection: after a discarded observation, a later response carrying that same `p9` raises
   * no alarm on the proposition half, because this client never claimed to have seen it.
   */
  get generation(): StoreGeneration | undefined {
    return this.#generation;
  }

  /** GET {baseUrl}/catalog */
  async getCatalog(): Promise<Catalog> {
    const response = await this.#fetch(`${this.#baseUrl}/catalog`, { method: 'GET' });
    return this.#read<Catalog>(response);
  }

  /** POST {baseUrl}/validate */
  async validate(request: ValidateRequest): Promise<ValidationResponse> {
    const response = await this.#post('/validate', request);
    return this.#read<ValidationResponse>(response);
  }

  /** POST {baseUrl}/evaluate */
  async evaluate(request: EvaluateRequest): Promise<EvaluationResult> {
    const response = await this.#post('/evaluate', request);
    return this.#read<EvaluationResult>(response);
  }

  /** GET {baseUrl}/rules */
  async listRules(): Promise<RuleListEntry[]> {
    const response = await this.#fetch(`${this.#baseUrl}/rules`, { method: 'GET' });
    return this.#read<RuleListEntry[]>(response);
  }

  /** GET {baseUrl}/rules/{name} */
  async getRule(name: string): Promise<RuleGetResponse> {
    const response = await this.#fetch(
      `${this.#baseUrl}/rules/${encodeURIComponent(name)}`,
      { method: 'GET' },
    );
    return this.#read<RuleGetResponse>(response);
  }

  /** PUT {baseUrl}/rules/{name} — 409/400 return typed outcomes rather than throwing. */
  async putRule(name: string, document: RuleDocument, baseVersion: number): Promise<RuleSaveResult> {
    const response = await this.#fetch(`${this.#baseUrl}/rules/${encodeURIComponent(name)}`, {
      method: 'PUT',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ document, baseVersion }),
    });
    return this.#readSaveResult(response);
  }

  /** DELETE {baseUrl}/rules/{name}?baseVersion=N — reverts to the rule's default. */
  async revertRule(name: string, baseVersion: number): Promise<RuleSaveResult> {
    const response = await this.#fetch(
      `${this.#baseUrl}/rules/${encodeURIComponent(name)}?baseVersion=${baseVersion}`,
      { method: 'DELETE' },
    );
    return this.#readSaveResult(response);
  }

  /** GET {baseUrl}/propositions */
  async listPropositions(): Promise<PropositionListEntry[]> {
    const response = await this.#fetch(`${this.#baseUrl}/propositions`, { method: 'GET' });
    return this.#read<PropositionListEntry[]>(response);
  }

  /** GET {baseUrl}/propositions/{name} */
  async getProposition(name: string): Promise<PropositionGetResponse> {
    const response = await this.#fetch(
      `${this.#baseUrl}/propositions/${encodeURIComponent(name)}`,
      { method: 'GET' },
    );
    return this.#read<PropositionGetResponse>(response);
  }

  /** POST {baseUrl}/propositions — 400/409 return typed outcomes rather than throwing. */
  async createProposition(request: PropositionCreateRequest): Promise<PropositionSaveResult> {
    const response = await this.#post('/propositions', request);
    // Only a create can collide with an existing name, so only a create reads an otherwise
    // unrecognised 409 that way.
    return this.#readPropositionResult(response, { unmatchedConflictIsNameTaken: true });
  }

  /** PUT {baseUrl}/propositions/{name} — 400/409 return typed outcomes rather than throwing. */
  async putProposition(
    name: string, document: RuleDocument, baseVersion: number,
  ): Promise<PropositionSaveResult> {
    const response = await this.#fetch(
      `${this.#baseUrl}/propositions/${encodeURIComponent(name)}`,
      {
        method: 'PUT',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({ document, baseVersion }),
      },
    );
    return this.#readPropositionResult(response);
  }

  /**
   * DELETE {baseUrl}/propositions/{name}?baseVersion=N — reverts to the compiled spec when one
   * exists, otherwise removes the proposition (refused while anything references it).
   *
   * The 200 response is `{"version":0}` in both cases and does not say which happened; call
   * `getProposition(name)` first and check `hasCompiledDefault` to know which outcome to expect.
   */
  async deleteProposition(name: string, baseVersion: number): Promise<PropositionSaveResult> {
    const response = await this.#fetch(
      `${this.#baseUrl}/propositions/${encodeURIComponent(name)}?baseVersion=${baseVersion}`,
      { method: 'DELETE' },
    );
    return this.#readPropositionResult(response);
  }

  /** GET {baseUrl}/propositions/{name}/dependents */
  async getDependents(name: string): Promise<DependentEntry[]> {
    const response = await this.#fetch(
      `${this.#baseUrl}/propositions/${encodeURIComponent(name)}/dependents`,
      { method: 'GET' },
    );
    // `?? []` rather than trusting the shape: an absent field would otherwise reach the caller as
    // `undefined` and only fail at `dependents.length`, during render, with the page blanked.
    const body = await this.#read<{ dependents?: DependentEntry[] }>(response);
    return body.dependents ?? [];
  }

  #post(path: string, body: unknown): Promise<Response> {
    return this.#fetch(`${this.#baseUrl}${path}`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(body),
    });
  }

  /**
   * Records the world a response came from, and reports a backwards move.
   *
   * Replicas converge eventually, so a client can be routed to one that has not caught up yet.
   * That is explicable staleness rather than incoherence — but only if the client can see it, so a
   * backwards move is surfaced rather than swallowed, and the generation kept is the last one that
   * was not behind.
   *
   * The two components come from stores that are never written in the same transaction, so
   * there is no total order between generations — a response is a backwards move if *either*
   * component is behind, even when the other has advanced. Such a response is then discarded whole
   * rather than merged component-wise; see `generation` for what that costs and why it is right.
   */
  #trackGeneration(response: Response): void {
    const observed = parseGeneration(response.headers.get('motiv-generation'));
    if (!observed) return;

    const accepted = this.#generation;
    if (!accepted) {
      this.#generation = observed;
      return;
    }

    if (observed.rules < accepted.rules || observed.propositions < accepted.propositions) {
      this.#onStaleGeneration?.(observed, accepted);
      return;
    }

    this.#generation = observed;
  }

  async #readSaveResult(response: Response): Promise<RuleSaveResult> {
    this.#trackGeneration(response);
    if (response.ok) {
      const body = (await response.json()) as { version: number };
      return { outcome: 'updated', version: body.version };
    }
    if (response.status === 409) {
      const body = (await response.json()) as { currentVersion: number };
      return { outcome: 'conflict', currentVersion: body.currentVersion };
    }
    if (response.status === 400) {
      // Framework binding failures return 400 with an empty body — only a parseable
      // ValidationResponse becomes a typed 'invalid' outcome; anything else throws,
      // surfacing the server's { error } message from guard failures when present.
      const body = (await response.json().catch(() => undefined)) as
        | ValidationResponse | ErrorResponse | undefined;
      if (body && typeof body === 'object' && 'errors' in body) {
        return { outcome: 'invalid', errors: body.errors };
      }
      const message = body && typeof body === 'object' && 'error' in body
        ? body.error
        : `Request failed (${response.status}).`;
      throw new RulesApiError(response.status, message);
    }
    return this.#throwFromErrorResponse(response); // 404 etc. → RulesApiError as elsewhere
  }

  /**
   * The shared reader for POST / PUT / DELETE on a proposition.
   *
   * `unmatchedConflictIsNameTaken` is what separates them: a 409 carrying neither `currentVersion`
   * nor `referrers` is a duplicate name only when something was being *created*. Reading it that
   * way for an update or a delete would answer "a proposition is already authored under that name"
   * to an operation where that sentence is not merely unhelpful but the opposite of what happened.
   */
  async #readPropositionResult(
    response: Response,
    options: { unmatchedConflictIsNameTaken?: boolean } = {},
  ): Promise<PropositionSaveResult> {
    this.#trackGeneration(response);
    if (response.ok) {
      const body = (await response.json()) as { version: number };
      return { outcome: 'saved', version: body.version };
    }

    const body = (await response.json().catch(() => undefined)) as PropositionErrorBody | undefined;

    if (response.status === 409) {
      // Three different 409s share the status but not the shape, so they are told apart by body.
      if (body && typeof body.currentVersion === 'number') {
        return { outcome: 'conflict', currentVersion: body.currentVersion };
      }
      if (body?.referrers) return { outcome: 'referenced', referrers: body.referrers };
      if (options.unmatchedConflictIsNameTaken) return { outcome: 'nameTaken' };
    }

    if (response.status === 400 && body && 'errors' in body) {
      return {
        outcome: 'invalid',
        errors: body.errors ?? [],
        brokenDependents: body.brokenDependents ?? [],
      };
    }

    const message = body?.error ?? `Request failed (${response.status}).`;
    throw new RulesApiError(response.status, message);
  }

  async #read<T>(response: Response): Promise<T> {
    this.#trackGeneration(response);
    if (response.ok) return (await response.json()) as T;
    return this.#throwFromErrorResponse(response);
  }

  /**
   * Shared error-body parsing for a non-2xx response, without tracking the generation header.
   *
   * `#readSaveResult`'s 404-etc. fallback delegates here rather than to `#read`: it has already
   * called `#trackGeneration` once for this response, and `#read` also tracks — calling it a
   * second time on the same response would report a single backwards move to `onStaleGeneration`
   * twice.
   */
  async #throwFromErrorResponse(response: Response): Promise<never> {
    const body = (await response.json().catch(() => undefined)) as
      | ValidationResponse | ErrorResponse | undefined;
    if (body && 'errors' in body) {
      throw new RulesApiError(response.status, `Request failed (${response.status}).`, body.errors);
    }
    const message = body && 'error' in body ? body.error : `Request failed (${response.status}).`;
    throw new RulesApiError(response.status, message);
  }
}
