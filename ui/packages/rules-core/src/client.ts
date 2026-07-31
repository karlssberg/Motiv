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

  constructor(options: RulesApiClientOptions) {
    this.#baseUrl = options.baseUrl.replace(/\/$/, '');
    this.#fetch = options.fetch ?? globalThis.fetch.bind(globalThis);
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
      `${this.#baseUrl}/propositions/${encodeURIComponent(name)}`, { method: 'GET' });
    return this.#read<PropositionGetResponse>(response);
  }

  /** POST {baseUrl}/propositions — 400/409 return typed outcomes rather than throwing. */
  async createProposition(request: PropositionCreateRequest): Promise<PropositionSaveResult> {
    return this.#readPropositionResult(await this.#post('/propositions', request));
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
      `${this.#baseUrl}/propositions/${encodeURIComponent(name)}/dependents`, { method: 'GET' });
    return (await this.#read<{ dependents: DependentEntry[] }>(response)).dependents;
  }

  async #readPropositionResult(response: Response): Promise<PropositionSaveResult> {
    if (response.ok) {
      const body = (await response.json()) as { version: number };
      return { outcome: 'saved', version: body.version };
    }

    const body = (await response.json().catch(() => undefined)) as
      | { currentVersion?: number; referrers?: string[]; errors?: RuleError[];
          brokenDependents?: BrokenDependent[]; error?: string }
      | undefined;

    if (response.status === 409) {
      // Three different 409s share the status but not the shape, so they are told apart by body.
      if (body && typeof body.currentVersion === 'number') {
        return { outcome: 'conflict', currentVersion: body.currentVersion };
      }
      if (body?.referrers) return { outcome: 'referenced', referrers: body.referrers };
      return { outcome: 'nameTaken' };
    }

    if (response.status === 400 && body && ('errors' in body || 'brokenDependents' in body)) {
      return {
        outcome: 'invalid',
        errors: body.errors ?? [],
        brokenDependents: body.brokenDependents ?? [],
      };
    }

    const message = body?.error ?? `Request failed (${response.status}).`;
    throw new RulesApiError(response.status, message);
  }

  #post(path: string, body: unknown): Promise<Response> {
    return this.#fetch(`${this.#baseUrl}${path}`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(body),
    });
  }

  async #readSaveResult(response: Response): Promise<RuleSaveResult> {
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
    return this.#read<never>(response); // 404 etc. → RulesApiError as elsewhere
  }

  async #read<T>(response: Response): Promise<T> {
    if (response.ok) return (await response.json()) as T;
    const body = (await response.json().catch(() => undefined)) as
      | ValidationResponse | ErrorResponse | undefined;
    if (body && 'errors' in body) {
      throw new RulesApiError(response.status, `Request failed (${response.status}).`, body.errors);
    }
    const message = body && 'error' in body ? body.error : `Request failed (${response.status}).`;
    throw new RulesApiError(response.status, message);
  }
}
