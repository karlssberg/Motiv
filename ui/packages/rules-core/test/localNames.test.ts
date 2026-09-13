import { describe, it, expect } from 'vitest';
import {
  LOCAL_NAME_PATTERN, RESERVED_LOCAL_NAMES, isValidLocalName, normalizeLocalName, finishLocalName,
} from '../src/localNames.js';

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
    ['all', false],
    ['param', false],
    ['let', false],
    ['integer', false],
    ['allx', true],
  ] as const)('isValidLocalName(%j) is %j', (name, expected) => {
    expect(isValidLocalName(name)).toBe(expected);
  });
});

describe('RESERVED_LOCAL_NAMES', () => {
  it('contains every DSL keyword, type and quantifier', () => {
    expect(RESERVED_LOCAL_NAMES.has('all')).toBe(true);
    expect(RESERVED_LOCAL_NAMES.has('param')).toBe(true);
    expect(RESERVED_LOCAL_NAMES.has('let')).toBe(true);
    expect(RESERVED_LOCAL_NAMES.has('integer')).toBe(true);
    expect(RESERVED_LOCAL_NAMES.has('allx')).toBe(false);
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
    ['a' + '-'.repeat(50_000), 'a'],
    ['-'.repeat(50_000) + 'a', 'a'],
  ] as const)('finishLocalName(%j) is %j', (typed, expected) => {
    expect(finishLocalName(typed)).toBe(expected);
  });
});
