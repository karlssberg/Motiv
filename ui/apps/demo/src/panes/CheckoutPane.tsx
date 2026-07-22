import { useState } from 'react';
import type { EvaluationResult } from '@motiv/rules-core';

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
 */
export function CheckoutPane() {
  const [customerJson, setCustomerJson] = useState(SAMPLE_CUSTOMER);
  const [outcome, setOutcome] = useState<CheckoutResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  const tryCheckout = async (): Promise<void> => {
    setError(null);
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
