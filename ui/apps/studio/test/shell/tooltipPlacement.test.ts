import { describe, expect, it } from 'vitest';
import { placeTooltip } from '../../src/shell/tooltipPlacement.js';

const viewport = { width: 800, height: 600 };
const tip = { width: 100, height: 24 };

describe('placeTooltip', () => {
  it('centres the tip above its anchor', () => {
    const at = placeTooltip({ top: 100, bottom: 128, left: 300, right: 340 }, tip, viewport);
    expect(at).toEqual({ top: 100 - 24 - 6, left: 320 - 50, side: 'above' });
  });

  it('flips below when there is no room above', () => {
    const at = placeTooltip({ top: 10, bottom: 38, left: 300, right: 340 }, tip, viewport);
    expect(at).toEqual({ top: 38 + 6, left: 270, side: 'below' });
  });

  it('clamps into the viewport horizontally, keeping a margin', () => {
    expect(placeTooltip({ top: 100, bottom: 128, left: 0, right: 20 }, tip, viewport).left).toBe(8);
    expect(placeTooltip({ top: 100, bottom: 128, left: 780, right: 800 }, tip, viewport).left).toBe(800 - 100 - 8);
  });

  it('survives a headless DOM that measures everything as zero', () => {
    expect(placeTooltip({ top: 0, bottom: 0, left: 0, right: 0 }, { width: 0, height: 0 }, { width: 0, height: 0 }))
      .toEqual({ top: 6, left: 8, side: 'below' });
  });
});
