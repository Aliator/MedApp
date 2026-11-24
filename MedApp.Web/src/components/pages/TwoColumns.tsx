import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import type {ReactNode} from 'react';
import {useIsNarrow} from './Page';

// Equal halves whatever their content: a zero flex basis, which StackItem has no prop for.
const HALF = {flexBasis: 0};

/**
 * Two equal columns side by side, filling the height they're given (e.g. a `Page` with `fill`). Each column is
 * held to that height, so a card in it fills it and scrolls its own content. Used by Home and the pathway page.
 * In a narrow layout (`useIsNarrow`: a phone, a half view) they stack, each as tall as its content, and the page
 * scrolls; a page that wants something else there (one card of folding sections) renders that instead.
 */
export function TwoColumns({start, end}: {start: ReactNode; end: ReactNode}) {
  const isNarrow = useIsNarrow();
  if (isNarrow)
    return (
      <VStack gap={6}>
        {start}
        {end}
      </VStack>
    );
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
