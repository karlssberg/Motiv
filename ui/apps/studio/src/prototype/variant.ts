/*
  PROTOTYPE — throwaway. The `?variant=` search param that picks which UI variant renders.

  Studio's own routing lives after the `#/` hash, so the search string is free: `?variant=B#/rules/x`
  carries both. Writes go through `history.replaceState`, which leaves the hash alone.
*/
import { useSyncExternalStore } from 'react';

export const VARIANTS = ['A', 'B', 'C'] as const;
export type Variant = (typeof VARIANTS)[number];

export const VARIANT_NAMES: Record<Variant, string> = {
  A: 'One link, both boxes',
  B: 'Per-outcome slots',
  C: 'Default preview, override in place',
};

function isVariant(v: string | null): v is Variant {
  return v !== null && (VARIANTS as readonly string[]).includes(v);
}

export function readVariant(): Variant {
  const v = new URLSearchParams(window.location.search).get('variant');
  return isVariant(v) ? v : 'A';
}

const listeners = new Set<() => void>();

export function setVariant(variant: Variant): void {
  const url = new URL(window.location.href);
  url.searchParams.set('variant', variant);
  window.history.replaceState(null, '', url);
  for (const l of listeners) l();
}

export function cycleVariant(step: 1 | -1): void {
  const i = VARIANTS.indexOf(readVariant());
  setVariant(VARIANTS[(i + step + VARIANTS.length) % VARIANTS.length]!);
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  window.addEventListener('popstate', listener);
  return () => {
    listeners.delete(listener);
    window.removeEventListener('popstate', listener);
  };
}

export function usePrototypeVariant(): Variant {
  return useSyncExternalStore(subscribe, readVariant, () => 'A');
}
