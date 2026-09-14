/*
  PROTOTYPE — throwaway. The floating bottom-centre bar that cycles `?variant=`.

  Deliberately styled unlike anything in Studio (a black pill in both colour schemes) so it is
  obviously not part of the design under evaluation. Mounted in App.tsx behind a NODE_ENV gate.
*/
import { useEffect } from 'react';
import { VARIANT_NAMES, cycleVariant, usePrototypeVariant } from './variant.js';
import './prototype.css';

function isTyping(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  return target.matches('input, textarea, select, [contenteditable], [contenteditable] *');
}

export function PrototypeSwitcher() {
  const variant = usePrototypeVariant();

  useEffect(() => {
    const onKey = (e: KeyboardEvent): void => {
      if (isTyping(e.target)) return;
      if (e.key === 'ArrowLeft') cycleVariant(-1);
      if (e.key === 'ArrowRight') cycleVariant(1);
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  return (
    <div className="proto-switcher" role="group" aria-label="prototype variant">
      <button type="button" onClick={() => cycleVariant(-1)} aria-label="previous variant">←</button>
      <span>
        <b>{variant}</b> · {VARIANT_NAMES[variant]}
      </span>
      <button type="button" onClick={() => cycleVariant(1)} aria-label="next variant">→</button>
    </div>
  );
}
