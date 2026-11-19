import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {RadioList, RadioListItem} from '@astryxdesign/core/RadioList';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextArea} from '@astryxdesign/core/TextArea';
import {useQuery, useQueryClient} from '@tanstack/react-query';
import {useMemo, useRef, useState} from 'react';
import {
  APPOINTMENT_LENGTHS,
  appointmentKeys,
  DEFAULT_APPOINTMENT_LENGTH,
  PAST_APPOINTMENT_DAYS,
  useRecordPastAppointment,
  type Appointment,
} from '../../api/appointments';
import {calendarDaysQuery, calendarKeys, staffQuery, type CalendarDay} from '../../api/calendar';
import {ApiError, ApiValidationError, fieldStatus, problemMessage, type ValidationErrors} from '../../api/errors';
import {whyCantChange} from '../../api/patients';
import {pathwayQuery} from '../../api/pathways';
import {pathwaySearchQuery} from '../../api/search';
import {useAnnounce} from '../../app/announcements';
import {useCurrentUser} from '../../auth/auth';
import {addDays, addMinutes, formatCalendarDay, formatTimeRange, intervalOf, SLOT_MINUTES, ukToday, worksThatDay} from '../../utils/calendarSlots';
import {formatShortDate, formatTime} from '../../utils/dates';
import {lengthLabel} from '../../utils/meetingTimes';
import {useSaveShortcut} from '../forms/SaveShortcut';
import {DialogFrame} from '../modals/DialogFrame';
import {useIsNarrow} from '../pages/Page';
import {AlertMessage, ValidationAlert} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import {AppointmentPatientBanner, useAppointmentPatient, type AppointmentPatientDetails} from './AppointmentPatient';
import {PatientPicker, visitWords, type PatientItem} from './BookAppointmentDialog';
import {AnchoredSelector} from '../forms/AnchoredSelector';

export interface RecordPastAppointmentDialogProps {
  isOpen: boolean;
  onClose: () => void;
  /** Called with the recorded appointment. */
  onSaved?: (appointment: Appointment) => void;
  /**
   * What's known already, and can't be changed (a stage's Record Past Visit): the patient, with who they are for their
   * banner, a pathway and a stage. The rest is asked for.
   */
  patient?: AppointmentPatientDetails;
  pathway?: {id: string; title: string};
  stage?: {id: string; name: string};
  /**
   * The notes of the stage's visit waiting in Requires Appointment, when it has one (a stage's Record Past Visit): the
   * server records that visit and keeps its notes, adding any new ones after them, so they're shown read-only.
   */
  waitingVisitNotes?: string | null;
  /** The member of staff to start with (the calendar's owner); otherwise the signed-in user. */
  staffUserId?: string;
  /** The steps start times come in: 15 minutes (the default), or 5 from a calendar showing 5-minute rows. */
  stepMinutes?: number;
}

/**
 * Recording an appointment that has already happened (docs/calendar-plan.md, decisions 74 and 76 to 78): a walk-in, or one
 * booked outside MedApp, recorded after the event with its outcome. Like booking (`BookAppointmentDialog`), whatever is
 * known is filled in: the patient (or a picker), what it was for (a stage up to the pathway's current one: a later one
 * hasn't been reached), with whom and how long; then the day (a weekday in the last 30, today included, closed days too,
 * labelled, as what happened, happened; never a weekend), the start (inside the working day, any time that had started by
 * now), the outcome (Attended or Did not attend: nothing chosen until someone chooses) and notes. It says it's recorded
 * with the signed-in user's name.
 *
 * It reads the person's days without their appointments (`calendarDaysQuery`), so it names no patients and records no
 * reads of them. So it doesn't know which times are taken: the server's 409 says when one overlaps another of their
 * appointments.
 *
 * Once the patient is known, their banner heads it (name, date of birth and NHS number), so staff can check who they're
 * recording (hazard H-34, and the walk-in hazard H-39: the wrong patient). A restricted or anonymised record can't be
 * changed, so it says why and offers no Record. The server has the final word: a 400 shows on the fields; a 409 (it
 * overlaps another appointment, the stage's visit is booked already, the visit was before the pathway was created, the
 * stage hasn't been reached) keeps the dialog open with its message and refreshes the calendars and appointment lists.
 * For a stage whose visit is in Requires Appointment, the server records that visit rather than a new one, keeping its
 * notes (shown read-only, `waitingVisitNotes`) and adding the new ones after them.
 *
 * Use it from the Calendar page's New menu and a stage's visit (Record Past Visit).
 */
export function RecordPastAppointmentDialog(props: RecordPastAppointmentDialogProps) {
  // A fresh form each time it opens: what was chosen last time doesn't carry over.
  const [session, setSession] = useState(0);
  const [wasOpen, setWasOpen] = useState(props.isOpen);
  if (props.isOpen !== wasOpen) {
    setWasOpen(props.isOpen);
    if (props.isOpen) setSession((s) => s + 1);
  }
  return <RecordPastForm key={session} {...props} />;
}

/** The fields a 400 can name, shown on the fields themselves. */
const FIELDS = ['patientId', 'pathwayId', 'stageId', 'staffUserId', 'startUtc', 'endUtc', 'outcome', 'notes'];

const NO_PATHWAY = 'none';
const NO_STAGE = 'none';
const MINUTE = 60_000;
/** The most an appointment's notes can hold (the API's limit): a waiting visit's own and the new ones together. */
const NOTES_MAX = 2000;
/** What joins new notes to a waiting visit's own: a blank line. */
const NOTES_JOIN = 2;

const OUTCOMES = [
  {value: 'Attended', label: 'Attended'},
  {value: 'DidNotAttend', label: 'Did not attend'},
] as const;
type Outcome = (typeof OUTCOMES)[number]['value'];

interface Problem {
  validation: ValidationErrors | null;
  message: string | null;
}

const NO_PROBLEM: Problem = {validation: null, message: null};

/**
 * The start times (UTC, ISO) on a past day the person works for an appointment `lengthMinutes` long: every
 * `stepMinutes` from the start of their working day, ending by its end, that had started by `now`. A closed day they
 * work counts (a visit that happened on it can be recorded); a day they don't work never does. Whether a time overlaps another appointment is the server's to say.
 */
export function pastStarts(day: CalendarDay, lengthMinutes: number, now: Date, stepMinutes: number = SLOT_MINUTES): string[] {
  if (!day.date || !worksThatDay(day) || !day.workStartUtc || !day.workEndUtc || lengthMinutes <= 0) return [];
  const {start: dayStart, end: dayEnd} = intervalOf(day.workStartUtc, day.workEndUtc);
  const starts: string[] = [];
  for (let start = dayStart; start + lengthMinutes * MINUTE <= dayEnd && start <= now.getTime(); start += stepMinutes * MINUTE) {
    starts.push(new Date(start).toISOString());
  }
  return starts;
}

/** A day to choose, in words: "Today, Wednesday 18 November", and a closed one's name ("…, closed: Christmas Day"). */
export function pastDayLabel(day: CalendarDay, today: string): string {
  const date = day.date ?? '';
  const words = date === today ? `Today, ${formatCalendarDay(date)}` : formatCalendarDay(date);
  return day.closedDayName ? `${words}, closed: ${day.closedDayName}` : words;
}

function RecordPastForm({
  isOpen,
  onClose,
  onSaved,
  patient,
  pathway,
  stage,
  waitingVisitNotes,
  staffUserId,
  stepMinutes = SLOT_MINUTES,
}: RecordPastAppointmentDialogProps) {
  const me = useCurrentUser();
  const isNarrow = useIsNarrow();
  const announce = useAnnounce();
  const queryClient = useQueryClient();
  const record = useRecordPastAppointment();
  const [now] = useState(() => new Date());
  const today = ukToday(now);
  const from = addDays(today, -PAST_APPOINTMENT_DAYS);

  const [pickedPatient, setPickedPatient] = useState<PatientItem | null>(null);
  const [pathwayId, setPathwayId] = useState(NO_PATHWAY);
  const [stageId, setStageId] = useState(NO_STAGE);
  const [chosenStaff, setChosenStaff] = useState<string | undefined>(staffUserId);
  const [length, setLength] = useState<number>(DEFAULT_APPOINTMENT_LENGTH);
  const [chosenDay, setChosenDay] = useState<string | undefined>();
  const [chosenStart, setChosenStart] = useState<string | undefined>();
  const [outcome, setOutcome] = useState<Outcome | undefined>();
  const [notes, setNotes] = useState('');
  const [problem, setProblem] = useState<Problem>(NO_PROBLEM);

  const staff = useQuery({...staffQuery, enabled: isOpen});
  const myId = staff.data?.find((s) => s.username === me.username)?.userId;
  const staffId = chosenStaff ?? myId;
  const staffName = staff.data?.find((s) => s.userId === staffId)?.username ?? '';

  const patientId = patient?.id ?? pickedPatient?.id;
  const patientState = useAppointmentPatient(isOpen ? patientId : undefined, patient ?? pickedPatient?.auxiliaryData);
  const refusal = patientState.patient ? whyCantChange(patientState.patient) : null;

  const isForFixed = pathway !== undefined;
  const pathways = useQuery({
    ...pathwaySearchQuery({PatientId: patientId, PageSize: 100, Sort: 'dueDate', Ascending: true}),
    enabled: isOpen && !isForFixed && patientId !== undefined,
  });
  const chosenPathwayId = pathway?.id ?? (pathwayId === NO_PATHWAY ? undefined : pathwayId);
  const pathwayDetail = useQuery({
    ...pathwayQuery(chosenPathwayId ?? ''),
    enabled: isOpen && stage === undefined && chosenPathwayId !== undefined,
  });
  // The current stage and those before it: a later one hasn't been reached (the server refuses it). Every stage when
  // none is current (a pathway that's finished).
  const allStages = pathwayDetail.data?.stages ?? [];
  const current = allStages.findIndex((s) => s.isCurrent);
  const reachedStages = current < 0 ? allStages : allStages.slice(0, current + 1);
  const chosenStageId = stage?.id ?? (chosenPathwayId && stageId !== NO_STAGE ? stageId : undefined);

  // The person's last 30 days and today, without their appointments (so no patient is named or recorded as listed):
  // their working hours, and which were closed. Whether a time is taken is the server's to say (a 409).
  const calendar = useQuery({...calendarDaysQuery({staffUserId: staffId, from, to: today}), enabled: isOpen && staffId !== undefined});
  const startsByDay = useMemo(
    () => new Map((calendar.data?.days ?? []).map((d) => [d.date ?? '', pastStarts(d, length, now, stepMinutes)])),
    [calendar.data, length, now, stepMinutes],
  );
  // Newest first; a day with no time that fits (a weekend, today before 08:00) isn't offered.
  const offered = (calendar.data?.days ?? []).filter((d) => (startsByDay.get(d.date ?? '')?.length ?? 0) > 0).reverse();
  const days = offered.map((d) => d.date ?? '');
  const day = chosenDay !== undefined && days.includes(chosenDay) ? chosenDay : days[0];
  const closedName = offered.find((d) => d.date === day)?.closedDayName;
  const starts = day ? (startsByDay.get(day) ?? []) : [];
  const start = starts.find((s) => s === chosenStart);
  const end = start ? addMinutes(start, length) : undefined;

  const isSaving = record.isPending;
  const canSave = patientId !== undefined && staffId !== undefined && start !== undefined && outcome !== undefined && refusal === null && !isSaving;

  const fail = (error: unknown) => {
    if (error instanceof ApiValidationError) {
      setProblem({validation: error.errors, message: null});
      return;
    }
    setProblem({validation: null, message: problemMessage(error, 'The appointment could not be recorded. Please try again.')});
    if (error instanceof ApiError && error.status === 409) {
      // Something changed since the dialog looked (the server's message says what): show calendars and lists as they are.
      void queryClient.invalidateQueries({queryKey: calendarKeys.all});
      void queryClient.invalidateQueries({queryKey: appointmentKeys.all});
    }
  };

  const save = () => {
    if (!canSave) return false;
    setProblem(NO_PROBLEM);
    record.mutate(
      {
        patientId,
        pathwayId: chosenPathwayId ?? null,
        stageId: chosenStageId ?? null,
        staffUserId: staffId,
        startUtc: start,
        endUtc: end,
        outcome,
        notes: notes.trim() || null,
      },
      {
        onSuccess: (saved) => {
          announce('Past appointment recorded');
          onSaved?.(saved);
          onClose();
        },
        onError: fail,
      },
    );
  };

  const content = useRef<HTMLDivElement>(null);
  useSaveShortcut(content, isOpen ? save : undefined);

  const choosePatient = (item: PatientItem | null) => {
    setPickedPatient(item);
    setPathwayId(NO_PATHWAY);
    setStageId(NO_STAGE);
  };

  const lengths = [...APPOINTMENT_LENGTHS];
  const startStatus = fieldStatus(problem.validation, 'startUtc') ?? fieldStatus(problem.validation, 'endUtc');
  // The waiting visit's notes are kept, with the new ones after them, all within the API's limit.
  const existingNotes = stage !== undefined ? waitingVisitNotes?.trim() || null : null;
  const notesMax = existingNotes ? Math.max(0, NOTES_MAX - existingNotes.length - NOTES_JOIN) : NOTES_MAX;

  const details = (
    <VStack gap={4} hAlign="stretch">
      {pathway && (
        <MetadataList label={{position: 'top'}}>
          <MetadataListItem label="For">{stage ? `${pathway.title}: ${stage.name}` : pathway.title}</MetadataListItem>
        </MetadataList>
      )}
      {patient === undefined && (
        <PatientPicker value={pickedPatient} onChange={choosePatient} status={fieldStatus(problem.validation, 'patientId')} />
      )}
      {!isForFixed && patientId !== undefined && (
        <AnchoredSelector
          label="For"
          isOptional
          value={pathwayId}
          onChange={(id) => {
            setPathwayId(id);
            setStageId(NO_STAGE);
          }}
          isLoading={pathways.isLoading}
          status={fieldStatus(problem.validation, 'pathwayId')}
          options={[
            {value: NO_PATHWAY, label: 'Not for a pathway'},
            ...(pathways.data?.items ?? []).map((p) => ({
              value: p.id ?? '',
              label: p.title ?? '',
              description: `Due ${formatShortDate(p.dueDate ?? '')}${p.currentStageName ? ` · ${p.currentStageName}` : ''}`,
            })),
          ]}
        />
      )}
      {stage === undefined && chosenPathwayId !== undefined && (
        <AnchoredSelector
          label="Stage"
          isOptional
          value={stageId}
          onChange={setStageId}
          isLoading={pathwayDetail.isLoading}
          status={fieldStatus(problem.validation, 'stageId')}
          options={[
            {value: NO_STAGE, label: 'No particular stage'},
            ...reachedStages.map((s) => ({
              value: s.id ?? '',
              label: s.name ?? '',
              description:
                [s.isCurrent ? 'Current stage' : s.state === 'Completed' ? 'Finished' : null, visitWords(s.visitRequirement)]
                  .filter(Boolean)
                  .join(' · ') || undefined,
            })),
          ]}
        />
      )}
      {existingNotes ? (
        <MetadataList label={{position: 'top'}}>
          <MetadataListItem label="Its notes, which are kept">{existingNotes}</MetadataListItem>
        </MetadataList>
      ) : (
        chosenStageId !== undefined && (
          <Text type="supporting" color="secondary">
            If the stage&apos;s visit is waiting in Requires Appointment, this records that visit, keeping its notes and adding
            these after them.
          </Text>
        )
      )}
      <AnchoredSelector
        label="With"
        value={staffId ?? ''}
        onChange={(id) => {
          setChosenStaff(id);
          setChosenStart(undefined);
        }}
        isLoading={staff.isLoading}
        hasSearch={(staff.data?.length ?? 0) > 8}
        status={fieldStatus(problem.validation, 'staffUserId')}
        options={(staff.data ?? []).map((s) => ({value: s.userId ?? '', label: s.userId === myId ? `${s.username} (you)` : (s.username ?? '')}))}
      />
      <TextArea
        label="Notes"
        isOptional
        description={
          existingNotes
            ? 'About the visit, added after its own notes. Staff see them on the appointment.'
            : 'About the visit, as pathway notes are. Staff see them on the appointment.'
        }
        value={notes}
        onChange={setNotes}
        rows={3}
        maxLength={notesMax}
        status={fieldStatus(problem.validation, 'notes')}
      />
    </VStack>
  );

  const when = (
    <VStack gap={4} hAlign="stretch">
      {calendar.isError ? (
        <AlertMessage label="Calendar not loaded" message="The calendar could not be loaded. Please try again." />
      ) : (
        <>
          <AnchoredSelector
            label="Day"
            description={`A weekday in the last ${PAST_APPOINTMENT_DAYS} days, or today; closed days too.`}
            value={day ?? ''}
            placeholder="Choose a day"
            onChange={(next) => {
              setChosenDay(next);
              setChosenStart(undefined);
            }}
            isLoading={calendar.isLoading}
            isDisabled={calendar.data !== undefined && days.length === 0}
            disabledMessage={`Nothing fits on a weekday in the last ${PAST_APPOINTMENT_DAYS} days.`}
            options={offered.map((d) => ({value: d.date ?? '', label: pastDayLabel(d, today)}))}
          />
          <HStack gap={3} vAlign="start">
            <StackItem size="fill">
              <AnchoredSelector
                label="Start"
                value={start ?? ''}
                placeholder="Choose a time"
                onChange={(next) => setChosenStart(next)}
                isDisabled={day === undefined}
                disabledMessage="Choose a day first."
                status={startStatus}
                options={starts.map((s) => ({value: s, label: formatTime(s)}))}
              />
            </StackItem>
            <StackItem size="fill">
              <AnchoredSelector
                label="Length"
                value={String(length)}
                onChange={(value) => {
                  setLength(Number(value));
                  setChosenStart(undefined);
                }}
                options={lengths.map((minutes) => ({value: String(minutes), label: lengthLabel(minutes)}))}
              />
            </StackItem>
          </HStack>
          <Text type="supporting" color="secondary">
            Times that had started by now. If one overlaps another of {staffName ? `${staffName}'s` : 'their'} appointments,
            you&apos;ll be told when you record it.
          </Text>
        </>
      )}
      <RadioList
        label="Outcome"
        isRequired
        value={outcome ?? ''}
        onChange={(value) => setOutcome(OUTCOMES.find((o) => o.value === value)?.value)}
        status={fieldStatus(problem.validation, 'outcome')}>
        {OUTCOMES.map((o) => (
          <RadioListItem key={o.value} value={o.value} label={o.label} />
        ))}
      </RadioList>
    </VStack>
  );

  const outcomeWords = OUTCOMES.find((o) => o.value === outcome)?.label;
  const summary =
    start && end && day ? (
      <Text type="body" weight="semibold" aria-live="polite">
        {`${formatCalendarDay(day)}${closedName ? ` (closed: ${closedName})` : ''}, ${formatTimeRange(start, end)} with ${staffName}${outcomeWords ? `, ${outcomeWords}` : ''}`}
      </Text>
    ) : (
      <Text type="body" color="secondary" aria-live="polite">
        Choose a day, a start and the outcome.
      </Text>
    );

  return (
    <DialogFrame
      isOpen={isOpen}
      title={stage ? 'Record Past Visit' : 'Record Past Appointment'}
      icon={<Icon icon={AppIcon.RecordPast} size="lg" color="accent" />}
      onClose={onClose}
      width={isNarrow ? 520 : 920}
      purpose={isSaving ? 'required' : 'form'}
      footerStart={isOpen ? summary : undefined}
      footer={
        <>
          <Button label="Cancel" variant="secondary" isDisabled={isSaving} onClick={onClose} />
          <Button label="Record" variant="primary" isDisabled={!canSave} isLoading={isSaving} onClick={save} />
        </>
      }>
      {isOpen && (
        <VStack ref={content} gap={4} hAlign="stretch">
          <Text type="body" color="secondary">
            For an appointment that has already happened: a walk-in, or one booked outside MedApp. It&apos;s recorded with your
            name ({me.username}), as recorded after the event.
          </Text>
          <AppointmentPatientBanner state={patientState} />
          {problem.message && <AlertMessage label="Appointment not recorded" message={problem.message} />}
          {problem.validation && <ValidationAlert errors={problem.validation} fields={FIELDS} />}
          {isNarrow ? (
            <VStack gap={6} hAlign="stretch">
              {details}
              {when}
            </VStack>
          ) : (
            <HStack gap={6} vAlign="start">
              <StackItem size="static">
                <VStack width={320}>{details}</VStack>
              </StackItem>
              <StackItem size="fill">{when}</StackItem>
            </HStack>
          )}
        </VStack>
      )}
    </DialogFrame>
  );
}
