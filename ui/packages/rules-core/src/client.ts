import type {
  Catalog, ErrorResponse, EvaluateRequest, EvaluationResult,
  RuleError, ValidateRequest, ValidationResponse,
} from './contracts.js';

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

  #post(path: string, body: unknown): Promise<Response> {
    return this.#fetch(`${this.#baseUrl}${path}`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(body),
    });
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
