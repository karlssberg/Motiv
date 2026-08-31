import axe from 'axe-core';
import { CRITERIA, WCAG_AA, axeTagFor, type Level } from './criteria.js';
import type { ConformanceRow, Evidence } from './conformance.js';

/**
 * Renders the Accessibility Conformance Report from the record.
 *
 * The document is generated rather than written because a hand-written one drifts: the report this
 * replaces claimed axe enforcement for a criterion axe has no rule for, and denied it for one axe
 * had been checking all along, and nothing anywhere could notice either. Here the mechanical rows
 * are resolved against axe's own tags at render time, so the published document says what the suite
 * that ran actually covers — and an axe upgrade that drops a rule changes the report rather than
 * quietly falsifying it.
 */

/** Criteria by number, for the numbered-and-titled headings each table wants. */
const BY_NUMBER = new Map(CRITERIA.map((criterion) => [criterion.number, criterion]));

/** How a criterion is named wherever the report refers to one: its number and its WCAG title. */
function nameOf(criterion: string): string {
  const found = BY_NUMBER.get(criterion);
  return found === undefined ? criterion : `${criterion} ${found.title}`;
}

/** Rule ids the sweep enables for a criterion, from axe's own tags. Sorted, so the report is stable. */
export function enabledAxeRules(criterion: string): string[] {
  const tag = axeTagFor(criterion);
  return axe.getRules([...WCAG_AA])
    .filter((rule) => (rule.tags ?? []).includes(tag))
    .map((rule) => rule.ruleId)
    .sort();
}

/** The evidence kinds carrying an argument, which the report publishes rather than leaving in source. */
function isArgued(evidence: Evidence): evidence is Extract<Evidence, { kind: 'reasoned' | 'not-applicable' }> {
  return evidence.kind === 'reasoned' || evidence.kind === 'not-applicable';
}

function isManual(evidence: Evidence): evidence is Extract<Evidence, { kind: 'manual' }> {
  return evidence.kind === 'manual';
}

/** The evidence column: the kinds a row rests on, strongest first. */
function evidenceSummary(row: ConformanceRow): string {
  const kinds = new Set(row.evidence.map((evidence) => evidence.kind));
  const parts: string[] = [];
  if (kinds.has('axe')) parts.push(`axe (${enabledAxeRules(row.criterion).length})`);
  if (kinds.has('keyboard')) parts.push('keyboard suite');
  if (kinds.has('reasoned')) parts.push('structural');
  if (kinds.has('not-applicable')) parts.push('not applicable');
  if (kinds.has('manual')) parts.push('**manual pass owed**');
  return parts.join(' · ');
}

/**
 * Text made safe to sit in a markdown table cell.
 *
 * A pipe would end the cell and a newline would end the row, so both are neutralised. **The
 * backslash is escaped first, and the order is the whole point**: escaping only the pipe turns a
 * remark that ends in a backslash before a pipe into `\\|`, which markdown reads as an escaped
 * backslash followed by a *bare* pipe — the cell ends early and every column after it shifts, in a
 * document whose entire purpose is to be read as a table of claims. Escaping the backslash first
 * makes that same input `\\\\\\|`: a literal backslash, then an escaped pipe.
 */
export function escapeCell(text: string): string {
  return text.replace(/\\/g, '\\\\').replace(/\|/g, '\\|').replace(/\n/g, ' ');
}

/**
 * The remarks column: the arguments first, then the row's own remark.
 *
 * A structural verdict *is* its argument — "Supports, structural" with the reason left behind in the
 * source file is the same unsupported assertion the record exists to prevent, merely relocated. So
 * the reason a criterion does not apply, or the property of the build that settles it, is published.
 */
function remarks(row: ConformanceRow): string {
  const argued = row.evidence.filter(isArgued).map((evidence) => evidence.because);
  return escapeCell([...argued, row.remark].join(' '));
}

/** One conformance table, for the criteria at a level. */
function table(rows: readonly ConformanceRow[]): string {
  return [
    '| Criterion | Conformance | Evidence | Remarks |',
    '|---|---|---|---|',
    ...rows.map((row) =>
      `| ${escapeCell(nameOf(row.criterion))} | ${row.conformance} | ${evidenceSummary(row)} | ${remarks(row)} |`),
  ].join('\n');
}

/** The tally a reader looking for the shape of the answer reads first. */
function tally(rows: readonly ConformanceRow[]): string {
  const counts = new Map<string, number>();
  for (const row of rows) counts.set(row.conformance, (counts.get(row.conformance) ?? 0) + 1);
  return ['Supports', 'Partially Supports', 'Does Not Support', 'Not Applicable', 'Not Evaluated']
    .filter((verdict) => counts.has(verdict))
    .map((verdict) => `| ${verdict} | ${counts.get(verdict)} |`)
    .join('\n');
}

/** What each outstanding row is waiting on, so the manual pass has a worklist rather than a mood. */
function owed(rows: readonly ConformanceRow[]): string {
  return rows
    .flatMap((row) => row.evidence.filter(isManual)
      .map((evidence) => `- **${nameOf(row.criterion)}** — establish ${evidence.because}.`))
    .join('\n');
}

/** Per criterion, the axe rules that ran for it. Criteria with none are omitted, not implied. */
function axeAppendix(rows: readonly ConformanceRow[]): string {
  return rows
    .map((row) => ({ criterion: row.criterion, rules: enabledAxeRules(row.criterion) }))
    .filter(({ rules }) => rules.length > 0)
    .map(({ criterion, rules }) =>
      `| ${escapeCell(nameOf(criterion))} | ${rules.map((rule) => `\`${rule}\``).join(', ')} |`)
    .join('\n');
}

/** The whole document. */
export function renderConformanceReport(rows: readonly ConformanceRow[]): string {
  const at = (level: Level): ConformanceRow[] =>
    rows.filter((row) => BY_NUMBER.get(row.criterion)?.level === level);

  return `---
title: Conformance Report
description: Motiv Studio's Accessibility Conformance Report — every WCAG 2.1 Level A and AA success criterion, the verdict for each, and the evidence the verdict rests on.
---

<!--
  Generated from ui/apps/studio/a11y/conformance.ts. Do not edit this file by hand: the gate in
  ui/apps/studio/test/a11y/conformance.test.ts regenerates it and fails if it has drifted.
  Regenerate with \`pnpm --filter @motiv-rules/studio a11y:report\`.
-->

# Accessibility Conformance Report — Motiv Studio

**Product:** Motiv Studio, the rules-governance application in this repository.
**Standard:** Web Content Accessibility Guidelines 2.1, Levels A and AA.
**Scope:** the Studio single-page application. The \`@motiv-rules/*\` packages are headless and ship
no components; see [Accessibility](index.md) for what that costs an adopter.

This report answers for **every** Level A and AA success criterion — all ${rows.length} of them —
because a criterion nobody listed is a criterion nobody decided about, and in a document a buyer
reads that is indistinguishable from a pass.

## How to read a row

| Verdict | Meaning |
|---|---|
| Supports | The criterion is met, on the evidence named in the row. |
| Partially Supports | Some of the functionality does not meet the criterion. |
| Does Not Support | The criterion is not met. |
| Not Applicable | The criterion does not apply to this application, for the reason given. |
| Not Evaluated | Nobody has judged it yet. The manual screen-reader pass is what would. |

\`Not Evaluated\` is not part of ITI's VPAT vocabulary and is here deliberately. The manual pass is
[scripted](index.md#manual--the-screen-reader-pass) and outstanding, and for a criterion only a
person can judge, the honest answer is that no person has. Claiming support on no evidence, or
reporting a failure nobody observed, would both be false — and the first is the one a buyer is
harmed by.

The **Evidence** column names what the verdict rests on:

- **axe (*n*)** — the [axe-core](https://github.com/dequelabs/axe-core) sweep covers this criterion,
  with *n* rules enabled for it. The sweep runs on every pull request, over every view and every open
  surface, in both colour schemes. The rules are listed in the appendix; neither this document nor
  the record behind it maintains that list by hand — it is read from axe's own tags, so a claim
  cannot outlive the rule it rests on.
- **keyboard suite** — Playwright tests in \`e2e-a11y/keyboard.spec.ts\` that drive the behaviour a
  role promises, which no scan can see.
- **structural** — an argument about how the application is built that settles the criterion.
- **not applicable** — the reason the criterion does not apply.
- **manual pass owed** — this row is waiting on a person with a screen reader.

## Summary

| Verdict | Criteria |
|---|---|
${tally(rows)}

## Level A

${table(at('A'))}

## Level AA

${table(at('AA'))}

## What the manual pass still owes

Each entry names what the [scripted pass](index.md#manual--the-screen-reader-pass) has to establish
before the criterion above it can be answered.

${owed(rows)}

## Appendix — the axe rules behind each mechanical claim

Read from axe-core ${axe.version} under the tags the sweep runs (\`${WCAG_AA.join('`, `')}\`).
Criteria absent from this table have no axe rule at all, which is why they are answered by the
keyboard suite, by a structural argument, or not yet at all.

| Criterion | Rules |
|---|---|
${axeAppendix(rows)}
`;
}
