import {Card} from '@astryxdesign/core/Card';
import {Section} from '@astryxdesign/core/Section';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Fragment, type ReactNode} from 'react';
import {CollapseButton, PanelHeader} from './PanelCard';

// Open sections share the space equally: a zero flex basis, which StackItem has no prop for.
const EQUAL_SHARE = {flexBasis: 0};

export interface FoldingSection {
  key: string;
  title: string;
  isOpen: boolean;
  onToggle: () => void;
  /** The section's body, which scrolls within the section's share of the card. Lay out its own padding. */
  children: ReactNode;
}

export interface FoldingCardProps {
  sections: FoldingSection[];
  /** Buttons in a footer pinned to the bottom, aligned to the end. */
  footerActions?: ReactNode;
}

/**
 * A card of sections, each under a header bar that folds it when clicked (its chevron button does the same). It
 * fills the height it's given, and the open sections share it equally (two open: half each; three: a third
 * each); a folded section is just its bar. Each open section's body scrolls on its own, so the card and page
 * never do.
 */
export function FoldingCard({sections, footerActions}: FoldingCardProps) {
  return (
    <Card padding={0} width="100%" height="100%">
      <VStack gap={0} height="100%">
        <StackItem size="fill">
          <VStack gap={0} height="100%">
            {sections.map((section, index) => {
              const bar = (
                <PanelHeader
                  title={section.title}
                  // A line above every bar but the first, and below an open one.
                  dividers={[...(index > 0 ? (['top'] as const) : []), ...(section.isOpen ? (['bottom'] as const) : [])]}
                  // The whole bar folds the section; its chevron is the keyboard control, and its click reaches the bar.
                  onClick={section.onToggle}
                  actions={<CollapseButton name={section.title.toLowerCase()} isOpen={section.isOpen} />}
                />
              );
              return section.isOpen ? (
                <StackItem key={section.key} size="fill" style={EQUAL_SHARE}>
                  <VStack gap={0} height="100%">
                    {bar}
                    <StackItem size="fill" isScrollable>
                      {section.children}
                    </StackItem>
                  </VStack>
                </StackItem>
              ) : (
                <Fragment key={section.key}>{bar}</Fragment>
              );
            })}
          </VStack>
        </StackItem>
        {footerActions && (
          <Section variant="muted" padding={4} dividers={['top']}>
            <HStack gap={2} vAlign="center" hAlign="end">
              {footerActions}
            </HStack>
          </Section>
        )}
      </VStack>
    </Card>
  );
}
