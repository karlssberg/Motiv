import { describe, it, expect } from 'vitest';
import { placePopover } from '../../src/dsl/popoverPlacement.js';

/** A roomy frame: the toolbar occupies the first 32px, and the surface runs to 400px. */
const FRAME = { width: 400, height: 400, minTop: 32 };
/** A card small enough to sit either side of a token in {@link FRAME}. */
const CARD = { width: 200, height: 100 };
/** A card too tall to fit either side of a token on the first line of {@link FRAME}. */
const TALL_CARD = { width: 200, height: 360 };
/** A token on the first line of the editing surface, just below the toolbar. */
const FIRST_LINE_TOKEN = { top: 34, bottom: 52, left: 10 };

describe('placePopover', () => {
  it('anchors just below the token', () => {
    const { top, left } = placePopover({ top: 50, bottom: 68, left: 40 }, CARD, FRAME);

    expect(top).toBeGreaterThan(68);
    expect(top).toBeLessThan(90);
    expect(left).toBe(40);
  });

  it('flips above the token when there is no room below', () => {
    const { top } = placePopover({ top: 330, bottom: 348, left: 40 }, CARD, FRAME);

    expect(top + CARD.height).toBeLessThan(330);
  });

  it('clamps a token near the right edge so the card stays inside the frame', () => {
    const { left } = placePopover({ top: 50, bottom: 68, left: 390 }, CARD, FRAME);

    expect(left + CARD.width).toBeLessThanOrEqual(FRAME.width);
  });

  it('clamps a token left of the frame back inside it', () => {
    const { left } = placePopover({ top: 50, bottom: 68, left: -120 }, CARD, FRAME);

    expect(left).toBeGreaterThanOrEqual(0);
  });

  it('never rides up over the toolbar', () => {
    // Neither below nor above the token fits a card this tall, so it is clamped instead.
    const { top } = placePopover(FIRST_LINE_TOKEN, TALL_CARD, FRAME);

    expect(top).toBeGreaterThanOrEqual(FRAME.minTop);
  });

  it('keeps the card inside the frame vertically', () => {
    const { top, maxHeight } = placePopover(FIRST_LINE_TOKEN, TALL_CARD, FRAME);

    expect(top + Math.min(TALL_CARD.height, maxHeight)).toBeLessThanOrEqual(FRAME.height);
  });

  it('caps the height at the room left below the toolbar', () => {
    const { maxHeight } = placePopover({ top: 50, bottom: 68, left: 40 }, CARD, FRAME);

    expect(maxHeight).toBeGreaterThan(0);
    expect(maxHeight).toBeLessThanOrEqual(FRAME.height - FRAME.minTop);
  });

  it('degrades to a placement inside the frame when every measurement is zero', () => {
    const { top, left, maxHeight } = placePopover(
      { top: 0, bottom: 0, left: 0 },
      { width: 0, height: 0 },
      { width: 0, height: 0, minTop: 0 },
    );

    expect(Number.isFinite(top)).toBe(true);
    expect(Number.isFinite(left)).toBe(true);
    expect(top).toBeGreaterThanOrEqual(0);
    expect(left).toBeGreaterThanOrEqual(0);
    expect(maxHeight).toBeGreaterThanOrEqual(0);
  });
});
