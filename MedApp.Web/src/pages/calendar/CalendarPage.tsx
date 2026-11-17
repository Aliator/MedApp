import {Button} from '@astryxdesign/core/Button';
import {Card} from '@astryxdesign/core/Card';
import {DateInput} from '@astryxdesign/core/DateInput';
import {DropdownMenu, type DropdownMenuOption} from '@astryxdesign/core/DropdownMenu';
import {Icon} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {MultiSelector} from '@astryxdesign/core/MultiSelector';
import {Popover} from '@astryxdesign/core/Popover';
import {SegmentedControl, SegmentedControlItem} from '@astryxdesign/core/SegmentedControl';
import {Skeleton} from '@astryxdesign/core/Skeleton';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import type {ISODateString} from '@astryxdesign/core/Calendar';
import {keepPreviousData, useQuery, type QueryClient} from '@tanstack/react-query';
import {useEffect, useRef, useState, type CSSProperties, type RefObject} from 'react';
import {appointmentQuery, appointmentsQuery, useAppointmentActions, type Appointment} from '../../api/appointments';
import {
  calendarQuery,
  dayTemplatesQuery,
  staffQuery,
  teamCalendarQuery,
  useCalendarActions,
  useMeetingActions,
  weekTemplatesQuery,
  type Calendar,
  type CalendarBlock,
} from '../../api/calendar';
import {problemMessage} from '../../api/errors';
import {preload} from '../../api/preload';
import {useAnnounce} from '../../app/announcements';
import {useCurrentUser} from '../../auth/auth';
import {AppointmentDialog, appointmentWhen} from '../../components/calendar/AppointmentDialog';
import {BlockDialog} from '../../components/calendar/BlockDialog';
import {BookAppointmentDialog} from '../../components/calendar/BookAppointmentDialog';
import {CalendarGrid, dayColumns, type CalendarColumn, type CalendarSelection} from '../../components/calendar/CalendarGrid';
import {appointmentKey, blockKey, calendarItems, meetingKey, type CalendarGridItem} from '../../components/calendar/calendarItems';
import {MeetingDialog, type MeetingDialogProps} from '../../components/calendar/MeetingDialog';
import {StaffPicker} from '../../components/calendar/StaffPicker';
import {NO_FIELD_STATUS_LINE} from '../../components/forms/fieldSpace';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {addDays, formatCalendarDay, formatTimeRange, nextWeekday, ukToday, weekStart} from '../../utils/calendarSlots';
import {formatDate, formatDayHeading, ukDay, WEEK_STARTS_ON} from '../../utils/dates';
import type {CalendarSearch, CalendarView} from './calendarSearch';

/** The toolbar's date and whose-calendar fields: enough for "Mon, 16 Nov 2026" and a username. */
const TOOLBAR_FIELD_WIDTH = 175;

/** Text in the secondary colour shown in the primary one instead (the View control's unselected choices). */
const PRIMARY_TEXT = {'--color-text-secondary': 'var(--color-text-primary)'} as CSSProperties;

/** The team view's first column, when everyone shown is free: its key on the grid (never a user ID). */
export const EVERYONE_FREE = 'everyone-free';

/** The days a Calendar page shows: its week (Monday to Sunday), or its one day (the day and team views). */
export function calendarRange(date: string, view: CalendarView): {from: string; to: string} {
  return view === 'week' ? {from: weekStart(date), to: addDays(weekStart(date), 6)} : {from: date, to: date};
}

/**
 * The view shown for a search: the one it names (the team view only for admins), or by default the week, and in a
 * narrow window or a half view the day. A week asked for there is kept, in its compact form (Monday to Friday, items on
 * one line); the team view works there too, scrolling sideways.
 */
export function shownView(search: CalendarSearch, isAdmin: boolean, isNarrow = false): CalendarView {
  const view = search.view === 'team' && !isAdmin ? undefined : search.view;
  return view ?? (isNarrow ? 'day' : 'week');
}

/** One person's calendar for a search (what its route preloads): the week by default, the user's own. */
export function calendarPageQuery(search: CalendarSearch, today: string = ukToday()) {
  return calendarQuery({staffUserId: search.staff, ...calendarRange(search.date ?? today, search.view === 'day' ? 'day' : 'week')});
}

/** The team view's day for a search (admins): everyone with a staff role, or those in `who`. */
export function teamPageQuery(search: CalendarSearch, today: string = ukToday()) {
  return teamCalendarQuery({date: search.date ?? today, staffUserIds: search.who ?? []});
}

/** The Calendar page's search, Requested appointments counted: the Needs booking link's number. */
export const needsBookingCountQuery = appointmentsQuery({Statuses: ['Requested'], PageSize: 1});

/**
 * What the Calendar page reads, for its route's loader: the team view's day (admins), or the person's calendar and
 * then (once it's known whose it is) their week templates, and on a day they can change, their day templates.
 * Rendering on the server it all waits; in the browser the templates start only if the calendar is already here.
 */
export async function preloadCalendarPage(queryClient: QueryClient, search: CalendarSearch, isAdmin: boolean, today: string = ukToday()) {
  const shared = [staffQuery, needsBookingCountQuery];
  if (shownView(search, isAdmin) === 'team') return preload(queryClient, teamPageQuery(search, today), ...shared);
  const query = calendarPageQuery(search, today);
  await preload(queryClient, query, ...shared);
  const calendar = queryClient.getQueryData(query.queryKey);
  if (!calendar?.staffUserId) return;
  const owner = calendar.staffUserId;
  await preload(queryClient, weekTemplatesQuery(owner), ...(search.view === 'day' && calendar.canEdit ? [dayTemplatesQuery(owner)] : []));
}

/** "23 to 29 November 2026", "30 November to 6 December 2026", or across a year, both dates in full. */
export function formatRange(from: string, to: string): string {
  const [fromYear, fromMonth] = from.split('-');
  const [toYear, toMonth] = to.split('-');
  const end = formatDate(to);
  if (fromYear !== toYear) return `${formatDate(from)} to ${end}`;
  const start = formatDate(from).replace(` ${fromYear}`, '');
  return `${fromMonth === toMonth ? start.split(' ')[0] : start} to ${end}`;
}

export interface CalendarPageProps {
  search: CalendarSearch;
  onSearchChange: (next: CalendarSearch) => void;
}

/** What's being moved by a drag, waiting for its confirmation (appointments only), and whose calendar it's on. */
interface PendingMove {
  appointment: Appointment;
  to: CalendarSelection;
  staffUserId: string;
}

/**
 * The Calendar page (docs/calendar-plan.md, decision 28): a member of staff's week or day on a `CalendarGrid`, yours
 * by default, anyone's from the staff picker, and for admins the team view: one day with a column for each member of
 * staff (all of them, or those picked in Who), after a first column of when everyone shown is free.
 *
 * Selecting free time offers Book appointment or Block time there (or goes straight to booking, on a calendar you can
 * only book on), in the team view for the person whose column it is; selecting in "Everyone free" offers New meeting
 * with them all at that time. New does the same without the grid, and admins can arrange a meeting from it. Opening an
 * item opens its dialog (an appointment, a block or a meeting); dragging one moves it (an appointment after a
 * confirmation naming the new time; a meeting by an admin, keeping its title, notes and attendees). The toolbar goes
 * between days, weeks and the team, applies one of the person's day templates to the day shown, sets their default
 * week from their week templates, and leads to their calendar templates.
 *
 * Its state is in the URL (`calendarSearch.ts`): the day, the view, whose calendar, an open appointment (which
 * notifications link to), and who the team view shows. In a narrow window or a half view it opens on a day rather than
 * the week, and a week is compact: Monday to Friday, a narrower hour gutter and items on one line (decision 67). The
 * team view still works there, scrolling sideways. Both apply after mount, as the server renders the wide page.
 */
export function CalendarPage({search, onSearchChange}: CalendarPageProps) {
  const me = useCurrentUser();
  const isNarrow = useIsNarrow();
  const announce = useAnnounce();
  const [today] = useState(() => ukToday());
  const date = search.date ?? today;
  const view = shownView(search, me.isAdmin, isNarrow);
  const isTeam = view === 'team';
  // A week in a narrow window or a half view: Monday to Friday, squeezed in.
  const isCompactWeek = isNarrow && view === 'week';
  const {from, to} = calendarRange(date, view);

  const staff = useQuery(staffQuery);
  const myId = staff.data?.find((s) => s.username === me.username)?.userId;
  const calendar = useQuery({...calendarQuery({staffUserId: search.staff, from, to}), placeholderData: keepPreviousData, enabled: !isTeam});
  const team = useQuery({...teamCalendarQuery({date, staffUserIds: search.who ?? []}), placeholderData: keepPreviousData, enabled: isTeam});
  // The one person's calendar shown (none in the team view).
  const person = isTeam ? undefined : calendar.data;
  const needsBooking = useQuery(needsBookingCountQuery);
  const calendarActions = useCalendarActions();
  const appointmentActions = useAppointmentActions();
  const meetingActions = useMeetingActions();

  const staffUserId = person?.staffUserId ?? search.staff ?? myId ?? '';
  const isOwn = search.staff === undefined || search.staff === myId;
  const canEdit = person?.canEdit === true;
  const canSetDefaultWeek = !isTeam && (isOwn || me.isAdmin);
  const personName = person?.staffUsername ?? staff.data?.find((s) => s.userId === search.staff)?.username ?? '';
  const whose = isTeam ? 'Team' : isOwn ? 'My calendar' : `${personName}'s calendar`;
  const rangeLabel = view === 'week' ? formatRange(from, to) : formatDayHeading(`${date}T12:00:00Z`);
  const days = person?.days ?? [];

  // The person's own templates: their day templates for the day shown, and their week templates for the default week.
  const dayTemplates = useQuery({...dayTemplatesQuery(staffUserId), enabled: canEdit && view === 'day' && staffUserId !== ''});
  const weekTemplates = useQuery({...weekTemplatesQuery(staffUserId), enabled: canSetDefaultWeek && staffUserId !== ''});

  // The team view: everyone shown, in name order, after "Everyone free".
  const members = [...(team.data?.members ?? [])].sort((a, b) => (a.staffUsername ?? '').localeCompare(b.staffUsername ?? ''));
  const memberIds = members.map((m) => m.staffUserId ?? '');

  const [message, setMessage] = useState<string | null>(null);
  const [held, setHeld] = useState<CalendarSelection | null>(null);
  const heldAnchor = useRef<HTMLElement | null>(null);
  const selectionRef = useRef<HTMLElement>(null) as RefObject<HTMLElement>;
  const [booking, setBooking] = useState<{selection?: CalendarSelection; staffUserId?: string} | null>(null);
  const [blocking, setBlocking] = useState<{selection?: CalendarSelection; block?: CalendarBlock; calendar?: Calendar} | null>(null);
  const [move, setMove] = useState<PendingMove | null>(null);
  const [meeting, setMeeting] = useState<Pick<MeetingDialogProps, 'meeting' | 'initial'> | null>(null);

  // An appointment in the URL (from a notification, or opened here): shown in its dialog, on its day.
  const linked = useQuery({...appointmentQuery(search.appointment ?? ''), enabled: search.appointment !== undefined});
  const linkedAppointment = linked.data ?? person?.appointments?.find((a) => a.id === search.appointment) ?? null;
  const linkedStart = linked.data?.startUtc;
  const linkedStaff = linked.data?.staffUserId;
  useEffect(() => {
    if (!linkedStart) return;
    const day = ukDay(linkedStart);
    // The team view stays the team view, on the appointment's day; otherwise the calendar is its member of staff's.
    const staffChanges = !isTeam && linkedStaff !== undefined && linkedStaff !== null && linkedStaff !== staffUserId;
    if (day < from || day > to || staffChanges) {
      onSearchChange({...search, date: day, staff: staffChanges ? (linkedStaff === myId ? undefined : linkedStaff) : search.staff});
    }
    // Only when the linked appointment arrives: moving on from its day afterwards is up to the person.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [linkedStart]);

  const go = (next: Partial<CalendarSearch>) => {
    setHeld(null);
    setMessage(null);
    onSearchChange({...search, ...next});
  };
  const step = (direction: 1 | -1) => go({date: view === 'week' ? addDays(date, 7 * direction) : nextWeekday(date, direction)});

  /** The calendar a column's items and selections belong to: the person's, or in the team view, that member's. */
  const calendarOf = (column: string | undefined): Calendar | undefined =>
    isTeam ? members.find((m) => m.staffUserId === column) : person;

  const onSelectRange = (selection: CalendarSelection, anchor: HTMLElement | null) => {
    setMessage(null);
    const owner = calendarOf(selection.column);
    if (selection.column === EVERYONE_FREE || owner?.canEdit === true) {
      heldAnchor.current = anchor;
      setHeld(selection);
    } else {
      setBooking({selection, staffUserId: owner?.staffUserId});
    }
  };

  const releaseSelection = () => {
    setHeld(null);
    // Back to the time that was selected (Escape, or a press on nothing), unless the menu closed because another
    // control was pressed: that press has moved the focus there, and it stays. The focus can still be in the closing
    // menu, or on the selection it opened from, both on their way out: those count as nothing else having it.
    window.setTimeout(() => {
      const focused = document.activeElement;
      const isLeaving = focused instanceof Element && focused.closest('[data-selected-time-menu], .mc-selection') !== null;
      if (!focused || focused === document.body || isLeaving) heldAnchor.current?.focus();
    }, 0);
  };

  // The grid's items are made from these calendars, so each one's appointment, block or meeting is in its column's.
  const meetingOf = (source: Calendar, item: CalendarGridItem) => source.meetings!.find((m) => meetingKey(m.id) === item.key)!;
  const appointmentOf = (source: Calendar, item: CalendarGridItem) => source.appointments!.find((a) => appointmentKey(a.id) === item.key)!;
  const blockOf = (source: Calendar, item: CalendarGridItem) => source.blocks!.find((b) => blockKey(b) === item.key)!;

  const openItem = (item: CalendarGridItem, column: CalendarColumn) => {
    setMessage(null);
    const source = calendarOf(column.key)!;
    if (item.kind === 'appointment') go({appointment: appointmentOf(source, item).id});
    else if (item.kind === 'meeting') setMeeting({meeting: meetingOf(source, item)});
    else setBlocking({block: blockOf(source, item), calendar: source});
  };

  const moveItem = (item: CalendarGridItem, to: CalendarSelection) => {
    setMessage(null);
    const source = calendarOf(to.column)!;
    if (item.kind === 'appointment') {
      setMove({appointment: appointmentOf(source, item), to, staffUserId: source.staffUserId ?? ''});
      return;
    }
    if (item.kind === 'meeting') {
      // The same meeting at the new time: its title, notes and attendees as they are. A clash is the API's 409, in
      // its words, and the meeting stays where it was.
      const m = meetingOf(source, item);
      meetingActions.save.mutate(
        {id: m.id ?? '', title: m.title, notes: m.notes, attendeeUserIds: (m.attendees ?? []).map((a) => a.userId ?? ''), startUtc: to.startUtc, endUtc: to.endUtc},
        {onSuccess: () => announce('Meeting moved'), onError: (error) => setMessage(problemMessage(error, 'The meeting could not be moved.'))},
      );
      return;
    }
    const block = blockOf(source, item);
    const values = {activityTypeId: block.activityTypeId, startUtc: to.startUtc, endUtc: to.endUtc, note: block.note};
    const options = {onSuccess: () => announce('Blocked time moved'), onError: (error: unknown) => setMessage(problemMessage(error, 'The blocked time could not be moved.'))};
    if (block.isFromTemplate) {
      calendarActions.changeTemplateBlock.mutate(
        {userId: source.staffUserId ?? '', date: block.date ?? '', templateBlockId: block.templateBlockId ?? '', ...values},
        options,
      );
    } else {
      calendarActions.saveBlock.mutate({id: block.blockId ?? '', ...values}, options);
    }
  };

  const confirmMove = (move: PendingMove) => {
    // A move of the appointment as the calendar showed it (Booked): if someone else changed it since, it's a 409.
    appointmentActions.book.mutate(
      {id: move.appointment.id ?? '', staffUserId: move.staffUserId, startUtc: move.to.startUtc, endUtc: move.to.endUtc, expectedStatus: move.appointment.status},
      {
        onSuccess: () => {
          announce('Appointment moved');
          setMove(null);
        },
        onError: (error) => {
          setMove(null);
          setMessage(problemMessage(error, 'The appointment could not be moved.'));
        },
      },
    );
  };

  const day = days.find((d) => d.date === date);
  const templateItems: DropdownMenuOption[] = [
    ...(dayTemplates.data ?? [])
      .filter((t) => !t.isArchived)
      .map((t) => ({
        label: t.name ?? '',
        icon: t.id === day?.dayTemplateId ? <Icon icon={AppIcon.Check} /> : undefined,
        onClick: () => applyDayTemplate(t.id ?? null, `${t.name} applied to ${formatCalendarDay(date)}`),
      })),
    {label: 'No template', icon: day && !day.dayTemplateId ? <Icon icon={AppIcon.Check} /> : undefined, onClick: () => applyDayTemplate(null, 'No template on this day')},
    ...(day?.isDayTemplateOverridden
      ? [
          {type: 'divider' as const},
          {
            label: "Back to the week's template",
            onClick: () =>
              calendarActions.clearDayTemplate.mutate(
                {userId: staffUserId, date},
                {onSuccess: () => announce("Back to the week's template"), onError: (error) => setMessage(problemMessage(error, 'The day could not be changed.'))},
              ),
          },
        ]
      : []),
  ];

  function applyDayTemplate(dayTemplateId: string | null, done: string) {
    setMessage(null);
    calendarActions.applyDayTemplate.mutate(
      {userId: staffUserId, date, dayTemplateId},
      {onSuccess: () => announce(done), onError: (error) => setMessage(problemMessage(error, 'The day could not be changed.'))},
    );
  }

  const weekItems: DropdownMenuOption[] = [
    ...(weekTemplates.data ?? [])
      .filter((t) => !t.isArchived || t.id === person?.defaultWeekTemplateId)
      .map((t) => ({
        label: t.name ?? '',
        icon: t.id === person?.defaultWeekTemplateId ? <Icon icon={AppIcon.Check} /> : undefined,
        onClick: () => setDefaultWeek(t.id ?? null, `Default week set to ${t.name}`),
      })),
    {
      label: 'None',
      icon: person && !person.defaultWeekTemplateId ? <Icon icon={AppIcon.Check} /> : undefined,
      onClick: () => setDefaultWeek(null, 'Default week removed'),
    },
  ];

  function setDefaultWeek(weekTemplateId: string | null, done: string) {
    setMessage(null);
    calendarActions.setWeekTemplate.mutate(
      {userId: staffUserId, weekTemplateId},
      {onSuccess: () => announce(done), onError: (error) => setMessage(problemMessage(error, 'The default week could not be changed.'))},
    );
  }

  // Their calendar templates: your own, or for an admin, whoever's calendar this is.
  const templatesLink = isTeam
    ? null
    : isOwn
      ? {label: 'My calendar templates', href: '/calendar/templates'}
      : me.isAdmin && personName
        ? {label: `${personName}'s calendar templates`, href: `/calendar/templates?staff=${staffUserId}`}
        : null;

  const whoOptions = (staff.data ?? [])
    .map((s) => ({value: s.userId ?? '', label: s.username ?? ''}))
    .sort((a, b) => a.label.localeCompare(b.label));

  // A new meeting from New: with the person whose calendar this is, or the people the team view shows when narrowed.
  const newMeetingWith = isTeam ? search.who : isOwn ? undefined : staffUserId ? [staffUserId] : undefined;

  const shown = isTeam ? team : calendar;
  const teamDay = team.data?.day;
  const teamDate = team.data?.date ?? date;
  const columns: CalendarColumn[] = !isTeam
    ? dayColumns(days, calendarItems(person), {weekdaysOnly: isCompactWeek})
    : teamDay
      ? [
          {key: EVERYONE_FREE, label: 'Everyone free', date: teamDate, day: teamDay, items: [], freeTime: team.data?.everyoneFree ?? []},
          ...members.map((m) => ({
            key: m.staffUserId ?? '',
            label: m.staffUsername ?? '',
            date: teamDate,
            day: m.days?.find((d) => d.date === teamDate) ?? teamDay,
            items: calendarItems(m),
          })),
        ]
      : [];

  const heldOwner = held && held.column !== EVERYONE_FREE ? calendarOf(held.column) : undefined;
  const viewChoices: {value: CalendarView; label: string}[] = [
    {value: 'day', label: 'Day'},
    {value: 'week', label: 'Week'},
    ...(me.isAdmin ? [{value: 'team' as const, label: 'Team'}] : []),
  ];
  const requestedCount = needsBooking.data?.totalCount ?? 0;

  return (
    <Page
      title="Calendar"
      subtitle={`${whose} · ${rangeLabel}`}
      fill
      message={
        message || linked.isError ? (
          <AlertMessage message={message ?? "That appointment couldn't be found. It may have been deleted."} />
        ) : undefined
      }
      actions={
        <HStack gap={2} vAlign="center">
          <Button
            label={requestedCount > 0 ? `Needs Booking (${requestedCount})` : 'Needs Booking'}
            variant="secondary"
            href="/calendar/needs-booking"
            icon={<Icon icon={AppIcon.NeedsBooking} />}
          />
          <DropdownMenu
            button={{label: 'New', variant: 'primary', icon: <Icon icon={AppIcon.Add} />}}
            items={[
              {label: 'Book appointment', icon: <Icon icon={AppIcon.Book} />, onClick: () => setBooking({})},
              ...(canEdit ? [{label: 'Block time', icon: <Icon icon={AppIcon.BlockTime} />, onClick: () => setBlocking({calendar: person})}] : []),
              ...(me.isAdmin
                ? [
                    {
                      label: 'New meeting',
                      icon: <Icon icon={AppIcon.Meeting} />,
                      onClick: () => setMeeting({initial: newMeetingWith ? {attendeeUserIds: newMeetingWith} : undefined}),
                    },
                  ]
                : []),
            ]}
          />
        </HStack>
      }
      dialogs={
        <>
          <BookAppointmentDialog
            isOpen={booking !== null}
            staffUserId={booking?.staffUserId ?? (isTeam ? undefined : staffUserId || undefined)}
            startUtc={booking?.selection?.startUtc}
            endUtc={booking?.selection?.endUtc}
            onClose={() => setBooking(null)}
          />
          <BlockDialog
            isOpen={blocking !== null}
            staffUserId={blocking?.calendar?.staffUserId ?? staffUserId}
            days={blocking?.calendar?.days ?? days}
            block={blocking?.block}
            canEdit={blocking?.block ? blocking.block.canEdit === true : (blocking?.calendar?.canEdit ?? canEdit)}
            date={blocking?.selection?.date ?? (days.some((d) => d.date === date && d.isWorkingDay && !d.isPast) ? date : undefined)}
            startUtc={blocking?.selection?.startUtc}
            endUtc={blocking?.selection?.endUtc}
            calendar={blocking?.calendar ?? person}
            onClose={() => setBlocking(null)}
          />
          <MeetingDialog isOpen={meeting !== null} meeting={meeting?.meeting} initial={meeting?.initial} onClose={() => setMeeting(null)} />
          <AppointmentDialog appointment={search.appointment ? linkedAppointment : null} onClose={() => go({appointment: undefined})} />
          <ConfirmDialog
            isOpen={move !== null}
            title="Move Appointment"
            icon={AppIcon.Book}
            message={
              move
                ? `Move ${move.appointment.patientName}'s appointment from ${appointmentWhen(move.appointment)} to ${formatCalendarDay(move.to.date)}, ${formatTimeRange(move.to.startUtc, move.to.endUtc)}?`
                : undefined
            }
            confirmText="Move"
            isProcessing={appointmentActions.book.isPending}
            onConfirm={() => confirmMove(move!)}
            onCancel={() => setMove(null)}
          />
          <Popover
            anchorRef={selectionRef}
            isOpen={held !== null}
            // As wide as its buttons: left to its default, it stretches to the selection it's anchored to, a whole
            // column wide (a day in a narrow window, or "Everyone free"), past the grid's edge.
            width="max-content"
            onOpenChange={(open) => {
              if (!open) releaseSelection();
            }}
            label="Selected time"
            placement="end"
            padding={1}
            content={
              <VStack gap={0.5} hAlign="stretch" data-selected-time-menu="">
                {held?.column === EVERYONE_FREE ? (
                  // When everyone shown is free: a meeting with them all, then.
                  <Button
                    label="New meeting"
                    variant="ghost"
                    icon={<Icon icon={AppIcon.Meeting} />}
                    onClick={() => {
                      setMeeting({initial: {attendeeUserIds: memberIds, startUtc: held.startUtc, endUtc: held.endUtc}});
                      setHeld(null);
                    }}
                  />
                ) : (
                  <>
                    <Button
                      label="Book appointment"
                      variant="ghost"
                      icon={<Icon icon={AppIcon.Book} />}
                      onClick={() => {
                        setBooking({selection: held ?? undefined, staffUserId: heldOwner?.staffUserId});
                        setHeld(null);
                      }}
                    />
                    <Button
                      label="Block time"
                      variant="ghost"
                      icon={<Icon icon={AppIcon.BlockTime} />}
                      onClick={() => {
                        setBlocking({selection: held ?? undefined, calendar: heldOwner});
                        setHeld(null);
                      }}
                    />
                  </>
                )}
              </VStack>
            }
          />
        </>
      }>
      <VStack gap={4} height="100%">
        {/* Two groups that wrap as wholes: getting about (left), and this calendar's templates (right). Its fields
            never show an error, so they drop the line kept for one, and line up with the buttons. */}
        <HStack gap={2} vAlign="center" wrap="wrap" style={NO_FIELD_STATUS_LINE}>
          <HStack gap={2} vAlign="center" wrap="wrap">
            <HStack gap={1} vAlign="center">
              <IconButton label={view === 'week' ? 'Previous week' : 'Previous day'} tooltip={view === 'week' ? 'Previous week' : 'Previous day'} icon={<Icon icon={AppIcon.ChevronLeft} />} variant="secondary" onClick={() => step(-1)} />
              <Button label="Today" variant="secondary" isDisabled={date === today && search.date === undefined} onClick={() => go({date: undefined})} />
              <IconButton label={view === 'week' ? 'Next week' : 'Next day'} tooltip={view === 'week' ? 'Next week' : 'Next day'} icon={<Icon icon={AppIcon.ChevronRight} />} variant="secondary" onClick={() => step(1)} />
            </HStack>
            <DateInput
              label="Go to date"
              isLabelHidden
              value={date as ISODateString}
              onChange={(value) => value && go({date: value === today ? undefined : value})}
              weekStartsOn={WEEK_STARTS_ON}
              format="date_weekday"
              width={TOOLBAR_FIELD_WIDTH}
            />
            {/* Its unselected choices use the primary text colour: the secondary one is under 4.5:1 on its track. The
                week is left out of the URL where it's the default (a wide window), and named in a narrow one, where
                the day is the default, so it stays a week. */}
            <SegmentedControl
              label="View"
              value={view}
              onChange={(value) => go({view: value === 'day' || value === 'team' ? value : isNarrow ? 'week' : undefined})}
              style={PRIMARY_TEXT}>
              {viewChoices.map((choice) => (
                <SegmentedControlItem key={choice.value} value={choice.value} label={choice.label} />
              ))}
            </SegmentedControl>
            {isTeam ? (
              <MultiSelector
                label="Who"
                isLabelHidden
                value={search.who ?? []}
                onChange={(value) => go({who: value.length ? value : undefined})}
                options={whoOptions}
                placeholder="Everyone"
                triggerDisplay="labels"
                hasSearch={whoOptions.length > 8}
                hasSelectAll
                hasClear
                isLoading={staff.isLoading}
                startIcon={AppIcon.Users}
                width={TOOLBAR_FIELD_WIDTH}
              />
            ) : (
              <StaffPicker
                label="Whose calendar"
                staff={staff.data}
                myId={myId}
                ownLabel="My calendar"
                value={staffUserId}
                onChange={(next) => go({staff: next, appointment: undefined})}
                isLoading={staff.isLoading}
                width={TOOLBAR_FIELD_WIDTH}
              />
            )}
          </HStack>
          <StackItem size="fill" />
          <HStack gap={2} vAlign="center" wrap="wrap">
            {view === 'day' && canEdit && day?.isWorkingDay && !day.isPast && (
              <DropdownMenu
                button={{label: day.dayTemplateName ? `This day: ${day.dayTemplateName}` : "This day's template", variant: 'secondary'}}
                items={templateItems}
              />
            )}
            {canSetDefaultWeek && person && (
              <DropdownMenu
                button={{
                  label: `Default week: ${person.defaultWeekTemplateName ?? 'None'}`,
                  variant: 'secondary',
                }}
                items={weekItems}
              />
            )}
            {templatesLink && <Button label={templatesLink.label} variant="secondary" href={templatesLink.href} icon={<Icon icon={AppIcon.CalendarTemplates} />} />}
          </HStack>
        </HStack>
        <StackItem size="fill">
          <Card padding={0} width="100%" height="100%">
            {shown.isError ? (
              <VStack padding={4}>
                <AlertMessage message="The calendar could not be loaded. Please try again." />
              </VStack>
            ) : !shown.data ? (
              <VStack gap={2} padding={4}>
                <Skeleton height="var(--spacing-10)" />
                <Skeleton height="var(--spacing-10)" />
                <Skeleton height="var(--spacing-10)" />
              </VStack>
            ) : (
              <CalendarGrid
                label={`${whose}, ${rangeLabel}`}
                columns={columns}
                selection={held}
                selectionRef={selectionRef}
                onSelectRange={onSelectRange}
                onItemOpen={openItem}
                onItemMove={moveItem}
                keepsItemsInColumn={isTeam}
                isCompact={isCompactWeek}
                onDayOpen={(d) => go({date: d, view: 'day'})}
              />
            )}
          </Card>
        </StackItem>
      </VStack>
    </Page>
  );
}
