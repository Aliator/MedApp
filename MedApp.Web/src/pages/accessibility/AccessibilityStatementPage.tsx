import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {List, ListItem} from '@astryxdesign/core/List';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {ACCESSIBILITY_SETTINGS} from '../../app/accessibility';
import {Page} from '../../components/pages/Page';
import {AppIcon} from '../../components/shared/icons';
import {CardSection} from '../../components/views/CardSection';
import {ViewCard} from '../../components/views/ViewCard';

/** When the statement was last reviewed, against the audit it describes. */
export const STATEMENT_DATE = '15 November 2026';

/** Problems the last audit found that aren't fixed yet. Empty: none known. */
const KNOWN_ISSUES: readonly string[] = [];

/**
 * The accessibility statement: the standard MedApp aims for, how it was checked, what people can change for
 * themselves (the settings in Account Settings, Accessibility), what isn't right yet, and how to report a problem.
 */
export function AccessibilityStatementPage() {
  return (
    <Page title="Accessibility statement" subtitle={`Last reviewed ${STATEMENT_DATE}`}>
      <VStack gap={6}>
        <ViewCard showFooter={false}>
          <CardSection title="Our aim">
            <Text type="body">
              MedApp aims to meet the Web Content Accessibility Guidelines (WCAG) 2.2 at level AA, so that everyone
              who works with it can use it: with a keyboard alone, with a screen reader, zoomed in up to 400%, and
              in light or dark mode.
            </Text>
          </CardSection>
        </ViewCard>
        <ViewCard
          footerActions={
            <Button label="Accessibility settings" variant="secondary" icon={<Icon icon={AppIcon.Accessibility} />} href="/account" />
          }>
          <CardSection title="What you can change">
            <VStack gap={3}>
              <Text type="body">
                In Account Settings you can change the text size, the colours, and these, which follow your account
                to any browser you sign in on:
              </Text>
              <List listStyle="disc" edge="flush" density="compact">
                {ACCESSIBILITY_SETTINGS.map((setting) => (
                  <ListItem key={setting.key} label={setting.label} description={setting.description} />
                ))}
              </List>
            </VStack>
          </CardSection>
        </ViewCard>
        <ViewCard showFooter={false}>
          <CardSection title="How we checked">
            <Text type="body">
              Every screen was checked with axe, an automated accessibility checker, against WCAG 2.2 A and AA, in
              light mode, dark mode and a colour theme. Keyboard use, focus, zoom and the settings above were
              checked by hand. The checks run again whenever the app’s screens change.
            </Text>
          </CardSection>
        </ViewCard>
        <ViewCard showFooter={false}>
          <CardSection title="Known problems">
            {KNOWN_ISSUES.length === 0 ? (
              <Text type="body">The last check found no problems that aren’t fixed.</Text>
            ) : (
              <List listStyle="disc" edge="flush" density="compact">
                {KNOWN_ISSUES.map((issue) => (
                  <ListItem key={issue} label={issue} />
                ))}
              </List>
            )}
          </CardSection>
        </ViewCard>
        <ViewCard showFooter={false}>
          <CardSection title="Reporting a problem">
            <Text type="body">
              If something in MedApp is hard to use, or doesn’t work with the way you use your computer, tell your
              system administrator. Say which page it was on and what you were trying to do.
            </Text>
          </CardSection>
        </ViewCard>
      </VStack>
    </Page>
  );
}
// CI proof: a scratch line, never merged.
