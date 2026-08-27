import { describe, it, expect } from 'vitest';
import {
  EMPTY_ACCORDION, closeAll, isCollapsed, isOpen, isPinned,
  toggleCollapsed, toggleOpen, togglePin,
} from '../src/accordion.js';

const A = '$.rule.and[0]';
const B = '$.rule.and[1]';

describe('accordion structure', () => {
  it('starts with every subtree expanded', () => {
    expect(isCollapsed(EMPTY_ACCORDION, A)).toBe(false);
  });

  it('collapses and re-expands a subtree', () => {
    const collapsed = toggleCollapsed(EMPTY_ACCORDION, A);
    expect(isCollapsed(collapsed, A)).toBe(true);
    expect(isCollapsed(toggleCollapsed(collapsed, A), A)).toBe(false);
  });

  it('does not touch detail state', () => {
    const model = toggleCollapsed(toggleOpen(EMPTY_ACCORDION, A), A);
    expect(isCollapsed(model, A)).toBe(true);
    expect(isOpen(model, A)).toBe(true);
  });
});

describe('accordion detail', () => {
  it('starts with every panel closed', () => {
    expect(isOpen(EMPTY_ACCORDION, A)).toBe(false);
  });

  it('opening one panel displaces the previous transient', () => {
    const model = toggleOpen(toggleOpen(EMPTY_ACCORDION, A), B);
    expect(isOpen(model, A)).toBe(false);
    expect(isOpen(model, B)).toBe(true);
  });

  it('closes a panel when its own row is toggled again', () => {
    const model = toggleOpen(toggleOpen(EMPTY_ACCORDION, A), A);
    expect(isOpen(model, A)).toBe(false);
  });

  it('a pinned panel survives another opening', () => {
    const model = toggleOpen(togglePin(toggleOpen(EMPTY_ACCORDION, A), A), B);
    expect(isPinned(model, A)).toBe(true);
    expect(isOpen(model, A)).toBe(true);
    expect(isOpen(model, B)).toBe(true);
  });

  it('pinning frees the transient slot', () => {
    const model = togglePin(toggleOpen(EMPTY_ACCORDION, A), A);
    expect(model.openPath).toBeNull();
  });

  it('unpinning keeps the panel open, as the transient', () => {
    const pinned = togglePin(toggleOpen(EMPTY_ACCORDION, A), A);
    const model = togglePin(pinned, A);
    expect(isPinned(model, A)).toBe(false);
    expect(isOpen(model, A)).toBe(true);
    expect(model.openPath).toBe(A);
  });

  it('closing a pinned panel unpins it — there is no pinned-but-closed state', () => {
    const pinned = togglePin(toggleOpen(EMPTY_ACCORDION, A), A);
    const model = toggleOpen(pinned, A);
    expect(isPinned(model, A)).toBe(false);
    expect(isOpen(model, A)).toBe(false);
  });

  it('close all clears the transient and every pin, leaving structure alone', () => {
    const collapsed = toggleCollapsed(EMPTY_ACCORDION, B);
    const model = closeAll(toggleOpen(togglePin(toggleOpen(collapsed, A), A), B));
    expect(model.openPath).toBeNull();
    expect(model.pinned.size).toBe(0);
    expect(isCollapsed(model, B)).toBe(true);
  });
});
