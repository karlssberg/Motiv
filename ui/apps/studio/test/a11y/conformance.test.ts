import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import axe from 'axe-core';
import { describe, expect, it } from 'vitest';
import { CRITERIA, WCAG_AA, axeTagFor } from '../../a11y/criteria.js';
import { CONFORMANCE, type ConformanceRow } from '../../a11y/conformance.js';
import { enabledAxeRules, renderConformanceReport } from '../../a11y/report.js';

/**
 * The gate on the conformance report (ticket 18: "a VPAT / Accessibility Conformance Report is an
 * explicit output").
 *
 * A conformance report is a document a buyer relies on, and its one failure mode that matters is a
 * row claiming evidence it does not have. Prose cannot be checked, so the report is not prose: it is
 * a record whose mechanical claims are resolved against the suites that actually run, and this file
 * refuses a record whose claims they do not support.
 *
 * What that catches, in both directions — neither of which the sweep passing can catch, because
 * both are statements *about* the sweep:
 *
 * - **Over-claiming.** `1.4.11 Non-text Contrast` was published as "Enforced by axe". axe-core has
 *   no rule for it: `color-contrast` is tagged `wcag143`, which is text contrast only. The row
 *   asserted an enforcement that did not exist anywhere.
 * - **Under-claiming.** `1.4.4 Resize Text` was published as "Not covered by the mechanical suite",
 *   while `meta-viewport` is tagged `wcag144` and has been running in the sweep all along.
 *
 * The record never names an axe rule, which is why neither can recur: it claims a *kind* of
 * evidence, and which rules that resolves to is read from axe's own tags at check and render time.
 */

/** Every rule the sweep would run, indexed by the criterion axe tags it for. */
const enabledByCriterion = ((): Map<string, string[]> => {
  const byCriterion = new Map<string, string[]>();
  for (const rule of axe.getRules([...WCAG_AA])) {
    for (const tag of rule.tags ?? []) {
      if (!/^wcag\d{3,4}$/.test(tag)) continue;
      const existing = byCriterion.get(tag);
      if (existing === undefined) byCriterion.set(tag, [rule.ruleId]);
      else existing.push(rule.ruleId);
    }
  }
  return byCriterion;
})();

/**
 * The titles the keyboard suite declares, read from its source.
 *
 * Read rather than imported: it is a Playwright suite, and importing it would run Playwright's test
 * registration inside vitest. The titles are what a reader of the report goes looking for, so they
 * are what the record cites and what this resolves.
 */
const keyboardTestTitles = ((): string[] => {
  // Composed rather than written as `new URL('…', import.meta.url)`: Vite rewrites that literal
  // pattern into an asset URL, so the path arrives as something `readFileSync` cannot open.
  const here = dirname(fileURLToPath(import.meta.url));
  const source = readFileSync(join(here, '..', '..', 'e2e-a11y', 'keyboard.spec.ts'), 'utf8');
  return [...source.matchAll(/^\s*test\(\s*'((?:[^'\\]|\\.)*)'/gm)].map((match) => match[1] as string);
})();

/** The rows claiming a given kind of evidence. */
const claiming = (kind: ConformanceRow['evidence'][number]['kind']): readonly ConformanceRow[] =>
  CONFORMANCE.filter((row) => row.evidence.some((evidence) => evidence.kind === kind));

describe('the conformance record covers WCAG 2.1 AA exactly', () => {
  it('answers for every Level A and AA criterion, in order, and for nothing else', () => {
    expect(CONFORMANCE.map((row) => row.criterion)).toEqual(CRITERIA.map((c) => c.number));
  });

  it('enumerates the fifty criteria Level A and AA come to', () => {
    // Asserted in its own right: a criterion dropped from the catalogue *and* the record at once
    // would keep them equal, and the report would silently stop answering for it. Enumeration is
    // the whole point — the report this replaces listed fourteen and dismissed the rest in a line.
    expect(CRITERIA.filter((c) => c.level === 'A')).toHaveLength(30);
    expect(CRITERIA.filter((c) => c.level === 'AA')).toHaveLength(20);
  });
});

describe('a mechanical claim resolves to a check that runs', () => {
  it('claims axe only where the sweep has a rule for the criterion', () => {
    const hollow = claiming('axe')
      .filter((row) => (enabledByCriterion.get(axeTagFor(row.criterion)) ?? []).length === 0)
      .map((row) => row.criterion);

    expect(hollow).toEqual([]);
  });

  it('claims axe wherever the sweep covers an applicable criterion', () => {
    // The under-claiming direction, and the reason it matters as much as the other: coverage the
    // report omits is coverage nobody would notice losing.
    const silent = CONFORMANCE
      .filter((row) => row.conformance !== 'Not Applicable')
      .filter((row) => (enabledByCriterion.get(axeTagFor(row.criterion)) ?? []).length > 0)
      .filter((row) => !row.evidence.some((evidence) => evidence.kind === 'axe'))
      .map((row) => row.criterion);

    expect(silent).toEqual([]);
  });

  it('cites only keyboard tests the suite declares', () => {
    const missing = CONFORMANCE.flatMap((row) =>
      row.evidence
        .flatMap((evidence) => (evidence.kind === 'keyboard' ? evidence.tests : []))
        .filter((title) => !keyboardTestTitles.includes(title))
        .map((title) => `${row.criterion} cites "${title}"`));

    expect(missing).toEqual([]);
    // A guard on the guard: a title regex that matched nothing would make the check above vacuous.
    expect(keyboardTestTitles.length).toBeGreaterThan(0);
  });

  it('agrees with the report on which rules the sweep runs', () => {
    // The rendered appendix names rules, and it must name axe's, not a list maintained beside it.
    expect(enabledAxeRules('1.4.3')).toEqual(['color-contrast']);
    expect(enabledAxeRules('1.4.11')).toEqual([]);
  });
});

describe('no row rests on nothing', () => {
  it('gives every criterion at least one piece of evidence', () => {
    expect(CONFORMANCE.filter((row) => row.evidence.length === 0).map((r) => r.criterion)).toEqual([]);
  });

  it('reserves Not Evaluated for what a person still has to judge, and requires it of them', () => {
    // Both directions, because this is the fail-closed hinge of the record: a row whose only
    // evidence is an owed manual pass cannot claim support, and a row claiming nothing was
    // evaluated has to name the pass that would evaluate it.
    const onlyManual = (row: ConformanceRow): boolean =>
      row.evidence.every((evidence) => evidence.kind === 'manual');

    expect(CONFORMANCE.filter(onlyManual).filter((row) => row.conformance !== 'Not Evaluated')
      .map((r) => r.criterion)).toEqual([]);
    expect(CONFORMANCE.filter((row) => row.conformance === 'Not Evaluated')
      .filter((row) => !row.evidence.some((evidence) => evidence.kind === 'manual'))
      .map((r) => r.criterion)).toEqual([]);
  });

  it('makes every judgement carry its reason', () => {
    const unreasoned = CONFORMANCE.flatMap((row) =>
      row.evidence
        .filter((evidence) => 'because' in evidence && evidence.because.trim() === '')
        .map(() => row.criterion));

    expect(unreasoned).toEqual([]);
  });

  it('backs a Not Applicable verdict with the reason it does not apply', () => {
    const unbacked = CONFORMANCE
      .filter((row) => row.conformance === 'Not Applicable')
      .filter((row) => !row.evidence.some((evidence) => evidence.kind === 'not-applicable'))
      .map((row) => row.criterion);

    expect(unbacked).toEqual([]);
  });
});

describe('the published report is the record', () => {
  it('holds the claim the accessibility page makes in prose', () => {
    // `docs/accessibility/index.md` says no criterion is reported as failing, and no gate can read a
    // sentence. It can read what the sentence is about: the day a row becomes a failure, that page
    // needs rewriting, and this is what says so.
    const failing = CONFORMANCE
      .filter((row) => row.conformance === 'Does Not Support' || row.conformance === 'Partially Supports')
      .map((row) => row.criterion);

    expect(failing).toEqual([]);
  });

  it('matches the committed conformance report', async () => {
    // The document a buyer is handed is generated from the record, so the two cannot drift: a
    // record edit that was never published fails here, and so does a document edit the record does
    // not support. An axe upgrade that drops a rule fails here too, which is the point — the
    // appendix names the rules that ran, and it has to keep being true. Regenerate: `pnpm a11y:report`.
    await expect(renderConformanceReport(CONFORMANCE))
      .toMatchFileSnapshot('../../../../../docs/accessibility/vpat.md');
  });
});
