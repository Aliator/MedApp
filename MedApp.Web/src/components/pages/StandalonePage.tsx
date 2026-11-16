import {Card} from '@astryxdesign/core/Card';
import {Center} from '@astryxdesign/core/Center';
import {Icon} from '@astryxdesign/core/Icon';
import {VStack} from '@astryxdesign/core/Stack';
import {Heading, Text} from '@astryxdesign/core/Text';
import type {CSSProperties, ReactNode} from 'react';
import {usePageTitle} from '../../app/announcements';
import {AppIcon} from '../shared/icons';

// These pages have no AppShell, so they paint the body background themselves (from Astryx's `login` template).
const pageStyle: CSSProperties = {minHeight: '100dvh', backgroundColor: 'var(--color-background-body)'};

export interface StandalonePageProps {
  /** The page's heading, and the browser tab's title. */
  title: string;
  /** How wide the card may grow, in pixels. Stack has no maxWidth prop, so it's set as a style. */
  maxWidth?: number;
  /** The card's content, under the heading: usually a form. */
  children: ReactNode;
}

/**
 * A page outside the signed-in layout, with no side nav: the MedApp name over one card, centred in the window, its
 * heading at the top. For signing in and what goes with it: `/login`, `/forgot-password`, `/reset-password`,
 * `/confirm-email`, and the pages an account is held on before it can go in (`/change-password`,
 * `/set-up-two-factor`). It names the page (`usePageTitle`), as `Page` does inside the layout.
 */
export function StandalonePage({title, maxWidth = 440, children}: StandalonePageProps) {
  usePageTitle(title, true);
  return (
    <Center axis="both" padding={6} style={pageStyle}>
      <VStack gap={4} hAlign="center" style={{width: '100%', maxWidth}}>
        <VStack gap={2} hAlign="center">
          <Icon icon={AppIcon.Shield} size="lg" color="accent" />
          <Text type="body" weight="bold" size="lg">
            MedApp
          </Text>
        </VStack>

        <Card padding={8} width="100%">
          <VStack gap={4} hAlign="stretch">
            <Heading level={1}>{title}</Heading>
            {children}
          </VStack>
        </Card>
      </VStack>
    </Center>
  );
}
