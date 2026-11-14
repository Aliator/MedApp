import {Card} from '@astryxdesign/core/Card';
import {Section} from '@astryxdesign/core/Section';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import type {ReactNode} from 'react';

export interface ViewCardProps {
  /** The card's sections, usually ViewSection. */
  children: ReactNode;
  /** Buttons in the footer, aligned to the end (e.g. Edit). */
  footerActions?: ReactNode;
  showFooter?: boolean;
}

/** Read-only details in a card (replaces Blazor's ViewCard); the read-only twin of FormCard. */
export function ViewCard({children, footerActions, showFooter = true}: ViewCardProps) {
  return (
    <Card padding={0} width="100%">
      <Section variant="transparent" padding={6}>
        <VStack gap={8}>{children}</VStack>
      </Section>
      {showFooter && footerActions && (
        <Section variant="muted" padding={4} dividers={['top']}>
          <HStack gap={2} vAlign="center" hAlign="end">
            {footerActions}
          </HStack>
        </Section>
      )}
    </Card>
  );
}
