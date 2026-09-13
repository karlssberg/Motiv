import { describe, it, expect } from 'vitest';
import { LOCAL_NAME_PATTERN, isValidLocalName, normalizeLocalName, finishLocalName } from '../src/localNames.js';

describe('LOCAL_NAME_PATTERN', () => {
  it('matches the isValidLocalName cases', () => {
    expect(LOCAL_NAME_PATTERN.test('a-b_1')).toBe(true);
    expect(LOCAL_NAME_PATTERN.test('a.b')).toBe(false);
  });
});

describe('isValidLocalName', () => {
  it.each([
    ['a-b_1', true],
    ['a.b', false],
    ['-a', false],
    ['', false],
    ['a b', false],
  ] as const)('isValidLocalName(%j) is %j', (name, expected) => {
    expect(isValidLocalName(name)).toBe(expected);
  });
});

describe('normalizeLocalName', () => {
  it.each([
    ['is active', 'is-active'],
    ['is  active', 'is--active'],
    ['9x', 'x'],
    ['a.b', 'ab'],
    ['', ''],
  ] as const)('normalizeLocalName(%j) is %j', (typed, expected) => {
    expect(normalizeLocalName(typed)).toBe(expected);
  });
});

describe('finishLocalName', () => {
  it.each([
    ['is--active-', 'is-active'],
    ['---', ''],
  ] as const)('finishLocalName(%j) is %j', (typed, expected) => {
    expect(finishLocalName(typed)).toBe(expected);
  });
});
