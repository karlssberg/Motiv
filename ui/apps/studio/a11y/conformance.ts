/**
 * Motiv Studio's answer to every WCAG 2.1 Level A and AA success criterion — the record the
 * published conformance report is generated from.
 *
 * Two properties make it a record rather than an opinion, and both are enforced by
 * `test/a11y/conformance.test.ts`:
 *
 * - **It answers for all fifty criteria.** Not the ones someone thought of. A criterion nobody
 *   listed is a criterion nobody decided about, and in a procurement document that reads as a pass.
 * - **No row rests on nothing.** Every verdict names its evidence, and `Not Evaluated` is the only
 *   verdict available to a criterion whose evidence is an owed manual pass. Claiming support by
 *   omission is exactly the failure the report exists to avoid.
 *
 * A row never names an axe rule. It claims that *axe covers this criterion*, and which rules that
 * resolves to is read from axe's own tags when the record is checked and when the report is
 * rendered — so the claim cannot outlive the rule, and an axe upgrade that drops one shows up as a
 * changed report rather than as a stale sentence nobody re-read.
 */

/**
 * The verdict vocabulary, as ITI's VPAT defines it, plus one addition.
 *
 * `Not Evaluated` is not in the four-term set, and it is here deliberately: the manual screen-reader
 * pass is scripted and outstanding, and the honest answer for a criterion only a person can judge is
 * that nobody has judged it yet. The alternatives were to claim support on no evidence, or to report
 * a failure that has not been observed — both are false, and the first is the one buyers are hurt by.
 */
export type Conformance =
  | 'Supports'
  | 'Partially Supports'
  | 'Does Not Support'
  | 'Not Applicable'
  | 'Not Evaluated';

/** What a verdict rests on. */
export type Evidence =
  /** The axe sweep covers this criterion. Which rules is resolved from axe's tags, never listed here. */
  | { readonly kind: 'axe' }
  /** Named tests in `e2e-a11y/keyboard.spec.ts` — the behaviour a role promises, which a scan cannot see. */
  | { readonly kind: 'keyboard'; readonly tests: readonly string[] }
  /** A structural argument: something about how the app is built settles the criterion. */
  | { readonly kind: 'reasoned'; readonly because: string }
  /** Owed. What the scripted screen-reader pass has to establish before this criterion is answered. */
  | { readonly kind: 'manual'; readonly because: string }
  /** Why the criterion does not apply to this application at all. */
  | { readonly kind: 'not-applicable'; readonly because: string };

/** One criterion's answer. */
export interface ConformanceRow {
  /** The dotted criterion number, matching `CRITERIA` in `criteria.ts`. */
  readonly criterion: string;
  readonly conformance: Conformance;
  readonly evidence: readonly Evidence[];
  /** The remark column: what a reader needs in order to weigh the verdict. */
  readonly remark: string;
}

/** The scripted manual pass, referred to by the rows that are waiting on it. */
const MANUAL = (because: string): Evidence => ({ kind: 'manual', because });

export const CONFORMANCE: readonly ConformanceRow[] = [
  {
    criterion: '1.1.1',
    conformance: 'Supports',
    evidence: [{ kind: 'axe' }],
    remark:
      'Icon-only controls carry `aria-label`; the inline SVG marks that decorate them are '
      + '`aria-hidden`. Studio ships no raster images.',
  },
  {
    criterion: '1.2.1',
    conformance: 'Not Applicable',
    evidence: [{ kind: 'not-applicable', because: 'Studio contains no audio or video.' }],
    remark: 'There is no media to provide an alternative for.',
  },
  {
    criterion: '1.2.2',
    conformance: 'Not Applicable',
    evidence: [{ kind: 'not-applicable', because: 'Studio contains no audio or video.' }],
    remark: 'There is no synchronised media to caption.',
  },
  {
    criterion: '1.2.3',
    conformance: 'Not Applicable',
    evidence: [{ kind: 'not-applicable', because: 'Studio contains no audio or video.' }],
    remark: 'There is no synchronised media to describe.',
  },
  {
    criterion: '1.2.4',
    conformance: 'Not Applicable',
    evidence: [{ kind: 'not-applicable', because: 'Studio contains no live media.' }],
    remark: 'Nothing in the application is broadcast.',
  },
  {
    criterion: '1.2.5',
    conformance: 'Not Applicable',
    evidence: [{ kind: 'not-applicable', because: 'Studio contains no audio or video.' }],
    remark: 'There is no video track to describe.',
  },
  {
    criterion: '1.3.1',
    conformance: 'Supports',
    evidence: [
      { kind: 'axe' },
      MANUAL('that the announced structure of a composition is comprehensible, not merely present'),
    ],
    remark:
      'A rule\'s composition is carried by nested labelled `group`s, each named by the expression it '
      + 'holds; disclosure controls name what they control while it is mounted. The structure is '
      + 'checked mechanically; whether it *reads* as the rule\'s meaning is the question the manual '
      + 'pass exists for.',
  },
  {
    criterion: '1.3.2',
    conformance: 'Not Evaluated',
    evidence: [MANUAL('that reading order matches visual order on the builder, the DSL strip and the palette')],
    remark: 'Sequence is a question about what a screen reader reads in what order. No scan sees it.',
  },
  {
    criterion: '1.3.3',
    conformance: 'Not Evaluated',
    evidence: [MANUAL('that no instruction in the UI depends on shape, position or visual appearance alone')],
    remark: 'Requires reading the interface copy as a person, against what is on screen.',
  },
  {
    criterion: '1.3.4',
    conformance: 'Supports',
    evidence: [{ kind: 'axe' }],
    remark: 'No orientation is locked; the layout reflows at the 900px breakpoint in either orientation.',
  },
  {
    criterion: '1.3.5',
    conformance: 'Not Applicable',
    evidence: [
      {
        kind: 'not-applicable',
        because:
          'Studio collects no information about the user. Its inputs name rules, propositions and '
          + 'grants — none of them is a field about the person filling it in.',
      },
    ],
    remark:
      'The axe rule for this criterion (`autocomplete-valid`) is enabled in the sweep regardless, so '
      + 'a field that did collect personal data would be checked the day it appeared.',
  },
  {
    criterion: '1.4.1',
    conformance: 'Not Evaluated',
    evidence: [
      { kind: 'axe' },
      MANUAL('that no state — validity, selection, staleness, async-ness — is signalled by colour alone'),
    ],
    remark:
      'axe checks only that links in a block of text are distinguishable without colour. The '
      + 'badges and validity marks the builder uses are the part a person has to judge.',
  },
  {
    criterion: '1.4.2',
    conformance: 'Not Applicable',
    evidence: [{ kind: 'not-applicable', because: 'Studio plays no audio.' }],
    remark: 'Nothing autoplays because there is nothing to play.',
  },
  {
    criterion: '1.4.3',
    conformance: 'Supports',
    evidence: [{ kind: 'axe' }],
    remark:
      'Enforced on every view and every open surface, in both colour schemes — the stylesheet '
      + 'defines a second palette under `prefers-color-scheme: dark`, and contrast holding in one '
      + 'says nothing about the other.',
  },
  {
    criterion: '1.4.4',
    conformance: 'Not Evaluated',
    evidence: [
      { kind: 'axe' },
      MANUAL('that content and function survive 200% text zoom without loss or clipping'),
    ],
    remark:
      'The viewport meta permits zoom, which is what axe checks here and what a `user-scalable=no` '
      + 'would break. Whether the layout survives the zoom is the remaining half.',
  },
  {
    criterion: '1.4.5',
    conformance: 'Supports',
    evidence: [
      {
        kind: 'reasoned',
        because:
          'Every string on screen is text. The only images are the inline SVG icons in '
          + '`src/shell/icons.tsx`, none of which renders a word.',
      },
    ],
    remark: 'There are no images of text to replace.',
  },
  {
    criterion: '1.4.10',
    conformance: 'Not Evaluated',
    evidence: [MANUAL('that no view scrolls in two dimensions at a 320px-equivalent width')],
    remark:
      'The layout has a single `max-width: 900px` breakpoint, which is coarser than this criterion '
      + 'asks about. The DSL strip and the builder rows are where two-dimensional scrolling would '
      + 'appear first.',
  },
  {
    criterion: '1.4.11',
    conformance: 'Not Evaluated',
    evidence: [
      MANUAL('that control boundaries, focus indicators and the builder\'s state marks meet 3:1 against their surrounds'),
    ],
    remark:
      'axe has no rule for this criterion at any version — `color-contrast` is text contrast only — '
      + 'so the sweep passing says nothing about it. This row previously read "Enforced by axe", '
      + 'which was the one false claim in the report and is what the record\'s gate now prevents.',
  },
  {
    criterion: '1.4.12',
    conformance: 'Supports',
    evidence: [{ kind: 'axe' }],
    remark: 'No inline style overrides line height, letter spacing or word spacing.',
  },
  {
    criterion: '1.4.13',
    conformance: 'Not Evaluated',
    evidence: [
      MANUAL('that the hover cards are dismissible, hoverable and persistent — the DSL payload popover and the node hover card'),
    ],
    remark:
      'Studio has additional content on hover and on focus: the DSL strip\'s payload popover and the '
      + 'builder\'s anchored cards. All three sub-requirements need driving by hand.',
  },
  {
    criterion: '2.1.1',
    conformance: 'Supports',
    evidence: [
      { kind: 'axe' },
      {
        kind: 'keyboard',
        tests: [
          'is one stop in the tab sequence, not one per proposition',
          'moves between rows on the arrow keys, and into and out of a subtree',
          'jumps to a namespace by typing its first letters',
          'chooses the focused proposition on Enter, so the palette is crossable by keyboard alone',
          'navigates on Enter, the key a link is operated with',
        ],
      },
    ],
    remark:
      'Every control is reachable and operable from the keyboard, and the surface that declares a '
      + 'keyboard pattern implements it: the palette\'s namespace tree has a roving tabindex, '
      + 'arrow-key movement, `Home`/`End` and type-ahead, all driven in a real browser.',
  },
  {
    criterion: '2.1.2',
    conformance: 'Supports',
    evidence: [
      {
        kind: 'reasoned',
        because:
          'The one trapping surface is the modal, and it is a native `<dialog>` opened with '
          + '`showModal()` — so the trap, Escape and the release are the platform\'s, not an '
          + 'implementation that could be wrong.',
      },
    ],
    remark: 'Nothing else in the application takes focus captive.',
  },
  {
    criterion: '2.1.4',
    conformance: 'Supports',
    evidence: [
      {
        kind: 'reasoned',
        because:
          'The only single-character shortcut is the namespace tree\'s type-ahead, which is active '
          + 'solely while focus is inside the tree — the exception this criterion names. The command '
          + 'palette is opened on a modified key.',
      },
    ],
    remark: 'No single-character shortcut is live on the document.',
  },
  {
    criterion: '2.2.1',
    conformance: 'Supports',
    evidence: [{ kind: 'axe' }],
    remark:
      'Studio imposes no time limit on any interaction. Editing state is held until the user acts on '
      + 'it, and validation is debounced rather than expiring.',
  },
  {
    criterion: '2.2.2',
    conformance: 'Supports',
    evidence: [{ kind: 'axe' }],
    remark: 'Nothing moves, blinks, scrolls or updates on its own.',
  },
  {
    criterion: '2.3.1',
    conformance: 'Supports',
    evidence: [{ kind: 'reasoned', because: 'Nothing in the application flashes at any rate.' }],
    remark: 'No flashing content exists.',
  },
  {
    criterion: '2.4.1',
    conformance: 'Supports',
    evidence: [{ kind: 'axe' }],
    remark:
      'The shell is landmarked — a `header` holding the page `nav`, and a `main` holding the page — '
      + 'so the repeated chrome is bypassable by landmark navigation.',
  },
  {
    criterion: '2.4.2',
    conformance: 'Supports',
    evidence: [
      { kind: 'axe' },
      {
        kind: 'reasoned',
        because:
          'The title names the current page and its selection, and is rewritten on every route '
          + 'change — so the three hash routes are three titles rather than one, in the history '
          + 'list and in what a screen reader announces on navigation.',
      },
    ],
    remark:
      'Enumerating this criterion is what found the defect: axe checks only that a title exists, and '
      + 'a single-page application with a static `<title>` passes it while every route shares one '
      + 'name. `useDocumentTitle` fixes that and is unit-tested.',
  },
  {
    criterion: '2.4.3',
    conformance: 'Not Evaluated',
    evidence: [
      {
        kind: 'keyboard',
        tests: ['is one stop in the tab sequence, not one per proposition'],
      },
      MANUAL('that focus order preserves meaning across the builder, the inline editors and the palette'),
    ],
    remark:
      'One ordering question is pinned mechanically — the namespace tree is a single tab stop rather '
      + 'than one per row. Whether the remaining order preserves meaning is a person\'s judgement.',
  },
  {
    criterion: '2.4.4',
    conformance: 'Supports',
    evidence: [{ kind: 'axe' }],
    remark: 'Every link has a name, and the page switcher\'s links name their destination.',
  },
  {
    criterion: '2.4.5',
    conformance: 'Supports',
    evidence: [
      {
        kind: 'reasoned',
        because:
          'A proposition is reachable two ways that do not depend on each other: browsing the '
          + 'namespace tree, and searching the command palette. Every view is additionally addressable '
          + 'by its hash route.',
      },
    ],
    remark: 'Studio is a single-page application; the routes are its pages and each is directly linkable.',
  },
  {
    criterion: '2.4.6',
    conformance: 'Not Evaluated',
    evidence: [MANUAL('that headings and labels describe topic or purpose, rather than merely existing')],
    remark:
      'Presence is checked mechanically under 4.1.2 and 3.3.2. Descriptiveness is a judgement about '
      + 'wording, which is what the manual pass reads for.',
  },
  {
    criterion: '2.4.7',
    conformance: 'Not Evaluated',
    evidence: [MANUAL('that every focusable control shows a visible indicator, in both colour schemes')],
    remark:
      '`:focus-visible` outlines are defined for the builder controls, the pickers, the DSL chips and '
      + 'the explorer rows. Whether *every* focusable control is covered — and whether the indicator '
      + 'is visible against both palettes — is not something the stylesheet can be read to prove.',
  },
  {
    criterion: '2.5.1',
    conformance: 'Supports',
    evidence: [
      {
        kind: 'reasoned',
        because:
          'No interaction is a path-based or multipoint gesture. Every action is a single click, tap '
          + 'or key press; there is no drag, pinch or swipe anywhere in the application.',
      },
    ],
    remark: 'Rule composition is done by menu, picker and text, not by dragging.',
  },
  {
    criterion: '2.5.2',
    conformance: 'Supports',
    evidence: [
      {
        kind: 'reasoned',
        because:
          'Every control activates on click — the up-event — through React\'s `onClick`. Nothing in '
          + 'the application acts on the down-event.',
      },
    ],
    remark: 'A press begun by mistake can be aborted by moving off the control.',
  },
  {
    criterion: '2.5.3',
    conformance: 'Supports',
    evidence: [{ kind: 'axe' }],
    remark: 'Accessible names contain their visible labels; axe checks the mismatch case directly.',
  },
  {
    criterion: '2.5.4',
    conformance: 'Not Applicable',
    evidence: [
      { kind: 'not-applicable', because: 'No function is operated by device or user motion.' },
    ],
    remark: 'Nothing responds to tilting, shaking or moving the device.',
  },
  {
    criterion: '3.1.1',
    conformance: 'Supports',
    evidence: [{ kind: 'axe' }],
    remark: 'The document declares `lang="en"`.',
  },
  {
    criterion: '3.1.2',
    conformance: 'Supports',
    evidence: [{ kind: 'axe' }],
    remark:
      'The interface is English throughout; no passage declares or requires a different language. '
      + 'A `lang` that appeared and was invalid would be caught by the sweep.',
  },
  {
    criterion: '3.2.1',
    conformance: 'Not Evaluated',
    evidence: [MANUAL('that focusing a control — a picker trigger, a DSL chip, a tree row — changes no context')],
    remark:
      'The inline editors open on activation rather than on focus, but establishing that no surface '
      + 'changes context on focus means visiting them all.',
  },
  {
    criterion: '3.2.2',
    conformance: 'Not Evaluated',
    evidence: [MANUAL('that no input changes context, distinguishing a context change from the live preview')],
    remark:
      'Typing in the DSL strip updates the builder beneath it. That is a content update rather than a '
      + 'change of context, but it is exactly the distinction a person has to make here.',
  },
  {
    criterion: '3.2.3',
    conformance: 'Supports',
    evidence: [
      {
        kind: 'reasoned',
        because:
          'The app bar, its page navigation and the command palette are rendered by the shell once, '
          + 'above the routed page — so they cannot differ between routes.',
      },
    ],
    remark: 'Repeated navigation is the same component in the same place on every view.',
  },
  {
    criterion: '3.2.4',
    conformance: 'Not Evaluated',
    evidence: [MANUAL('that controls with the same function are named the same across the rules, propositions and admin pages')],
    remark: 'A consistency judgement across three pages, which no per-page scan is positioned to make.',
  },
  {
    criterion: '3.3.1',
    conformance: 'Supports',
    evidence: [
      {
        kind: 'reasoned',
        because:
          'Every error surface is a text `role="alert"` rendered next to the control that produced '
          + 'it, and schema violations carry the `$.rule…` path of the node at fault.',
      },
      MANUAL('that each error is announced when it appears, and identifies the item in error to a listener'),
    ],
    remark: 'Errors are identified in text and located in the document; how they announce is 4.1.3\'s manual half.',
  },
  {
    criterion: '3.3.2',
    conformance: 'Supports',
    evidence: [{ kind: 'axe' }],
    remark:
      'Every input carries a name, including the CodeMirror content elements, which name themselves '
      + 'rather than inheriting one.',
  },
  {
    criterion: '3.3.3',
    conformance: 'Not Evaluated',
    evidence: [MANUAL('that validation and lint messages suggest a correction where one is known')],
    remark:
      'The DSL lint and the server\'s schema violations both produce messages; whether they suggest '
      + 'the fix rather than only naming the fault has not been assessed.',
  },
  {
    criterion: '3.3.4',
    conformance: 'Not Evaluated',
    evidence: [MANUAL('that a publish is confirmable, reversible or checked from the interface, not only from the API')],
    remark:
      'Publishing modifies governed data. The mechanisms exist below the UI — an optimistic '
      + '`baseVersion` check that surfaces as a conflict banner, and append-only version history that '
      + 'makes a change reversible — but whether the interface offers the user one of the three has '
      + 'not been assessed.',
  },
  {
    criterion: '4.1.1',
    conformance: 'Supports',
    evidence: [
      {
        kind: 'reasoned',
        because:
          'The criterion was withdrawn by the WCAG 2.1 erratum of September 2023 and removed in WCAG '
          + '2.2; it is satisfied by all content. The markup is generated by React, which cannot emit '
          + 'duplicate ids or unclosed elements from valid JSX.',
      },
    ],
    remark: 'Retained in the table because a 2.1 report is expected to answer for it.',
  },
  {
    criterion: '4.1.2',
    conformance: 'Supports',
    evidence: [
      { kind: 'axe' },
      {
        kind: 'keyboard',
        tests: [
          'is one stop in the tab sequence, not one per proposition',
          'moves between rows on the arrow keys, and into and out of a subtree',
          'offers links that say which page is current',
        ],
      },
    ],
    remark:
      'Names, roles and states are present throughout and checked by the largest group of rules in '
      + 'the sweep. Every declared role is also honoured rather than merely well-formed: the '
      + 'palette\'s tree implements the tree pattern, and the page switcher — which controls no panel '
      + '— is a `nav` of links carrying `aria-current` rather than a `tablist`.',
  },
  {
    criterion: '4.1.3',
    conformance: 'Supports',
    evidence: [
      {
        kind: 'reasoned',
        because:
          'Status messages use the platform mechanism: the palette\'s result count is a `status` '
          + 'region, and every error is an `alert`, so both are announced without taking focus.',
      },
      MANUAL('that the announcements are useful rather than merely present, and that the count is announced as it changes'),
    ],
    remark:
      'axe has no rule for this criterion, so its presence is a structural fact rather than a '
      + 'mechanical one, and its usefulness is a listening judgement.',
  },
];
