import {HStack, StackItem} from '@astryxdesign/core/Stack';
import type {ReactNode} from 'react';

// Equal halves whatever their content: a zero flex basis, which StackItem has no prop for.
const HALF = {flexBasis: 0};

/**
 * Two equal columns side by side, filling the height they're given (e.g. a `Page` with `fill`). Each column is
 * held to that height, so a card in it fills it and scrolls its own content. Used by Home and the procedure page.
 */
export function TwoColumns({start, end}: {start: ReactNode; end: ReactNode}) {
  return (
    <HStack gap={6} height="100%">
      <StackItem size="fill" style={HALF}>
        {start}
      </StackItem>
      <StackItem size="fill" style={HALF}>
        {end}
      </StackItem>
    </HStack>
  );
}
