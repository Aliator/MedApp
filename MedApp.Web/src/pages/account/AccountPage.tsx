import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {MetadataListItem} from '@astryxdesign/core/MetadataList';
import {NumberInput} from '@astryxdesign/core/NumberInput';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {RadioList, RadioListItem} from '@astryxdesign/core/RadioList';
import {Slider} from '@astryxdesign/core/Slider';
import {Switch} from '@astryxdesign/core/Switch';
import {useQuery} from '@tanstack/react-query';
import {ACCESSIBILITY_SETTINGS, useAccessibility} from '../../app/accessibility';
import {useColorMode} from '../../app/colorMode';
import {useRef, useState} from 'react';
import {MAX_PAGE_SIZE, MIN_PAGE_SIZE, useListSettings} from '../../app/listSettings';
import {FLYOUT_DELAYS, START_PAGES, useNavSettings, type StartPage} from '../../app/navSettings';
import {TEXT_SIZES, useTextSize, type TextSize} from '../../app/textSize';
import {ACCOUNT_TYPE_DESCRIPTIONS, ACCOUNT_TYPE_LABELS} from '../../auth/accountTypes';
import {sortRoles} from '../../auth/roles';
import {myTeamsQuery, teamNames} from '../../api/teams';
import {accountTypeOfUser, useCurrentUser} from '../../auth/auth';
import {NO_STATUS_LINE} from '../../components/forms/fieldSpace';
import {Page} from '../../components/pages/Page';
import {AppIcon} from '../../components/shared/icons';
import {CardSection} from '../../components/views/CardSection';
import {ViewCard} from '../../components/views/ViewCard';
import {ViewSection} from '../../components/views/ViewSection';
import {NotificationSettings} from './NotificationSettings';
import {StaffDetailsCard} from './StaffDetailsCard';

/** The signed-in user's teams, read only: an admin sets them on the Users page. */
function MyTeams() {
  const {data, isPending, isError} = useQuery(myTeamsQuery);
  if (isPending) return 'Loading...';
  if (isError) return 'Your teams could not be loaded.';
  return teamNames(data, 'None yet. An admin sets them on the Users page.');
}

/**
 * The signed-in user's Account Settings, as cards: the account (who they are, their teams, and pages of their own for the email
 * address, security (two-factor authentication) and Change Password); their full name and job title for letters
 * (`StaffDetailsCard`, kept on the server); how the app looks (light or dark mode, and Change Colours, a page of its own); which notifications they get
 * (kept on the server, which checks it before sending: `NotificationSettings`); navigation (the page sign-in
 * opens on); lists (rows per page, and where pathway lists
 * start); and accessibility (the text size, how long the folded nav waits on hover, and the switches in
 * `ACCESSIBILITY_SETTINGS`, with a link to the accessibility statement). All but the account and notifications are kept in this
 * browser and with the account.
 */
export function AccountPage() {
  const me = useCurrentUser();
  const {mode, setMode} = useColorMode();
  const {textSize, setTextSize} = useTextSize();
  const {flyoutDelay, setFlyoutDelay, startPage, setStartPage, foldsInHalfView, setFoldsInHalfView} = useNavSettings();
  const {pageSize, setPageSize, startsOnMine, setStartsOnMine} = useListSettings();
  const accessibility = useAccessibility();
  // Rows per page, as typed: it takes effect on Apply, or Enter. (Enter commits the typed number and applies it in
  // one go, before a re-render, so the latest is kept in a ref too.)
  const [rowsDraft, setRowsDraft] = useState<number | null>(null);
  const typedRows = useRef<number | null>(null);
  // While the box has focus, Apply stays on: a typed number only commits when it loses focus (or on Enter).
  const [isEditingRows, setIsEditingRows] = useState(false);
  const rows = rowsDraft ?? pageSize;
  const typeRows = (value: number) => {
    typedRows.current = value;
    setRowsDraft(value);
  };
  const applyRows = () => {
    setPageSize(typedRows.current ?? rows);
    typedRows.current = null;
    setRowsDraft(null);
  };

  return (
    <Page title="Account Settings" subtitle="Your account and how the app looks">
      <VStack gap={6}>
        <ViewCard
          footerActions={
            <>
              <Button label="Email Address" variant="secondary" icon={<Icon icon={AppIcon.Email} />} href="/account/email" />
              <Button label="Security" variant="secondary" icon={<Icon icon={AppIcon.TwoFactor} />} href="/account/security" />
              <Button
                label="Change Password"
                variant="secondary"
                icon={<Icon icon={AppIcon.Password} />}
                href="/account/password"
              />
            </>
          }>
          <ViewSection title="Account">
            <MetadataListItem label="Username">{me.username}</MetadataListItem>
            {/* Admin, clinic admin or staff (decision 182): from their roles and teams. */}
            <MetadataListItem label="Account type">
              {`${ACCOUNT_TYPE_LABELS[accountTypeOfUser(me)]}: ${ACCOUNT_TYPE_DESCRIPTIONS[accountTypeOfUser(me)]}`}
            </MetadataListItem>
            {/* Your roles, read only: admins give them on the Users page. */}
            <MetadataListItem label={me.roles.length === 1 ? 'Role' : 'Roles'}>
              {me.roles.length > 0 ? sortRoles(me.roles).join(', ') : 'No role'}
            </MetadataListItem>
            <MetadataListItem label="Your teams">
              <MyTeams />
            </MetadataListItem>
          </ViewSection>
        </ViewCard>
        <StaffDetailsCard />
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
            <Switch
              label="Dark mode"
              description="Kept with your account, as are your colours."
              value={mode === 'dark'}
              onChange={(isDark) => setMode(isDark ? 'dark' : 'light')}
            />
          </CardSection>
        </ViewCard>
        <NotificationSettings />
        <ViewCard showFooter={false}>
          <CardSection title="Navigation">
            <VStack gap={5}>
              <RadioList
                label="Start page"
                description="The page you see first after signing in."
                orientation="horizontal"
                value={startPage}
                onChange={(value) => setStartPage(value as StartPage)}>
                {START_PAGES.map((page) => (
                  <RadioListItem key={page.value} value={page.value} label={page.label} />
                ))}
              </RadioList>
              <Switch
                label="Fold the side nav in half view"
                description="Opening a half view folds the side nav to its icons. Turn off to keep it open beside the two halves."
                value={foldsInHalfView}
                onChange={setFoldsInHalfView}
              />
            </VStack>
          </CardSection>
        </ViewCard>
        <ViewCard showFooter={false}>
          <CardSection title="Lists">
            <VStack gap={5}>
              <HStack gap={2} vAlign="end">
                <NumberInput
                  label="Rows per page"
                  description={`How many rows each list shows, from ${MIN_PAGE_SIZE} to ${MAX_PAGE_SIZE}.`}
                  value={rows}
                  onChange={typeRows}
                  onEnter={applyRows}
                  onFocus={() => setIsEditingRows(true)}
                  onBlur={() => setIsEditingRows(false)}
                  min={MIN_PAGE_SIZE}
                  max={MAX_PAGE_SIZE}
                  isIntegerOnly
                  isWheelEnabled={false}
                  width={280}
                  containerStyle={NO_STATUS_LINE}
                />
                <Button label="Apply" variant="secondary" isDisabled={rows === pageSize && !isEditingRows} onClick={applyRows} />
              </HStack>
              <Switch
                label="Pathway lists start on Assigned to me"
                description="Pathways, and a patient's pathways, show only yours to begin with. Remove the filter to see everyone's."
                value={startsOnMine}
                onChange={setStartsOnMine}
              />
            </VStack>
          </CardSection>
        </ViewCard>
        <ViewCard
          footerActions={
            <Button
              label="Accessibility statement"
              variant="secondary"
              icon={<Icon icon={AppIcon.Accessibility} />}
              href="/accessibility"
            />
          }>
          <CardSection title="Accessibility">
            <VStack gap={5}>
              <RadioList
                label="Text size"
                description="Makes the app's text (and the space around it) bigger or smaller."
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
              {ACCESSIBILITY_SETTINGS.map((setting) => (
                <Switch
                  key={setting.key}
                  label={setting.label}
                  description={setting.description}
                  value={accessibility.flags[setting.key]}
                  onChange={(isOn) => accessibility.set(setting.key, isOn)}
                />
              ))}
            </VStack>
          </CardSection>
        </ViewCard>
      </VStack>
    </Page>
  );
}
