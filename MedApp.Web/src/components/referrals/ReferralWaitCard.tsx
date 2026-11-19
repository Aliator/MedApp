import {Button} from '@astryxdesign/core/Button';
import type {ISODateString} from '@astryxdesign/core/Calendar';
import {DateInput} from '@astryxdesign/core/DateInput';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {RadioList, RadioListItem} from '@astryxdesign/core/RadioList';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextArea} from '@astryxdesign/core/TextArea';
import {useState} from 'react';
import {fieldStatus} from '../../api/errors';
import {
  CLOCK_PAUSE_REASONS,
  MAX_PAUSE_DETAIL,
  MAX_PAUSE_REVIEW_DAYS,
  pauseNeedsDetail,
  pauseReasonWords,
  useClockActions,
  type PathwayReferral,
} from '../../api/referrals';
import {useAnnounce} from '../../app/announcements';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../../pages/saveProblem';
import {addDays, ukToday} from '../../utils/calendarSlots';
import {formatDate, formatShortDate, WEEK_STARTS_ON} from '../../utils/dates';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {PatientBanner, type BannerPatient} from '../patients/PatientBanner';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import {ClockPauseList, WaitingClockDetails} from './WaitingClock';

/** What the pause dialog says before anything is chosen (decisions 32 and 33). */
export const PAUSE_EXPLANATION =
  "Only a delay the patient chose or caused pauses the clock: the centre's own waits, such as waiting for results or for a clinic, always count. While it's paused, the pathway's target date is the review date.";

export interface ReferralWaitCardProps {
  /** The referral the pathway was accepted from, with its clock and pauses (`pathway.referral`). */
  referral: PathwayReferral;
  /** The pathway whose page this is: its target date moves with the clock. */
  pathwayId: string;
  /** Who the pathway is for, shown in the dialogs as `PatientBanner` shows them (the dialogs cover the page's). */
  patient?: BannerPatient | null;
}

/**
 * The pathway page's **Referral and wait**: the referral the pathway came from (received, team, a link to it),
 * its waiting-time clock ("Day 84 of 126", Wait breached, Paused with the review date, Stopped and why: decisions 27 to 38)
 * and every pause. **Pause Clock** (while `canPauseClock`) asks why (Patient chose to wait, Patient unavailable, Other
 * with a detail), and when to review it (tomorrow to 90 days ahead, which becomes the pathway's target date);
 * **Resume Clock** (while `canResumeClock`) asks first. Both send the referral's version the page showed, so a change
 * made meanwhile is the API's 409, shown in the dialog in its words, and the pathway is read again. Nothing is
 * suggested: staff choose the reason and date. Changing a review date is resuming and pausing again (decision 34).
 */
export function ReferralWaitCard({referral, pathwayId, patient}: ReferralWaitCardProps) {
  const announce = useAnnounce();
  const actions = useClockActions(referral.id ?? '', pathwayId);
  const [dialog, setDialog] = useState<'pause' | 'resume' | null>(null);
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);
  const clock = referral.clock;

  const open = (which: 'pause' | 'resume') => {
    setProblem(NO_PROBLEM);
    setDialog(which);
  };
  const close = () => {
    setProblem(NO_PROBLEM);
    setDialog(null);
  };

  const resume = () => {
    setProblem(NO_PROBLEM);
    actions.resume.mutate(
      {expectedVersion: referral.version ?? 0},
      {
        onSuccess: () => {
          announce('Clock resumed');
          setDialog(null);
        },
        onError: (error) => setProblem(saveProblem(error, 'save', 'referral')),
      },
    );
  };

  return (
    <VStack gap={6} hAlign="stretch">
      <HStack gap={3} vAlign="center" wrap="wrap">
        <Text type="body">{`Received ${formatDate(referral.receivedOn ?? '')}, ${referral.teamName ?? ''}`}</Text>
        <Link href={`/referrals/${referral.id}`}>Open the referral</Link>
      </HStack>
      {clock ? (
        <WaitingClockDetails clock={clock} />
      ) : (
        <Text type="body" color="secondary">
          This referral has no waiting-time clock.
        </Text>
      )}
      {(referral.canPauseClock || referral.canResumeClock) && (
        <HStack gap={2} wrap="wrap">
          {referral.canPauseClock && (
            <Button label="Pause Clock" variant="secondary" icon={<Icon icon={AppIcon.PauseClock} />} onClick={() => open('pause')} />
          )}
          {referral.canResumeClock && (
            <Button label="Resume Clock" variant="secondary" icon={<Icon icon={AppIcon.ResumeClock} />} onClick={() => open('resume')} />
          )}
        </HStack>
      )}
      <VStack gap={2} hAlign="stretch">
        <Text type="body" weight="semibold">
          Pauses
        </Text>
        <ClockPauseList pauses={referral.pauses ?? []} />
      </VStack>
      {dialog === 'pause' && (
        <PauseClockDialog
          referral={referral}
          patient={patient}
          problem={problem}
          isProcessing={actions.pause.isPending}
          onConfirm={(input) => {
            setProblem(NO_PROBLEM);
            actions.pause.mutate(
              {...input, expectedVersion: referral.version ?? 0},
              {
                onSuccess: () => {
                  announce(`Clock paused until its review on ${formatShortDate(input.reviewOn)}`);
                  setDialog(null);
                },
                onError: (error) => setProblem(saveProblem(error, 'save', 'referral')),
              },
            );
          }}
          onCancel={close}
        />
      )}
      <ConfirmDialog
        isOpen={dialog === 'resume'}
        title="Resume Clock?"
        message="The wait counts again from today, and the pathway's target date goes back to the clock's."
        icon={AppIcon.ResumeClock}
        confirmText="Resume Clock"
        isProcessing={actions.resume.isPending}
        onConfirm={resume}
        onCancel={close}>
        <VStack gap={3} hAlign="stretch">
          {problem.message && <AlertMessage label="Clock not resumed" message={problem.message} />}
          {patient && <PatientBanner patient={patient} isCompact />}
          {clock?.pauseReviewOn && (
            <Text type="body">{`Paused since ${formatDate(clock.pausedSinceOn ?? '')}, for review on ${formatDate(clock.pauseReviewOn)}.`}</Text>
          )}
        </VStack>
      </ConfirmDialog>
    </VStack>
  );
}

/** What the pause dialog sends (the version is added by the card). */
export interface PauseChoice {
  reason: string;
  detail: string;
  reviewOn: string;
}

/** Whether a review date can be chosen: tomorrow to 90 days ahead, as UK dates. */
export function reviewDateRange(now: Date = new Date()): {min: string; max: string} {
  const today = ukToday(now);
  return {min: addDays(today, 1), max: addDays(today, MAX_PAUSE_REVIEW_DAYS)};
}

interface PauseClockDialogProps {
  referral: PathwayReferral;
  patient?: BannerPatient | null;
  problem: SaveProblem;
  isProcessing: boolean;
  onConfirm: (choice: PauseChoice) => void;
  onCancel: () => void;
}

/**
 * Pausing the clock: why (a fixed choice, nothing chosen to start with), a detail (500 characters, required for
 * Other), and the review date (required, tomorrow to 90 days ahead, UK dates). The API's 400 shows on the fields;
 * anything else in its words above them. Mounted only while open, so it starts empty each time.
 */
export function PauseClockDialog({referral, patient, problem, isProcessing, onConfirm, onCancel}: PauseClockDialogProps) {
  const [reason, setReason] = useState('');
  const [detail, setDetail] = useState('');
  const [reviewOn, setReviewOn] = useState('');
  const {min, max} = reviewDateRange();
  const isTooLong = detail.length > MAX_PAUSE_DETAIL;
  const isDateValid = reviewOn !== '' && reviewOn >= min && reviewOn <= max;
  const canConfirm = reason !== '' && isDateValid && !isTooLong && (!pauseNeedsDetail(reason) || detail.trim() !== '');
  const errors = problem.validation;

  return (
    <ConfirmDialog
      isOpen
      title="Pause Clock"
      message={PAUSE_EXPLANATION}
      icon={AppIcon.PauseClock}
      confirmText="Pause Clock"
      confirmDisabled={!canConfirm}
      isProcessing={isProcessing}
      onConfirm={() => onConfirm({reason, detail, reviewOn})}
      onCancel={onCancel}>
      <VStack gap={4} hAlign="stretch">
        {problem.message && <AlertMessage label="Clock not paused" message={problem.message} />}
        {patient && <PatientBanner patient={patient} isCompact />}
        {referral.clock && (
          <Text type="body" color="secondary">
            {`Referral to ${referral.teamName ?? ''}, received ${formatDate(referral.receivedOn ?? '')}: day ${referral.clock.daysWaited ?? 0} of ${referral.clock.targetDays ?? 0}.`}
          </Text>
        )}
        <RadioList label="Reason" value={reason} onChange={setReason} isRequired isDisabled={isProcessing} status={fieldStatus(errors, 'reason')}>
          {CLOCK_PAUSE_REASONS.map((r) => (
            <RadioListItem key={r.value} value={r.value} label={pauseReasonWords(r.value)} />
          ))}
        </RadioList>
        <TextArea
          label={pauseNeedsDetail(reason) ? 'Detail' : 'Detail (optional)'}
          description="Say more if it helps."
          value={detail}
          onChange={setDetail}
          rows={2}
          isRequired={pauseNeedsDetail(reason)}
          isDisabled={isProcessing}
          status={
            fieldStatus(errors, 'detail') ?? (isTooLong ? {type: 'error', message: `At most ${MAX_PAUSE_DETAIL} characters.`} : undefined)
          }
        />
        <DateInput
          label="Review date"
          description={`When someone must look at it again: tomorrow to ${MAX_PAUSE_REVIEW_DAYS} days ahead. It becomes the pathway's target date.`}
          value={(reviewOn || undefined) as ISODateString | undefined}
          onChange={(value) => setReviewOn(value ?? '')}
          min={min as ISODateString}
          max={max as ISODateString}
          weekStartsOn={WEEK_STARTS_ON}
          isRequired
          isDisabled={isProcessing}
          status={fieldStatus(errors, 'reviewOn')}
        />
      </VStack>
    </ConfirmDialog>
  );
}
