/*
 * PROTOTYPE — throwaway. Do not ship.
 *
 * The floating bar that flips between variants. Writes `?variant=` so a variant is shareable and
 * reload-stable; ←/→ cycle when nothing else has focus.
 */
import { useEffect } from 'react';

export interface VariantEntry { key: string; name: string }

export function PrototypeSwitcher(props: { variants: VariantEntry[]; current: string; onChange: (key: string) => void }) {
  const index = Math.max(0, props.variants.findIndex((variant) => variant.key === props.current));
  const step = (delta: number): void => {
    const next = props.variants[(index + delta + props.variants.length) % props.variants.length]!;
    props.onChange(next.key);
  };

  useEffect(() => {
    const onKey = (event: KeyboardEvent): void => {
      const target = event.target as HTMLElement | null;
      if (target && target !== document.body) return;
      if (event.key === 'ArrowLeft') step(-1);
      if (event.key === 'ArrowRight') step(1);
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  });

  const current = props.variants[index]!;
  return (
    <div className="proto-switcher" role="group" aria-label="Prototype variant">
      <button type="button" onClick={() => step(-1)} aria-label="Previous variant">←</button>
      <span className="proto-label"><strong>{current.key}</strong> {current.name}</span>
      <button type="button" onClick={() => step(1)} aria-label="Next variant">→</button>
    </div>
  );
}
