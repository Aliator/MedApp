import {Button} from '@astryxdesign/core/Button';
import {Grid} from '@astryxdesign/core/Grid';
import {useListFocus} from '@astryxdesign/core/hooks';
import {Icon} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {Selector} from '@astryxdesign/core/Selector';
import {Skeleton} from '@astryxdesign/core/Skeleton';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextArea} from '@astryxdesign/core/TextArea';
import {ToggleButton} from '@astryxdesign/core/ToggleButton';
import {Typeahead, type SearchableItem, type SearchSource} from '@astryxdesign/core/Typeahead';
import {useQuery, useQueryClient} from '@tanstack/react-query';
import {useMemo, useRef, useState, type ReactNode, type RefObject} from 'react';
import {
  APPOINTMENT_LENGTHS,
  DEFAULT_APPOINTMENT_LENGTH,
  useAppointmentActions,
  useCreateAppointment,
  type Appointment,
} from '../../api/appointments';
import {calendarKeys, calendarQuery, staffQuery} from '../../api/calendar';
import {ApiError, ApiValidationError, fieldStatus, problemMessage, type ValidationErrors} from '../../api/errors';
import {procedureQuery} from '../../api/procedures';
import {patientSearchQuery, procedureSearchQuery} from '../../api/search';
import {useAnnounce} from '../../app/announcements';
import {useCurrentUser} from '../../auth/auth';
import {
  addDays,
  addMinutes,
  formatCalendarDay,
  formatShortDay,
  formatTimeRange,
  freeStartsByDay,
  isMorning,
  minutesBetween,
  sameMoment,
  ukToday,
  weekStart,
} from '../../utils/calendarSlots';
import {formatShortDate, formatTime, ukDay} from '../../utils/dates';
import {useSaveShortcut} from '../forms/SaveShortcut';
import {DialogFrame} from '../modals/DialogFrame';
import {useIsNarrow} from '../pages/Page';
import {ValidationAlert, AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';

export interface BookAppointmentDialogProps {
  isOpen: boolean;
  onClose: () => void;
  /** Called with the saved appointment (booked, or added to Needs booking). */
  onSaved?: (appointment: Appointment) => void;
  /** What's known already; the dialog asks for whatever isn't. */
  patient?: {id: string; name: string};
  procedure?: {id: string; title: string};
  stage?: {id: string; name: string};
  /** A Requested appointment to book, or a Booked one to reschedule (its patient, procedure and stage are fixed). */
  appointment?: Appointment;
  /** A preselected member of staff and time, e.g. from a click on the calendar. */
  staffUserId?: string;
  startUtc?: string;
  endUtc?: string;
  /** The member of staff to suggest when none is preselected (e.g. a procedure's only assignee); otherwise the signed-in user. */
  suggestedStaffUserId?: string;
}

/**
 * Booking an appointment (docs/calendar-plan.md, decision 30): one dialog, not a wizard. On the left, who it's for
 * (the patient, and optionally one of their procedures and its stage), with whom, how long, and notes; on the right,
 * the week's working days, each with how many times are free, and the chosen day's free start times. Whatever is
 * already known (the patient, the procedure, a time picked on the calendar) is filled in, and everything else has a
 * default, so the common path is one click on a time, then Book.
 *
 * Without a time it can add the appointment to Needs booking instead (a Requested appointment). With `appointment`,
 * it books a Requested appointment or reschedules a Booked one. Use it wherever an appointment is booked: the
 * calendar, Needs booking, the procedure and patient pages, and an appointment's own dialog.
 */
export function BookAppointmentDialog(props: BookAppointmentDialogProps) {
  // A fresh form each time it opens: what was chosen last time doesn't carry over.
  const [session, setSession] = useState(0);
  const [wasOpen, setWasOpen] = useState(props.isOpen);
  if (props.isOpen !== wasOpen) {
    setWasOpen(props.isOpen);
    if (props.isOpen) setSession((s) => s + 1);
  }
  return <BookingForm key={session} {...props} />;
}

/** The lengths offered, in words; a preselected length that isn't one of them is offered too. */
function lengthLabel(minutes: number): string {
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  if (hours === 0) return `${rest} minutes`;
  const hourWords = hours === 1 ? '1 hour' : `${hours} hours`;
  return rest === 0 ? hourWords : `${hourWords} ${rest} minutes`;
}

/** The fields a 400 can name, shown on the fields themselves. */
const FIELDS = ['patientId', 'procedureId', 'stageId', 'staffUserId', 'notes'];

const NO_PROCEDURE = 'none';
const NO_STAGE = 'none';

interface Problem {
  validation: ValidationErrors | null;
  message: string | null;
}

const NO_PROBLEM: Problem = {validation: null, message: null};

/** How many weeks ahead the dialog looks for a free day when it opens, before settling on the first. */
const WEEKS_TO_LOOK_AHEAD = 4;

function BookingForm({
  isOpen,
  onClose,
  onSaved,
  patient,
  procedure,
  stage,
  appointment,
  staffUserId,
  startUtc,
  endUtc,
  suggestedStaffUserId,
}: BookAppointmentDialogProps) {
  const me = useCurrentUser();
  const isNarrow = useIsNarrow();
  const announce = useAnnounce();
  const queryClient = useQueryClient();
  const create = useCreateAppointment();
  const {book} = useAppointmentActions();
  const [now] = useState(() => new Date());
  const today = ukToday(now);

  const isRescheduling = appointment?.status === 'Booked';
  const fixedPatient = appointment ? {id: appointment.patientId ?? '', name: appointment.patientName ?? ''} : patient;
  const fixedProcedure = appointment
    ? appointment.procedureId
      ? {id: appointment.procedureId, title: appointment.procedureTitle ?? ''}
      : null
    : procedure;
  const fixedStage = appointment ? (appointment.stageId ? {id: appointment.stageId, name: appointment.stageName ?? ''} : null) : stage;
  const isForFixed = appointment !== undefined || procedure !== undefined;

  const initialLength =
    startUtc && endUtc ? minutesBetween(startUtc, endUtc) : (appointment?.durationMinutes ?? DEFAULT_APPOINTMENT_LENGTH);
  const initialDay = startUtc ? ukDay(startUtc) : undefined;

  const [pickedPatient, setPickedPatient] = useState<PatientItem | null>(null);
  const [procedureId, setProcedureId] = useState(NO_PROCEDURE);
  const [stageId, setStageId] = useState(NO_STAGE);
  const [chosenStaff, setChosenStaff] = useState<string | undefined>(staffUserId ?? appointment?.staffUserId ?? undefined);
  const [length, setLength] = useState(initialLength > 0 ? initialLength : DEFAULT_APPOINTMENT_LENGTH);
  const [week, setWeek] = useState(weekStart(initialDay ?? today));
  const [hasMovedWeek, setHasMovedWeek] = useState(initialDay !== undefined);
  const [chosenDay, setChosenDay] = useState<string | undefined>(initialDay);
  const [chosenStart, setChosenStart] = useState<string | undefined>(startUtc);
  const [notes, setNotes] = useState('');
  const [problem, setProblem] = useState<Problem>(NO_PROBLEM);

  const staff = useQuery({...staffQuery, enabled: isOpen});
  const myId = staff.data?.find((s) => s.username === me.username)?.userId;
  const staffId = chosenStaff ?? suggestedStaffUserId ?? myId;
  const staffName = staff.data?.find((s) => s.userId === staffId)?.username ?? '';

  const patientId = fixedPatient?.id ?? pickedPatient?.id;
  const procedures = useQuery({
    ...procedureSearchQuery({PatientId: patientId, PageSize: 100, Sort: 'dueDate', Ascending: true}),
    enabled: isOpen && !isForFixed && patientId !== undefined,
  });
  const chosenProcedureId = fixedProcedure?.id ?? (procedureId === NO_PROCEDURE ? undefined : procedureId);
  const procedureDetail = useQuery({
    ...procedureQuery(chosenProcedureId ?? ''),
    enabled: isOpen && fixedStage === undefined && chosenProcedureId !== undefined,
  });
  const openStages = (procedureDetail.data?.stages ?? []).filter((s) => s.state !== 'Completed');

  const weekEnd = addDays(week, 4);
  const calendar = useQuery({...calendarQuery({staffUserId: staffId, from: week, to: weekEnd}), enabled: isOpen && staffId !== undefined});
  const free = useMemo(
    () => (calendar.data ? freeStartsByDay(calendar.data, length, now, appointment?.id) : {}),
    [calendar.data, length, now, appointment?.id],
  );
  const weekDays = (calendar.data?.days ?? []).filter((d) => d.isWorkingDay);
  const firstFreeDay = weekDays.find((d) => (free[d.date ?? '']?.length ?? 0) > 0)?.date;

  // Nothing free this week when it opens (a Friday evening, a full week): look a few weeks on for the first free day.
  if (calendar.data && !hasMovedWeek && firstFreeDay === undefined && week < addDays(weekStart(today), 7 * WEEKS_TO_LOOK_AHEAD)) {
    setWeek(addDays(week, 7));
  }

  const day = chosenDay !== undefined && chosenDay >= week && chosenDay <= weekEnd ? chosenDay : firstFreeDay;
  const dayStarts = day ? (free[day] ?? []) : [];
  const start = dayStarts.find((s) => sameMoment(s, chosenStart));
  const end = start ? addMinutes(start, length) : undefined;

  const isSaving = create.isPending || book.isPending;
  const canBook = patientId !== undefined && staffId !== undefined && start !== undefined && !isSaving;
  const canRequest = !appointment && patientId !== undefined && start === undefined && !isSaving;

  const title = isRescheduling ? 'Reschedule Appointment' : fixedStage || (stageId !== NO_STAGE && stageId) ? 'Book Visit' : 'Book Appointment';

  const fail = (error: unknown) => {
    if (error instanceof ApiValidationError) {
      setProblem({validation: error.errors, message: null});
      return;
    }
    setProblem({validation: null, message: problemMessage(error, 'The appointment could not be saved. Please try again.')});
    if (error instanceof ApiError && error.status === 409) {
      // Someone took the time: show what's free now.
      setChosenStart(undefined);
      void queryClient.invalidateQueries({queryKey: calendarKeys.all});
    }
  };

  const succeed = (saved: Appointment, message: string) => {
    announce(message);
    onSaved?.(saved);
    onClose();
  };

  const save = () => {
    if (!canBook) return false;
    setProblem(NO_PROBLEM);
    const time = {staffUserId: staffId, startUtc: start, endUtc: end};
    if (appointment) {
      book.mutate(
        {id: appointment.id ?? '', ...time},
        {onSuccess: (saved) => succeed(saved, isRescheduling ? 'Appointment rescheduled' : 'Appointment booked'), onError: fail},
      );
    } else {
      create.mutate(
        {...forWhom(), ...time, notes: notes.trim() || null},
        {onSuccess: (saved) => succeed(saved, 'Appointment booked'), onError: fail},
      );
    }
  };

  const request = () => {
    setProblem(NO_PROBLEM);
    create.mutate({...forWhom(), notes: notes.trim() || null}, {onSuccess: (saved) => succeed(saved, 'Added to Needs booking'), onError: fail});
  };

  function forWhom() {
    return {
      patientId,
      procedureId: chosenProcedureId ?? null,
      stageId: fixedStage?.id ?? (chosenProcedureId && stageId !== NO_STAGE ? stageId : null),
    };
  }

  const content = useRef<HTMLDivElement>(null);
  useSaveShortcut(content, isOpen ? save : undefined);

  const choosePatient = (item: PatientItem | null) => {
    setPickedPatient(item);
    setProcedureId(NO_PROCEDURE);
    setStageId(NO_STAGE);
  };

  const chooseProcedure = (id: string) => {
    setProcedureId(id);
    setStageId(NO_STAGE);
  };

  const moveWeek = (weeks: number) => {
    setHasMovedWeek(true);
    setWeek(addDays(week, 7 * weeks));
  };

  const lengths = APPOINTMENT_LENGTHS.includes(length as (typeof APPOINTMENT_LENGTHS)[number])
    ? [...APPOINTMENT_LENGTHS]
    : [...APPOINTMENT_LENGTHS, length].sort((a, b) => a - b);

  const fixedItems: ReactNode[] = [];
  if (fixedPatient) fixedItems.push(<MetadataListItem key="patient" label="Patient">
        {fixedPatient.name}
      </MetadataListItem>);
  if (isForFixed) {
    fixedItems.push(
      <MetadataListItem key="for" label="For">
        {fixedProcedure ? (fixedStage ? `${fixedProcedure.title}: ${fixedStage.name}` : fixedProcedure.title) : 'Not for a procedure'}
      </MetadataListItem>,
    );
  }
  if (isRescheduling && appointment.startUtc && appointment.endUtc) {
    fixedItems.push(
      <MetadataListItem key="now" label="Booked now">
        {`${formatCalendarDay(ukDay(appointment.startUtc))}, ${formatTimeRange(appointment.startUtc, appointment.endUtc)} with ${appointment.staffUsername ?? ''}`}
      </MetadataListItem>,
    );
  }

  const details = (
    <VStack gap={4} hAlign="stretch">
      {fixedItems.length > 0 && <MetadataList label={{position: 'top'}}>{fixedItems}</MetadataList>}
      {!fixedPatient && (
        <PatientPicker value={pickedPatient} onChange={choosePatient} status={fieldStatus(problem.validation, 'patientId')} />
      )}
      {!isForFixed && patientId !== undefined && (
        <Selector
          label="For"
          isOptional
          value={procedureId}
          onChange={chooseProcedure}
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
      {fixedStage === undefined && chosenProcedureId !== undefined && (
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
      <Selector
        label="Length"
        value={String(length)}
        onChange={(value) => setLength(Number(value))}
        options={lengths.map((minutes) => ({value: String(minutes), label: lengthLabel(minutes)}))}
      />
      {!appointment && (
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
      )}
    </VStack>
  );

  const times = (
    <VStack gap={4} hAlign="stretch">
      <HStack gap={2} vAlign="center">
        <StackItem size="fill">
          <Text type="label" weight="semibold">
            Week of {formatCalendarDay(week)}
          </Text>
        </StackItem>
        <IconButton
          label="Previous week"
          size="sm"
          variant="ghost"
          icon={<Icon icon={AppIcon.ChevronLeft} />}
          isDisabled={week <= weekStart(today)}
          onClick={() => moveWeek(-1)}
        />
        <IconButton label="Next week" size="sm" variant="ghost" icon={<Icon icon={AppIcon.ChevronRight} />} onClick={() => moveWeek(1)} />
      </HStack>
      {calendar.isError ? (
        <AlertMessage message="The calendar could not be loaded. Please try again." />
      ) : !calendar.data ? (
        <VStack gap={3}>
          <Skeleton height="var(--spacing-12)" />
          <Skeleton height="var(--spacing-20)" />
        </VStack>
      ) : (
        <>
          <RovingGroup label={`Days in the week of ${formatCalendarDay(week)}`}>
            <Grid columns={5} gap={1.5}>
              {weekDays.map((d) => {
                const count = free[d.date ?? '']?.length ?? 0;
                const words = d.isPast ? 'Past' : count === 0 ? 'Full' : `${count} free`;
                return (
                  <ToggleButton
                    key={d.date}
                    label={`${formatCalendarDay(d.date ?? '')}, ${count === 0 ? (d.isPast ? 'past' : 'nothing free') : `${count} free ${count === 1 ? 'time' : 'times'}`}`}
                    isPressed={d.date === day}
                    isDisabled={count === 0}
                    // Two lines (the day, then how many times are free): a button's own height fits one.
                    style={{inlineSize: '100%', blockSize: 'auto', paddingBlock: 'var(--spacing-1-5)'}}
                    onPressedChange={() => {
                      setChosenDay(d.date);
                      setChosenStart(undefined);
                    }}>
                    <VStack gap={0} hAlign="center">
                      <Text type="body" weight="semibold" maxLines={1}>
                        {formatShortDay(d.date ?? '')}
                      </Text>
                      <Text type="supporting" color="secondary" maxLines={1}>
                        {words}
                      </Text>
                    </VStack>
                  </ToggleButton>
                );
              })}
            </Grid>
          </RovingGroup>
          {day === undefined ? (
            <Text type="body" color="secondary">
              Nothing is free this week{staffName ? ` for ${staffName}` : ''}. Try the next week, someone else, or a shorter length.
            </Text>
          ) : (
            <VStack gap={3} hAlign="stretch">
              <TimeChoices
                label={`Morning times on ${formatCalendarDay(day)}`}
                heading="Morning"
                starts={dayStarts.filter(isMorning)}
                chosen={start}
                onChoose={setChosenStart}
              />
              <TimeChoices
                label={`Afternoon times on ${formatCalendarDay(day)}`}
                heading="Afternoon"
                starts={dayStarts.filter((s) => !isMorning(s))}
                chosen={start}
                onChoose={setChosenStart}
              />
            </VStack>
          )}
        </>
      )}
    </VStack>
  );

  const summary =
    start && end && day ? (
      <Text type="body" weight="semibold" aria-live="polite">
        {`${formatCalendarDay(day)}, ${formatTimeRange(start, end)} with ${staffName}`}
      </Text>
    ) : (
      <Text type="body" color="secondary" aria-live="polite">
        {appointment ? 'Choose a time.' : 'Choose a time, or add it to Needs booking to book later.'}
      </Text>
    );

  return (
    <DialogFrame
      isOpen={isOpen}
      title={title}
      icon={<Icon icon={AppIcon.Book} size="lg" color="accent" />}
      onClose={onClose}
      width={isNarrow ? 520 : 920}
      purpose={isSaving ? 'required' : 'form'}
      footerStart={isOpen ? summary : undefined}
      footer={
        <>
          <Button label="Cancel" variant="secondary" isDisabled={isSaving} onClick={onClose} />
          {!appointment && start === undefined && (
            <Button label="Add to Needs booking" variant="secondary" isDisabled={!canRequest} isLoading={create.isPending} onClick={request} />
          )}
          <Button
            label={isRescheduling ? 'Reschedule' : 'Book'}
            variant="primary"
            isDisabled={!canBook}
            isLoading={isSaving && start !== undefined}
            onClick={save}
          />
        </>
      }>
      {isOpen && (
        <VStack ref={content} gap={4} hAlign="stretch">
          {problem.message && <AlertMessage message={problem.message} />}
          {problem.validation && <ValidationAlert errors={problem.validation} fields={FIELDS} />}
          {isNarrow ? (
            <VStack gap={6} hAlign="stretch">
              {details}
              {times}
            </VStack>
          ) : (
            <HStack gap={6} vAlign="start">
              <StackItem size="static">
                <VStack width={320}>{details}</VStack>
              </StackItem>
              <StackItem size="fill">{times}</StackItem>
            </HStack>
          )}
        </VStack>
      )}
    </DialogFrame>
  );
}

function visitWords(requirement: string | null | undefined): string | null {
  return requirement === 'Required' ? 'Visit required' : requirement === 'Suggested' ? 'Visit suggested' : null;
}

/** A group of toggle buttons that is one tab stop, with the arrow keys (and Home and End) moving between them. */
function RovingGroup({label, children}: {label: string; children: ReactNode}) {
  const {listRef, handleKeyDown, handleFocus} = useListFocus({itemSelector: 'button', orientation: 'both', hasRovingTabIndex: true});
  return (
    <VStack ref={listRef as RefObject<HTMLDivElement>} role="group" aria-label={label} hAlign="stretch" onKeyDown={handleKeyDown} onFocus={handleFocus}>
      {children}
    </VStack>
  );
}

/** A morning's or an afternoon's free start times, as toggle buttons that wrap; nothing when there are none. */
function TimeChoices({
  label,
  heading,
  starts,
  chosen,
  onChoose,
}: {
  label: string;
  heading: string;
  starts: string[];
  chosen: string | undefined;
  onChoose: (start: string) => void;
}) {
  if (starts.length === 0) return null;
  return (
    <VStack gap={2} hAlign="stretch">
      <Text type="supporting" color="secondary" weight="semibold">
        {heading}
      </Text>
      <RovingGroup label={label}>
        <HStack gap={1.5} wrap="wrap">
          {starts.map((s) => (
            <ToggleButton key={s} size="sm" label={formatTime(s)} isPressed={sameMoment(s, chosen)} onPressedChange={() => onChoose(s)} />
          ))}
        </HStack>
      </RovingGroup>
    </VStack>
  );
}

type PatientItem = SearchableItem<{dateOfBirth?: string}>;

/** Finds a patient by name as it's typed: one word matches first or last names, two or more a first and a last name. */
function PatientPicker({
  value,
  onChange,
  status,
}: {
  value: PatientItem | null;
  onChange: (item: PatientItem | null) => void;
  status: ReturnType<typeof fieldStatus>;
}) {
  const queryClient = useQueryClient();
  const source = useMemo<SearchSource<PatientItem>>(
    () => ({
      bootstrap: () => [],
      async search(query) {
        const words = query.trim().split(/\s+/).filter(Boolean);
        const searches = words.length > 1 ? [{FirstName: words[0], LastName: words.slice(1).join(' ')}] : [{FirstName: words[0]}, {LastName: words[0]}];
        const pages = await Promise.all(searches.map((search) => queryClient.fetchQuery(patientSearchQuery({...search, PageSize: 10}))));
        const seen = new Set<string>();
        return pages
          .flatMap((page) => page.items ?? [])
          .filter((p) => !seen.has(p.id ?? '') && seen.add(p.id ?? ''))
          .map((p) => ({id: p.id ?? '', label: `${p.firstName} ${p.lastName}`, auxiliaryData: {dateOfBirth: p.dateOfBirth}}));
      },
    }),
    [queryClient],
  );
  return (
    <Typeahead
      label="Patient"
      isRequired
      placeholder="Type a name"
      searchSource={source}
      value={value}
      onChange={onChange}
      status={status}
      emptySearchText="No patients match"
      renderItem={(item) => (
        <HStack gap={2} vAlign="center">
          <Text type="body">{item.label}</Text>
          {item.auxiliaryData?.dateOfBirth && (
            <Text type="supporting" color="secondary">
              Born {formatShortDate(item.auxiliaryData.dateOfBirth)}
            </Text>
          )}
        </HStack>
      )}
    />
  );
}

