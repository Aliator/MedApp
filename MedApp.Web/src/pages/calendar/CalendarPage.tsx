import {Button} from '@astryxdesign/core/Button';
import {Card} from '@astryxdesign/core/Card';
import {DateInput} from '@astryxdesign/core/DateInput';
import {DropdownMenu, type DropdownMenuOption} from '@astryxdesign/core/DropdownMenu';
import {Icon} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {Popover} from '@astryxdesign/core/Popover';
import {SegmentedControl, SegmentedControlItem} from '@astryxdesign/core/SegmentedControl';
import {Selector} from '@astryxdesign/core/Selector';
import {Skeleton} from '@astryxdesign/core/Skeleton';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import type {ISODateString} from '@astryxdesign/core/Calendar';
import {keepPreviousData, useQuery} from '@tanstack/react-query';
import {useEffect, useRef, useState, type CSSProperties, type RefObject} from 'react';
import {appointmentQuery, appointmentsQuery, useAppointmentActions, type Appointment} from '../../api/appointments';
import {
  calendarQuery,
  dayTemplatesQuery,
  staffQuery,
  useCalendarActions,
  weekTemplatesQuery,
  type CalendarBlock,
} from '../../api/calendar';
import {problemMessage} from '../../api/errors';
import {useAnnounce} from '../../app/announcements';
import {useCurrentUser} from '../../auth/auth';
import {AppointmentDialog, appointmentWhen} from '../../components/calendar/AppointmentDialog';
import {BlockDialog} from '../../components/calendar/BlockDialog';
import {BookAppointmentDialog} from '../../components/calendar/BookAppointmentDialog';
import {CalendarGrid, type CalendarSelection} from '../../components/calendar/CalendarGrid';
import {appointmentKey, blockKey, calendarItems, type CalendarGridItem} from '../../components/calendar/calendarItems';
import {NO_FIELD_STATUS_LINE} from '../../components/forms/fieldSpace';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {addDays, formatCalendarDay, formatTimeRange, nextWeekday, ukToday, weekStart} from '../../utils/calendarSlots';
import {formatDate, formatDayHeading, ukDay, WEEK_STARTS_ON} from '../../utils/dates';
import type {CalendarSearch} from './calendarSearch';

/** The toolbar's date and whose-calendar fields: enough for "Mon, 16 Nov 2026" and a username. */
const TOOLBAR_FIELD_WIDTH = 175;

/** Text in the secondary colour shown in the primary one instead (the View control's unselected choices). */
const PRIMARY_TEXT = {'--color-text-secondary': 'var(--color-text-primary)'} as CSSProperties;

/** The days a Calendar page shows: its week (Monday to Sunday), or its one day. */
export function calendarRange(date: string, view: 'day' | 'week'): {from: string; to: string} {
  return view === 'day' ? {from: date, to: date} : {from: weekStart(date), to: addDays(weekStart(date), 6)};
}

/** The calendar the page shows for a search (what its route preloads): the week by default, the user's own. */
export function calendarPageQuery(search: CalendarSearch, today: string = ukToday()) {
  return calendarQuery({staffUserId: search.staff, ...calendarRange(search.date ?? today, search.view ?? 'week')});
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

/** The Calendar page's search, Requested appointments counted: the Needs booking link's number. */
export const needsBookingCountQuery = appointmentsQuery({Statuses: ['Requested'], PageSize: 1});

export interface CalendarPageProps {
  search: CalendarSearch;
  onSearchChange: (next: CalendarSearch) => void;
}

/** What's being moved by a drag, waiting for its confirmation (appointments only). */
interface PendingMove {
  appointment: Appointment;
  to: CalendarSelection;
}

/**
 * The Calendar page (docs/calendar-plan.md, decision 28): a member of staff's week or day on a `CalendarGrid`, yours
 * by default, anyone's from the staff picker. Selecting free time offers Book appointment or Block time there (or goes
 * straight to booking, on a calendar you can only book on); New does the same without the grid. Opening an item opens
 * its dialog; dragging one moves it (an appointment after a confirmation naming the new time). The toolbar goes
 * between days and weeks, applies a day template to the day shown, and sets the default week.
 *
 * Its state is in the URL (`calendarSearch.ts`): the day, the view, whose calendar, and an open appointment, which
 * notifications link to. In a narrow window or a half view it shows a day.
 */
export function CalendarPage({search, onSearchChange}: CalendarPageProps) {
  const me = useCurrentUser();
  const isNarrow = useIsNarrow();
  const announce = useAnnounce();
  const [today] = useState(() => ukToday());
  const date = search.date ?? today;
  const view = isNarrow ? 'day' : (search.view ?? 'week');
  const {from, to} = calendarRange(date, view);

  const staff = useQuery(staffQuery);
  const myId = staff.data?.find((s) => s.username === me.username)?.userId;
  const calendar = useQuery({...calendarQuery({staffUserId: search.staff, from, to}), placeholderData: keepPreviousData});
  const needsBooking = useQuery(needsBookingCountQuery);
  const dayTemplates = useQuery({...dayTemplatesQuery, enabled: calendar.data?.canEdit === true && view === 'day'});
  const weekTemplates = useQuery(weekTemplatesQuery);
  const calendarActions = useCalendarActions();
  const appointmentActions = useAppointmentActions();

  const staffUserId = calendar.data?.staffUserId ?? search.staff ?? myId ?? '';
  const isOwn = search.staff === undefined || search.staff === myId;
  const canEdit = calendar.data?.canEdit === true;
  const canSetDefaultWeek = isOwn || me.isAdmin;
  const whose = isOwn ? 'My calendar' : `${calendar.data?.staffUsername ?? staff.data?.find((s) => s.userId === search.staff)?.username ?? ''}'s calendar`;
  const rangeLabel = view === 'day' ? formatDayHeading(`${date}T12:00:00Z`) : formatRange(from, to);
  const days = calendar.data?.days ?? [];
  const items = calendarItems(calendar.data);

  const [message, setMessage] = useState<string | null>(null);
  const [held, setHeld] = useState<CalendarSelection | null>(null);
  const heldAnchor = useRef<HTMLElement | null>(null);
  const selectionRef = useRef<HTMLElement>(null) as RefObject<HTMLElement>;
  const [booking, setBooking] = useState<{selection?: CalendarSelection} | null>(null);
  const [blocking, setBlocking] = useState<{selection?: CalendarSelection; block?: CalendarBlock} | null>(null);
  const [move, setMove] = useState<PendingMove | null>(null);

  // An appointment in the URL (from a notification, or opened here): shown in its dialog, on its day.
  const linked = useQuery({...appointmentQuery(search.appointment ?? ''), enabled: search.appointment !== undefined});
  const linkedAppointment = linked.data ?? calendar.data?.appointments?.find((a) => a.id === search.appointment) ?? null;
  const linkedStart = linked.data?.startUtc;
  const linkedStaff = linked.data?.staffUserId;
  useEffect(() => {
    if (!linkedStart) return;
    const day = ukDay(linkedStart);
    const staffChanges = linkedStaff !== undefined && linkedStaff !== null && linkedStaff !== staffUserId;
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
  const step = (direction: 1 | -1) => go({date: view === 'day' ? nextWeekday(date, direction) : addDays(date, 7 * direction)});

  const onSelectRange = (selection: CalendarSelection, anchor: HTMLElement | null) => {
    setMessage(null);
    if (canEdit) {
      heldAnchor.current = anchor;
      setHeld(selection);
    } else {
      setBooking({selection});
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

  // The grid's items are made from this calendar, so each one's appointment or block is in it.
  const openItem = (item: CalendarGridItem) => {
    setMessage(null);
    if (item.kind === 'appointment') {
      go({appointment: calendar.data!.appointments!.find((a) => appointmentKey(a.id) === item.key)!.id});
      return;
    }
    setBlocking({block: calendar.data!.blocks!.find((b) => blockKey(b) === item.key)!});
  };

  const moveItem = (item: CalendarGridItem, to: CalendarSelection) => {
    setMessage(null);
    if (item.kind === 'appointment') {
      setMove({appointment: calendar.data!.appointments!.find((a) => appointmentKey(a.id) === item.key)!, to});
      return;
    }
    // The item is one of this calendar's blocks.
    const block = calendar.data!.blocks!.find((b) => blockKey(b) === item.key)!;
    const values = {activityTypeId: block.activityTypeId, startUtc: to.startUtc, endUtc: to.endUtc, note: block.note};
    const options = {onSuccess: () => announce('Blocked time moved'), onError: (error: unknown) => setMessage(problemMessage(error, 'The blocked time could not be moved.'))};
    if (block.isFromTemplate) {
      calendarActions.changeTemplateBlock.mutate(
        {userId: staffUserId, date: block.date ?? '', templateBlockId: block.templateBlockId ?? '', ...values},
        options,
      );
    } else {
      calendarActions.saveBlock.mutate({id: block.blockId ?? '', ...values}, options);
    }
  };

  const confirmMove = (move: PendingMove) => {
    // A move of the appointment as the calendar showed it (Booked): if someone else changed it since, it's a 409.
    appointmentActions.book.mutate(
      {id: move.appointment.id ?? '', staffUserId, startUtc: move.to.startUtc, endUtc: move.to.endUtc, expectedStatus: move.appointment.status},
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
      .filter((t) => !t.isArchived || t.id === calendar.data?.defaultWeekTemplateId)
      .map((t) => ({
        label: t.name ?? '',
        icon: t.id === calendar.data?.defaultWeekTemplateId ? <Icon icon={AppIcon.Check} /> : undefined,
        onClick: () => setDefaultWeek(t.id ?? null, `Default week set to ${t.name}`),
      })),
    {
      label: 'None',
      icon: calendar.data && !calendar.data.defaultWeekTemplateId ? <Icon icon={AppIcon.Check} /> : undefined,
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

  const staffOptions = [
    ...(myId ? [{value: myId, label: 'My calendar'}] : []),
    ...(staff.data ?? [])
      .filter((s) => s.userId !== myId)
      .map((s) => ({value: s.userId ?? '', label: s.username ?? ''}))
      .sort((a, b) => a.label.localeCompare(b.label)),
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
              ...(canEdit ? [{label: 'Block time', icon: <Icon icon={AppIcon.BlockTime} />, onClick: () => setBlocking({})}] : []),
            ]}
          />
        </HStack>
      }
      dialogs={
        <>
          <BookAppointmentDialog
            isOpen={booking !== null}
            staffUserId={staffUserId || undefined}
            startUtc={booking?.selection?.startUtc}
            endUtc={booking?.selection?.endUtc}
            onClose={() => setBooking(null)}
          />
          <BlockDialog
            isOpen={blocking !== null}
            staffUserId={staffUserId}
            days={days}
            block={blocking?.block}
            canEdit={blocking?.block ? blocking.block.canEdit === true : canEdit}
            date={blocking?.selection?.date ?? (days.some((d) => d.date === date && d.isWorkingDay && !d.isPast) ? date : undefined)}
            startUtc={blocking?.selection?.startUtc}
            endUtc={blocking?.selection?.endUtc}
            calendar={calendar.data}
            onClose={() => setBlocking(null)}
          />
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
            onOpenChange={(open) => {
              if (!open) releaseSelection();
            }}
            label="Selected time"
            placement="end"
            padding={1}
            content={
              <VStack gap={0.5} hAlign="stretch" data-selected-time-menu="">
                <Button
                  label="Book appointment"
                  variant="ghost"
                  icon={<Icon icon={AppIcon.Book} />}
                  onClick={() => {
                    setBooking({selection: held ?? undefined});
                    setHeld(null);
                  }}
                />
                <Button
                  label="Block time"
                  variant="ghost"
                  icon={<Icon icon={AppIcon.BlockTime} />}
                  onClick={() => {
                    setBlocking({selection: held ?? undefined});
                    setHeld(null);
                  }}
                />
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
              <IconButton label={view === 'day' ? 'Previous day' : 'Previous week'} tooltip={view === 'day' ? 'Previous day' : 'Previous week'} icon={<Icon icon={AppIcon.ChevronLeft} />} variant="secondary" onClick={() => step(-1)} />
              <Button label="Today" variant="secondary" isDisabled={date === today && search.date === undefined} onClick={() => go({date: undefined})} />
              <IconButton label={view === 'day' ? 'Next day' : 'Next week'} tooltip={view === 'day' ? 'Next day' : 'Next week'} icon={<Icon icon={AppIcon.ChevronRight} />} variant="secondary" onClick={() => step(1)} />
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
            {!isNarrow && (
              // Its unselected choices use the primary text colour: the secondary one is under 4.5:1 on its track.
              <SegmentedControl label="View" value={view} onChange={(value) => go({view: value === 'day' ? 'day' : undefined})} style={PRIMARY_TEXT}>
                <SegmentedControlItem value="day" label="Day" />
                <SegmentedControlItem value="week" label="Week" />
              </SegmentedControl>
            )}
            <Selector
              label="Whose calendar"
              isLabelHidden
              value={staffUserId}
              onChange={(value) => go({staff: value === myId ? undefined : value, appointment: undefined})}
              options={staffOptions}
              hasSearch={staffOptions.length > 8}
              isLoading={staff.isLoading}
              startIcon={AppIcon.Users}
              width={TOOLBAR_FIELD_WIDTH}
            />
          </HStack>
          <StackItem size="fill" />
          <HStack gap={2} vAlign="center" wrap="wrap">
            {view === 'day' && canEdit && day?.isWorkingDay && !day.isPast && (
              <DropdownMenu
                button={{label: day.dayTemplateName ? `This day: ${day.dayTemplateName}` : "This day's template", variant: 'secondary'}}
                items={templateItems}
              />
            )}
            {canSetDefaultWeek && calendar.data && (
              <DropdownMenu
                button={{
                  label: `Default week: ${calendar.data.defaultWeekTemplateName ?? 'None'}`,
                  variant: 'secondary',
                }}
                items={weekItems}
              />
            )}
          </HStack>
        </HStack>
        <StackItem size="fill">
          <Card padding={0} width="100%" height="100%">
            {calendar.isError ? (
              <VStack padding={4}>
                <AlertMessage message="The calendar could not be loaded. Please try again." />
              </VStack>
            ) : !calendar.data ? (
              <VStack gap={2} padding={4}>
                <Skeleton height="var(--spacing-10)" />
                <Skeleton height="var(--spacing-10)" />
                <Skeleton height="var(--spacing-10)" />
              </VStack>
            ) : (
              <CalendarGrid
                label={`${whose}, ${rangeLabel}`}
                days={days}
                items={items}
                selection={held}
                selectionRef={selectionRef}
                onSelectRange={onSelectRange}
                onItemOpen={openItem}
                onItemMove={moveItem}
                onDayOpen={(d) => go({date: d, view: 'day'})}
              />
            )}
          </Card>
        </StackItem>
      </VStack>
    </Page>
  );
}
