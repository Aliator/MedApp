import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {RadioList, RadioListItem} from '@astryxdesign/core/RadioList';
import {Selector} from '@astryxdesign/core/Selector';
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
import {calendarKeys, calendarQuery, staffQuery, type CalendarDay} from '../../api/calendar';
import {ApiError, ApiValidationError, fieldStatus, problemMessage, type ValidationErrors} from '../../api/errors';
import {whyCantChange} from '../../api/patients';
import {procedureQuery} from '../../api/procedures';
import {procedureSearchQuery} from '../../api/search';
import {useAnnounce} from '../../app/announcements';
import {useCurrentUser} from '../../auth/auth';
import {addDays, addMinutes, busyTimes, formatCalendarDay, formatTimeRange, intervalOf, overlaps, SLOT_MINUTES, ukToday, type Interval} from '../../utils/calendarSlots';
import {formatShortDate, formatTime} from '../../utils/dates';
import {lengthLabel} from '../../utils/meetingTimes';
import {useSaveShortcut} from '../forms/SaveShortcut';
import {DialogFrame} from '../modals/DialogFrame';
import {useIsNarrow} from '../pages/Page';
import {AlertMessage, ValidationAlert} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import {AppointmentPatientBanner, useAppointmentPatient, type AppointmentPatientDetails} from './AppointmentPatient';
import {PatientPicker, visitWords, type PatientItem} from './BookAppointmentDialog';

export interface RecordPastAppointmentDialogProps {
  isOpen: boolean;
  onClose: () => void;
  /** Called with the recorded appointment. */
  onSaved?: (appointment: Appointment) => void;
  /**
   * What's known already, and can't be changed (a stage's Record Past Visit): the patient, with who they are for their
   * banner, a procedure and a stage. The rest is asked for.
   */
  patient?: AppointmentPatientDetails;
  procedure?: {id: string; title: string};
  stage?: {id: string; name: string};
  /** The member of staff to start with (the calendar's owner); otherwise the signed-in user. */
  staffUserId?: string;
  /** The steps start times come in: 15 minutes (the default), or 5 from a calendar showing 5-minute rows. */
  stepMinutes?: number;
}

/**
 * Recording an appointment that has already happened (docs/calendar-plan.md): a walk-in, or one booked outside MedApp,
 * recorded after the event with its outcome. Like booking (`BookAppointmentDialog`), whatever is known is filled in:
 * the patient (or a picker), what it was for, with whom and how long; then the day (a working day in the last 30,
 * today included: never a weekend or a closed day), the start (inside the working day, only times that had started by
 * now, and none over another of that person's appointments), the outcome (Attended or Did not attend: nothing chosen
 * until someone chooses) and notes. It says it's recorded with the signed-in user's name.
 *
 * Once the patient is known, their banner heads it (name, date of birth and NHS number), so staff can check who they're
 * recording (hazard H-34, and the walk-in hazard: the wrong patient). A restricted or anonymised record can't be
 * changed, so it says why and offers no Record. The server has the final word: a 400 shows on the fields; a 409 (it
 * overlaps another appointment, the stage's visit is booked already) keeps the dialog open with its message and
 * refreshes the calendars and appointment lists. For a stage whose visit is in Requires Appointment, the server records that
 * visit rather than a new one.
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
const FIELDS = ['patientId', 'procedureId', 'stageId', 'staffUserId', 'startUtc', 'endUtc', 'outcome', 'notes'];

const NO_PROCEDURE = 'none';
const NO_STAGE = 'none';
const MINUTE = 60_000;

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
 * The start times (UTC, ISO) on a past working day for an appointment `lengthMinutes` long: every `stepMinutes` from the
 * start of the working day, ending by its end, that had started by `now`, and overlapping nothing in `taken` (the
 * person's other appointments). None on a day that isn't a working day.
 */
export function pastStarts(day: CalendarDay, taken: Interval[], lengthMinutes: number, now: Date, stepMinutes: number = SLOT_MINUTES): string[] {
  if (!day.isWorkingDay || !day.workStartUtc || !day.workEndUtc || lengthMinutes <= 0) return [];
  const {start: dayStart, end: dayEnd} = intervalOf(day.workStartUtc, day.workEndUtc);
  const starts: string[] = [];
  for (let start = dayStart; start + lengthMinutes * MINUTE <= dayEnd && start <= now.getTime(); start += stepMinutes * MINUTE) {
    if (!taken.some((t) => overlaps({start, end: start + lengthMinutes * MINUTE}, t))) starts.push(new Date(start).toISOString());
  }
  return starts;
}

function RecordPastForm({isOpen, onClose, onSaved, patient, procedure, stage, staffUserId, stepMinutes = SLOT_MINUTES}: RecordPastAppointmentDialogProps) {
  const me = useCurrentUser();
  const isNarrow = useIsNarrow();
  const announce = useAnnounce();
  const queryClient = useQueryClient();
  const record = useRecordPastAppointment();
  const [now] = useState(() => new Date());
  const today = ukToday(now);
  const from = addDays(today, -PAST_APPOINTMENT_DAYS);

  const [pickedPatient, setPickedPatient] = useState<PatientItem | null>(null);
  const [procedureId, setProcedureId] = useState(NO_PROCEDURE);
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

  const isForFixed = procedure !== undefined;
  const procedures = useQuery({
    ...procedureSearchQuery({PatientId: patientId, PageSize: 100, Sort: 'dueDate', Ascending: true}),
    enabled: isOpen && !isForFixed && patientId !== undefined,
  });
  const chosenProcedureId = procedure?.id ?? (procedureId === NO_PROCEDURE ? undefined : procedureId);
  const procedureDetail = useQuery({
    ...procedureQuery(chosenProcedureId ?? ''),
    enabled: isOpen && stage === undefined && chosenProcedureId !== undefined,
  });
  const openStages = (procedureDetail.data?.stages ?? []).filter((s) => s.state !== 'Completed');
  const chosenStageId = stage?.id ?? (chosenProcedureId && stageId !== NO_STAGE ? stageId : undefined);

  // The person's last 30 days and today: which were working days (not weekends or closed days), and their appointments,
  // which a past one can't overlap.
  const calendar = useQuery({...calendarQuery({staffUserId: staffId, from, to: today}), enabled: isOpen && staffId !== undefined});
  const startsByDay = useMemo(() => {
    const taken = busyTimes({appointments: calendar.data?.appointments ?? []});
    return new Map((calendar.data?.days ?? []).map((d) => [d.date ?? '', pastStarts(d, taken, length, now, stepMinutes)]));
  }, [calendar.data, length, now, stepMinutes]);
  // Newest first; a day with no time that fits (today before 08:00, a full day) isn't offered.
  const days = (calendar.data?.days ?? [])
    .filter((d) => d.isWorkingDay && (startsByDay.get(d.date ?? '')?.length ?? 0) > 0)
    .map((d) => d.date ?? '')
    .reverse();
  const day = chosenDay !== undefined && days.includes(chosenDay) ? chosenDay : days[0];
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
        procedureId: chosenProcedureId ?? null,
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
    setProcedureId(NO_PROCEDURE);
    setStageId(NO_STAGE);
  };

  const lengths = [...APPOINTMENT_LENGTHS];
  const startStatus = fieldStatus(problem.validation, 'startUtc') ?? fieldStatus(problem.validation, 'endUtc');

  const details = (
    <VStack gap={4} hAlign="stretch">
      {procedure && (
        <MetadataList label={{position: 'top'}}>
          <MetadataListItem label="For">{stage ? `${procedure.title}: ${stage.name}` : procedure.title}</MetadataListItem>
        </MetadataList>
      )}
      {patient === undefined && (
        <PatientPicker value={pickedPatient} onChange={choosePatient} status={fieldStatus(problem.validation, 'patientId')} />
      )}
      {!isForFixed && patientId !== undefined && (
        <Selector
          label="For"
          isOptional
          value={procedureId}
          onChange={(id) => {
            setProcedureId(id);
            setStageId(NO_STAGE);
          }}
          isLoading={procedures.isLoading}
          status={fieldStatus(problem.validation, 'procedureId')}
          options={[
            {value: NO_PROCEDURE, label: 'Not for a procedure'},
            ...(procedures.data?.items ?? []).map((p) => ({
              value: p.id ?? '',
              label: p.title ?? '',
              description: `Due ${formatShortDate(p.dueDate ?? '')}${p.currentStageName ? ` · ${p.currentStageName}` : ''}`,
            })),
          ]}
        />
      )}
      {stage === undefined && chosenProcedureId !== undefined && (
        <Selector
          label="Stage"
          isOptional
          value={stageId}
          onChange={setStageId}
          isLoading={procedureDetail.isLoading}
          status={fieldStatus(problem.validation, 'stageId')}
          options={[
            {value: NO_STAGE, label: 'No particular stage'},
            ...openStages.map((s) => ({
              value: s.id ?? '',
              label: s.name ?? '',
              description: [s.isCurrent ? 'Current stage' : null, visitWords(s.visitRequirement)].filter(Boolean).join(' · ') || undefined,
            })),
          ]}
        />
      )}
      {chosenStageId !== undefined && (
        <Text type="supporting" color="secondary">
          If the stage&apos;s visit is waiting in Requires Appointment, this records that visit.
        </Text>
      )}
      <Selector
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
        description="About the visit, as procedure notes are. Staff see them on the appointment."
        value={notes}
        onChange={setNotes}
        rows={3}
        maxLength={2000}
        status={fieldStatus(problem.validation, 'notes')}
      />
    </VStack>
  );

  const when = (
    <VStack gap={4} hAlign="stretch">
      {calendar.isError ? (
        <AlertMessage message="The calendar could not be loaded. Please try again." />
      ) : (
        <>
          <Selector
            label="Day"
            description={`A working day in the last ${PAST_APPOINTMENT_DAYS} days, or today.`}
            value={day ?? ''}
            placeholder="Choose a day"
            onChange={(next) => {
              setChosenDay(next);
              setChosenStart(undefined);
            }}
            isLoading={calendar.isLoading}
            isDisabled={calendar.data !== undefined && days.length === 0}
            disabledMessage={`Nothing fits on a working day in the last ${PAST_APPOINTMENT_DAYS} days.`}
            options={days.map((d) => ({value: d, label: d === today ? `Today, ${formatCalendarDay(d)}` : formatCalendarDay(d)}))}
          />
          <HStack gap={3} vAlign="start">
            <StackItem size="fill">
              <Selector
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
              <Selector
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
            Times that had started by now, and don&apos;t overlap {staffName ? `${staffName}'s` : 'their'} other appointments.
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
        {`${formatCalendarDay(day)}, ${formatTimeRange(start, end)} with ${staffName}${outcomeWords ? `, ${outcomeWords}` : ''}`}
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
          {problem.message && <AlertMessage message={problem.message} />}
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
