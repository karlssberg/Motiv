import { useRef, useState } from 'react';
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { RuleEditorStore } from '@motiv-rules/core';
import { RuleEditorProvider } from '@motiv-rules/react';
import { ExtractLocalPrompt } from '../../src/builder/ExtractLocalPrompt.js';

const DOC = { rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } };

/** A stand-in trigger button plus the prompt, wired the way a menu item would wire it. */
function Harness(props: { store: RuleEditorStore; seed: string; onDone?: (name: string) => void }) {
  const { store, seed, onDone } = props;
  const [open, setOpen] = useState(true);
  const triggerRef = useRef<HTMLButtonElement | null>(null);

  return (
    <RuleEditorProvider store={store}>
      <button ref={triggerRef} type="button">menu trigger</button>
      <ExtractLocalPrompt
        path="$.rule.and[0]"
        open={open}
        setOpen={setOpen}
        triggerRef={triggerRef}
        seed={seed}
        onDone={onDone}
      />
    </RuleEditorProvider>
  );
}

describe('ExtractLocalPrompt', () => {
  it('seeds the input from a finished form of the seed', async () => {
    render(<Harness store={new RuleEditorStore(DOC)} seed="is active" />);
    const input = await screen.findByLabelText('local name for $.rule.and[0]') as HTMLInputElement;
    expect(input.value).toBe('is-active');
  });

  it('is titled and labelled as an extract-to-definition dialog', async () => {
    render(<Harness store={new RuleEditorStore(DOC)} seed="is active" />);
    expect(await screen.findByRole('dialog', { name: 'Extract to definition' })).toBeDefined();
  });

  it('creates the definition and reports the name', async () => {
    const store = new RuleEditorStore(DOC);
    const onDone = vi.fn();
    render(<Harness store={store} seed="is active" onDone={onDone} />);

    fireEvent.click(await screen.findByRole('button', { name: 'Create' }));

    expect(store.getState().document.definitions?.['is-active']).toBeDefined();
    expect(store.getState().document.rule).toEqual({
      and: [{ local: 'is-active' }, { spec: 'is-adult' }],
    });
    expect(onDone).toHaveBeenCalledWith('is-active');
    expect(screen.queryByRole('dialog')).toBeNull();
  });

  it('disables Create while the name is invalid', async () => {
    render(<Harness store={new RuleEditorStore(DOC)} seed="all" />);
    const create = await screen.findByRole('button', { name: 'Create' }) as HTMLButtonElement;
    expect(create.disabled).toBe(true);
  });

  it('disables Create while the name is already taken', async () => {
    const store = new RuleEditorStore({
      ...DOC,
      definitions: { 'is-active': { rule: { spec: 'is-active' } } },
    });
    render(<Harness store={store} seed="is active" />);
    const create = await screen.findByRole('button', { name: 'Create' }) as HTMLButtonElement;
    expect(create.disabled).toBe(true);
  });

  it('Cancel closes without changing the document', async () => {
    const store = new RuleEditorStore(DOC);
    render(<Harness store={store} seed="is active" />);
    fireEvent.click(await screen.findByRole('button', { name: 'Cancel' }));

    expect(screen.queryByRole('dialog')).toBeNull();
    expect(store.getState().document).toEqual(DOC);
  });

  it('Escape closes without changes and returns focus to the trigger', async () => {
    const store = new RuleEditorStore(DOC);
    render(<Harness store={store} seed="is active" />);
    await screen.findByRole('dialog');

    fireEvent.keyDown(document, { key: 'Escape' });

    expect(screen.queryByRole('dialog')).toBeNull();
    expect(store.getState().document).toEqual(DOC);
    expect(document.activeElement).toBe(screen.getByRole('button', { name: 'menu trigger' }));
  });
});
