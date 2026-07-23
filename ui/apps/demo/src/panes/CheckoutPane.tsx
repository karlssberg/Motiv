import { useEffect, useState } from 'react';
import {
  validateAgainstSchema,
  type EvaluationResult,
  type JsonSchema,
  type RulesApiClient,
  type SchemaViolation,
} from '@motiv/rules-core';
import { MODEL_TYPE } from '../App.js';

interface CheckoutResponse {
  approved: boolean;
  eligibility: EvaluationResult;
  screening: EvaluationResult;
}

const SAMPLE_CUSTOMER = '{\n  "age": 30,\n  "isActive": true,\n  "orderCount": 3,\n  "orders": [{ "total": 120 }]\n}';

/**
 * Seam: the rule being *used*. POST /api/checkout executes the live CanCheckoutRule (sync)
 * and FraudScreeningRule (async) on the server — save a rule change and the very next
 * checkout reflects it, no restart.
 *
 * The pane deliberately talks raw HTTP (no RulesApiClient) to show the consuming side.
 * The optional client exists only to fetch the catalog's `customer` model schema so
 * obviously malformed customers are caught before the POST; without it (or without
 * `modelTypes` in the catalog) enforcement simply doesn't run.
 */
export function CheckoutPane(props: { client?: RulesApiClient }) {
  const [customerJson, setCustomerJson] = useState(SAMPLE_CUSTOMER);
  const [outcome, setOutcome] = useState<CheckoutResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [violations, setViolations] = useState<SchemaViolation[]>([]);
  const [customerSchema, setCustomerSchema] = useState<JsonSchema | undefined>(undefined);

  const client = props.client;
  useEffect(() => {
    if (!client) return;
    let active = true;
    client.getCatalog()
      .then((catalog) => { if (active) setCustomerSchema(catalog.modelTypes?.[MODEL_TYPE]); })
      .catch(() => { /* no catalog → no enforcement */ });
    return () => { active = false; };
  }, [client]);

  const tryCheckout = async (): Promise<void> => {
    setError(null);
    // Enforce the catalog's model schema when we have one and the text parses;
    // unparseable text still goes to the server, which answers with a bare 400.
    if (customerSchema) {
      let customer: unknown;
      try {
        customer = JSON.parse(customerJson);
      } catch {
        customer = undefined;
      }
      if (customer !== undefined) {
        const found = validateAgainstSchema(customer, customerSchema);
        setViolations(found);
        if (found.length > 0) {
          setOutcome(null);
          return;
        }
      }
    }
    setViolations([]);
    try {
      const response = await fetch('/api/checkout', {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: customerJson,
      });
      // A malformed body yields a bare 400 with an empty body — never assume JSON here.
      if (!response.ok) throw new Error(`Checkout failed (${response.status}).`);
      setOutcome((await response.json()) as CheckoutResponse);
    } catch (cause) {
      setOutcome(null);
      setError(cause instanceof Error ? cause.message : String(cause));
    }
  };

  return (
    <section aria-label="Checkout" className="pane">
      <h2>Checkout (live rules)</h2>
      <label className="field">
        <span>Customer</span>
        <textarea
          aria-label="customer"
          className="control"
          value={customerJson}
          onChange={(e) => setCustomerJson(e.target.value)}
          rows={6}
        />
      </label>
      <button type="button" className="btn" onClick={() => void tryCheckout()}>
        Try checkout
      </button>
      {error && <p role="alert">{error}</p>}
      {violations.length > 0 && (
        <ul aria-label="schema violations" className="errors">
          {violations.map((violation, i) => (
            <li key={`${violation.path}-${i}`} role="alert" className="error">
              {violation.path}: {violation.message}
            </li>
          ))}
        </ul>
      )}
      {outcome && (
        <div className="checkout-outcome">
          <strong className="outcome">{outcome.approved ? 'Approved' : 'Rejected'}</strong>
          <Verdict title="Eligibility (sync rule)" result={outcome.eligibility} />
          <Verdict title="Screening (async rule)" result={outcome.screening} />
        </div>
      )}
    </section>
  );
}

function Verdict(props: { title: string; result: EvaluationResult }) {
  return (
    <div className="verdict">
      <h3>{props.title}</h3>
      <ul>
        {props.result.assertions.map((assertion) => (
          <li key={assertion}>{assertion}</li>
        ))}
      </ul>
    </div>
  );
}
