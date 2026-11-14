import {Card} from '@astryxdesign/core/Card';
import {Section} from '@astryxdesign/core/Section';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import type {ReactNode} from 'react';
import {InCardContext} from './CardSection';

export interface ViewCardProps {
  /** The card's sections, usually ViewSection. */
  children: ReactNode;
  /** Buttons in the footer, aligned to the end (e.g. Edit). */
  footerActions?: ReactNode;
  showFooter?: boolean;
  /** Fill the height it's given (e.g. a column of a two-column page): the sections scroll, the footer stays at the bottom. */
  fill?: boolean;
}

/**
 * Read-only details in a card (replaces Blazor's ViewCard); the read-only twin of FormCard. Each ViewSection's
 * title is a header bar across the card, as on the procedure page's cards.
 */
export function ViewCard({children, footerActions, showFooter = true, fill = false}: ViewCardProps) {
  return (
    <Card padding={0} width="100%" height={fill ? '100%' : undefined}>
      <VStack gap={0} height={fill ? '100%' : undefined}>
        <StackItem size={fill ? 'fill' : 'static'} isScrollable={fill}>
          <InCardContext.Provider value>{children}</InCardContext.Provider>
        </StackItem>
        {showFooter && footerActions && (
          <Section variant="muted" padding={4}>
            <HStack gap={2} vAlign="center" hAlign="end">
              {footerActions}
            </HStack>
          </Section>
        )}
      </VStack>
    </Card>
  );
}
