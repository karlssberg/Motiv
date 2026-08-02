/**
 * The shell's glyph set, hand-drawn as inline SVG.
 *
 * `ui/` takes no new runtime dependencies, so an icon package is out. Unicode glyphs were the
 * other candidate and were rejected: they render inconsistently enough across platforms that a
 * toolbar built from them looks broken on someone else's machine.
 *
 * Every glyph strokes in `currentColor` and is `aria-hidden`. The colour makes the ghost hover
 * treatment possible; the hiding keeps the button's `aria-label` the single accessible name.
 */

import type { ReactNode } from 'react';

export interface IconProps {
  /** Edge length in pixels. Defaults to 17, the size the toolbar uses. */
  size?: number;
}

function Glyph(props: IconProps & { children: ReactNode }) {
  const size = props.size ?? 17;
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.6"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      {props.children}
    </svg>
  );
}

export const IconOpen = (props: IconProps) => (
  <Glyph {...props}><path d="M4 5h5l1.5 2H20v11a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1z" /></Glyph>
);

export const IconSave = (props: IconProps) => (
  <Glyph {...props}>
    <path d="M12 4v10m0 0l-3.5-3.5M12 14l3.5-3.5" />
    <path d="M5 17v2a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-2" />
  </Glyph>
);

export const IconJson = (props: IconProps) => (
  <Glyph {...props}>
    <path d="M9 4c-2 0-2.5 1-2.5 3S6 10 4.5 12c1.5 2 2 2.5 2 5s.5 3 2.5 3" />
    <path d="M15 4c2 0 2.5 1 2.5 3s.5 3 2 5c-1.5 2-2 2.5-2 5s-.5 3-2.5 3" />
  </Glyph>
);

export const IconRules = (props: IconProps) => (
  <Glyph {...props}>
    <path d="M4 6h10M4 12h10M4 18h10" />
    <path d="M18 5.5l1.6 1.6L22 4.7" />
  </Glyph>
);

export const IconPropositions = (props: IconProps) => (
  <Glyph {...props}>
    <circle cx="6" cy="12" r="2.2" />
    <circle cx="18" cy="6" r="2.2" />
    <circle cx="18" cy="18" r="2.2" />
    <path d="M8.2 11L15.8 7M8.2 13l7.6 4" />
  </Glyph>
);

export const IconSearch = (props: IconProps) => (
  <Glyph {...props}><circle cx="11" cy="11" r="6" /><path d="M15.5 15.5L20 20" /></Glyph>
);

export const IconNew = (props: IconProps) => (
  <Glyph {...props}><path d="M12 5v14M5 12h14" /></Glyph>
);

export const IconDerive = (props: IconProps) => (
  <Glyph {...props}><path d="M6 3v12a3 3 0 0 0 3 3h9" /><path d="M15 15l3 3-3 3" /></Glyph>
);

export const IconOverride = (props: IconProps) => (
  <Glyph {...props}><path d="M4 8h11a4 4 0 0 1 0 8H8" /><path d="M11 13l-3 3 3 3" /></Glyph>
);

export const IconDelete = (props: IconProps) => (
  <Glyph {...props}><path d="M5 7h14M10 7V5h4v2M7 7l1 13h8l1-13" /></Glyph>
);

export const IconClose = (props: IconProps) => (
  <Glyph {...props}><path d="M6 6l12 12M18 6L6 18" /></Glyph>
);
