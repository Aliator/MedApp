import {useAppShellMobile} from '@astryxdesign/core/AppShell';
import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {List, ListItem} from '@astryxdesign/core/List';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useNavigate} from '@tanstack/react-router';
import {useContext, useEffect, useState} from 'react';
import {useCurrentUser} from '../auth/auth';
import {useNotepad} from '../components/notepad/NotepadContext';
import {Page} from '../components/pages/Page';
import {InHalfPaneContext, useHalfViews} from '../components/pages/halfView';
import {AlertMessage} from '../components/shared/AlertMessage';
import {AppIcon} from '../components/shared/icons';
import {CardSection} from '../components/views/CardSection';
import {ViewCard} from '../components/views/ViewCard';
import {ADMIN, DEMO_ACCOUNTS, STAFF} from './accounts';
import {DemoBanner} from './DemoBanner';
import {markReadMeSeen, READ_ME_PATH} from './readMeSeen';
import {resetDemo, switchAccount} from './session';
import {db} from './store';
import {NOT_IN_THE_LIGHT_VERSION, TOUR, type TourStep} from './tour';

/** The IDs in a path (`/pathways/<id>`), by the kind of record the page before them names. */
const RECORDS: Record<string, (id: string) => boolean> = {
  patients: (id) => db().patients.some((p) => p.id === id),
  pathways: (id) => db().pathways.some((p) => p.id === id),
  referrals: (id) => db().referrals.some((r) => r.id === id),
};

/** Why a tour step can't open what it says (a record gone, or the step already done), or null. */
export function whyStepCantOpen(step: TourStep): string | null {
  const [, kind, id] = (step.path ?? '').split(/[/?]/);
  if (id && RECORDS[kind] && !RECORDS[kind](id))
    return 'That record isn’t in your copy of the demo any more. Reset (in the banner at the top of the Read Me) brings it back.';
  return step.whyNot?.(db()) ?? null;
}

/**
 * The light demo's Read Me (the demo build only): what this is, the two accounts, a guided tour whose buttons open the
 * exact page or record (switching account first when the step needs it), and what the full version does that this one
 * doesn't, with the demo's banner at the top. Shown on the first visit (`DemoFirstVisit`), and in the side nav.
 */
export function ReadMePage() {
  const me = useCurrentUser();
  const navigate = useNavigate();
  const halfViews = useHalfViews();
  const notepad = useNotepad();
  const [notes, setNotes] = useState<Record<string, string>>({});

  useEffect(() => markReadMeSeen(), []);

  // Pages open on the left, keeping the Read Me on the right (a half view), so it can be read beside them: from the
  // Read Me on its own, as Ctrl-click opens one (it moves to the right); from the right half, in the main view. A phone
  // has no half view, and nowhere without the half views to open them (a test of the page alone) has one either: there
  // the page opens in the Read Me's place.
  const isOnRight = useContext(InHalfPaneContext);
  const {isMobile} = useAppShellMobile();
  const beside = isMobile ? null : halfViews;
  const openPage = async (path: string) => {
    if (beside) {
      if (isOnRight) beside.showOnLeft(path);
      else beside.openOnLeft(path);
      return;
    }
    if (halfViews?.openPath) halfViews.close();
    await navigate({href: path} as Parameters<typeof navigate>[0]);
  };

  const start = async (step: TourStep) => {
    const why = whyStepCantOpen(step);
    if (why) {
      setNotes((current) => ({...current, [step.key]: why}));
      // Something already done still opens: the step says what's different. A record that's gone doesn't.
      if (!step.whyNot?.(db())) return;
    }
    if (step.account && step.account !== me.username) {
      // After the reload, the same arrangement: the page on the left, the Read Me on the right.
      await switchAccount(step.account, step.path ?? READ_ME_PATH, {readMeOnRight: beside !== null && step.path !== undefined});
      return;
    }
    if (step.staysOnReadMe) {
      // The Read Me on its own, where the step's instructions start: from the right half, it takes the whole view.
      if (isOnRight) halfViews?.openFull(READ_ME_PATH);
      else if (halfViews?.openPath) halfViews.close();
      return;
    }
    // A step with no page (the notepad's) stays where it is.
    if (step.path) await openPage(step.path);
    if (step.opensNotepad) notepad?.open();
  };

  const other = me.username === ADMIN.username ? STAFF : ADMIN;

  return (
    <Page title="Read Me" subtitle="The light version of MedApp, for trying it out">
      <VStack gap={6}>
        <DemoBanner />
        <ViewCard
          footerActions={
            <Button label="Start Using MedApp" variant="primary" icon={<Icon icon={AppIcon.Home} />} onClick={() => void openPage('/')} />
          }>
          <CardSection title="What this is">
            <List listStyle="disc" edge="flush" density="compact">
              <ListItem label="A light version of MedApp, a patient and pathway app, with no server: it runs entirely in your browser." />
              <ListItem label="The data is synthetic: made-up patients, staff and NHS numbers (from a range never issued). Don’t enter real patient details." />
              <ListItem label="Your changes stay in this browser only. Reset, in the banner at the top of this page, puts everything back as it started." />
              <ListItem label="The rules are simplified: the full version checks every change on its server." />
            </List>
          </CardSection>
        </ViewCard>

        <ViewCard
          footerActions={
            <Button
              label={`Switch to ${other.fullName}`}
              variant="secondary"
              icon={<Icon icon={AppIcon.Account} />}
              onClick={() => void switchAccount(other.username, '/read-me')}
            />
          }>
          <CardSection title="Two accounts">
            <VStack gap={3}>
              <List listStyle="disc" edge="flush" density="compact">
                {DEMO_ACCOUNTS.map((account) => (
                  <ListItem
                    key={account.id}
                    label={`${account.fullName} (${account.username})${account.username === me.username ? ': you, now' : ''}`}
                    description={`${account.roles.join(' and ')}. ${account.jobTitle}.`}
                  />
                ))}
              </List>
              <Text type="body">
                Switch between them with the button below, or let the tour’s buttons switch for you. The admin can do everything, including the
                setup pages and Insights; {STAFF.fullName} can’t, so each sees MedApp as that role does.
              </Text>
            </VStack>
          </CardSection>
        </ViewCard>

        <ViewCard showFooter={false}>
          <CardSection title="A guided tour">
            <VStack gap={5}>
              <Text type="body">
                {beside
                  ? 'Each button opens the exact page on the left, keeping this Read Me on the right to follow, and switches account first if the step needs it.'
                  : 'Each button opens the exact page, switching account first if the step needs it.'}
              </Text>
              {TOUR.map((step, i) => (
                <VStack key={step.key} gap={2}>
                  <Text type="body" weight="semibold" as="h3">
                    {`${i + 1}. ${step.title}`}
                    {step.account ? ` (as ${step.account === ADMIN.username ? 'the admin' : STAFF.fullName})` : ''}
                  </Text>
                  <List listStyle={step.isNumbered ? 'decimal' : 'disc'} edge="flush" density="compact">
                    {step.steps.map((line) => (
                      <ListItem key={line} label={line} />
                    ))}
                  </List>
                  {notes[step.key] && <AlertMessage status="info" message={notes[step.key]} />}
                  <HStack>
                    <Button label={step.buttonLabel} variant="secondary" icon={<Icon icon={AppIcon.ChevronRight} />} onClick={() => void start(step)} />
                  </HStack>
                </VStack>
              ))}
            </VStack>
          </CardSection>
        </ViewCard>

        <ViewCard showFooter={false}>
          <CardSection title="What doesn’t work in the light version">
            <List listStyle="disc" edge="flush" density="compact">
              {NOT_IN_THE_LIGHT_VERSION.map((item) => (
                <ListItem key={item.title} label={item.title} description={`The full version: ${item.full}`} />
              ))}
            </List>
          </CardSection>
        </ViewCard>

        <ViewCard
          footerActions={
            <Button label="Reset the Demo" variant="secondary" icon={<Icon icon={AppIcon.Refresh} />} onClick={() => void resetDemo()} />
          }>
          <CardSection title="How it was made light">
            <Text type="body">
              The full MedApp is a .NET API with SQL Server, rendered on a server and deployed to Kubernetes, behind a
              security baseline. This version is the same React app, built as static files for GitHub Pages: its calls to
              the API are answered in the browser by a small stand-in, which keeps its data in this browser’s storage.
            </Text>
          </CardSection>
        </ViewCard>
      </VStack>
    </Page>
  );
}
