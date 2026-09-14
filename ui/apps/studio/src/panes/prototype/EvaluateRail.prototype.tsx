// PROTOTYPE — throwaway. Four variants of the rule rail, switchable via `?variant=`, on the
// existing rule route: "current" (Evaluate + Checkout as shipped), then A/B/C which fold the
// before/after comparison INTO Evaluate and scope it to the open rule.
import type { RulesApiClient } from '@motiv-rules/core';
import { PrototypeSwitcher, useVariant } from '../../shell/PrototypeSwitcher.js';
import { EvaluatePane } from '../EvaluatePane.js';
import { CheckoutPane } from '../CheckoutPane.js';
import { VariantA } from './VariantA.js';
import { VariantB } from './VariantB.js';
import { VariantC } from './VariantC.js';
import { ScenarioTable } from './ScenarioTable.js';
import './prototype.css';

// `current` stays first: with no `?variant=` the rail is the shipped design, so tests see main.
const VARIANTS = [
  { key: 'current', name: 'as shipped: Evaluate + Checkout' },
  { key: 'C1', name: 'scenarios: reveal only once evaluated, name opens editor' },
  { key: 'C2', name: 'scenarios: always reveal, model editor over explanation' },
  { key: 'C3', name: 'scenarios: always reveal, Model | Why tabs' },
  { key: 'C', name: 'scenario table (round one)' },
  { key: 'A', name: 'compare mode, one diff list' },
  { key: 'B', name: 'live | draft side by side' },
] as const;

export function EvaluateRailPrototype(props: { client: RulesApiClient; ruleName: string; version?: number | undefined }) {
  const [variant, setVariant] = useVariant(VARIANTS.map((v) => v.key));
  return (
    <div className="rail">
      {variant === 'current' && <><EvaluatePane client={props.client} /><CheckoutPane client={props.client} /></>}
      {variant === 'A' && <VariantA {...props} />}
      {variant === 'B' && <VariantB {...props} />}
      {variant === 'C' && <VariantC {...props} />}
      {variant === 'C1' && <ScenarioTable {...props} reveal="evaluated-only" />}
      {variant === 'C2' && <ScenarioTable {...props} reveal="stacked" />}
      {variant === 'C3' && <ScenarioTable {...props} reveal="tabs" />}
      <PrototypeSwitcher variants={VARIANTS} current={variant} onChange={setVariant} />
    </div>
  );
}
