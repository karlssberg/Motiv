import type {
  Catalog, ErrorResponse, EvaluateRequest, EvaluationResult,
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
