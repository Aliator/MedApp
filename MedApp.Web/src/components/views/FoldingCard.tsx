import {Card} from '@astryxdesign/core/Card';
import {Section} from '@astryxdesign/core/Section';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Fragment, type ReactNode} from 'react';
import {CollapseButton, PanelHeader} from './PanelCard';

export interface FoldingSection {
  key: string;
  title: string;
  isOpen: boolean;
  onToggle: () => void;
  /** False for a section that is always open: its bar has no chevron and doesn't fold it. True by default. */
  canFold?: boolean;
  /** Shown in the bar after the title (see PanelHeader's `summary`), whether the section is open or folded. */
  summary?: ReactNode;
  /** The section's body, at its own height (the card's sections scroll together). Lay out its own padding. */
  children: ReactNode;
}

export interface FoldingCardProps {
  sections: FoldingSection[];
  /** Buttons in a footer pinned to the bottom, aligned to the end (wrapping onto another line when they don't fit). */
  footerActions?: ReactNode;
}

/**
 * A card of sections, each under a header bar that folds it when clicked (its chevron button does the same). It
 * fills the height it's given; its sections sit one under another at their own heights (a folded one is just its
 * bar), and scroll together, as one, when they're taller than the card, so the page never does. The footer stays
 * pinned below them.
 */
export function FoldingCard({sections, footerActions}: FoldingCardProps) {
  return (
    <Card padding={0} width="100%" height="100%">
      <VStack gap={0} height="100%">
        <StackItem size="fill" isScrollable>
          <VStack gap={0}>
            {sections.map((section, index) => (
              <Fragment key={section.key}>
                <PanelHeader
                  title={section.title}
                  summary={section.summary}
                  // A line above every bar but the first, and below an open one.
                  dividers={[...(index > 0 ? (['top'] as const) : []), ...(section.isOpen ? (['bottom'] as const) : [])]}
                  // The whole bar folds the section; its chevron is the keyboard control, and its click reaches the bar.
                  onClick={section.canFold === false ? undefined : section.onToggle}
                  actions={section.canFold === false ? undefined : <CollapseButton name={section.title.toLowerCase()} isOpen={section.isOpen} />}
                />
                {section.isOpen && section.children}
              </Fragment>
            ))}
          </VStack>
        </StackItem>
        {footerActions && (
          <Section variant="muted" padding={4} dividers={['top']}>
            <HStack gap={2} vAlign="center" hAlign="end" wrap="wrap">
              {footerActions}
            </HStack>
          </Section>
        )}
      </VStack>
    </Card>
  );
}
