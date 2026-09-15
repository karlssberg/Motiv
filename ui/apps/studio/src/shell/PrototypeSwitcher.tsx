/// <reference types="vite/client" />
// PROTOTYPE — throwaway. Not part of Studio's design; hidden outside dev builds.
import { useEffect, useState } from 'react';

/**
 * Reads `?variant=` from the address bar's search (which survives hash navigation), and writes it
 * back with `replaceState` so a variant is shareable and reload-stable.
 */
export function useVariant(keys: readonly string[]): [string, (key: string) => void] {
  const read = (): string => {
    const key = new URLSearchParams(window.location.search).get('variant');
    return key !== null && keys.includes(key) ? key : keys[0]!;
  };
  const [variant, setVariant] = useState<string>(read);
  useEffect(() => {
    const sync = (): void => setVariant(read());
    window.addEventListener('popstate', sync);
    return () => window.removeEventListener('popstate', sync);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);
  const navigate = (key: string): void => {
    const url = new URL(window.location.href);
    url.searchParams.set('variant', key);
    window.history.replaceState(null, '', url);
    setVariant(key);
  };
  return [variant, navigate];
}

/** Floating bottom-centre pill: ← key (name) →. Arrow keys cycle unless a field is focused. */
export function PrototypeSwitcher(props: {
  variants: readonly { key: string; name: string }[];
  current: string;
  onChange: (key: string) => void;
}) {
  const { variants, current, onChange } = props;
  const index = Math.max(0, variants.findIndex((v) => v.key === current));
  const step = (delta: number): void => {
    const next = variants[(index + delta + variants.length) % variants.length]!;
    onChange(next.key);
  };
  useEffect(() => {
    if (!import.meta.env.DEV) return undefined;
    const onKey = (event: KeyboardEvent): void => {
      const target = event.target as HTMLElement | null;
      if (target && (target.closest('input, textarea, [contenteditable]') || target.closest('.cm-editor'))) return;
      if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') return;
      event.preventDefault();
      const delta = event.key === 'ArrowLeft' ? -1 : 1;
      const next = variants[(index + delta + variants.length) % variants.length]!;
      onChange(next.key);
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [variants, index, onChange]);
  if (!import.meta.env.DEV) return null;
  return (
    <div className="proto-switcher" role="group" aria-label="prototype variant">
      <button type="button" onClick={() => step(-1)} aria-label="previous variant">←</button>
      <span className="proto-switcher-label">
        <b>{current}</b> {variants[index]?.name}
      </span>
      <button type="button" onClick={() => step(1)} aria-label="next variant">→</button>
    </div>
  );
}
