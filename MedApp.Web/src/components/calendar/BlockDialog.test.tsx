import {fireEvent, screen, waitFor, within} from '@testing-library/react';
import {useState} from 'react';
import {SaveShortcutProvider} from '../forms/SaveShortcut';
import {calendarDay, STAFF_ME, testBlock, ukTime} from '../../test/calendar';
import {expectAnnounced, isDisabled, json, mockApi, openDialog, renderWithApp, staffUser, withAnnouncements} from '../../test/render';
import {addDays} from '../../utils/calendarSlots';
import {BlockDialog, type BlockDialogProps} from './BlockDialog';

const MONDAY = '2026-11-23';
const DAYS = Array.from({length: 7}, (_, i) => calendarDay(addDays(MONDAY, i), MONDAY, i === 2 ? {dayTemplateName: 'Standard day'} : {}));
const ACTIVITIES = [
  {id: 'activity-lunch', name: 'Lunch', colour: 'green', isArchived: false},
  {id: 'activity-training', name: 'Training', colour: 'purple', isArchived: false},
  {id: 'activity-old', name: 'Old', colour: 'gray', isArchived: true},
];

function renderDialog(props: Partial<BlockDialogProps> = {}, handlers: Parameters<typeof mockApi>[0] = {}) {
  const onClose = vi.fn();
  const fetch = mockApi({'GET /api/calendar/activity-types': () => json(ACTIVITIES), ...handlers});
  const result = renderWithApp(
    <SaveShortcutProvider>
      <BlockDialog isOpen onClose={onClose} staffUserId={STAFF_ME.userId} days={DAYS} {...props} />
    </SaveShortcutProvider>,
    {user: staffUser},
  );
  return {...result, fetch, onClose};
}

/** The open dialog, or the whole page until one opens (for `findBy` queries, which wait). */
const dialog = () => within(openDialog() ?? document.body);
const bodyOf = async (fetch: ReturnType<typeof mockApi>, method: string, path: string) => {
  const request = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === method && new URL(r.url).pathname === path)!;
  return request.clone().json();
};

describe('BlockDialog', () => {
  beforeEach(() => {
    vi.useFakeTimers({toFake: ['Date'], now: new Date('2026-11-23T07:00:00Z')});
  });

  it('blocks a selected time for the first activity, with a note', async () => {
    withAnnouncements();
    const {user, fetch, onClose} = renderDialog(
      {date: '2026-11-24', startUtc: ukTime('2026-11-24', '14:00'), endUtc: ukTime('2026-11-24', '15:30')},
      {'POST /api/calendar/blocks': () => json(testBlock('2026-11-24', '14:00', '15:30'))},
    );

    expect(await screen.findByRole('heading', {name: 'Block Time'})).toBeInTheDocument();
    await waitFor(() => expect(dialog().getByRole('combobox', {name: /Activity/})).toHaveTextContent('Lunch'));
    expect(dialog().getByRole('combobox', {name: /Day/})).toHaveTextContent('Tuesday 24 November');
    expect(dialog().getByRole('combobox', {name: /Start/})).toHaveTextContent('14:00');
    expect(dialog().getByRole('combobox', {name: /End/})).toHaveTextContent('15:30');
    expect(dialog().getByText("About the time, for staff. Don't put patient details here.")).toBeInTheDocument();

    await user.click(dialog().getByRole('combobox', {name: /Activity/}));
    expect(screen.queryByRole('option', {name: 'Old'})).not.toBeInTheDocument();
    await user.click(screen.getByRole('option', {name: 'Training'}));
    await user.type(dialog().getByRole('textbox', {name: /Note/}), 'Fire safety');
    await user.click(dialog().getByRole('button', {name: 'Block Time'}));

    await expectAnnounced('Time blocked');
    expect(onClose).toHaveBeenCalled();
    expect(await bodyOf(fetch, 'POST', '/api/calendar/blocks')).toEqual({
      staffUserId: STAFF_ME.userId,
      activityTypeId: 'activity-training',
      startUtc: '2026-11-24T14:00:00.000Z',
      endUtc: '2026-11-24T15:30:00.000Z',
      note: 'Fire safety',
    });
  });

  it('starts on the first day that is not past, keeps the length when the start moves, and flags an end before the start', async () => {
    const {user} = renderDialog({days: DAYS.map((d, i) => (i === 0 ? {...d, isPast: true} : d))});

    await waitFor(() => expect(dialog().getByRole('combobox', {name: /Activity/})).toHaveTextContent('Lunch'));
    expect(dialog().getByRole('combobox', {name: /Day/})).toHaveTextContent('Tuesday 24 November');
    expect(dialog().getByRole('combobox', {name: /Start/})).toHaveTextContent('08:00');
    expect(dialog().getByRole('combobox', {name: /End/})).toHaveTextContent('09:00');

    await user.click(dialog().getByRole('combobox', {name: /Start/}));
    await user.click(screen.getByRole('option', {name: '10:00'}));
    expect(dialog().getByRole('combobox', {name: /End/})).toHaveTextContent('11:00');

    await user.click(dialog().getByRole('combobox', {name: /End/}));
    await user.click(screen.getByRole('option', {name: '09:30'}));
    expect(dialog().getByText('Ends before it starts.')).toBeInTheDocument();
    expect(isDisabled(dialog().getByRole('button', {name: 'Block Time'}))).toBe(true);

    await user.click(dialog().getByRole('combobox', {name: /Day/}));
    expect(screen.queryByRole('option', {name: 'Monday 23 November'})).not.toBeInTheDocument();
    expect(screen.queryByRole('option', {name: 'Saturday 28 November'})).not.toBeInTheDocument();
    await user.click(screen.getByRole('option', {name: 'Thursday 26 November'}));
    expect(dialog().getByRole('combobox', {name: /Day/})).toHaveTextContent('Thursday 26 November');
  });

  it('saves or deletes a stored block, keeping a time that is not on a 15-minute step', async () => {
    withAnnouncements();
    const block = testBlock(MONDAY, '09:05', '10:00', {blockId: 'block-1', note: 'Catch up'});
    const {user, fetch} = renderDialog(
      {block},
      {'PUT /api/calendar/blocks/block-1': () => json(block), 'DELETE /api/calendar/blocks/block-1': () => new Response(null, {status: 204})},
    );

    expect(await screen.findByRole('heading', {name: 'Change Blocked Time'})).toBeInTheDocument();
    expect(dialog().getByRole('combobox', {name: /Start/})).toHaveTextContent('09:05');
    expect(dialog().getByRole('textbox', {name: /Note/})).toHaveValue('Catch up');
    await waitFor(() => expect(dialog().getByRole('combobox', {name: /Activity/})).toHaveTextContent('Lunch'));
    await user.click(dialog().getByRole('button', {name: 'Save'}));
    await expectAnnounced('Blocked time saved');
    expect(await bodyOf(fetch, 'PUT', '/api/calendar/blocks/block-1')).toEqual({
      activityTypeId: 'activity-lunch',
      startUtc: '2026-11-23T09:05:00.000Z',
      endUtc: '2026-11-23T10:00:00.000Z',
      note: 'Catch up',
    });

    await user.click(dialog().getByRole('button', {name: 'Delete'}));
    await expectAnnounced('Blocked time deleted');
  });

  it("changes or removes a template block for its day only, naming its template", async () => {
    withAnnouncements();
    const date = '2026-11-25';
    const block = testBlock(date, '12:00', '13:00', {isFromTemplate: true, templateBlockId: 'tb-1', overlapsAppointment: true});
    const path = `/api/calendar/staff/${STAFF_ME.userId}/days/${date}/template-blocks/tb-1`;
    const {user, fetch} = renderDialog({block}, {[`PUT ${path}`]: () => json(block), [`DELETE ${path}`]: () => new Response(null, {status: 204})});

    expect(await dialog().findByText("From the 'Standard day' day template")).toBeInTheDocument();
    expect(dialog().getByText('Changes here apply to this day only; the template stays as it is.')).toBeInTheDocument();
    expect(dialog().getByText(/This overlaps an appointment/)).toBeInTheDocument();
    expect(isDisabled(dialog().getByRole('combobox', {name: /Day/}))).toBe(true);

    await user.click(dialog().getByRole('combobox', {name: /End/}));
    await user.click(screen.getByRole('option', {name: '12:30'}));
    await user.click(dialog().getByRole('button', {name: 'Change for This Day'}));
    await expectAnnounced('Changed for this day');
    expect(await bodyOf(fetch, 'PUT', path)).toMatchObject({endUtc: '2026-11-25T12:30:00.000Z'});

    await user.click(dialog().getByRole('button', {name: 'Remove from This Day'}));
    await expectAnnounced('Removed from this day');
  });

  it("shows a block that can't be changed, without changing anything", async () => {
    const block = testBlock(MONDAY, '12:00', '13:00', {note: 'Team lunch', isFromTemplate: true, overlapsAppointment: true});
    const {user, onClose} = renderDialog({block, canEdit: false, days: DAYS.map((d) => ({...d, dayTemplateName: null}))});

    expect(await screen.findByRole('heading', {name: 'Blocked Time'})).toBeInTheDocument();
    expect(dialog().getByText('From a day template')).toBeInTheDocument();
    expect(dialog().getByText('Monday 23 November, 12:00 to 13:00')).toBeInTheDocument();
    expect(dialog().getByText('Team lunch')).toBeInTheDocument();
    expect(dialog().queryByRole('button', {name: /Save|Change/})).not.toBeInTheDocument();
    await user.click(dialog().getAllByRole('button', {name: 'Close'}).at(-1)!);
    expect(onClose).toHaveBeenCalled();
  });

  it("shows the server's clash message and field messages in the dialog", async () => {
    let answer = json({title: 'That time overlaps an appointment, 09:00 to 09:30.'}, 409);
    const {user} = renderDialog(
      {date: MONDAY, startUtc: ukTime(MONDAY, '09:00'), endUtc: ukTime(MONDAY, '10:00')},
      {'POST /api/calendar/blocks': () => answer},
    );

    await waitFor(() => expect(dialog().getByRole('combobox', {name: /Activity/})).toHaveTextContent('Lunch'));
    await user.click(dialog().getByRole('button', {name: 'Block Time'}));
    expect(await dialog().findByText('That time overlaps an appointment, 09:00 to 09:30.')).toBeInTheDocument();

    answer = json({title: 'One or more validation errors occurred.', errors: {Note: ['The note is too long.'], StaffUserId: ['Unknown.']}}, 400);
    await user.click(dialog().getByRole('button', {name: 'Block Time'}));
    expect(await dialog().findByText('The note is too long.')).toBeInTheDocument();
    expect(dialog().getByText('Unknown.')).toBeInTheDocument();
  });

  it('starts afresh each time it opens', async () => {
    mockApi({'GET /api/calendar/activity-types': () => json(ACTIVITIES)});
    function Harness() {
      const [isOpen, setIsOpen] = useState(true);
      return (
        <>
          <button type="button" onClick={() => setIsOpen(true)}>
            Open
          </button>
          <BlockDialog isOpen={isOpen} onClose={() => setIsOpen(false)} staffUserId={STAFF_ME.userId} days={DAYS} date={MONDAY} />
        </>
      );
    }
    const {user} = renderWithApp(<Harness />, {user: staffUser});

    await user.type(await dialog().findByRole('textbox', {name: /Note/}), 'Draft');
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());
    await user.click(screen.getByRole('button', {name: 'Open'}));
    expect(await dialog().findByRole('textbox', {name: /Note/})).toHaveValue('');
  });

  it('saves with Ctrl+S when it can', async () => {
    const {fetch} = renderDialog({date: MONDAY}, {'POST /api/calendar/blocks': () => json(testBlock(MONDAY, '08:00', '09:00'))});

    const note = await dialog().findByRole('textbox', {name: /Note/});
    await waitFor(() => expect(dialog().getByRole('combobox', {name: /Activity/})).toHaveTextContent('Lunch'));
    note.focus();
    fireEvent.keyDown(note, {key: 's', ctrlKey: true});
    await waitFor(() => expect(fetch.mock.calls.some(([r]) => (r as Request).method === 'POST')).toBe(true));
  });
});
