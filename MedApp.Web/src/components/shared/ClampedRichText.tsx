import {Button} from '@astryxdesign/core/Button';
import {VStack} from '@astryxdesign/core/Stack';
import {useEffect, useId, useRef, useState} from 'react';
import {RichText} from './RichText';

/** How many lines a long note shows until it's expanded. */
export const CLAMPED_LINES = 3;

/** The first lines only, the rest cut off with an ellipsis. */
const CLAMPED = {
  display: '-webkit-box',
  WebkitBoxOrient: 'vertical',
  WebkitLineClamp: CLAMPED_LINES,
  overflow: 'hidden',
} as const;

/**
 * Formatted text (`RichText`) cut to its first three lines, with Show More to read the rest and Show Less to fold it
 * again (the owner, 19 November 2026), so a pathway's notes and its stages' notes stay short in their lists. Text that
 * fits in three lines shows as it is, with no button. The whole text is always in the page (only hidden from view), so
 * find-in-page and screen readers still reach it.
 */
export function ClampedRichText({children}: {children: string}) {
  const box = useRef<HTMLElement>(null);
  const id = useId();
  const [isOpen, setIsOpen] = useState(false);
  const [isLong, setIsLong] = useState(false);

  // Whether it runs past three lines, measured while it's folded (and again as its width changes): only then is
  // there anything to expand. The observer reports the size as it starts watching, and on every change after.
  useEffect(() => {
    const element = box.current;
    if (!element || isOpen || typeof ResizeObserver === 'undefined') return;
    const observer = new ResizeObserver(() => setIsLong(element.scrollHeight > element.clientHeight + 1));
    observer.observe(element);
    return () => observer.disconnect();
  }, [children, isOpen]);

  return (
    <VStack gap={1} hAlign="start">
      <VStack ref={box} id={id} hAlign="stretch" style={isOpen ? undefined : CLAMPED}>
        <RichText>{children}</RichText>
      </VStack>
      {(isLong || isOpen) && (
        <Button
          label={isOpen ? 'Show Less' : 'Show More'}
          variant="ghost"
          size="sm"
          aria-expanded={isOpen}
          aria-controls={id}
          onClick={() => setIsOpen((open) => !open)}
        />
      )}
    </VStack>
  );
}
