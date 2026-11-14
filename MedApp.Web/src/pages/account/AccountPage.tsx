import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {MetadataListItem} from '@astryxdesign/core/MetadataList';
import {VStack} from '@astryxdesign/core/Stack';
import {RadioList, RadioListItem} from '@astryxdesign/core/RadioList';
import {Slider} from '@astryxdesign/core/Slider';
import {Switch} from '@astryxdesign/core/Switch';
import {Text} from '@astryxdesign/core/Text';
import {useColorMode} from '../../app/colorMode';
import {FLYOUT_DELAYS, useNavSettings} from '../../app/navSettings';
import {TEXT_SIZES, useTextSize, type TextSize} from '../../app/textSize';
import {useCurrentUser} from '../../auth/auth';
import {Page} from '../../components/pages/Page';
import {AppIcon} from '../../components/shared/icons';
import {CardSection} from '../../components/views/CardSection';
import {ViewCard} from '../../components/views/ViewCard';
import {ViewSection} from '../../components/views/ViewSection';

/**
 * The signed-in user's Account Settings, as cards: the account (who they are, and Change Password, a page of its
 * own); how the app looks (light or dark mode, and Change Colours, a page of its own); half views (what the side
 * nav does while one is open); and accessibility (the text size, and how long the folded nav waits on hover). All but the account are kept in this browser.
 */
export function AccountPage() {
  const me = useCurrentUser();
  const {mode, setMode} = useColorMode();
  const {textSize, setTextSize} = useTextSize();
  const {closesHalfView, setClosesHalfView, flyoutDelay, setFlyoutDelay} = useNavSettings();

  return (
    <Page title="Account Settings" subtitle="Your account and how the app looks">
      <VStack gap={6}>
        <ViewCard
          footerActions={
            <Button
              label="Change Password"
              variant="secondary"
              icon={<Icon icon={AppIcon.Password} />}
              href="/account/password"
            />
          }>
          <ViewSection title="Account">
            <MetadataListItem label="Username">{me.username}</MetadataListItem>
            <MetadataListItem label="Role">{me.isAdmin ? 'Administrator' : 'User'}</MetadataListItem>
          </ViewSection>
        </ViewCard>
        <ViewCard
          footerActions={
            <Button
              label="Change Colours"
              variant="secondary"
              icon={<Icon icon={AppIcon.Colours} />}
              href="/account/colours"
            />
          }>
          <CardSection title="Appearance">
            <VStack gap={1}>
              <Switch
                label="Dark mode"
                value={mode === 'dark'}
                onChange={(isDark) => setMode(isDark ? 'dark' : 'light')}
              />
              <Text type="supporting" color="secondary">
                Remembered in this browser, as are your colours.
              </Text>
            </VStack>
          </CardSection>
        </ViewCard>
        <ViewCard showFooter={false}>
          <CardSection title="Half Views">
            <Switch
              label="Nav items close the half view"
              description="On: clicking a side nav item while a half view is open shows its page on its own. Off: it opens on the left, beside the half view. Either way, the item's buttons open it on the left or the right."
              value={closesHalfView}
              onChange={setClosesHalfView}
            />
          </CardSection>
        </ViewCard>
        <ViewCard showFooter={false}>
          <CardSection title="Accessibility">
            <VStack gap={5}>
              <RadioList
                label="Text size"
                description="Makes the app's text (and the space around it) bigger or smaller, in this browser."
                orientation="horizontal"
                value={textSize}
                onChange={(value) => setTextSize(value as TextSize)}>
                {TEXT_SIZES.map((size) => (
                  <RadioListItem key={size.value} value={size.value} label={size.label} />
                ))}
              </RadioList>
              {/* A slider through the fixed delays, by position (they aren't evenly spaced), each marked with its time. */}
              <Slider
                label="Side nav hover delay"
                description="With a half view open, how long the pointer rests on a side nav icon before its item comes out."
                min={0}
                max={FLYOUT_DELAYS.length - 1}
                step={1}
                value={FLYOUT_DELAYS.findIndex((delay) => delay.value === flyoutDelay)}
                onChange={(index: number) => setFlyoutDelay(FLYOUT_DELAYS[index].value)}
                formatValue={(index) => `${FLYOUT_DELAYS[index].label} (${FLYOUT_DELAYS[index].time})`}
                valueDisplay="none"
                marks={FLYOUT_DELAYS.map((delay, index) => ({value: index, label: delay.time}))}
                width="min(400px, 100%)"
              />
            </VStack>
          </CardSection>
        </ViewCard>
      </VStack>
    </Page>
  );
}
