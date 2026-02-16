import { useEffect, useState, useCallback } from 'react';

const KONAMI_SEQUENCE = [
  'ArrowUp', 'ArrowUp',
  'ArrowDown', 'ArrowDown',
  'ArrowLeft', 'ArrowRight',
  'ArrowLeft', 'ArrowRight',
  'KeyB', 'KeyA',
];

const CRT_DURATION = 8000;

export function useKonamiCode() {
  const [active, setActive] = useState(false);
  const [progress, setProgress] = useState(0);

  const reset = useCallback(() => {
    setActive(false);
  }, []);

  useEffect(() => {
    let sequenceIndex = 0;

    const handleKeyDown = (e: KeyboardEvent) => {
      if (active) return;

      const expected = KONAMI_SEQUENCE[sequenceIndex];
      if (e.code === expected) {
        sequenceIndex++;
        setProgress(sequenceIndex / KONAMI_SEQUENCE.length);
        if (sequenceIndex === KONAMI_SEQUENCE.length) {
          setActive(true);
          setProgress(0);
          sequenceIndex = 0;
        }
      } else {
        sequenceIndex = 0;
        setProgress(0);
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [active]);

  // Auto-fade after timeout
  useEffect(() => {
    if (!active) return;
    const timer = setTimeout(() => setActive(false), CRT_DURATION);
    return () => clearTimeout(timer);
  }, [active]);

  return { active, progress, reset };
}
