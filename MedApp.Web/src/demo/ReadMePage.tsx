import {useAppShellMobile} from '@astryxdesign/core/AppShell';
import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {List, ListItem} from '@astryxdesign/core/List';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Heading, Text} from '@astryxdesign/core/Text';
import {useNavigate} from '@tanstack/react-router';
import {useContext, useEffect, useRef, useState} from 'react';
import {useCurrentUser} from '../auth/auth';
import {Page} from '../components/pages/Page';
import {InHalfPaneContext, useHalfView, useHalfViews} from '../components/pages/halfView';
import {AlertMessage} from '../components/shared/AlertMessage';
import {AppIcon} from '../components/shared/icons';
import {CardSection} from '../components/views/CardSection';
import {ADMIN, accountLabel, personByUsername} from './accounts';
import {PLACE_ATTRIBUTE, rememberReadMePlace, restoreReadMePlace} from './readMePlace';
import {markReadMeSeen, READ_ME_PATH} from './readMeSeen';
import {SampleText} from './SampleText';
import {startCoachTour} from './coach/coachState';
import {switchAccount} from './session';
import {db} from './store';
import {TOUR, TOUR_PARTS, type TourPart, type TourStep} from './tour';

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
    return 'That record isn’t in your copy of the demo any more. Reset brings it back.';
  return step.whyNot?.(db()) ?? null;
}

/** A part of the walkthrough's title, with its intro when it has one. */
function TourPartHeading({part}: {part: TourPart}) {
  const {title, intro}: {title: string; intro?: string} = TOUR_PARTS.find((p) => p.key === part)!;
  return (
    <VStack gap={1}>
      <Heading level={3}>{title}</Heading>
      {intro && <Text type="body">{intro}</Text>}
    </VStack>
  );
}

/** The light demo's Read Me: just the tutorial walkthrough. */
export function ReadMePage() {
  const me = useCurrentUser();
  const navigate = useNavigate();
  const halfViews = useHalfViews();
  const [notes, setNotes] = useState<Record<string, string>>({});
  const root = useRef<HTMLDivElement>(null);

  useEffect(() => markReadMeSeen(), []);

  // Keep the walkthrough beside a page it opens when half views are available.
  const isOnRight = useContext(InHalfPaneContext);
  const {isHalf} = useHalfView();
  const {isMobile} = useAppShellMobile();
  const beside = isMobile ? null : halfViews;

  useEffect(() => {
    if (!isOnRight && isHalf) return;
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
      if (!step.whyNot?.(db())) return;
    }
    if (step.account && step.account !== me.username) {
      const readMeOnRight = beside !== null && step.path !== undefined;
      if (readMeOnRight || step.path === undefined) rememberReadMePlace(root.current, step.key, readMeOnRight ? 'right' : 'whole');
      await switchAccount(step.account, step.path ?? READ_ME_PATH, {readMeOnRight});
      return;
    }
    if (step.path) await openPage(step.path, step.key);
  };

  const switchForStep = async (step: TourStep, username: string) => {
    rememberReadMePlace(root.current, step.key, 'whole');
    await switchAccount(username, READ_ME_PATH);
  };

  return (
    <Page title="Read Me">
      <VStack ref={root} gap={6}>
        <CardSection title="Tutorial walkthrough">
          <VStack gap={5}>
            <HStack>
              <Button label="Start the Guided Tour" variant="primary" icon={<Icon icon={AppIcon.Question} />} onClick={() => startCoachTour()} />
            </HStack>
            <Text type="body">
              {beside
                ? 'Each button opens the exact page on the left, keeping this walkthrough on the right to follow, and switches account first if the step needs it.'
                : 'Each button opens the exact page, switching account first if the step needs it.'}
            </Text>
            {TOUR.map((step, i) => (
              <VStack key={step.key} gap={2} {...{[PLACE_ATTRIBUTE]: step.key}}>
                {step.part !== TOUR[i - 1]?.part && <TourPartHeading part={step.part} />}
                <Heading level={4}>
                  {`${i + 1}. ${step.title}`}
                  {step.account ? ` (as ${accountLabel(personByUsername(step.account) ?? ADMIN)})` : ''}
                </Heading>
                <List listStyle={step.isNumbered ? 'decimal' : 'disc'} edge="flush" density="compact">
                  {step.steps.map((line) => (
                    <ListItem key={line} label={<Text type="body">{line}</Text>} />
                  ))}
                </List>
                {step.switchTo && step.switchTo !== me.username && (
                  <HStack>
                    <Button
                      label={`Switch to ${step.switchTo}`}
                      variant="secondary"
                      icon={<Icon icon={AppIcon.Account} />}
                      onClick={() => void switchForStep(step, step.switchTo!)}
                    />
                  </HStack>
                )}
                {step.sampleText && <SampleText lines={step.sampleText} />}
                {notes[step.key] && <AlertMessage status="info" label="About this step" message={notes[step.key]} />}
                <HStack gap={2} wrap="wrap">
                  {step.buttonLabel && <Button label={step.buttonLabel} variant="secondary" icon={<Icon icon={AppIcon.ChevronRight} />} onClick={() => void start(step)} />}
                  <Button label="Guide Me Through It" variant="ghost" icon={<Icon icon={AppIcon.Question} />} onClick={() => startCoachTour(i)} />
                </HStack>
              </VStack>
            ))}
          </VStack>
        </CardSection>
      </VStack>
    </Page>
  );
}
