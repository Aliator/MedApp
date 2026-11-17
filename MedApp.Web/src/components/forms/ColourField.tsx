import {Field} from '@astryxdesign/core/Field';
import {HStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {type MouseEvent, useId, useRef, useState} from 'react';
import {NO_FIELD_STATUS_LINE, NO_STATUS_LINE} from './fieldSpace';

/**
 * Padded and rounded like a list row (and as wide as the list's rows), so its hover shade has room. It keeps no empty
 * line for a message: a warning, when there is one, is detached (below the colour, in the flow), so it takes its own
 * room. The attached style is for bordered text boxes: it pulls itself up to join the box, over the colour and its value.
 */
const ROW = {
  ...NO_STATUS_LINE,
  ...NO_FIELD_STATUS_LINE,
  padding: 'var(--spacing-3)',
  borderRadius: 'var(--radius-element)',
  cursor: 'pointer',
};
const HOVERED = {backgroundColor: 'color-mix(in srgb, var(--color-background-card) 92%, light-dark(black, white))'};

export interface ColourFieldProps {
  label: string;
  description?: string;
  /** A hex colour, `#rrggbb`. */
  value: string;
  /** Called as the colour is picked (while dragging too), so what it colours can follow at once. */
  onChange: (value: string) => void;
  /** A warning under the colour, e.g. that text on it is hard to read. Screen readers say it as it appears. */
  warning?: string;
}

/**
 * Picks a colour, with its hex value beside it. Astryx has no colour picker, so this is the browser's own colour
 * input inside an Astryx Field (its label and description), as Field expects of a custom control.
 */
export function ColourField({label, description, value, onChange, warning}: ColourFieldProps) {
  const id = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const reachedInputRef = useRef(false);
  const [isHovered, setIsHovered] = useState(false);

  // A click anywhere on the row opens the picker, as a list row acts on a click anywhere. Some clicks already open it,
  // and are left alone rather than opening it twice:
  // - the colour itself, and its label, which hands its click on afterwards, as the browser's default action
  // - anything that hands its click on to the colour first (Astryx's description does): the input notes the click as
  //   it passes, and forgets it once the click has finished
  const openPicker = (event: MouseEvent) => {
    if (event.target instanceof Element && event.target.closest('input, label')) return;
    if (reachedInputRef.current) return;
    inputRef.current?.click();
  };

  return (
    // The whole field shades under the pointer, as list rows do. Keyboard users reach the colour input itself.
    <Field
      label={label}
      description={description}
      inputID={id}
      status={warning ? {type: 'warning', message: warning} : undefined}
      statusVariant="detached"
      onClick={openPicker}
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}
      style={{...ROW, ...(isHovered ? HOVERED : undefined)}}>
      <HStack gap={3} vAlign="center">
        <input
          ref={inputRef}
          onClick={() => {
            reachedInputRef.current = true;
            queueMicrotask(() => {
              reachedInputRef.current = false;
            });
          }}
          id={id}
          type="color"
          value={value}
          onChange={(event) => onChange(event.target.value)}
          style={{inlineSize: 'var(--spacing-12)', blockSize: 'var(--spacing-8)', padding: 0, border: 'none', background: 'none', cursor: 'pointer'}}
        />
        <Text type="code">{value.toLowerCase()}</Text>
      </HStack>
    </Field>
  );
}

/**
 * A few colours side by side, as small squares, e.g. a colour theme's page, card and nav. Decoration only (the
 * names are in the label beside it); Astryx has no swatch, so each is a plain element filled with its colour.
 */
export function ColourSwatches({colours, size = 'md'}: {colours: readonly string[]; size?: 'md' | 'lg'}) {
  const side = size === 'lg' ? 'var(--spacing-6)' : 'var(--spacing-4)';
  return (
    <HStack gap={1} aria-hidden>
      {colours.map((colour, i) => (
        <span
          key={`${i}-${colour}`}
          style={{
            inlineSize: side,
            blockSize: side,
            borderRadius: 'var(--radius-inner)',
            background: colour,
            boxShadow: 'inset 0 0 0 1px var(--color-border)',
          }}
        />
      ))}
    </HStack>
  );
}
