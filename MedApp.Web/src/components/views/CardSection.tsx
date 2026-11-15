import {Section} from '@astryxdesign/core/Section';
import {VStack} from '@astryxdesign/core/Stack';
import {Heading} from '@astryxdesign/core/Text';
import {createContext, useContext, type ReactNode} from 'react';
import {PanelHeader} from './PanelCard';

/** Set by cards (ViewCard, FormCard) whose sections should show their titles as header bars. */
export const InCardContext = createContext(false);

/**
 * The layout of a titled section (ViewSection, FormSection). In a card, the title is a header bar across the card
 * (as Details and Stages have) with the content padded below. Elsewhere, such as inside a PanelCard's body, it's
 * a plain heading above the content.
 */
export function CardSection({title, children}: {title?: string; children: ReactNode}) {
  const isInCard = useContext(InCardContext);

  if (!isInCard) {
    return (
      <VStack gap={4}>
        {title && <Heading level={2}>{title}</Heading>}
        {children}
      </VStack>
    );
  }

  // A line under each section's content, so the next section's bar (or the card's footer) is ruled off from it.
  return (
    <>
      {title && <PanelHeader title={title} />}
      <Section variant="transparent" padding={6} dividers={['bottom']}>
        {children}
      </Section>
    </>
  );
}
