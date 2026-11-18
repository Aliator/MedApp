import {screen, waitFor, within} from '@testing-library/react';
import {useState} from 'react';
import {STAFF_ME} from '../../test/calendar';
import {ROOM, TEAM, testPattern} from '../../test/clinics';
import {expectAnnounced, isDisabled, json, managerUser, mockApi, openDialog, renderWithApp, withAnnouncements} from '../../test/render';
import {PatternFormDialog, type PatternFormDialogProps} from './PatternFormDialog';

function renderDialog(props: Partial<PatternFormDialogProps> = {}, handlers: Parameters<typeof mockApi>[0] = {}) {
  const onClose = vi.fn();
  const fetch = mockApi({
    'GET /api/calendar/staff': () => json([{userId: STAFF_ME.userId, username: STAFF_ME.username}]),
    'GET /api/rooms': () => json([ROOM]),
    'GET /api/teams': () => json([TEAM]),
    ...handlers,
  });
  const result = renderWithApp(<PatternFormDialog isOpen onClose={onClose} {...props} />, {user: managerUser});
  return {...result, fetch, onClose};
}

const dialog = () => within(openDialog() ?? document.body);
const choose = async (user: ReturnType<typeof renderDialog>['user'], field: RegExp, option: string) => {
  await user.click(dialog().getByRole('combobox', {name: field}));
  await user.click(await screen.findByRole('option', {name: option}));
};
const bodyOf = (fetch: ReturnType<typeof mockApi>, method: string) =>
  fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === method)!.clone().json();

describe('PatternFormDialog', {timeout: 30_000}, () => {
  beforeEach(() => {
    vi.useFakeTimers({toFake: ['Date'], now: new Date('2026-11-23T10:00:00Z')});
  });

  it('adds a pattern with its day, times, slots and how often', async () => {
    withAnnouncements();
    const {user, fetch, onClose} = renderDialog({}, {'POST /api/clinics/patterns': () => json(testPattern(), 201)});

    expect(await screen.findByRole('heading', {name: 'New Clinic Pattern'})).toBeInTheDocument();
    await waitFor(() => expect(dialog().getByRole('combobox', {name: /Clinician/})).not.toHaveAttribute('aria-disabled', 'true'));
    await choose(user, /Clinician/, STAFF_ME.username);
    await choose(user, /^Day/, 'Tuesday');
    await choose(user, /Every/, '2 weeks');
    await choose(user, /Room/, 'Room 2 (Main site)');
    await choose(user, /Team/, TEAM.name);
    await user.click(dialog().getByRole('button', {name: 'Save'}));

    await expectAnnounced('Clinic pattern added');
    expect(onClose).toHaveBeenCalled();
    expect(await bodyOf(fetch, 'POST')).toEqual({
      staffUserId: STAFF_ME.userId,
      roomId: ROOM.id,
      teamId: TEAM.id,
      dayOfWeek: 'Tuesday',
      starts: '09:00',
      ends: '12:00',
      slotMinutes: 20,
      everyWeeks: 2,
      fromDate: '2026-11-23',
      untilDate: null,
    });
  });

  it('won’t save times that end before they start, or don’t divide into slots', async () => {
    const {user} = renderDialog({pattern: testPattern()});
    await waitFor(() => expect(dialog().getByRole('combobox', {name: /Room/})).toHaveTextContent('Room 2 (Main site)'));
    await choose(user, /Ends/, '08:00');
    expect(dialog().getByText('It must end after it starts.')).toBeInTheDocument();
    expect(isDisabled(dialog().getByRole('button', {name: 'Save'}))).toBe(true);
    await choose(user, /Ends/, '10:15');
    expect(dialog().getByText('It must divide into whole 20-minute slots.')).toBeInTheDocument();
  });

  it("changes a pattern from the version read, and shows the API's words", async () => {
    const pattern = testPattern();
    const {user, fetch} = renderDialog({pattern}, {'PUT /api/clinics/patterns/pattern-1': () => json({title: 'Someone changed this clinic pattern.'}, 409)});

    expect(await screen.findByRole('heading', {name: 'Edit Clinic Pattern'})).toBeInTheDocument();
    await waitFor(() => expect(dialog().getByRole('combobox', {name: /Team/})).toHaveTextContent(TEAM.name));
    await user.click(dialog().getByRole('button', {name: 'Save'}));

    expect(await dialog().findByText('Someone changed this clinic pattern.')).toBeInTheDocument();
    expect(await bodyOf(fetch, 'PUT')).toMatchObject({dayOfWeek: 'Tuesday', starts: '09:00', ends: '12:00', expectedLastUpdated: pattern.lastUpdated});
  });

  it("shows the API's 400, with a new slot length, and starts afresh when reopened", async () => {
    mockApi({
      'GET /api/calendar/staff': () => json([]),
      'GET /api/rooms': () => json([ROOM]),
      'GET /api/teams': () => json([TEAM]),
      'PUT /api/clinics/patterns/pattern-1': () =>
        json({title: 'One or more validation errors occurred.', status: 400, errors: {EveryWeeks: ['Every must be 1 to 4 weeks.']}}, 400),
    });
    function Harness() {
      const [isOpen, setIsOpen] = useState(true);
      return (
        <>
          <button type="button" onClick={() => setIsOpen(true)}>
            Reopen
          </button>
          <PatternFormDialog isOpen={isOpen} onClose={() => setIsOpen(false)} pattern={testPattern()} />
        </>
      );
    }
    const {user} = renderWithApp(<Harness />, {user: managerUser});
    await waitFor(() => expect(dialog().getByRole('combobox', {name: /Team/})).toHaveTextContent(TEAM.name));
    await choose(user, /Slot length/, '30 minutes');
    await user.click(dialog().getByRole('button', {name: 'Save'}));
    expect(await dialog().findAllByText('Every must be 1 to 4 weeks.')).not.toHaveLength(0);

    await user.click(dialog().getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());
    await user.click(screen.getByRole('button', {name: 'Reopen'}));
    await waitFor(() => expect(dialog().queryByText('Every must be 1 to 4 weeks.')).not.toBeInTheDocument());
  });
});
