/**
 * The WCAG 2.1 success criteria at Level A and AA — the floor ticket 18 set — and the axe tag set
 * the mechanical sweep runs.
 *
 * This file is the *catalogue*, not the claim: it says which criteria a conformance report for this
 * application has to answer for, and says nothing about how any of them fare. The answers live in
 * `conformance.ts`, one row per criterion, and the gate in `test/a11y/conformance.test.ts` refuses a
 * record that does not cover this list exactly.
 *
 * Enumerating them matters because the alternative is what the report did before: name the fourteen
 * criteria someone thought of and dismiss the other thirty-six in a sentence. A criterion nobody
 * listed is a criterion nobody decided about, and in a document a buyer reads that reads as a pass.
 */

/**
 * The tags that make up the AA floor, and the single definition of it.
 *
 * The axe sweep runs exactly these, and the conformance record's mechanical claims are checked
 * against exactly these — so a claim of "enforced by axe" is true of the suite that actually runs
 * rather than of a suite someone remembers. Shared for that reason: two copies could drift, and the
 * drift would be silent in the direction that matters (a report claiming coverage that was dropped).
 *
 * `best-practice` is deliberately excluded: it is not AA.
 */
export const WCAG_AA = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] as const;

/** The two levels this report answers for. AAA is out of scope, as the target is an AA floor. */
export type Level = 'A' | 'AA';

/** One success criterion, as WCAG numbers and names it. */
export interface SuccessCriterion {
  /** The dotted number, e.g. `1.4.3`. */
  readonly number: string;
  /** The criterion's WCAG title, e.g. `Contrast (Minimum)`. */
  readonly title: string;
  readonly level: Level;
}

/**
 * The axe tag naming a criterion: axe tags each rule with the criteria it speaks to, as `wcag`
 * followed by the number's digits — `1.4.3` is `wcag143` and `1.4.11` is `wcag1411`. This is the
 * join between the record's claims and axe's own account of what it can check.
 */
export function axeTagFor(criterion: string): string {
  return `wcag${criterion.split('.').join('')}`;
}

/** Every WCAG 2.1 Level A and AA success criterion, in WCAG's order. */
export const CRITERIA: readonly SuccessCriterion[] = [
  { number: '1.1.1', title: 'Non-text Content', level: 'A' },
  { number: '1.2.1', title: 'Audio-only and Video-only (Prerecorded)', level: 'A' },
  { number: '1.2.2', title: 'Captions (Prerecorded)', level: 'A' },
  { number: '1.2.3', title: 'Audio Description or Media Alternative (Prerecorded)', level: 'A' },
  { number: '1.2.4', title: 'Captions (Live)', level: 'AA' },
  { number: '1.2.5', title: 'Audio Description (Prerecorded)', level: 'AA' },
  { number: '1.3.1', title: 'Info and Relationships', level: 'A' },
  { number: '1.3.2', title: 'Meaningful Sequence', level: 'A' },
  { number: '1.3.3', title: 'Sensory Characteristics', level: 'A' },
  { number: '1.3.4', title: 'Orientation', level: 'AA' },
  { number: '1.3.5', title: 'Identify Input Purpose', level: 'AA' },
  { number: '1.4.1', title: 'Use of Color', level: 'A' },
  { number: '1.4.2', title: 'Audio Control', level: 'A' },
  { number: '1.4.3', title: 'Contrast (Minimum)', level: 'AA' },
  { number: '1.4.4', title: 'Resize Text', level: 'AA' },
  { number: '1.4.5', title: 'Images of Text', level: 'AA' },
  { number: '1.4.10', title: 'Reflow', level: 'AA' },
  { number: '1.4.11', title: 'Non-text Contrast', level: 'AA' },
  { number: '1.4.12', title: 'Text Spacing', level: 'AA' },
  { number: '1.4.13', title: 'Content on Hover or Focus', level: 'AA' },
  { number: '2.1.1', title: 'Keyboard', level: 'A' },
  { number: '2.1.2', title: 'No Keyboard Trap', level: 'A' },
  { number: '2.1.4', title: 'Character Key Shortcuts', level: 'A' },
  { number: '2.2.1', title: 'Timing Adjustable', level: 'A' },
  { number: '2.2.2', title: 'Pause, Stop, Hide', level: 'A' },
  { number: '2.3.1', title: 'Three Flashes or Below Threshold', level: 'A' },
  { number: '2.4.1', title: 'Bypass Blocks', level: 'A' },
  { number: '2.4.2', title: 'Page Titled', level: 'A' },
  { number: '2.4.3', title: 'Focus Order', level: 'A' },
  { number: '2.4.4', title: 'Link Purpose (In Context)', level: 'A' },
  { number: '2.4.5', title: 'Multiple Ways', level: 'AA' },
  { number: '2.4.6', title: 'Headings and Labels', level: 'AA' },
  { number: '2.4.7', title: 'Focus Visible', level: 'AA' },
  { number: '2.5.1', title: 'Pointer Gestures', level: 'A' },
  { number: '2.5.2', title: 'Pointer Cancellation', level: 'A' },
  { number: '2.5.3', title: 'Label in Name', level: 'A' },
  { number: '2.5.4', title: 'Motion Actuation', level: 'A' },
  { number: '3.1.1', title: 'Language of Page', level: 'A' },
  { number: '3.1.2', title: 'Language of Parts', level: 'AA' },
  { number: '3.2.1', title: 'On Focus', level: 'A' },
  { number: '3.2.2', title: 'On Input', level: 'A' },
  { number: '3.2.3', title: 'Consistent Navigation', level: 'AA' },
  { number: '3.2.4', title: 'Consistent Identification', level: 'AA' },
  { number: '3.3.1', title: 'Error Identification', level: 'A' },
  { number: '3.3.2', title: 'Labels or Instructions', level: 'A' },
  { number: '3.3.3', title: 'Error Suggestion', level: 'AA' },
  { number: '3.3.4', title: 'Error Prevention (Legal, Financial, Data)', level: 'AA' },
  { number: '4.1.1', title: 'Parsing', level: 'A' },
  { number: '4.1.2', title: 'Name, Role, Value', level: 'A' },
  { number: '4.1.3', title: 'Status Messages', level: 'AA' },
];
