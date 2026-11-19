import {useAppShellMobile} from '@astryxdesign/core/AppShell';
import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {List, ListItem} from '@astryxdesign/core/List';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useNavigate} from '@tanstack/react-router';
import {useContext, useEffect, useRef, useState} from 'react';
import {useCurrentUser} from '../auth/auth';
import {useNotepad} from '../components/notepad/NotepadContext';
import {Page} from '../components/pages/Page';
import {InHalfPaneContext, useHalfView, useHalfViews} from '../components/pages/halfView';
import {AlertMessage} from '../components/shared/AlertMessage';
import {AppIcon} from '../components/shared/icons';
import {CardSection} from '../components/views/CardSection';
import {ViewCard} from '../components/views/ViewCard';
import {accountDescription, accountLabel, ADMIN, DEMO_ACCOUNTS, LAB, personByUsername, STAFF} from './accounts';
import {DemoBanner} from './DemoBanner';
import {PLACE_ATTRIBUTE, rememberReadMePlace, restoreReadMePlace} from './readMePlace';
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
 * The light demo's Read Me (the demo build only): what this is, the three accounts, a guided tour whose buttons open the
 * exact page or record (switching account first when the step needs it), and what the full version does that this one
 * doesn't, with the demo's banner at the top. Shown on the first visit (`DemoFirstVisit`), and in the side nav.
 */
export function ReadMePage() {
  const me = useCurrentUser();
  const navigate = useNavigate();
  const halfViews = useHalfViews();
  const notepad = useNotepad();
  const [notes, setNotes] = useState<Record<string, string>>({});
  // This Read Me's content, where its place is kept and put back.
  const root = useRef<HTMLDivElement>(null);

  useEffect(() => markReadMeSeen(), []);

  // Pages open on the left, keeping the Read Me on the right (a half view), so it can be read beside them: from the
  // Read Me on its own, as Ctrl-click opens one (it moves to the right); from the right half, in the main view. A phone
  // has no half view, and nowhere without the half views to open them (a test of the page alone) has one either: there
  // the page opens in the Read Me's place.
  const isOnRight = useContext(InHalfPaneContext);
  const {isHalf} = useHalfView();
  const {isMobile} = useAppShellMobile();
  const beside = isMobile ? null : halfViews;

  // Back where the reader was, when a tour button moved the Read Me here (it would otherwise start at the top): once
  // it's laid out, in the right half or on its own. One in the left half (the moment before a page replaces it there)
  // leaves the place for the right one.
  useEffect(() => {
    if (!isOnRight && isHalf) return;
    // Tried on each frame until it's laid out in its scrolling area (a half pane's page settles over a few), for up to
    // two seconds or so.
    let frame = 0;
    let tries = 0;
    const attempt = () => {
      if (restoreReadMePlace(root.current, isOnRight ? 'right' : 'whole') === 'not yet' && ++tries < 120) frame = requestAnimationFrame(attempt);
    };
    frame = requestAnimationFrame(attempt);
    return () => cancelAnimationFrame(frame);
  }, [isOnRight, isHalf]);
  const openPage = async (path: string, place?: string) => {
    if (beside) {
      if (isOnRight) beside.showOnLeft(path);
      else {
        // The Read Me moves to the right half, as a new page there.
        if (place) rememberReadMePlace(root.current, place, 'right');
        beside.openOnLeft(path);
      }
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
      const readMeOnRight = beside !== null && step.path !== undefined;
      // The Read Me shows again after it (beside the page, or alone when the step has no page): kept where the reader was.
      if (readMeOnRight || step.path === undefined) rememberReadMePlace(root.current, step.key, readMeOnRight ? 'right' : 'whole');
      await switchAccount(step.account, step.path ?? READ_ME_PATH, {readMeOnRight});
      return;
    }
    if (step.staysOnReadMe) {
      // The Read Me on its own, where the step's instructions start: from the right half, it takes the whole view.
      if (isOnRight || halfViews?.openPath) rememberReadMePlace(root.current, step.key, 'whole');
      if (isOnRight) halfViews?.openFull(READ_ME_PATH);
      else if (halfViews?.openPath) halfViews.close();
      return;
    }
    // A step with no page (the notepad's) stays where it is.
    if (step.path) await openPage(step.path, step.key);
    if (step.opensNotepad) notepad?.open();
  };

  const others = DEMO_ACCOUNTS.filter((account) => account.username !== me.username);

  return (
    <Page title="Read Me" subtitle="The light version of MedApp, for trying it out">
      <VStack ref={root} gap={6}>
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
            <HStack gap={2} wrap="wrap" {...{[PLACE_ATTRIBUTE]: 'switch-account'}}>
              {others.map((other) => (
                <Button
                  key={other.id}
                  label={`Switch to ${other.fullName}`}
                  variant="secondary"
                  icon={<Icon icon={AppIcon.Account} />}
                  onClick={() => {
                    // A full reload, back to the Read Me on its own: kept where the reader was.
                    rememberReadMePlace(root.current, 'switch-account', 'whole');
                    void switchAccount(other.username, READ_ME_PATH);
                  }}
                />
              ))}
            </HStack>
          }>
          <CardSection title="Three accounts">
            <VStack gap={3}>
              <List listStyle="disc" edge="flush" density="compact">
                {DEMO_ACCOUNTS.map((account) => (
                  <ListItem
                    key={account.id}
                    label={`${account.fullName} (${account.username})${account.username === me.username ? ': you, now' : ''}`}
                    description={accountDescription(account)}
                  />
                ))}
              </List>
              <Text type="body">
                Switch between them with the buttons below, or let the tour’s buttons switch for you. The admin runs the centre: accounts, setup,
                Insights, and every team’s records to read, but no clinical changes. Only staff do clinical work: {STAFF.fullName} as a
                Dermatology clinician, and {LAB.fullName} in the lab. A clinic admin is staff who also looks after their clinic; its extra rights
                come later, so for now she works as any clinician does.
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
                <VStack key={step.key} gap={2} {...{[PLACE_ATTRIBUTE]: step.key}}>
                  <Text type="body" weight="semibold" as="h3">
                    {`${i + 1}. ${step.title}`}
                    {step.account ? ` (as ${accountLabel(personByUsername(step.account) ?? ADMIN)})` : ''}
                  </Text>
                  <List listStyle={step.isNumbered ? 'decimal' : 'disc'} edge="flush" density="compact">
                    {/* Each as text, so a long one wraps (a plain string label is cut to one line). */}
                    {step.steps.map((line) => (
                      <ListItem key={line} label={<Text type="body">{line}</Text>} />
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
