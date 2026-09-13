import { useState } from 'react';
import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@testing-library/react';
import { LocalNameInput } from '../../src/builder/LocalNameInput.js';

/** Renders the input as a controlled component, so `onChange` writes back through `value`. */
function Controlled(props: { initial: string; onCommit?: () => void; taken?: ReadonlySet<string> }) {
  const { initial, onCommit = () => {}, taken = new Set<string>() } = props;
  const [value, setValue] = useState(initial);
  return (
    <LocalNameInput
      value={value}
      onChange={setValue}
      onCommit={() => { onCommit(); }}
      ariaLabel="local name for $.rule"
      taken={taken}
    />
  );
}

describe('LocalNameInput', () => {
  it('turns a typed space into a hyphen, caret landing after it', () => {
    const { getByLabelText } = render(<Controlled initial="" />);
    const input = getByLabelText('local name for $.rule') as HTMLInputElement;

    fireEvent.change(input, { target: { value: 'is active', selectionStart: 9 } });

    expect(input.value).toBe('is-active');
    expect(input.selectionStart).toBe(9);
  });

  it('drops characters outside the grammar, moving the caret left by what was dropped', () => {
    const { getByLabelText } = render(<Controlled initial="" />);
    const input = getByLabelText('local name for $.rule') as HTMLInputElement;

    fireEvent.change(input, { target: { value: 'a.b', selectionStart: 3 } });

    expect(input.value).toBe('ab');
    expect(input.selectionStart).toBe(2);
  });

  it('collapses runs of hyphens and trims a trailing one on blur', () => {
    const { getByLabelText } = render(<Controlled initial="a--b-" />);
    const input = getByLabelText('local name for $.rule') as HTMLInputElement;

    fireEvent.blur(input);

    expect(input.value).toBe('a-b');
  });

  it('calls onCommit on blur', () => {
    const onCommit = vi.fn();
    const { getByLabelText } = render(<Controlled initial="a-b" onCommit={onCommit} />);
    const input = getByLabelText('local name for $.rule') as HTMLInputElement;

    fireEvent.blur(input);

    expect(onCommit).toHaveBeenCalledTimes(1);
  });

  it('flags a taken name as invalid, with a visible error line', () => {
    const { getByLabelText, getByText } = render(
      <Controlled initial="taken" taken={new Set(['taken'])} />,
    );
    const input = getByLabelText('local name for $.rule') as HTMLInputElement;

    expect(input.getAttribute('aria-invalid')).toBe('true');
    expect(getByText(/already/i)).toBeDefined();
  });

  it('flags a grammatically invalid name (e.g. reserved word) as invalid', () => {
    const { getByLabelText } = render(<Controlled initial="all" />);
    const input = getByLabelText('local name for $.rule') as HTMLInputElement;

    expect(input.getAttribute('aria-invalid')).toBe('true');
  });

  it('is not invalid once the name is grammatical and not taken', () => {
    const { getByLabelText } = render(<Controlled initial="" />);
    const input = getByLabelText('local name for $.rule') as HTMLInputElement;

    fireEvent.change(input, { target: { value: 'ok-name', selectionStart: 7 } });
    expect((getByLabelText('local name for $.rule') as HTMLInputElement).getAttribute('aria-invalid'))
      .toBe('false');
  });

  it('points aria-describedby at the error element by a resolvable id', () => {
    const { getByLabelText, getByRole } = render(
      <Controlled initial="taken" taken={new Set(['taken'])} />,
    );
    const input = getByLabelText('local name for $.rule') as HTMLInputElement;

    const describedBy = input.getAttribute('aria-describedby');
    const alert = getByRole('alert');
    expect(describedBy).toBe(alert.id);
    // The IDREF must actually resolve: an id built from the label would carry spaces, which
    // splits the IDREF list into tokens that name nothing.
    expect(describedBy).not.toMatch(/\s/);
    expect(document.getElementById(describedBy!)).toBe(alert);
  });
});
