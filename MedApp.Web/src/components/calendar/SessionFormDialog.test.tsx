import {screen, waitFor, within} from '@testing-library/react';
import {STAFF_ME, testCalendar, ukTime} from '../../test/calendar';
import {ROOM, SPECIALTY, testSession} from '../../test/clinics';
import {expectAnnounced, isDisabled, json, managerUser, mockApi, openDialog, renderWithApp, withAnnouncements} from '../../test/render';
import {SessionFormDialog, type SessionFormDialogProps} from './SessionFormDialog';

const DAY = '2026-11-24';

function renderDialog(props: Partial<SessionFormDialogProps> = {}, handlers: Parameters<typeof mockApi>[0] = {}) {
  const onClose = vi.fn();
  const fetch = mockApi({
    'GET /api/calendar/staff': () => json([{userId: STAFF_ME.userId, username: STAFF_ME.username}]),
    'GET /api/rooms': () => json([ROOM]),
    'GET /api/specialties': () => json([SPECIALTY]),
    'GET /api/calendar': () => json(testCalendar({from: DAY, dayCount: 1, today: '2026-11-23'})),
    ...handlers,
  });
  const result = renderWithApp(<SessionFormDialog isOpen onClose={onClose} date={DAY} {...props} />, {user: managerUser});
  return {...result, fetch, onClose};
}

const dialog = () => within(openDialog() ?? document.body);
const choose = async (user: ReturnType<typeof renderDialog>['user'], field: RegExp, option: string) => {
  await user.click(dialog().getByRole('combobox', {name: field}));
  await user.click(await screen.findByRole('option', {name: option}));
};
const bodyOf = (fetch: ReturnType<typeof mockApi>, method: string) =>
  fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === method)!.clone().json();

describe('SessionFormDialog', {timeout: 30_000}, () => {
  beforeEach(() => {
    vi.useFakeTimers({toFake: ['Date'], now: new Date('2026-11-23T10:00:00Z')});
  });

  it('adds a session inside the clinician’s working day, in whole slots', async () => {
    withAnnouncements();
    const {user, fetch, onClose} = renderDialog({}, {'POST /api/clinics/sessions': () => json(testSession(), 201)});

    expect(await screen.findByRole('heading', {name: 'New Clinic Session'})).toBeInTheDocument();
    await waitFor(() => expect(dialog().getByRole('combobox', {name: /Clinician/})).not.toHaveAttribute('aria-disabled', 'true'));
    await choose(user, /Clinician/, STAFF_ME.username);
    await waitFor(() => expect(isDisabled(dialog().getByRole('combobox', {name: /Start/}))).toBe(false));
    await choose(user, /Start/, '09:00');
    await choose(user, /End/, '12:00');
    expect(dialog().getByText('9 slots')).toBeInTheDocument();
    await choose(user, /Room/, 'Room 2 (Main site)');
    await choose(user, /Specialty/, SPECIALTY.name);
    await user.click(dialog().getByRole('button', {name: 'Save'}));

    await expectAnnounced('Clinic session added');
    expect(onClose).toHaveBeenCalled();
    expect(await bodyOf(fetch, 'POST')).toEqual({
      staffUserId: STAFF_ME.userId,
      roomId: ROOM.id,
      specialtyId: SPECIALTY.id,
      startUtc: ukTime(DAY, '09:00').replace('Z', '.000Z'),
      endUtc: ukTime(DAY, '12:00').replace('Z', '.000Z'),
      slotMinutes: 20,
    });
  });

  it('says when the slots don’t divide the session, and won’t save', async () => {
    const {user} = renderDialog();
    await waitFor(() => expect(dialog().getByRole('combobox', {name: /Clinician/})).not.toHaveAttribute('aria-disabled', 'true'));
    await choose(user, /Clinician/, STAFF_ME.username);
    await waitFor(() => expect(isDisabled(dialog().getByRole('combobox', {name: /Start/}))).toBe(false));
    await choose(user, /Start/, '09:00');
    await choose(user, /End/, '10:15');
    expect(dialog().getByText('The session must divide into whole 20-minute slots.')).toBeInTheDocument();
    expect(isDisabled(dialog().getByRole('button', {name: 'Save'}))).toBe(true);
  });

  it("changes a session from the version read, keeping its clinician, and shows the API's 409", async () => {
    const session = testSession({booked: 1});
    const {user, fetch} = renderDialog(
      {session},
      {'PUT /api/clinics/sessions/session-1': () => json({title: 'This clinic session has appointments booked into it.'}, 409)},
    );

    expect(await screen.findByRole('heading', {name: 'Edit Clinic Session'})).toBeInTheDocument();
    await waitFor(() => expect(dialog().getByRole('combobox', {name: /Room/})).toHaveTextContent('Room 2 (Main site)'));
    expect(isDisabled(dialog().getByRole('combobox', {name: /Clinician/}))).toBe(true);
    await waitFor(() => expect(dialog().getByRole('combobox', {name: /Specialty/})).toHaveTextContent(SPECIALTY.name));
    await user.click(dialog().getByRole('button', {name: 'Save'}));

    expect(await dialog().findByText('This clinic session has appointments booked into it.')).toBeInTheDocument();
    expect(await bodyOf(fetch, 'PUT')).toMatchObject({roomId: ROOM.id, expectedLastUpdated: session.lastUpdated, slotMinutes: 20});
  });

  it('says when the clinician doesn’t work that day', async () => {
    const {user} = renderDialog(
      {},
      {'GET /api/calendar': () => json(testCalendar({from: '2026-11-28', dayCount: 1, today: '2026-11-23'}))},
    );
    await waitFor(() => expect(dialog().getByRole('combobox', {name: /Clinician/})).not.toHaveAttribute('aria-disabled', 'true'));
    await choose(user, /Clinician/, STAFF_ME.username);
    await waitFor(() => expect(isDisabled(dialog().getByRole('combobox', {name: /Start/}))).toBe(true));
  });
});
