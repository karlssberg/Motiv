import { describe, it, expect } from 'vitest';
import { placePopover } from '../../src/dsl/popoverPlacement.js';

/** A desktop viewport, with the editor's toolbar ending 206px down it. */
const VIEWPORT = { width: 1280, height: 900, minTop: 206 };
/** The card, at the width the stylesheet gives it. */
const CARD = { width: 320, height: 340 };
/** A card too tall to fit either side of a token on the first line of {@link VIEWPORT}. */
const TALL_CARD = { width: 320, height: 880 };
/** A token on the editor's first line, just below the toolbar. */
const FIRST_LINE_TOKEN = { top: 216, bottom: 235, left: 62 };
/** How wide the editor pane is — the card is free to overhang it. */
const PANE_RIGHT = 416;

describe('placePopover', () => {
  it('anchors just below the token', () => {
    const { top, left } = placePopover(FIRST_LINE_TOKEN, CARD, VIEWPORT);

    expect(top).toBeGreaterThan(FIRST_LINE_TOKEN.bottom);
    expect(top).toBeLessThan(FIRST_LINE_TOKEN.bottom + 16);
    expect(left).toBe(FIRST_LINE_TOKEN.left);
  });

  it('flips above the token when there is no room below', () => {
    const { top } = placePopover({ top: 850, bottom: 868, left: 62 }, CARD, VIEWPORT);

    expect(top + CARD.height).toBeLessThan(850);
    expect(top).toBeGreaterThanOrEqual(VIEWPORT.minTop);
  });

  // The card is a hover-card over the page, not a panel inside the editor: overhanging the pane
  // it belongs to is the point. Only the viewport may clamp it.
  it('lets the card overhang the editor pane it was opened from', () => {
    const { left } = placePopover({ ...FIRST_LINE_TOKEN, left: 300 }, CARD, VIEWPORT);

    expect(left).toBe(300);
    expect(left + CARD.width).toBeGreaterThan(PANE_RIGHT);
  });

  it('clamps a token near the right of the viewport', () => {
    const { left } = placePopover({ ...FIRST_LINE_TOKEN, left: 1270 }, CARD, VIEWPORT);

    expect(left + CARD.width).toBeLessThanOrEqual(VIEWPORT.width);
  });

  it('clamps a token scrolled off the left of the viewport', () => {
    const { left } = placePopover({ ...FIRST_LINE_TOKEN, left: -120 }, CARD, VIEWPORT);

    expect(left).toBeGreaterThanOrEqual(0);
  });

  it('never rides up over the toolbar', () => {
    // Neither below nor above the token fits a card this tall, so it is clamped, not floated up.
    const { top } = placePopover(FIRST_LINE_TOKEN, TALL_CARD, VIEWPORT);

    expect(top).toBeGreaterThanOrEqual(VIEWPORT.minTop);
  });

  it('keeps the card inside the viewport vertically', () => {
    const { top, maxHeight } = placePopover(FIRST_LINE_TOKEN, TALL_CARD, VIEWPORT);

    expect(top + Math.min(TALL_CARD.height, maxHeight)).toBeLessThanOrEqual(VIEWPORT.height);
  });

  it('caps the height at the room left below the toolbar', () => {
    const { maxHeight } = placePopover(FIRST_LINE_TOKEN, CARD, VIEWPORT);

    expect(maxHeight).toBeGreaterThan(0);
    expect(maxHeight).toBeLessThanOrEqual(VIEWPORT.height - VIEWPORT.minTop);
  });

  it('holds the card below the viewport top even when the toolbar has scrolled off it', () => {
    const { top } = placePopover({ top: -40, bottom: -20, left: 62 }, CARD, { ...VIEWPORT, minTop: -60 });

    expect(top).toBeGreaterThanOrEqual(0);
  });

  it('degrades to a placement inside the viewport when every measurement is zero', () => {
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
