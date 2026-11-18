import {Button} from '@astryxdesign/core/Button';
import type {ISODateString} from '@astryxdesign/core/Calendar';
import {CheckboxInput} from '@astryxdesign/core/CheckboxInput';
import {DateInput} from '@astryxdesign/core/DateInput';
import {useListFocus} from '@astryxdesign/core/hooks';
import {Icon} from '@astryxdesign/core/Icon';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {MultiSelector} from '@astryxdesign/core/MultiSelector';
import {Selector} from '@astryxdesign/core/Selector';
import {Skeleton} from '@astryxdesign/core/Skeleton';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextArea} from '@astryxdesign/core/TextArea';
import {TextInput} from '@astryxdesign/core/TextInput';
import {ToggleButton, ToggleButtonGroup} from '@astryxdesign/core/ToggleButton';
import {useQuery, useQueryClient} from '@tanstack/react-query';
import {useRef, useState, type ReactNode, type RefObject} from 'react';
import {
  availabilityQuery,
  calendarKeys,
  staffQuery,
  useMeetingActions,
  useMeetingReply,
  type Meeting,
  type MeetingAttendee,
  type MeetingReply,
} from '../../api/calendar';
import {ApiError, ApiValidationError, fieldStatus, problemMessage, type ValidationErrors} from '../../api/errors';
import {useAnnounce} from '../../app/announcements';
import {useCurrentUser} from '../../auth/auth';
import {formatCalendarDay, formatTimeRange, minutesBetween, sameMoment, ukToday} from '../../utils/calendarSlots';
import {WEEK_STARTS_ON} from '../../utils/dates';
import {
  busyLines,
  clockTime,
  DEFAULT_MEETING_LENGTH,
  firstFit,
  formatSuggestion,
  laterFrom,
  lengthLabel,
  lengthOptions,
  startOptions,
  suggestionRange,
  WORKING_DAY_MINUTES,
  workingDayOffset,
  workingTime,
} from '../../utils/meetingTimes';
import {useSaveShortcut} from '../forms/SaveShortcut';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {DialogFrame} from '../modals/DialogFrame';
import {useIsNarrow} from '../pages/Page';
import {AlertMessage, ValidationAlert} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import {MeetingReplyToken} from './CalendarTokens';

export interface MeetingDialogProps {
  isOpen: boolean;
  onClose: () => void;
  /** An existing meeting: its details for everyone; admins (its `canEdit`) can change or cancel it. */
  meeting?: Meeting | null;
  /** A new meeting (admins): who and when to start with, e.g. from the team view's "Everyone free" column. */
  initial?: {attendeeUserIds?: string[]; startUtc?: string; endUtc?: string};
  /** After a save or a cancel: the saved meeting, or null once cancelled. */
  onSaved?: (meeting: Meeting | null) => void;
}

/**
 * A meeting (team calendar plan, sections 2 and 4): its details for everyone (title, when, organiser, attendees with
 * their replies and a summary of them, notes); for an attendee, Accept and Decline (pressed as they last replied:
 * decision 70); and for admins, Change and Cancel Meeting; or, without a `meeting`, New Meeting (admins).
 *
 * The form asks for a title, who (a multi-select of staff, with Everyone and Clear; the organiser goes too unless
 * they untick "I'm going too"), a length and notes (about staff, never patients). It suggests times in the next two
 * weeks when everyone is free (`availabilityQuery`), refreshed as who and the length change, with Later suggestions
 * to look further on. A day and start can be chosen by hand too: the dialog then says who's busy at that time, but
 * still lets it be saved, since the server's check (a 409, shown in its words) is the final word.
 *
 * Use it wherever a meeting is opened or arranged: a meeting on the calendar, the Calendar page's New menu, and the
 * team view's "Everyone free" column (with `initial`).
 */
export function MeetingDialog(props: MeetingDialogProps) {
  // A fresh dialog each time it opens: what was chosen last time doesn't carry over.
  const [session, setSession] = useState(0);
  const [wasOpen, setWasOpen] = useState(props.isOpen);
  if (props.isOpen !== wasOpen) {
    setWasOpen(props.isOpen);
    if (props.isOpen) setSession((s) => s + 1);
  }
  return <MeetingDialogBody key={session} {...props} />;
}

type Mode = 'view' | 'change' | 'cancel';

function MeetingDialogBody({isOpen, onClose, meeting: given, initial, onSaved}: MeetingDialogProps) {
  const announce = useAnnounce();
  const {cancel} = useMeetingActions();
  const replyTo = useMeetingReply();
  const [mode, setMode] = useState<Mode>('view');
  const [problem, setProblem] = useState<string | null>(null);
  // The meeting as the last reply returned it, until the dialog closes (the calendar behind refreshes too).
  const [replied, setReplied] = useState<Meeting | null>(null);
  const meeting = replied && replied.id === given?.id ? replied : given;

  if (!meeting) return <MeetingForm isOpen={isOpen} onClose={onClose} initial={initial} onSaved={onSaved} />;

  const reply = (answer: ReplyAnswer) => {
    if (answer === meeting.myReply || replyTo.isPending) return;
    setProblem(null);
    replyTo.mutate(
      {id: meeting.id ?? '', reply: answer},
      {
        onSuccess: (saved) => {
          setReplied(saved);
          announce(answer === 'Accepted' ? 'Meeting accepted' : 'Meeting declined');
        },
        onError: (error) => setProblem(problemMessage(error, 'Your reply could not be saved. Please try again.')),
      },
    );
  };

  const cancelMeeting = () => {
    setProblem(null);
    cancel.mutate(meeting.id ?? '', {
      onSuccess: () => {
        announce('Meeting cancelled');
        onSaved?.(null);
        onClose();
      },
      onError: (error) => {
        setProblem(problemMessage(error, 'The meeting could not be cancelled. Please try again.'));
        setMode('view');
      },
    });
  };

  const when = meetingWhen(meeting);
  return (
    <>
      <DialogFrame
        isOpen={isOpen && mode === 'view'}
        title="Meeting"
        icon={<Icon icon={AppIcon.Meeting} size="lg" color="accent" />}
        onClose={onClose}
        width={520}
        purpose="info"
        footer={
          <HStack gap={2} wrap="wrap" hAlign="end">
            {meeting.canEdit && (
              <>
                <Button label="Cancel Meeting" variant="destructive" onClick={() => setMode('cancel')} />
                <Button label="Change" variant="secondary" icon={<Icon icon={AppIcon.Edit} />} onClick={() => setMode('change')} />
              </>
            )}
            <Button label="Close" variant="secondary" onClick={onClose} />
          </HStack>
        }>
        <VStack gap={5} hAlign="stretch">
          {problem && <AlertMessage message={problem} />}
          <VStack gap={1}>
            <Text type="large" weight="semibold">
              {meeting.title ?? ''}
            </Text>
            <Text type="body" color="secondary">
              {when}
            </Text>
          </VStack>
          {meeting.canReply && (
            <VStack gap={2} hAlign="start">
              <ToggleButtonGroup
                type="single"
                label="Your reply"
                value={isAnswer(meeting.myReply) ? meeting.myReply : null}
                onChange={(answer) => {
                  if (isAnswer(answer)) reply(answer);
                }}
                isDisabled={replyTo.isPending}>
                <ToggleButton value="Accepted" label="Accept" icon={<Icon icon={AppIcon.MeetingAccepted} />} />
                <ToggleButton value="Declined" label="Decline" icon={<Icon icon={AppIcon.MeetingDeclined} />} />
              </ToggleButtonGroup>
              {meeting.myReply === 'Declined' && (
                <Text type="supporting" color="secondary">
                  You declined: this time is free on your calendar.
                </Text>
              )}
            </VStack>
          )}
          <MetadataList label={{position: 'start', width: 120}}>
            <MetadataListItem label="Organiser">{meeting.organiserUsername ?? DELETED_USER}</MetadataListItem>
            <MetadataListItem label={`Attendees (${meeting.attendees?.length ?? 0})`}>
              <VStack gap={2} hAlign="stretch">
                {(meeting.attendees?.length ?? 0) > 0 && (
                  <Text type="body" color="secondary">
                    {replySummary(meeting.attendees ?? [])}
                  </Text>
                )}
                {(meeting.attendees ?? []).map((a) => (
                  <HStack key={a.userId} gap={2} vAlign="center" wrap="wrap">
                    <Text type="body">{a.username ?? DELETED_USER}</Text>
                    <MeetingReplyToken reply={a.reply} />
                  </HStack>
                ))}
              </VStack>
            </MetadataListItem>
            {meeting.notes && <MetadataListItem label="Notes">{meeting.notes}</MetadataListItem>}
          </MetadataList>
        </VStack>
      </DialogFrame>
      {mode === 'change' && (
        <MeetingForm isOpen={isOpen} onClose={onClose} onBack={() => setMode('view')} meeting={meeting} onSaved={onSaved} />
      )}
      <ConfirmDialog
        isOpen={isOpen && mode === 'cancel'}
        title="Cancel Meeting"
        message={`Cancel '${meeting.title ?? ''}', ${when}? Its attendees are told it's cancelled.`}
        icon={AppIcon.Cancel}
        isDanger
        confirmText="Cancel Meeting"
        cancelText="Keep It"
        isProcessing={cancel.isPending}
        onConfirm={cancelMeeting}
        onCancel={() => setMode('view')}
      />
    </>
  );
}

const DELETED_USER = 'Deleted user';

/** What an attendee can answer: Accepted or Declined (never back to no reply). */
type ReplyAnswer = Exclude<MeetingReply, 'NoReply'>;

const isAnswer = (value: string | null | undefined): value is ReplyAnswer => value === 'Accepted' || value === 'Declined';

/** How a meeting's attendees replied, in words: "3 accepted, 1 declined, 2 haven't replied" (leaving out the noughts). */
function replySummary(attendees: MeetingAttendee[]): string {
  const count = (reply: string) => attendees.filter((a) => (a.reply ?? 'NoReply') === reply).length;
  const accepted = count('Accepted');
  const declined = count('Declined');
  const waiting = attendees.length - accepted - declined;
  return [
    accepted > 0 ? `${accepted} accepted` : null,
    declined > 0 ? `${declined} declined` : null,
    waiting > 0 ? `${waiting} ${waiting === 1 ? "hasn't" : "haven't"} replied` : null,
  ]
    .filter(Boolean)
    .join(', ');
}

/** When a meeting is, in words: "Tuesday 24 November, 10:00 to 11:00". */
function meetingWhen(meeting: Meeting): string {
  return `${formatCalendarDay(meeting.date ?? '')}, ${formatTimeRange(meeting.startUtc ?? '', meeting.endUtc ?? '')}`;
}

/** The fields a 400 can name, shown on the fields themselves. */
const FIELDS = ['title', 'notes', 'startUtc', 'endUtc', 'attendeeUserIds'];

interface Problem {
  validation: ValidationErrors | null;
  message: string | null;
}

const NO_PROBLEM: Problem = {validation: null, message: null};

/** A day can be chosen when it's a weekday (the date comes in this browser's local time, at midnight). */
const WEEKDAYS_ONLY = [(date: Date) => date.getDay() >= 1 && date.getDay() <= 5];

interface MeetingFormProps {
  isOpen: boolean;
  onClose: () => void;
  /** Changing a meeting: back to its details. */
  onBack?: () => void;
  meeting?: Meeting;
  initial?: MeetingDialogProps['initial'];
  onSaved?: (meeting: Meeting | null) => void;
}

/** New Meeting, or Change Meeting (`meeting`). */
function MeetingForm({isOpen, onClose, onBack, meeting, initial, onSaved}: MeetingFormProps) {
  const me = useCurrentUser();
  const isNarrow = useIsNarrow();
  const announce = useAnnounce();
  const queryClient = useQueryClient();
  const actions = useMeetingActions();
  const [today] = useState(() => ukToday());
  const isNew = !meeting;

  const startsAt = meeting ? meeting.startUtc : initial?.startUtc;
  const endsAt = meeting ? meeting.endUtc : initial?.endUtc;
  const [initialTime] = useState(() => {
    const offset = startsAt ? workingDayOffset(startsAt) : undefined;
    const length = startsAt && endsAt ? minutesBetween(startsAt, endsAt) : 0;
    return {date: offset?.date, start: offset?.minutes, length: length > 0 ? length : DEFAULT_MEETING_LENGTH};
  });

  const [title, setTitle] = useState(meeting?.title ?? '');
  const [notes, setNotes] = useState(meeting?.notes ?? '');
  const [who, setWho] = useState<string[]>(() =>
    meeting ? (meeting.attendees ?? []).map((a) => a.userId ?? '') : [...(initial?.attendeeUserIds ?? [])],
  );
  const [includeMeChoice, setIncludeMe] = useState<boolean>();
  const [length, setLength] = useState(initialTime.length);
  const [date, setDate] = useState<string | undefined>(initialTime.date);
  const [chosenStart, setChosenStart] = useState<number | undefined>(initialTime.start);
  const [from, setFrom] = useState(today);
  const [problem, setProblem] = useState<Problem>(NO_PROBLEM);

  const staff = useQuery({...staffQuery, enabled: isOpen});
  const myId = staff.data?.find((s) => s.username === me.username)?.userId;

  // On a new meeting the organiser (the signed-in user) goes unless they untick "I'm going too", so they aren't in
  // Who. Starting from given people (the team view's), they go when they're among them; otherwise they go.
  const organiserId = isNew ? myId : undefined;
  const includeMe = includeMeChoice ?? (initial?.attendeeUserIds ? myId !== undefined && initial.attendeeUserIds.includes(myId) : true);

  // Everyone with a role, but the organiser; and anyone already going whose account has gone.
  const people = [
    ...(staff.data ?? []).filter((s) => s.userId !== organiserId).map((s) => ({value: s.userId ?? '', label: s.username ?? ''})),
    ...(meeting?.attendees ?? [])
      .filter((a) => staff.data && !staff.data.some((s) => s.userId === a.userId))
      .map((a) => ({value: a.userId ?? '', label: a.username ?? DELETED_USER})),
  ];
  const everyone = people.map((p) => p.value);
  // Only people offered: someone given (by the team view) who no longer has a role is left out.
  const others = who.filter((id) => everyone.includes(id));
  const attendees = organiserId && includeMe ? [...others, organiserId] : others;
  const isReady = isOpen && staff.data !== undefined && attendees.length > 0;
  const isEveryone = everyone.length > 0 && everyone.every((id) => others.includes(id));

  // Suggested times: two weeks from `from`, for everyone going, the meeting being changed left out of their time.
  const range = suggestionRange(from);
  const forWhom = {staffUserIds: attendees, durationMinutes: length, exceptMeetingId: meeting?.id};
  const suggestions = useQuery({...availabilityQuery({...forWhom, ...range}), enabled: isReady});
  const suggested = (suggestions.data?.suggestions ?? []).flatMap((s) => (s.startUtc && s.endUtc ? [{startUtc: s.startUtc, endUtc: s.endUtc}] : []));

  // Who's free on the chosen day: from the suggestions when it's in their range, otherwise asked for that day alone.
  const isDayInRange = date !== undefined && date >= range.from && date <= range.to;
  const dayQuery = useQuery({
    ...availabilityQuery({...forWhom, from: date ?? '', to: date ?? ''}),
    enabled: isReady && date !== undefined && !isDayInRange,
  });
  const dayAvailability = isDayInRange ? suggestions : dayQuery;
  const dayFree = dayAvailability.data?.days?.find((d) => d.date === date)?.free ?? [];

  // A day chosen by hand without a start starts at the first time everyone's free for the whole length.
  const start = chosenStart ?? (date ? firstFit(date, dayFree, length) : undefined);
  const endsLate = start !== undefined && start + length > WORKING_DAY_MINUTES;
  const times =
    date !== undefined && start !== undefined && !endsLate
      ? {date, startUtc: workingTime(date, start), endUtc: workingTime(date, start + length)}
      : undefined;
  const busy = times && dayAvailability.data ? busyLines(dayAvailability.data.busy ?? [], times.startUtc, times.endUtc) : undefined;

  const isSaving = actions.create.isPending || actions.save.isPending;
  const canSave = title.trim() !== '' && attendees.length > 0 && times !== undefined && !isSaving;

  const fail = (error: unknown) => {
    if (error instanceof ApiValidationError) {
      setProblem({validation: error.errors, message: null});
      return;
    }
    setProblem({validation: null, message: problemMessage(error, 'The meeting could not be saved. Please try again.')});
    // Someone's time changed since the dialog looked (the server's message says whose): show what's free now.
    if (error instanceof ApiError && error.status === 409) void queryClient.invalidateQueries({queryKey: calendarKeys.all});
  };

  const succeed = (saved: Meeting) => {
    announce('Meeting saved');
    onSaved?.(saved);
    onClose();
  };

  const save = () => {
    if (!canSave) return false;
    setProblem(NO_PROBLEM);
    const body = {title: title.trim(), notes: notes.trim() || null, startUtc: times.startUtc, endUtc: times.endUtc, attendeeUserIds: attendees};
    // A change says which version of the meeting it was made from: if anyone has changed it since, it's a 409.
    if (meeting) actions.save.mutate({id: meeting.id ?? '', ...body, expectedLastUpdated: meeting.lastUpdated}, {onSuccess: succeed, onError: fail});
    else actions.create.mutate(body, {onSuccess: succeed, onError: fail});
  };

  const content = useRef<HTMLDivElement>(null);
  useSaveShortcut(content, isOpen ? save : undefined);

  const choose = (suggestion: {startUtc: string}) => {
    const offset = workingDayOffset(suggestion.startUtc);
    setDate(offset.date);
    setChosenStart(offset.minutes);
  };

  const validation = problem.validation;
  const details = (
    <VStack gap={4} hAlign="stretch">
      <TextInput
        label="Title"
        isRequired
        description="All staff can see it, and it's in attendees' notifications. Don't put patient details here."
        value={title}
        onChange={setTitle}
        autoComplete="off"
        status={fieldStatus(validation, 'title')}
      />
      <VStack gap={2} hAlign="stretch">
        <MultiSelector
          label="Who"
          description={isNew ? 'Who else is going.' : "Who's going."}
          value={others}
          onChange={setWho}
          options={people}
          placeholder="Choose people"
          isLoading={staff.isLoading}
          hasSearch={people.length > 8}
          formatValue={(items) => (isEveryone ? 'Everyone' : items.length > 3 ? `${items.length} people` : items.map((i) => i.label).join(', '))}
          status={fieldStatus(validation, 'attendeeUserIds')}
        />
        <HStack gap={2}>
          <Button label="Everyone" variant="secondary" size="sm" isDisabled={isEveryone} onClick={() => setWho(everyone)} />
          <Button label="Clear" variant="secondary" size="sm" isDisabled={others.length === 0} onClick={() => setWho([])} />
        </HStack>
      </VStack>
      {isNew && <CheckboxInput label={`I'm going too (${me.username})`} value={includeMe} onChange={setIncludeMe} />}
      <Selector
        label="Length"
        value={String(length)}
        onChange={(value) => setLength(Number(value))}
        options={lengthOptions(length).map((minutes) => ({value: String(minutes), label: lengthLabel(minutes)}))}
      />
      <TextArea
        label="Notes"
        isOptional
        description="About the meeting, for staff. Don't put patient details here."
        value={notes}
        onChange={setNotes}
        rows={3}
        maxLength={500}
        status={fieldStatus(validation, 'notes')}
      />
    </VStack>
  );

  const suggestionList = staff.isError ? (
    <AlertMessage message="The staff could not be loaded. Please try again." />
  ) : !staff.data ? (
    <Skeleton height="var(--spacing-20)" />
  ) : attendees.length === 0 ? (
      <Text type="body" color="secondary">
        Choose who’s going to see when they’re all free.
      </Text>
    ) : suggestions.isError ? (
      <AlertMessage message="The suggested times could not be loaded. Please try again." />
    ) : !suggestions.data ? (
      <Skeleton height="var(--spacing-20)" />
    ) : suggested.length === 0 ? (
      <Text type="body" color="secondary">
        Nothing fits for everyone in these two weeks. Try later suggestions, fewer people or a shorter length.
      </Text>
    ) : (
      <RovingGroup label="Suggested times">
        <HStack gap={1.5} wrap="wrap">
          {suggested.map((s) => (
            <ToggleButton
              key={s.startUtc}
              size="sm"
              label={formatSuggestion(s.startUtc, s.endUtc)}
              isPressed={sameMoment(s.startUtc, times?.startUtc) && sameMoment(s.endUtc, times?.endUtc)}
              onPressedChange={() => choose(s)}
            />
          ))}
        </HStack>
      </RovingGroup>
    );

  const timesSection = (
    <VStack gap={5} hAlign="stretch">
      <VStack gap={3} hAlign="stretch">
        <HStack gap={2} vAlign="center">
          <Icon icon={AppIcon.Suggestion} color="secondary" />
          <StackItem size="fill">
            <Text type="label" weight="semibold">
              Suggested times
            </Text>
          </StackItem>
        </HStack>
        <Text type="supporting" color="secondary">
          {`${formatCalendarDay(range.from)} to ${formatCalendarDay(range.to)}`}
        </Text>
        {suggestionList}
        <HStack gap={2} wrap="wrap">
          {from > today && <Button label="Soonest" variant="ghost" size="sm" onClick={() => setFrom(today)} />}
          <Button
            label="Later suggestions"
            variant="secondary"
            size="sm"
            icon={<Icon icon={AppIcon.ChevronRight} />}
            isDisabled={!suggestions.data || attendees.length === 0}
            onClick={() => setFrom(laterFrom(range, suggested))}
          />
        </HStack>
      </VStack>
      <VStack gap={3} hAlign="stretch">
        <Text type="label" weight="semibold">
          Or choose a day and time
        </Text>
        <HStack gap={3} vAlign="start">
          <StackItem size="fill">
            <DateInput
              label="Day"
              value={date as ISODateString | undefined}
              onChange={(value) => setDate(value)}
              min={today as ISODateString}
              dateConstraints={WEEKDAYS_ONLY}
              weekStartsOn={WEEK_STARTS_ON}
              format="date_weekday"
            />
          </StackItem>
          <StackItem size="fill">
            <Selector
              label="Start"
              value={start === undefined ? '' : String(start)}
              placeholder="Choose a time"
              onChange={(value) => setChosenStart(Number(value))}
              isDisabled={date === undefined}
              disabledMessage={date === undefined ? 'Choose a day first.' : undefined}
              options={startOptions(length, start).map((m) => ({value: String(m), label: clockTime(m)}))}
              status={
                endsLate
                  ? {type: 'error', message: 'It would end after 18:00.'}
                  : (fieldStatus(validation, 'startUtc') ?? fieldStatus(validation, 'endUtc'))
              }
            />
          </StackItem>
        </HStack>
        {times && attendees.length > 0 && (
          <BusyThen busy={busy} isError={dayAvailability.isError} />
        )}
      </VStack>
    </VStack>
  );

  const summary = times ? (
    <Text type="body" weight="semibold" aria-live="polite">
      {`${formatCalendarDay(times.date)}, ${formatTimeRange(times.startUtc, times.endUtc)}, ${attendees.length === 1 ? '1 person' : `${attendees.length} people`}`}
    </Text>
  ) : (
    <Text type="body" color="secondary" aria-live="polite">
      Choose a suggested time, or a day and a start.
    </Text>
  );

  return (
    <DialogFrame
      isOpen={isOpen}
      title={isNew ? 'New Meeting' : 'Change Meeting'}
      icon={<Icon icon={AppIcon.Meeting} size="lg" color="accent" />}
      onClose={onClose}
      width={isNarrow ? 520 : 920}
      purpose={isSaving ? 'required' : 'form'}
      footerStart={isOpen ? summary : undefined}
      footer={
        <>
          <Button label={onBack ? 'Back' : 'Cancel'} variant="secondary" isDisabled={isSaving} onClick={onBack ?? onClose} />
          <Button label={isNew ? 'Add Meeting' : 'Save'} variant="primary" isDisabled={!canSave} isLoading={isSaving} onClick={save} />
        </>
      }>
      {isOpen && (
        <VStack ref={content} gap={4} hAlign="stretch">
          {problem.message && <AlertMessage message={problem.message} />}
          {validation && <ValidationAlert errors={validation} fields={FIELDS} />}
          {isNarrow ? (
            <VStack gap={6} hAlign="stretch">
              {details}
              {timesSection}
            </VStack>
          ) : (
            <HStack gap={6} vAlign="start">
              <StackItem size="static">
                <VStack width={320}>{details}</VStack>
              </StackItem>
              <StackItem size="fill">{timesSection}</StackItem>
            </HStack>
          )}
        </VStack>
      )}
    </DialogFrame>
  );
}

/**
 * Who's busy at the time chosen (`busy`, in words), or that everyone's free; nothing while it's being found out. It
 * only warns: the meeting can still be saved, and the server has the final word.
 */
function BusyThen({busy, isError}: {busy: string[] | undefined; isError: boolean}) {
  if (isError) return <AlertMessage status="warning" message="Who's free then could not be checked. Saving still checks it." />;
  if (!busy) return null;
  if (busy.length === 0) {
    return (
      <Text type="body" color="secondary">
        Everyone is free then.
      </Text>
    );
  }
  return <AlertMessage status="warning" message="Not everyone is free then" details={busy} />;
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
