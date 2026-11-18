import {fireEvent, screen, waitFor, within} from '@testing-library/react';
import {useState} from 'react';
import type {CalendarDay} from '../../api/calendar';
import {calendarDay, testAppointment, testBlock, testMeeting, ukTime} from '../../test/calendar';
import {renderWithApp} from '../../test/render';
import {addDays} from '../../utils/calendarSlots';
import {appointmentItem, blockItem, meetingItem, type CalendarGridItem} from './calendarItems';
import {CalendarGrid, dayColumns, type CalendarColumn, type CalendarGridProps, type CalendarRowMinutes} from './CalendarGrid';

const MONDAY = '2026-11-23';
const week = (today = MONDAY) => Array.from({length: 7}, (_, i) => calendarDay(addDays(MONDAY, i), today));

/** Monday 23 November 2026, 07:00 UK time, before the working day starts. */
const atMondayMorning = (time = '2026-11-23T07:00:00Z') => vi.useFakeTimers({toFake: ['Date'], now: new Date(time)});

/** A grid of one person's days (a week by default), or of `columns` when given. */
function renderGrid({days = week(), items = [], ...props}: Partial<CalendarGridProps> & {days?: CalendarDay[]; items?: CalendarGridItem[]} = {}) {
  const onSelectRange = vi.fn();
  const onItemOpen = vi.fn();
  const onItemMove = vi.fn();
  const result = renderWithApp(
    <CalendarGrid
      label="test-user's calendar"
      columns={dayColumns(days, items)}
      onSelectRange={onSelectRange}
      onItemOpen={onItemOpen}
      onItemMove={onItemMove}
      {...props}
    />,
  );
  return {...result, onSelectRange, onItemOpen, onItemMove};
}

const column = (name: string | RegExp) => screen.getByRole('group', {name});
const slot = (name: string) => screen.getByRole('button', {name});

/** Gives each day's column a box, as a browser would lay them out: 100px wide, 880px tall (22px a row), side by side. */
function layOutColumns() {
  const columns = document.querySelectorAll<HTMLElement>('.mc-col');
  columns.forEach((element, i) => {
    element.getBoundingClientRect = () => ({left: i * 100, right: (i + 1) * 100, top: 0, bottom: 880, width: 100, height: 880, x: i * 100, y: 0, toJSON: () => ({})});
  });
}

/** The y position of a row's middle (row 0 is 08:00). */
const yOf = (row: number) => row * 22 + 11;

describe('CalendarGrid', () => {
  it('shows a week, with weekends as narrow columns that do nothing, and the hours down the side', async () => {
    atMondayMorning();
    renderGrid();

    expect(await screen.findByRole('group', {name: "test-user's calendar"})).toBeInTheDocument();
    expect(column('Monday 23 November, today')).toBeInTheDocument();
    expect(column('Saturday 28 November, not a working day')).toHaveTextContent('Not a working day');
    expect(within(column('Saturday 28 November, not a working day')).queryAllByRole('button')).toHaveLength(0);
    expect(screen.getByText('08:00')).toBeInTheDocument();
    expect(screen.getByText('17:00')).toBeInTheDocument();
    expect(screen.getByText('Mon 23')).toBeInTheDocument();
    // "Not a working day" sits at the top of its column, kept in view as the day scrolls (5-minute rows make it tall).
    const css = document.querySelector('.medapp-calendar style')!.textContent!;
    expect(css).toMatch(/\.mc-col\[data-weekend\] \{[^}]*align-items: flex-start/);
    expect(css).toMatch(/\.mc-weekend-text \{ position: sticky; top: /);
    // 40 free 15-minute slots on each of the five working days.
    expect(screen.getAllByRole('button', {name: /, free$/})).toHaveLength(200);
  });

  it('greys a closed day like a weekend, but at full width and named, with nothing to select and what is on it flagged', async () => {
    atMondayMorning();
    const days = week().map((d) => (d.date === '2026-11-25' ? {...d, isWorkingDay: false, closedDayName: 'Staff training day'} : d));
    const appointment = appointmentItem(testAppointment({date: '2026-11-25', start: '09:00', end: '10:00'}));
    const onDayOpen = vi.fn();
    const {user} = renderGrid({days, items: [appointment], onDayOpen});

    const closed = await screen.findByRole('group', {name: 'Wednesday 25 November, closed: Staff training day'});
    expect(closed).toHaveTextContent('Closed: Staff training day');
    expect(closed).toHaveAttribute('data-weekend');
    expect(closed).toHaveAttribute('data-closed');
    expect(closed).not.toHaveAttribute('data-narrow');
    expect(within(closed).queryAllByRole('button', {name: /, free$/})).toHaveLength(0);
    // Only the other four working days have free time.
    expect(screen.getAllByRole('button', {name: /, free$/})).toHaveLength(160);
    // What was there before it closed still shows, flagged, and says so.
    const card = within(closed).getByRole('button', {name: 'Test Patient, Knee review: Assessment, 09:00 to 10:00, Booked, on a closed day'});
    expect(card.closest('[data-kind]')!.querySelector('svg')).not.toBeNull();
    // Its name reads across the column, not up it as a weekend's does.
    expect(document.querySelector('.medapp-calendar style')!.textContent).toMatch(/\.mc-col\[data-closed\] \.mc-weekend-text \{ writing-mode: horizontal-tb/);
    // Its heading still opens the day, and a compact week keeps it.
    await user.click(screen.getByRole('button', {name: 'Wed 25: show the day'}));
    expect(onDayOpen).toHaveBeenCalledWith('2026-11-25');
    expect(dayColumns(days, [], {weekdaysOnly: true}).map((c) => c.date)).toEqual(['2026-11-23', '2026-11-24', '2026-11-25', '2026-11-26', '2026-11-27']);
  });

  it('shows appointments and blocks, which open when clicked or pressed', async () => {
    atMondayMorning();
    const appointment = appointmentItem(testAppointment({date: MONDAY, start: '09:00', end: '10:00'}));
    const block = blockItem(testBlock(MONDAY, '12:00', '13:00', {note: 'Team lunch'}));
    const short = appointmentItem(testAppointment({id: 'short', date: '2026-11-24', start: '10:00', end: '10:15', procedureTitle: null}));
    const {user, onItemOpen} = renderGrid({items: [block, appointment, short]});

    const card = await screen.findByRole('button', {name: 'Test Patient, Knee review: Assessment, 09:00 to 10:00, Booked'});
    expect(card.closest('[data-kind]')).toHaveTextContent('Test Patient');
    await user.click(card);
    expect(onItemOpen).toHaveBeenCalledWith(appointment, expect.objectContaining({key: MONDAY, date: MONDAY}));

    const lunch = screen.getByRole('button', {name: 'Lunch, Team lunch, 12:00 to 13:00'});
    lunch.focus();
    await user.keyboard('{Enter}');
    expect(onItemOpen).toHaveBeenCalledWith(block, expect.objectContaining({key: MONDAY}));

    // A 15-minute appointment shows on one line; one that isn't for a procedure shows whose it is, and when.
    const shortCard = screen.getByRole('button', {name: 'Test Patient, appointment, 10:00 to 10:15, Booked'}).closest('[data-kind]');
    expect(shortCard).toHaveTextContent('Test Patient, 10:00 to 10:15');
    expect(shortCard).not.toHaveTextContent('Appointment');
    // Times under something aren't free.
    expect(screen.queryByRole('button', {name: 'Monday 23 November, 09:15, free'})).not.toBeInTheDocument();
    expect(slot('Monday 23 November, 10:00, free')).toBeInTheDocument();
  });

  it('orders the lines of a card the same way, whatever its height: who, what for, then when', async () => {
    atMondayMorning();
    const half = appointmentItem(testAppointment({id: 'half', date: MONDAY, start: '09:00', end: '09:30'}));
    const hour = appointmentItem(testAppointment({id: 'hour', date: MONDAY, start: '10:00', end: '11:00'}));
    const block = blockItem(testBlock(MONDAY, '12:00', '12:30', {overlapsAppointment: true}));
    const quiet = blockItem(testBlock(MONDAY, '13:00', '13:30', {blockId: 'quiet'}));
    renderGrid({items: [half, hour, block, quiet]});

    const lines = (name: RegExp) =>
      Array.from(screen.getByRole('button', {name}).closest('[data-kind]')!.querySelectorAll('.astryx-text'))
        .filter((t) => !t.parentElement?.closest('.astryx-text'))
        .map((t) => t.textContent);
    expect(await screen.findByRole('button', {name: /09:00 to 09:30/})).toBeInTheDocument();
    expect(lines(/09:00 to 09:30/)).toEqual(['Test Patient', 'Knee review: Assessment, 09:00 to 09:30']);
    expect(lines(/10:00 to 11:00/)).toEqual(['Test Patient', 'Knee review: Assessment', '10:00 to 11:00']);
    // A flagged block shows its warning beside its name; one without a note shows its time.
    expect(screen.getByRole('button', {name: /^Lunch, 12:00/}).closest('[data-kind]')!.querySelector('svg')).not.toBeNull();
    expect(lines(/^Lunch, 13:00/)).toEqual(['Lunch', '13:00 to 13:30']);
  });

  it('keeps items shorter than 15 minutes legible: a small line, or only a tooltip when too short for one', async () => {
    atMondayMorning();
    const ten = appointmentItem(testAppointment({id: 'ten', date: MONDAY, start: '09:00', end: '09:10'}));
    const five = blockItem(testBlock(MONDAY, '12:00', '12:05', {overlapsAppointment: true}));
    const {user} = renderGrid({items: [ten, five]});

    const tenCard = (await screen.findByRole('button', {name: 'Test Patient, Knee review: Assessment, 09:00 to 09:10, Booked'})).closest('[data-kind]')!;
    expect(tenCard).toHaveAttribute('data-fit', 'short');
    expect(tenCard.querySelector('.mc-item-short')).toHaveTextContent('Test Patient, Knee review: Assessment');

    const fiveButton = screen.getByRole('button', {name: 'Lunch, 12:00 to 12:05, overlaps an appointment'});
    const fiveCard = fiveButton.closest('[data-kind]')!;
    expect(fiveCard).toHaveAttribute('data-fit', 'tiny');
    expect(fiveCard.textContent).toBe('');
    await user.hover(fiveButton);
    expect(await screen.findByRole('tooltip')).toHaveTextContent('Lunch, 12:00 to 12:05, overlaps an appointment');
  });

  it('shows read-only items too short for text with a tooltip, named for screen readers', async () => {
    atMondayMorning();
    const five = {...blockItem(testBlock(MONDAY, '12:00', '12:05')), isReadOnly: true};
    renderWithApp(<CalendarGrid label="Today" columns={dayColumns([calendarDay(MONDAY, MONDAY)], [five])} onItemOpen={vi.fn()} isHeadingHidden />);

    await screen.findByRole('group', {name: 'Today'});
    const card = document.querySelector('[data-kind="block"]')!;
    expect(card).toHaveAttribute('data-fit', 'tiny');
    expect(within(card as HTMLElement).getByText('Lunch, 12:00 to 12:05')).toBeInTheDocument();
    expect(within(card as HTMLElement).queryByRole('button')).not.toBeInTheDocument();
    // Its column heading is left out: something above names the day.
    expect(document.querySelector('.mc-head')).toBeNull();
    expect(column('Monday 23 November, today')).toBeInTheDocument();
  });

  it('puts items that overlap side by side', async () => {
    atMondayMorning();
    const appointment = appointmentItem(testAppointment({date: MONDAY, start: '12:30', end: '13:00'}));
    const block = blockItem(testBlock(MONDAY, '12:00', '13:00', {overlapsAppointment: true}));
    renderGrid({items: [appointment, block]});

    const blockCard = (await screen.findByRole('button', {name: /^Lunch, 12:00 to 13:00, overlaps an appointment/})).closest<HTMLElement>('[data-kind]')!;
    const appointmentCard = screen.getByRole('button', {name: /^Test Patient/}).closest<HTMLElement>('[data-kind]')!;
    expect(blockCard.style.inlineSize).toBe('calc(50% - var(--spacing-1))');
    expect(appointmentCard.style.insetInlineStart).toBe('calc(50% + var(--spacing-0-5))');
    expect(blockCard.querySelector('svg')).not.toBeNull();
  });

  it('selects 15 minutes with a click on free time', async () => {
    atMondayMorning();
    const {onSelectRange} = renderGrid();

    const free = await screen.findByRole('button', {name: 'Tuesday 24 November, 09:15, free'});
    fireEvent.pointerDown(free, {button: 0, pointerId: 1});
    fireEvent.pointerUp(free, {pointerId: 1});
    // Not while the browser is still handling the release: a menu opened then would take it, and the click after it,
    // as a press outside it, and close at once.
    expect(onSelectRange).not.toHaveBeenCalled();

    await waitFor(() =>
      expect(onSelectRange).toHaveBeenCalledWith(
        {date: '2026-11-24', startUtc: ukTime('2026-11-24', '09:15').replace('Z', '.000Z'), endUtc: '2026-11-24T09:30:00.000Z', column: '2026-11-24'},
        free,
      ),
    );
  });

  it('selects a range by dragging, stopping at anything in the way', async () => {
    atMondayMorning();
    const block = blockItem(testBlock(MONDAY, '10:00', '11:00'));
    const {onSelectRange} = renderGrid({items: [block]});
    await screen.findByRole('group', {name: 'Monday 23 November, today'});
    layOutColumns();

    const start = slot('Monday 23 November, 09:00, free');
    fireEvent.pointerDown(start, {button: 0, pointerId: 1, clientX: 50, clientY: yOf(4)});
    // Dragging down past the block (10:00, row 8) stops at 09:45.
    fireEvent.pointerMove(column('Monday 23 November, today'), {pointerId: 1, clientX: 50, clientY: yOf(12)});
    expect(screen.getByText('09:00 to 10:00')).toBeInTheDocument();
    fireEvent.pointerUp(column('Monday 23 November, today'), {pointerId: 1});
    expect(onSelectRange).not.toHaveBeenCalled();

    await waitFor(() =>
      expect(onSelectRange).toHaveBeenCalledWith(
        {date: MONDAY, startUtc: '2026-11-23T09:00:00.000Z', endUtc: '2026-11-23T10:00:00.000Z', column: MONDAY},
        expect.anything(),
      ),
    );
  });

  it('is one tab stop: the arrow keys move between free times, Shift selects more, and Enter selects', async () => {
    atMondayMorning();
    const block = blockItem(testBlock(MONDAY, '08:15', '08:30'));
    const {user, onSelectRange} = renderGrid({items: [block]});

    const first = await screen.findByRole('button', {name: 'Monday 23 November, 08:00, free'});
    expect(first).toHaveAttribute('tabindex', '0');
    expect(slot('Monday 23 November, 08:30, free')).toHaveAttribute('tabindex', '-1');
    first.focus();

    await user.keyboard('{ArrowDown}');
    expect(slot('Monday 23 November, 08:30, free')).toHaveFocus();
    await user.keyboard('{ArrowRight}');
    expect(slot('Tuesday 24 November, 08:30, free')).toHaveFocus();
    await user.keyboard('{ArrowLeft}{ArrowUp}');
    expect(slot('Monday 23 November, 08:00, free')).toHaveFocus();
    await user.keyboard('{End}');
    expect(slot('Monday 23 November, 17:45, free')).toHaveFocus();
    await user.keyboard('{Home}{ArrowDown}');

    await user.keyboard('{Shift>}{ArrowDown}{ArrowDown}{/Shift}');
    expect(slot('Monday 23 November, 09:00, free')).toHaveFocus();
    expect(screen.getByText('08:30 to 09:15')).toBeInTheDocument();
    await user.keyboard('{Enter}');

    expect(onSelectRange).toHaveBeenCalledWith(
      {date: MONDAY, startUtc: '2026-11-23T08:30:00.000Z', endUtc: '2026-11-23T09:15:00.000Z', column: MONDAY},
      slot('Monday 23 November, 09:00, free'),
    );
  });

  it('lets go of a selection being made with Escape, and selects one slot with Space', async () => {
    atMondayMorning();
    const {user, onSelectRange} = renderGrid();

    (await screen.findByRole('button', {name: 'Monday 23 November, 08:00, free'})).focus();
    await user.keyboard('{Shift>}{ArrowDown}{/Shift}');
    expect(screen.getByText('08:00 to 08:30')).toBeInTheDocument();
    await user.keyboard('{Escape}');
    expect(screen.queryByText('08:00 to 08:30')).not.toBeInTheDocument();

    await user.keyboard(' ');
    expect(onSelectRange).toHaveBeenCalledWith({date: MONDAY, startUtc: '2026-11-23T08:15:00.000Z', endUtc: '2026-11-23T08:30:00.000Z', column: MONDAY}, expect.anything());
  });

  it("doesn't offer past days or times that have started, dims them, and shows the time now", async () => {
    atMondayMorning('2026-11-24T10:05:00Z');
    renderGrid({days: week('2026-11-24')});

    expect(await screen.findByRole('group', {name: 'Tuesday 24 November, today'})).toBeInTheDocument();
    expect(within(column('Monday 23 November')).queryAllByRole('button')).toHaveLength(0);
    expect(column('Monday 23 November')).toHaveAttribute('data-past', 'true');
    expect(screen.queryByRole('button', {name: 'Tuesday 24 November, 10:00, free'})).not.toBeInTheDocument();
    expect(slot('Tuesday 24 November, 10:15, free')).toHaveAttribute('tabindex', '0');
    // The line for now is under the items (before them), so it never runs through their text; its dot is over them.
    const now = document.querySelector('.mc-now')!;
    expect(now).not.toBeNull();
    expect(now.parentElement!.querySelector('.mc-now-dot')).not.toBeNull();
  });

  it('draws the line for now behind items', async () => {
    atMondayMorning('2026-11-23T09:10:00Z');
    renderGrid({items: [appointmentItem(testAppointment({date: MONDAY, start: '09:00', end: '09:30'}))]});

    const card = (await screen.findByRole('button', {name: /^Test Patient/})).closest('[data-kind]')!;
    const now = document.querySelector('.mc-now')!;
    expect(now.compareDocumentPosition(card) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(document.querySelector('.medapp-calendar style')!.textContent).toMatch(/\.mc-now \{[^}]*z-index: 0;/);
  });

  it('opens scrolled to an hour before now when today is shown and under way, and only the first time', async () => {
    // Columns 880px tall, as a browser would lay them out (jsdom lays out nothing).
    const offsetHeight = Object.getOwnPropertyDescriptor(HTMLElement.prototype, 'offsetHeight')!;
    Object.defineProperty(HTMLElement.prototype, 'offsetHeight', {
      configurable: true,
      get(this: HTMLElement) {
        return this.classList.contains('mc-col') ? 880 : 0;
      },
    });
    try {
      // 11:30: 10:30 is a quarter of the way through the working day.
      atMondayMorning('2026-11-23T11:30:00Z');
      const {rerender} = renderGrid();
      await screen.findByRole('group', {name: 'Monday 23 November, today'});
      const scroller = document.querySelector<HTMLElement>('.mc-scroll')!;
      expect(scroller.scrollTop).toBe(220);

      scroller.scrollTop = 0;
      rerender(<CalendarGrid label="test-user's calendar" columns={dayColumns(week('2026-11-24'), [])} />);
      expect(scroller.scrollTop).toBe(0);
    } finally {
      Object.defineProperty(HTMLElement.prototype, 'offsetHeight', offsetHeight);
    }
  });

  it("stays at the start of the day when it's early, or today isn't shown", async () => {
    atMondayMorning('2026-11-23T08:30:00Z');
    const {unmount} = renderGrid();
    await screen.findByRole('group', {name: 'Monday 23 November, today'});
    expect(document.querySelector<HTMLElement>('.mc-scroll')!.scrollTop).toBe(0);
    unmount();

    atMondayMorning('2026-11-30T11:30:00Z');
    renderGrid({days: week('2026-11-30')});
    await screen.findByRole('group', {name: "test-user's calendar"});
    expect(document.querySelector<HTMLElement>('.mc-scroll')!.scrollTop).toBe(0);
  });

  it('is read-only without onSelectRange: items still open', async () => {
    atMondayMorning();
    const appointment = appointmentItem(testAppointment({date: MONDAY}));
    const onItemOpen = vi.fn();
    const {user} = renderWithApp(<CalendarGrid label="Today" columns={dayColumns([calendarDay(MONDAY, MONDAY)], [appointment])} onItemOpen={onItemOpen} />);

    await user.click(await screen.findByRole('button', {name: /^Test Patient/}));
    expect(onItemOpen).toHaveBeenCalled();
    expect(screen.queryAllByRole('button', {name: /, free$/})).toHaveLength(0);
    // One day is headed by its weekday: the page around it gives the date.
    expect(screen.getByText('Monday')).toBeInTheDocument();
    expect(screen.queryByText('Monday 23 November')).not.toBeInTheDocument();
    expect(column('Monday 23 November, today')).toBeInTheDocument();
    // Its item can take the focus, so the scrolling area isn't a tab stop of its own.
    expect(document.querySelector('.mc-scroll')).not.toHaveAttribute('tabindex');
  });

  it('makes its scrolling area a named tab stop when nothing in it can take the focus, so the keyboard can scroll it', async () => {
    atMondayMorning();
    // A read-only day with only a read-only item (Home's day calendar, say).
    const block = {...blockItem(testBlock(MONDAY, '12:00', '13:00')), isReadOnly: true};
    renderWithApp(<CalendarGrid label="Today" columns={dayColumns([calendarDay(MONDAY, MONDAY)], [block])} isHeadingHidden />);

    const area = await screen.findByRole('group', {name: 'Today, scrollable'});
    expect(area).toHaveClass('mc-scroll');
    expect(area).toHaveAttribute('tabindex', '0');
  });

  it('shows a held selection, and opens a day from its heading', async () => {
    atMondayMorning();
    const onDayOpen = vi.fn();
    const selectionRef = {current: null as HTMLElement | null};
    const {user} = renderGrid({
      selection: {date: '2026-11-25', startUtc: ukTime('2026-11-25', '14:00'), endUtc: ukTime('2026-11-25', '15:00')},
      selectionRef,
      onDayOpen,
    });

    expect(await screen.findByText('14:00 to 15:00')).toBeInTheDocument();
    expect(selectionRef.current).toHaveTextContent('14:00 to 15:00');
    await user.click(screen.getByRole('button', {name: 'Wed 25: show the day'}));
    expect(onDayOpen).toHaveBeenCalledWith('2026-11-25');
  });

  it('moves a movable item by dragging it, keeping its length, and opens nothing after a drag', async () => {
    atMondayMorning();
    const appointment = appointmentItem(testAppointment({date: MONDAY, start: '09:00', end: '09:30'}));
    const {onItemMove, onItemOpen} = renderGrid({items: [appointment]});
    const card = (await screen.findByRole('button', {name: /^Test Patient/})).closest<HTMLElement>('[data-kind]')!;
    layOutColumns();

    // Picked up by its top (09:00, row 4), dropped on Wednesday at 14:00 (row 24).
    fireEvent.pointerDown(card, {button: 0, pointerId: 7, clientX: 50, clientY: yOf(4)});
    fireEvent.pointerMove(card, {pointerId: 7, clientX: 250, clientY: yOf(24)});
    expect(screen.getByText('14:00 to 14:30')).toBeInTheDocument();
    fireEvent.pointerUp(card, {pointerId: 7});
    fireEvent.click(card);

    expect(onItemMove).toHaveBeenCalledWith(appointment, {date: '2026-11-25', startUtc: '2026-11-25T14:00:00.000Z', endUtc: '2026-11-25T14:30:00.000Z', column: '2026-11-25'});
    expect(onItemOpen).not.toHaveBeenCalled();
  });

  it("doesn't move an item dropped where it was, on a weekend, or after Escape", async () => {
    atMondayMorning();
    const appointment = appointmentItem(testAppointment({date: MONDAY, start: '09:00', end: '09:30'}));
    const fixed: CalendarGridItem = {...blockItem(testBlock(MONDAY, '12:00', '13:00')), isMovable: false};
    const {onItemMove, onItemOpen} = renderGrid({items: [appointment, fixed]});
    const card = (await screen.findByRole('button', {name: /^Test Patient/})).closest<HTMLElement>('[data-kind]')!;
    layOutColumns();

    // A wobble on its own row takes it nowhere: a click, which opens it.
    fireEvent.pointerDown(card, {button: 0, pointerId: 1, clientX: 50, clientY: yOf(4)});
    fireEvent.pointerMove(card, {pointerId: 1, clientX: 60, clientY: yOf(4) + 10});
    fireEvent.pointerUp(card, {pointerId: 1});
    fireEvent.click(card);

    fireEvent.pointerDown(card, {button: 0, pointerId: 2, clientX: 50, clientY: yOf(4)});
    fireEvent.pointerMove(card, {pointerId: 2, clientX: 550, clientY: yOf(10)});
    fireEvent.pointerUp(card, {pointerId: 2});
    fireEvent.click(card);

    fireEvent.pointerDown(card, {button: 0, pointerId: 3, clientX: 50, clientY: yOf(4)});
    fireEvent.pointerMove(card, {pointerId: 3, clientX: 150, clientY: yOf(10)});
    fireEvent.keyDown(window, {key: 'Escape'});
    fireEvent.pointerUp(card, {pointerId: 3});
    fireEvent.click(card);

    // A press without moving is a click.
    fireEvent.pointerDown(card, {button: 0, pointerId: 4, clientX: 50, clientY: yOf(4)});
    fireEvent.pointerUp(card, {pointerId: 4});
    fireEvent.click(card);

    const fixedCard = screen.getByRole('button', {name: /^Lunch/}).closest<HTMLElement>('[data-kind]')!;
    fireEvent.pointerDown(fixedCard, {button: 0, pointerId: 5, clientX: 50, clientY: yOf(16)});
    fireEvent.pointerMove(fixedCard, {pointerId: 5, clientX: 150, clientY: yOf(20)});
    fireEvent.pointerUp(fixedCard, {pointerId: 5});

    expect(onItemMove).not.toHaveBeenCalled();
    expect(onItemOpen).toHaveBeenCalledTimes(2);
    expect(onItemOpen).toHaveBeenCalledWith(appointment, expect.objectContaining({key: MONDAY}));
  });

  it('moves an item between rows by whole rows from where it is, keeping its exact length', async () => {
    atMondayMorning();
    // 10:05 to 10:25 in 15-minute rows: it starts a third of the way into row 8 (10:00) and ends partway through row 9.
    const block = blockItem(testBlock(MONDAY, '10:05', '10:25'));
    const {onItemMove, onItemOpen} = renderGrid({items: [block]});
    const card = (await screen.findByRole('button', {name: /^Lunch, 10:05 to 10:25/})).closest<HTMLElement>('[data-kind]')!;
    layOutColumns();
    const pressed = yOf(8) + 4;

    // Nudged 6px on its own row (a touchpad's click): it stays where it is, and the click opens it.
    fireEvent.pointerDown(card, {button: 0, pointerId: 1, clientX: 50, clientY: pressed});
    fireEvent.pointerMove(card, {pointerId: 1, clientX: 50, clientY: pressed + 6});
    expect(document.querySelector('.mc-ghost')).toBeNull();
    fireEvent.pointerUp(card, {pointerId: 1});
    fireEvent.click(card);
    expect(onItemMove).not.toHaveBeenCalled();
    expect(onItemOpen).toHaveBeenCalledTimes(1);

    // A row down: 15 minutes later, the same 20 minutes long.
    fireEvent.pointerDown(card, {button: 0, pointerId: 2, clientX: 50, clientY: pressed});
    fireEvent.pointerMove(card, {pointerId: 2, clientX: 50, clientY: pressed + 22});
    expect(screen.getByText('10:20 to 10:40')).toBeInTheDocument();
    fireEvent.pointerUp(card, {pointerId: 2});
    fireEvent.click(card);
    expect(onItemMove).toHaveBeenLastCalledWith(block, {date: MONDAY, startUtc: '2026-11-23T10:20:00.000Z', endUtc: '2026-11-23T10:40:00.000Z', column: MONDAY});

    // To Tuesday on the same row: the same time of day.
    fireEvent.pointerDown(card, {button: 0, pointerId: 3, clientX: 50, clientY: pressed});
    fireEvent.pointerMove(card, {pointerId: 3, clientX: 150, clientY: pressed});
    fireEvent.pointerUp(card, {pointerId: 3});
    fireEvent.click(card);
    expect(onItemMove).toHaveBeenLastCalledWith(block, {date: '2026-11-24', startUtc: '2026-11-24T10:05:00.000Z', endUtc: '2026-11-24T10:25:00.000Z', column: '2026-11-24'});

    // Away and back again: a drag that ended where it began, so nothing moves and nothing opens.
    fireEvent.pointerDown(card, {button: 0, pointerId: 4, clientX: 50, clientY: pressed});
    fireEvent.pointerMove(card, {pointerId: 4, clientX: 50, clientY: pressed + 44});
    fireEvent.pointerMove(card, {pointerId: 4, clientX: 50, clientY: pressed + 2});
    fireEvent.pointerUp(card, {pointerId: 4});
    fireEvent.click(card);
    expect(onItemMove).toHaveBeenCalledTimes(2);
    expect(onItemOpen).toHaveBeenCalledTimes(1);
  });

  it('opens an item pressed with a wobble under 5px, or on its own row, even once it has started; only a drag moves it', async () => {
    // 10:30 on Monday: the 09:00 to 12:00 block has started, so it can't be moved anywhere, but it still opens.
    atMondayMorning('2026-11-23T10:30:00Z');
    const started = blockItem(testBlock(MONDAY, '09:00', '12:00', {blockId: 'started'}));
    const later = blockItem(testBlock(MONDAY, '14:00', '15:00', {blockId: 'later'}));
    const {onItemMove, onItemOpen} = renderGrid({items: [started, later]});
    const startedCard = (await screen.findByRole('button', {name: /^Lunch, 09:00 to 12:00/})).closest<HTMLElement>('[data-kind]')!;
    const laterCard = screen.getByRole('button', {name: /^Lunch, 14:00 to 15:00/}).closest<HTMLElement>('[data-kind]')!;
    layOutColumns();

    // Its words can't be selected: a wobble across them would, and Astryx's card ignores a click while they are.
    expect(startedCard.style.userSelect).toBe('none');

    // A 6px sideways wobble on the started block, on its own row: a click.
    fireEvent.pointerDown(startedCard, {button: 0, pointerId: 1, clientX: 50, clientY: yOf(10)});
    fireEvent.pointerMove(startedCard, {pointerId: 1, clientX: 56, clientY: yOf(10)});
    fireEvent.pointerUp(startedCard, {pointerId: 1});
    fireEvent.click(startedCard);
    expect(onItemOpen).toHaveBeenCalledTimes(1);

    // 4px down across a row line (from the bottom of 14:00's row into 14:15's): still a click, and nothing moves.
    const rowEnd = 24 * 22 + 20;
    fireEvent.pointerDown(laterCard, {button: 0, pointerId: 2, clientX: 50, clientY: rowEnd});
    fireEvent.pointerMove(laterCard, {pointerId: 2, clientX: 50, clientY: rowEnd + 4});
    expect(document.querySelector('.mc-ghost')).toBeNull();
    fireEvent.pointerUp(laterCard, {pointerId: 2});
    fireEvent.click(laterCard);
    expect(onItemOpen).toHaveBeenCalledTimes(2);
    expect(onItemMove).not.toHaveBeenCalled();

    // The same press carried 6px into the next row is a drag: it moves a row, and opens nothing.
    fireEvent.pointerDown(laterCard, {button: 0, pointerId: 3, clientX: 50, clientY: rowEnd});
    fireEvent.pointerMove(laterCard, {pointerId: 3, clientX: 50, clientY: rowEnd + 6});
    fireEvent.pointerUp(laterCard, {pointerId: 3});
    fireEvent.click(laterCard);
    expect(onItemMove).toHaveBeenCalledWith(later, {date: MONDAY, startUtc: '2026-11-23T14:15:00.000Z', endUtc: '2026-11-23T15:15:00.000Z', column: MONDAY});
    expect(onItemOpen).toHaveBeenCalledTimes(2);
  });

  it('skips days with nothing free, looks both ways for the nearest free time, and stops at the edges', async () => {
    atMondayMorning();
    // Tuesday is full; Wednesday is free only at 08:00.
    const tuesday = blockItem(testBlock('2026-11-24', '08:00', '18:00', {blockId: 'full'}));
    const wednesday = blockItem(testBlock('2026-11-25', '08:15', '18:00', {blockId: 'most'}));
    const {user} = renderGrid({items: [tuesday, wednesday]});

    const late = await screen.findByRole('button', {name: 'Monday 23 November, 12:00, free'});
    late.focus();
    await user.keyboard('{ArrowRight}');
    expect(slot('Wednesday 25 November, 08:00, free')).toHaveFocus();
    await user.keyboard('{ArrowUp}{ArrowDown}');
    expect(slot('Wednesday 25 November, 08:00, free')).toHaveFocus();
    // Nothing free on the weekend, so it stays on Friday.
    await user.keyboard('{ArrowRight}{ArrowRight}{ArrowRight}');
    expect(slot('Friday 27 November, 08:00, free')).toHaveFocus();
    await user.keyboard('{Tab}');
  });

  it('ignores other buttons, and a pointer that is cancelled or never moves far', async () => {
    atMondayMorning();
    const appointment = appointmentItem(testAppointment({date: MONDAY, start: '09:00', end: '09:30'}));
    const {onSelectRange, onItemMove} = renderGrid({items: [appointment]});
    const card = (await screen.findByRole('button', {name: /^Test Patient/})).closest<HTMLElement>('[data-kind]')!;
    const monday = column('Monday 23 November, today');

    fireEvent.pointerDown(slot('Monday 23 November, 10:00, free'), {button: 2, pointerId: 1});
    fireEvent.pointerUp(monday, {pointerId: 1});
    fireEvent.pointerDown(slot('Monday 23 November, 10:00, free'), {button: 0, pointerId: 2});
    // No boxes laid out: the pointer's row can't be told.
    fireEvent.pointerMove(monday, {pointerId: 2, clientX: 50, clientY: 300});
    fireEvent.pointerCancel(monday, {pointerId: 2});
    fireEvent.pointerMove(monday, {pointerId: 2, clientX: 50, clientY: 300});
    // A pointer selection is reported just after its press ends, so wait past that before saying none was.
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(onSelectRange).not.toHaveBeenCalled();

    fireEvent.pointerDown(card, {button: 2, pointerId: 3});
    fireEvent.pointerDown(card, {button: 0, pointerId: 4, clientX: 50, clientY: 100});
    fireEvent.pointerMove(card, {pointerId: 5, clientX: 90, clientY: 190});
    fireEvent.pointerMove(card, {pointerId: 4, clientX: 51, clientY: 101});
    fireEvent.keyDown(window, {key: 'a'});
    fireEvent.pointerUp(card, {pointerId: 5});
    fireEvent.pointerUp(card, {pointerId: 4});
    expect(onItemMove).not.toHaveBeenCalled();
  });

  it("shows a meeting with its icon, so it doesn't rely on colour alone", async () => {
    atMondayMorning();
    const meeting = meetingItem(testMeeting({date: MONDAY, start: '09:00', end: '10:00', title: 'MDT'}));
    const {user, onItemOpen} = renderGrid({items: [meeting]});

    const card = await screen.findByRole('button', {name: 'MDT, meeting, 09:00 to 10:00, 2 attending'});
    const box = card.closest('[data-kind]')!;
    expect(box).toHaveAttribute('data-kind', 'meeting');
    expect(box.querySelector('svg')).not.toBeNull();
    expect(box).toHaveTextContent('MDT');
    expect(box).toHaveTextContent('2 attending');
    await user.click(card);
    expect(onItemOpen).toHaveBeenCalledWith(meeting, expect.objectContaining({key: MONDAY}));
  });

  describe('with a column for each person', () => {
    const day = calendarDay(MONDAY, MONDAY);
    const lunch = blockItem(testBlock(MONDAY, '12:00', '13:00', {blockId: 'k-lunch'}));
    const huddle = meetingItem(testMeeting({date: MONDAY, start: '09:00', end: '10:00'}));
    const team = (): CalendarColumn[] => [
      {key: 'everyone', label: 'Everyone free', date: MONDAY, day, items: [], freeTime: [{startUtc: ukTime(MONDAY, '10:00'), endUtc: ukTime(MONDAY, '11:00')}, {}]},
      {key: 'k', label: 'k.ashford', date: MONDAY, day, items: [lunch, huddle]},
      {key: 'b', label: 'b.staff', date: MONDAY, day, items: [huddle]},
    ];

    it('heads and names each column for who it is, and offers only the free times in a column that has them', async () => {
      atMondayMorning();
      renderGrid({columns: team()});

      expect(await screen.findByRole('group', {name: 'k.ashford, Monday 23 November, today'})).toBeInTheDocument();
      expect(column('Everyone free, Monday 23 November, today')).toBeInTheDocument();
      expect(document.querySelector('.mc-head')).toHaveTextContent('Everyone freek.ashfordb.staff');
      expect(slot('k.ashford, Monday 23 November, 10:00, free')).toBeInTheDocument();
      expect(screen.queryByRole('button', {name: 'k.ashford, Monday 23 November, 12:00, free'})).not.toBeInTheDocument();
      // Everyone free: 10:00 to 11:00 only, marked in words; the rest is shaded as unavailable.
      const everyone = column('Everyone free, Monday 23 November, today');
      expect(within(everyone).getAllByRole('button')).toHaveLength(4);
      expect(within(everyone).getByRole('button', {name: 'Everyone free, Monday 23 November, 10:00, free'})).toHaveAttribute('tabindex', '0');
      expect(everyone.querySelector('.mc-free')).toHaveTextContent('Free, 10:00 to 11:00');
      expect(everyone.querySelectorAll('[data-unavailable]')).toHaveLength(36);
      // Each person keeps a sensible width: more people than fit scroll sideways.
      expect(screen.getByRole('group', {name: "test-user's calendar"}).style.getPropertyValue('--mc-min-inline')).toBe(
        'calc(var(--mc-gutter) + 0 * var(--mc-day-min) + 3 * var(--mc-labelled-min) + 0 * var(--mc-narrow))',
      );
    });

    it('says which column a selection is in, and shows a held one only there', async () => {
      atMondayMorning();
      const {user, onSelectRange, rerender} = renderGrid({columns: team()});

      (await screen.findByRole('button', {name: 'Everyone free, Monday 23 November, 10:00, free'})).focus();
      await user.keyboard('{ArrowRight}');
      expect(slot('k.ashford, Monday 23 November, 10:00, free')).toHaveFocus();
      await user.keyboard('{Enter}');
      expect(onSelectRange).toHaveBeenCalledWith({date: MONDAY, startUtc: '2026-11-23T10:00:00.000Z', endUtc: '2026-11-23T10:15:00.000Z', column: 'k'}, expect.anything());

      rerender(<CalendarGrid label="Team" columns={team()} selection={{date: MONDAY, startUtc: '2026-11-23T10:00:00.000Z', endUtc: '2026-11-23T10:15:00.000Z', column: 'b'}} />);
      expect(within(column('b.staff, Monday 23 November, today')).getByRole('button', {name: 'Selected time, 10:00 to 10:15'})).toBeInTheDocument();
      expect(within(column('k.ashford, Monday 23 November, today')).queryByRole('button', {name: /^Selected time/})).not.toBeInTheDocument();
    });

    it("keeps a dragged item in its own column, and says which column it's opened in", async () => {
      atMondayMorning();
      const {onItemMove, onItemOpen} = renderGrid({columns: team(), keepsItemsInColumn: true});
      const card = within(await screen.findByRole('group', {name: 'b.staff, Monday 23 November, today'})).getByRole('button', {name: /^Team huddle/});
      layOutColumns();

      // Dropped over k.ashford's column: nowhere to go.
      fireEvent.pointerDown(card, {button: 0, pointerId: 1, clientX: 250, clientY: yOf(4)});
      fireEvent.pointerMove(card, {pointerId: 1, clientX: 150, clientY: yOf(12)});
      expect(document.querySelector('.mc-ghost')).toBeNull();
      fireEvent.pointerUp(card, {pointerId: 1});
      // Within its own column, it moves.
      fireEvent.pointerDown(card, {button: 0, pointerId: 2, clientX: 250, clientY: yOf(4)});
      fireEvent.pointerMove(card, {pointerId: 2, clientX: 250, clientY: yOf(12)});
      // Only the copy being dragged is dimmed, not the same meeting in k.ashford's column.
      expect(document.querySelectorAll('[data-kind="meeting"][style*="opacity"]')).toHaveLength(1);
      fireEvent.pointerUp(card, {pointerId: 2});
      expect(onItemMove).toHaveBeenCalledTimes(1);
      expect(onItemMove).toHaveBeenCalledWith(huddle, {date: MONDAY, startUtc: '2026-11-23T11:00:00.000Z', endUtc: '2026-11-23T12:00:00.000Z', column: 'b'});

      fireEvent.click(card);
      fireEvent.click(card);
      expect(onItemOpen).toHaveBeenCalledWith(huddle, expect.objectContaining({key: 'b', label: 'b.staff'}));
    });

    it("never drops an item on a column of only free times, and heads people's columns on a weekend at full width", async () => {
      atMondayMorning();
      const saturday = calendarDay('2026-11-28', MONDAY);
      const {onItemMove, unmount} = renderGrid({columns: team()});
      const card = within(await screen.findByRole('group', {name: 'k.ashford, Monday 23 November, today'})).getByRole('button', {name: /^Lunch/});
      layOutColumns();
      fireEvent.pointerDown(card, {button: 0, pointerId: 1, clientX: 150, clientY: yOf(16)});
      fireEvent.pointerMove(card, {pointerId: 1, clientX: 50, clientY: yOf(20)});
      fireEvent.pointerUp(card, {pointerId: 1});
      expect(onItemMove).not.toHaveBeenCalled();
      unmount();

      renderGrid({columns: [{key: 'k', label: 'k.ashford', date: '2026-11-28', day: saturday, items: []}]});
      const weekend = await screen.findByRole('group', {name: 'k.ashford, Saturday 28 November, not a working day'});
      expect(weekend).not.toHaveAttribute('data-narrow');
      expect(document.querySelector('.mc-head')).toHaveTextContent('k.ashford');
    });
  });

  describe('compact (a week in a narrow window)', () => {
    it('leaves out Saturday and Sunday, unless something is somehow on one', () => {
      const weekday = appointmentItem(testAppointment({date: MONDAY, start: '09:00', end: '09:30'}));
      expect(dayColumns(week(), [weekday], {weekdaysOnly: true}).map((c) => c.date)).toEqual(['2026-11-23', '2026-11-24', '2026-11-25', '2026-11-26', '2026-11-27']);
      expect(dayColumns(week(), [weekday]).map((c) => c.date)).toHaveLength(7);

      // Nothing can be on a weekend, but if something were, it would never be hidden.
      const saturday = appointmentItem(testAppointment({id: 'saturday', date: '2026-11-28', start: '09:00', end: '09:30'}));
      expect(dayColumns(week(), [saturday], {weekdaysOnly: true}).map((c) => c.date)).toContain('2026-11-28');
    });

    it('heads each day short, naming its full date to screen readers, and selects as the full grid does', async () => {
      atMondayMorning();
      const onDayOpen = vi.fn();
      const {user, onSelectRange} = renderGrid({columns: dayColumns(week(), [], {weekdaysOnly: true}), isCompact: true, onDayOpen});

      const grid = await screen.findByRole('group', {name: "test-user's calendar"});
      expect(grid).toHaveAttribute('data-compact');
      expect(document.querySelectorAll('.mc-col')).toHaveLength(5);
      expect(document.querySelector('.mc-head')).toHaveTextContent('Mon 23Tue 24Wed 25Thu 26Fri 27');
      // The weekday over the date, so a narrow column tells Tue from Thu.
      expect(screen.getByRole('button', {name: 'Thu 26: show Thursday 26 November'}).querySelectorAll('.astryx-text')).toHaveLength(2);
      expect(column('Tuesday 24 November')).toBeInTheDocument();
      await user.click(screen.getByRole('button', {name: 'Mon 23, today: show Monday 23 November'}));
      expect(onDayOpen).toHaveBeenCalledWith(MONDAY);
      expect(screen.getByRole('button', {name: 'Fri 27: show Friday 27 November'})).toBeInTheDocument();

      fireEvent.pointerDown(slot('Tuesday 24 November, 10:00, free'), {button: 0, pointerId: 1});
      fireEvent.pointerUp(column('Tuesday 24 November'), {pointerId: 1});
      await waitFor(() => expect(onSelectRange).toHaveBeenCalledWith(expect.objectContaining({date: '2026-11-24', column: '2026-11-24'}), expect.anything()));
    });

    it('shows each item in its colour, its words wrapping to the lines it has room for, in full in its tooltip and name', async () => {
      atMondayMorning();
      const hour = appointmentItem(testAppointment({id: 'hour', date: MONDAY, start: '10:00', end: '11:00'}));
      const block = blockItem(testBlock(MONDAY, '12:00', '13:00', {note: 'Team lunch'}));
      const short = blockItem(testBlock(MONDAY, '14:00', '14:10', {blockId: 'short'}));
      const {user, onItemOpen} = renderGrid({columns: dayColumns(week(), [hour, block, short], {weekdaysOnly: true}), isCompact: true});

      const name = 'Test Patient, Knee review: Assessment, 10:00 to 11:00, Booked';
      const card = await screen.findByRole('button', {name});
      const box = card.closest('[data-kind]')!;
      // Its words, wrapping to the four lines an hour has room for; and its start time with its name under it, which
      // the grid's CSS shows instead when the card is too narrow for words (a phone).
      const full = box.querySelector('.mc-full .astryx-text')!;
      expect(full).toHaveTextContent('Test Patient, Knee review: Assessment');
      expect(box.querySelector('.mc-short')).toHaveTextContent('10:00Test Patient');
      // Under two rows, the time alone.
      expect(screen.getByRole('button', {name: /^Lunch, 14:00 to 14:10/}).closest('[data-kind]')!.querySelector('.mc-short')).toHaveTextContent(/^14:00$/);
      expect(box.getAttribute('style')).toContain('container-type: inline-size');
      const css = document.querySelector('.medapp-calendar style')!.textContent!;
      expect(css).toContain('@container (max-width: 4.5rem) { .medapp-calendar .mc-full { display: none; } .medapp-calendar .mc-short { display: block; } }');
      // A 10-minute item's text is the same size as the rest in a compact week.
      expect(screen.getByRole('button', {name: /^Lunch, 14:00 to 14:10/}).closest('[data-kind]')!.querySelector('.mc-item-short')).not.toBeNull();
      expect(css).toContain('.medapp-calendar[data-compact] .mc-item-short { font-size: var(--text-supporting-size');
      expect(card.closest('[data-kind]')!.getAttribute('style')).toContain(`var(--color-border-${hour.colour})`);
      await user.hover(card);
      expect(await screen.findByRole('tooltip')).toHaveTextContent(name);
      await user.click(card);
      expect(onItemOpen).toHaveBeenCalledWith(hour, expect.objectContaining({key: MONDAY}));

      expect(screen.getByRole('button', {name: 'Lunch, Team lunch, 12:00 to 13:00'}).closest('[data-kind]')).toHaveTextContent('Lunch, Team lunch');
    });
  });

  describe('with 5-minute rows', () => {
    /** Each day's column laid out with 5-minute rows as tall as 15-minute ones: 22px a row, 120 rows. */
    function layOutFiveMinuteColumns() {
      document.querySelectorAll<HTMLElement>('.mc-col').forEach((element, i) => {
        element.getBoundingClientRect = () => ({left: i * 100, right: (i + 1) * 100, top: 0, bottom: 2640, width: 100, height: 2640, x: i * 100, y: 0, toJSON: () => ({})});
      });
    }

    it('has three times the rows, with smaller quarter hours down the side, and selects 5 minutes at a time', async () => {
      atMondayMorning();
      // Lunch from 12:40, so a click at 12:30 has only 10 minutes free.
      const {user, onSelectRange} = renderGrid({rowMinutes: 5, items: [blockItem(testBlock('2026-11-24', '12:40', '13:00'))]});

      expect(await screen.findByRole('group', {name: "test-user's calendar"})).toHaveAttribute('data-row-minutes', '5');
      // 120 free 5-minute rows on each of the five working days.
      expect(screen.getAllByRole('button', {name: /, free$/})).toHaveLength(596);
      expect(screen.getByText('08:15').closest('.mc-hour')).toHaveAttribute('data-quarter');
      expect(screen.getByText('09:00').closest('.mc-hour')).not.toHaveAttribute('data-quarter');
      // Smaller, but never fainter: the secondary text colour at full strength is what passes 4.5:1.
      const css = document.querySelector('.medapp-calendar style')!.textContent!;
      expect(css).toMatch(/\.mc-hour\[data-quarter\] \.astryx-text \{ font-size: /);
      expect(css).not.toMatch(/data-quarter\][^{]*\{[^}]*opacity/);
      expect(document.querySelector('[data-slot="0-1"]')).toHaveAttribute('data-line', 'minor');
      expect(document.querySelector('[data-slot="0-3"]')).toHaveAttribute('data-line', 'quarter');

      // A click selects 15 minutes, not one 5-minute row: a single row would make a 5-minute booking.
      const free = slot('Tuesday 24 November, 09:05, free');
      fireEvent.pointerDown(free, {button: 0, pointerId: 1});
      fireEvent.pointerUp(free, {pointerId: 1});
      await waitFor(() =>
        expect(onSelectRange).toHaveBeenCalledWith({date: '2026-11-24', startUtc: '2026-11-24T09:05:00.000Z', endUtc: '2026-11-24T09:20:00.000Z', column: '2026-11-24'}, free),
      );
      // Or up to whatever comes sooner.
      const beforeLunch = slot('Tuesday 24 November, 12:30, free');
      fireEvent.pointerDown(beforeLunch, {button: 0, pointerId: 2});
      fireEvent.pointerUp(beforeLunch, {pointerId: 2});
      await waitFor(() =>
        expect(onSelectRange).toHaveBeenLastCalledWith({date: '2026-11-24', startUtc: '2026-11-24T12:30:00.000Z', endUtc: '2026-11-24T12:40:00.000Z', column: '2026-11-24'}, beforeLunch),
      );

      // The keyboard goes a row (5 minutes) at a time; Enter alone selects 15 minutes too, Shift any number of rows.
      free.focus();
      await user.keyboard('{ArrowDown}');
      expect(slot('Tuesday 24 November, 09:10, free')).toHaveFocus();
      await user.keyboard('{Enter}');
      expect(onSelectRange).toHaveBeenLastCalledWith(
        {date: '2026-11-24', startUtc: '2026-11-24T09:10:00.000Z', endUtc: '2026-11-24T09:25:00.000Z', column: '2026-11-24'},
        slot('Tuesday 24 November, 09:10, free'),
      );
      await user.keyboard('{Shift>}{ArrowDown}{/Shift}{Enter}');
      expect(onSelectRange).toHaveBeenLastCalledWith(
        {date: '2026-11-24', startUtc: '2026-11-24T09:10:00.000Z', endUtc: '2026-11-24T09:20:00.000Z', column: '2026-11-24'},
        expect.anything(),
      );
    });

    it('drags a selection and an item by 5 minutes', async () => {
      atMondayMorning();
      const appointment = appointmentItem(testAppointment({date: MONDAY, start: '09:00', end: '09:30'}));
      const {onSelectRange, onItemMove} = renderGrid({rowMinutes: 5, items: [appointment]});
      const card = (await screen.findByRole('button', {name: /^Test Patient/})).closest<HTMLElement>('[data-kind]')!;
      layOutFiveMinuteColumns();

      // From 10:00 (row 24) down to 10:10 (row 26).
      fireEvent.pointerDown(slot('Tuesday 24 November, 10:00, free'), {button: 0, pointerId: 1, clientX: 150, clientY: yOf(24)});
      fireEvent.pointerMove(column('Tuesday 24 November'), {pointerId: 1, clientX: 150, clientY: yOf(26)});
      fireEvent.pointerUp(column('Tuesday 24 November'), {pointerId: 1});
      await waitFor(() =>
        expect(onSelectRange).toHaveBeenCalledWith({date: '2026-11-24', startUtc: '2026-11-24T10:00:00.000Z', endUtc: '2026-11-24T10:15:00.000Z', column: '2026-11-24'}, expect.anything()),
      );

      // Picked up by its top (09:00, row 12), dropped on Wednesday at 14:05 (row 73), keeping its 30 minutes.
      fireEvent.pointerDown(card, {button: 0, pointerId: 7, clientX: 50, clientY: yOf(12)});
      fireEvent.pointerMove(card, {pointerId: 7, clientX: 250, clientY: yOf(73)});
      expect(screen.getByText('14:05 to 14:35')).toBeInTheDocument();
      fireEvent.pointerUp(card, {pointerId: 7});
      expect(onItemMove).toHaveBeenCalledWith(appointment, {date: '2026-11-25', startUtc: '2026-11-25T14:05:00.000Z', endUtc: '2026-11-25T14:35:00.000Z', column: '2026-11-25'});
    });

    it('keeps the same time at the top when the row length changes', async () => {
      // Columns as tall as their rows, 22px each, as a browser would lay them out (jsdom lays out nothing).
      const offsetHeight = Object.getOwnPropertyDescriptor(HTMLElement.prototype, 'offsetHeight')!;
      Object.defineProperty(HTMLElement.prototype, 'offsetHeight', {
        configurable: true,
        get(this: HTMLElement) {
          return this.classList.contains('mc-col') ? Number(this.closest<HTMLElement>('.medapp-calendar')!.dataset.rowMinutes === '5' ? 2640 : 880) : 0;
        },
      });
      try {
        atMondayMorning();
        // The same grid throughout, its row length switched as the Calendar page's control does.
        function Switchable() {
          const [rowMinutes, setRowMinutes] = useState<CalendarRowMinutes>(15);
          return (
            <>
              <button type="button" onClick={() => setRowMinutes(rowMinutes === 15 ? 5 : 15)}>
                Switch
              </button>
              <CalendarGrid label="test-user's calendar" columns={dayColumns(week(), [])} rowMinutes={rowMinutes} />
            </>
          );
        }
        const {user} = renderWithApp(<Switchable />);
        await screen.findByRole('group', {name: 'Monday 23 November, today'});
        const scroller = document.querySelector<HTMLElement>('.mc-scroll')!;

        // 10:30 at the top: a quarter of the way through the working day.
        scroller.scrollTop = 220;
        fireEvent.scroll(scroller);
        await user.click(screen.getByRole('button', {name: 'Switch'}));
        expect(document.querySelector('.medapp-calendar')).toHaveAttribute('data-row-minutes', '5');
        expect(scroller.scrollTop).toBe(660);

        await user.click(screen.getByRole('button', {name: 'Switch'}));
        expect(scroller.scrollTop).toBe(220);
      } finally {
        Object.defineProperty(HTMLElement.prototype, 'offsetHeight', offsetHeight);
      }
    });

    it("keeps the keyboard's place on the same time when the row length changes", async () => {
      atMondayMorning();
      function Switchable() {
        const [rowMinutes, setRowMinutes] = useState<CalendarRowMinutes>(15);
        return (
          <>
            <button type="button" onClick={() => setRowMinutes(rowMinutes === 15 ? 5 : 15)}>
              Switch
            </button>
            <CalendarGrid label="test-user's calendar" columns={dayColumns(week(), [])} rowMinutes={rowMinutes} onSelectRange={() => {}} />
          </>
        );
      }
      const {user} = renderWithApp(<Switchable />);
      (await screen.findByRole('button', {name: 'Tuesday 24 November, 10:00, free'})).focus();

      await user.click(screen.getByRole('button', {name: 'Switch'}));
      expect(slot('Tuesday 24 November, 10:00, free')).toHaveAttribute('tabindex', '0');

      // In 5-minute rows, to 10:10; back in 15-minute rows, the row it's in: 10:00.
      slot('Tuesday 24 November, 10:00, free').focus();
      await user.keyboard('{ArrowDown}{ArrowDown}');
      expect(slot('Tuesday 24 November, 10:10, free')).toHaveFocus();
      await user.click(screen.getByRole('button', {name: 'Switch'}));
      expect(slot('Tuesday 24 November, 10:00, free')).toHaveAttribute('tabindex', '0');
      expect(slot('Monday 23 November, 08:00, free')).toHaveAttribute('tabindex', '-1');
    });

    it('goes to the time now on switching, when the whole day was in view and today is under way', async () => {
      // A tall window: the 15-minute day (40px of headings and 880px of rows) fits its 1,000px, so nothing scrolled.
      const descriptors = Object.fromEntries(
        (['offsetHeight', 'clientHeight', 'scrollHeight'] as const).map((name) => [name, Object.getOwnPropertyDescriptor(HTMLElement.prototype, name)]),
      );
      const rowsTall = (element: HTMLElement) => (element.closest<HTMLElement>('.medapp-calendar')!.dataset.rowMinutes === '5' ? 2640 : 880);
      Object.defineProperty(HTMLElement.prototype, 'offsetHeight', {
        configurable: true,
        get(this: HTMLElement) {
          return this.classList.contains('mc-col') ? rowsTall(this) : 0;
        },
      });
      Object.defineProperty(HTMLElement.prototype, 'clientHeight', {
        configurable: true,
        get(this: HTMLElement) {
          return this.classList.contains('mc-scroll') ? 1000 : 0;
        },
      });
      Object.defineProperty(HTMLElement.prototype, 'scrollHeight', {
        configurable: true,
        get(this: HTMLElement) {
          return this.classList.contains('mc-scroll') ? 40 + rowsTall(this) : 0;
        },
      });
      try {
        atMondayMorning('2026-11-23T15:00:00Z');
        function Switchable() {
          const [rowMinutes, setRowMinutes] = useState<CalendarRowMinutes>(15);
          return (
            <>
              <button type="button" onClick={() => setRowMinutes(rowMinutes === 15 ? 5 : 15)}>
                Switch
              </button>
              <CalendarGrid label="test-user's calendar" columns={dayColumns(week(), [])} rowMinutes={rowMinutes} />
            </>
          );
        }
        const {user} = renderWithApp(<Switchable />);
        await screen.findByRole('group', {name: 'Monday 23 November, today'});
        const scroller = document.querySelector<HTMLElement>('.mc-scroll')!;
        // 15:00 is 7 hours into the 10-hour day: 1,848px down 2,640, less the hour before it (264px) kept above.
        await user.click(screen.getByRole('button', {name: 'Switch'}));
        expect(scroller.scrollTop).toBeCloseTo(1584);
      } finally {
        // clientHeight and scrollHeight are Element's, not HTMLElement's: removing these brings them back.
        for (const [name, descriptor] of Object.entries(descriptors)) {
          if (descriptor) Object.defineProperty(HTMLElement.prototype, name, descriptor);
          else delete (HTMLElement.prototype as unknown as Record<string, unknown>)[name];
        }
      }
    });
  });

  it('opens at the start of the working day once it has ended', async () => {
    const offsetHeight = Object.getOwnPropertyDescriptor(HTMLElement.prototype, 'offsetHeight')!;
    Object.defineProperty(HTMLElement.prototype, 'offsetHeight', {
      configurable: true,
      get(this: HTMLElement) {
        return this.classList.contains('mc-col') ? 880 : 0;
      },
    });
    try {
      atMondayMorning('2026-11-23T19:00:00Z');
      renderGrid();
      await screen.findByRole('group', {name: 'Monday 23 November, today'});
      expect(document.querySelector<HTMLElement>('.mc-scroll')!.scrollTop).toBe(0);
    } finally {
      Object.defineProperty(HTMLElement.prototype, 'offsetHeight', offsetHeight);
    }
  });

  it('shows nothing much for no days', async () => {
    renderWithApp(<CalendarGrid label="Nothing" columns={[]} />);
    expect(await screen.findByRole('group', {name: 'Nothing'})).toBeInTheDocument();
  });
});
