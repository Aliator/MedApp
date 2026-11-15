import {Field} from '@astryxdesign/core/Field';
import {HStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useId, useState} from 'react';
import {NO_STATUS_LINE} from './fieldSpace';

/** Padded and rounded like a list row (and as wide as the list's rows), so its hover shade has room. */
const ROW = {
  ...NO_STATUS_LINE,
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
}

/**
 * Picks a colour, with its hex value beside it. Astryx has no colour picker, so this is the browser's own colour
 * input inside an Astryx Field (its label and description), as Field expects of a custom control.
 */
export function ColourField({label, description, value, onChange}: ColourFieldProps) {
  const id = useId();
  const [isHovered, setIsHovered] = useState(false);
  return (
    // The whole field shades under the pointer, as list rows do; its label opens the picker too.
    <Field
      label={label}
      description={description}
      inputID={id}
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}
      style={{...ROW, ...(isHovered ? HOVERED : undefined)}}>
      <HStack gap={3} vAlign="center">
        <input
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
