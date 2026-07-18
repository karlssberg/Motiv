import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

describe('react test harness', () => {
  it('renders into jsdom', () => {
    render(<div>hello</div>);
    expect(screen.getByText('hello')).toBeDefined();
  });
});
