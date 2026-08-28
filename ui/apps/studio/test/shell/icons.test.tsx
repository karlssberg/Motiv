import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';
import { IconOpen, IconSave } from '../../src/shell/icons.js';

describe('icons', () => {
  it('renders at the requested size', () => {
    const { container } = render(<IconOpen size={20} />);
    const svg = container.querySelector('svg');
    expect(svg?.getAttribute('width')).toBe('20');
    expect(svg?.getAttribute('height')).toBe('20');
  });

  it('inherits colour from its button rather than hard-coding one', () => {
    // The whole ghost treatment is a colour change on hover, which only works if the glyph
    // takes its colour from the element around it.
    const { container } = render(<IconSave />);
    expect(container.querySelector('svg')?.getAttribute('stroke')).toBe('currentColor');
  });

  it('is hidden from assistive technology, because the button carries the name', () => {
    // Without this the button announces its aria-label and then the glyph again.
    const { container } = render(<IconSave />);
    expect(container.querySelector('svg')?.getAttribute('aria-hidden')).toBe('true');
  });
});
