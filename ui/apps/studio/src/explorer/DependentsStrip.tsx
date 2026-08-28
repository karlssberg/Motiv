import type { DependentEntry } from '@motiv-rules/core';

/** "1 rule and 2 propositions", pluralised, omitting a kind with no members. */
function summarise(dependents: DependentEntry[]): string {
  const rules = dependents.filter((dependent) => dependent.kind === 'rule').length;
  const propositions = dependents.length - rules;
  const parts: string[] = [];
  if (rules > 0) parts.push(`${rules} rule${rules === 1 ? '' : 's'}`);
  if (propositions > 0) parts.push(`${propositions} proposition${propositions === 1 ? '' : 's'}`);
  return parts.join(' and ');
}

/**
 * The blast radius, shown while editing rather than sprung at the moment of saving. Who references
 * this proposition is a fact about *other* documents, so it stays accurate as the user types.
 */
export function DependentsStrip(props: { dependents: DependentEntry[] }) {
  if (props.dependents.length === 0) return null;

  return (
    <div className="dependents-strip">
      <strong>Changing this affects {summarise(props.dependents)}:</strong>
      <ul>
        {props.dependents.map((dependent) => (
          <li key={`${dependent.kind}:${dependent.name}`}>
            <span className="origin-badge">{dependent.kind}</span> {dependent.name}
          </li>
        ))}
      </ul>
    </div>
  );
}
