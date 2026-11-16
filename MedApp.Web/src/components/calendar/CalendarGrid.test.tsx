import {fireEvent, screen, waitFor, within} from '@testing-library/react';
import {calendarDay, testAppointment, testBlock, ukTime} from '../../test/calendar';
import {renderWithApp} from '../../test/render';
import {addDays} from '../../utils/calendarSlots';
import {appointmentItem, blockItem, type CalendarGridItem} from './calendarItems';
import {CalendarGrid, type CalendarGridProps} from './CalendarGrid';

const MONDAY = '2026-11-23';
const week = (today = MONDAY) => Array.from({length: 7}, (_, i) => calendarDay(addDays(MONDAY, i), today));

/** Monday 23 November 2026, 07:00 UK time, before the working day starts. */
const atMondayMorning = (time = '2026-11-23T07:00:00Z') => vi.useFakeTimers({toFake: ['Date'], now: new Date(time)});

function renderGrid(props: Partial<CalendarGridProps> = {}) {
  const onSelectRange = vi.fn();
  const onItemOpen = vi.fn();
  const onItemMove = vi.fn();
  const result = renderWithApp(
    <CalendarGrid label="test-user's calendar" days={week()} items={[]} onSelectRange={onSelectRange} onItemOpen={onItemOpen} onItemMove={onItemMove} {...props} />,
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
    // 40 free 15-minute slots on each of the five working days.
    expect(screen.getAllByRole('button', {name: /, free$/})).toHaveLength(200);
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
    expect(onItemOpen).toHaveBeenCalledWith(appointment);

    const lunch = screen.getByRole('button', {name: 'Lunch, Team lunch, 12:00 to 13:00'});
    lunch.focus();
    await user.keyboard('{Enter}');
    expect(onItemOpen).toHaveBeenCalledWith(block);

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
    renderWithApp(<CalendarGrid label="Today" days={[calendarDay(MONDAY, MONDAY)]} items={[five]} onItemOpen={vi.fn()} isHeadingHidden />);

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
      expect(onSelectRange).toHaveBeenCalledWith({date: '2026-11-24', startUtc: ukTime('2026-11-24', '09:15').replace('Z', '.000Z'), endUtc: '2026-11-24T09:30:00.000Z'}, free),
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
      expect(onSelectRange).toHaveBeenCalledWith({date: MONDAY, startUtc: '2026-11-23T09:00:00.000Z', endUtc: '2026-11-23T10:00:00.000Z'}, expect.anything()),
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

    expect(onSelectRange).toHaveBeenCalledWith({date: MONDAY, startUtc: '2026-11-23T08:30:00.000Z', endUtc: '2026-11-23T09:15:00.000Z'}, slot('Monday 23 November, 09:00, free'));
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
    expect(onSelectRange).toHaveBeenCalledWith({date: MONDAY, startUtc: '2026-11-23T08:15:00.000Z', endUtc: '2026-11-23T08:30:00.000Z'}, expect.anything());
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
      rerender(<CalendarGrid label="test-user's calendar" days={week('2026-11-24')} items={[]} />);
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
    const {user} = renderWithApp(<CalendarGrid label="Today" days={[calendarDay(MONDAY, MONDAY)]} items={[appointment]} onItemOpen={onItemOpen} />);

    await user.click(await screen.findByRole('button', {name: /^Test Patient/}));
    expect(onItemOpen).toHaveBeenCalled();
    expect(screen.queryAllByRole('button', {name: /, free$/})).toHaveLength(0);
    // One day is headed by its weekday: the page around it gives the date.
    expect(screen.getByText('Monday')).toBeInTheDocument();
    expect(screen.queryByText('Monday 23 November')).not.toBeInTheDocument();
    expect(column('Monday 23 November, today')).toBeInTheDocument();
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

    expect(onItemMove).toHaveBeenCalledWith(appointment, {date: '2026-11-25', startUtc: '2026-11-25T14:00:00.000Z', endUtc: '2026-11-25T14:30:00.000Z'});
    expect(onItemOpen).not.toHaveBeenCalled();
  });

  it("doesn't move an item dropped where it was, on a weekend, or after Escape", async () => {
    atMondayMorning();
    const appointment = appointmentItem(testAppointment({date: MONDAY, start: '09:00', end: '09:30'}));
    const fixed: CalendarGridItem = {...blockItem(testBlock(MONDAY, '12:00', '13:00')), isMovable: false};
    const {onItemMove, onItemOpen} = renderGrid({items: [appointment, fixed]});
    const card = (await screen.findByRole('button', {name: /^Test Patient/})).closest<HTMLElement>('[data-kind]')!;
    layOutColumns();

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
    expect(onItemOpen).toHaveBeenCalledTimes(1);
    expect(onItemOpen).toHaveBeenCalledWith(appointment);
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
    await user.keyboard('{ArrowRight}{ArrowRight}');
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

  it('shows nothing much for no days', async () => {
    renderWithApp(<CalendarGrid label="Nothing" days={[]} items={[]} />);
    expect(await screen.findByRole('group', {name: 'Nothing'})).toBeInTheDocument();
  });
});
