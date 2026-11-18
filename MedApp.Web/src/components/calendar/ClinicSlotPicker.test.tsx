import {screen, within} from '@testing-library/react';
import {SPECIALTY, testSession} from '../../test/clinics';
import {calls, json, mockApi, renderWithApp, staffUser} from '../../test/render';
import {ClinicSlotPicker, sessionHeading, slotOf} from './ClinicSlotPicker';

const SESSIONS = '/api/clinics/sessions';

function renderPicker(props: Partial<Parameters<typeof ClinicSlotPicker>[0]> = {}, sessions = [testSession({booked: 7})], status = 200) {
  const onChoose = vi.fn();
  const fetch = mockApi({
    [`GET ${SESSIONS}`]: () => json(status === 200 ? sessions : {title: 'Error'}, status),
    'GET /api/specialties': () => json([SPECIALTY]),
  });
  const result = renderWithApp(<ClinicSlotPicker from="2026-11-23" to="2026-12-20" onChoose={onChoose} {...props} />, {user: staffUser});
  return {...result, fetch, onChoose};
}

describe('ClinicSlotPicker', () => {
  beforeEach(() => {
    vi.useFakeTimers({toFake: ['Date'], now: new Date('2026-11-23T10:00:00Z')});
  });

  it("lists each session's free slots, and chooses one", async () => {
    const {user, onChoose, fetch} = renderPicker();

    const group = await screen.findByRole('group', {name: 'Tue 24, test-user, Room 2, 2 of 9 free'});
    expect(within(group).getByText('Synthetic dermatology')).toBeInTheDocument();
    await user.click(within(group).getByRole('button', {name: '11:40'}));

    expect(onChoose).toHaveBeenCalledWith({
      clinicSessionId: 'session-1',
      staffUserId: expect.any(String),
      staffUsername: 'test-user',
      roomName: 'Room 2',
      startUtc: '2026-11-24T11:40:00Z',
      endUtc: '2026-11-24T12:00:00.000Z',
    });
    expect(calls(fetch)).toEqual([`GET ${SESSIONS}?From=2026-11-23&To=2026-12-20&WithFreeSlots=true`]);
  });

  it('shows the chosen slot pressed', async () => {
    const session = testSession({booked: 7});
    renderPicker({chosen: slotOf(session, '2026-11-24T11:40:00Z')}, [session]);
    expect(await screen.findByRole('button', {name: '11:40', pressed: true})).toBeInTheDocument();
    expect(screen.getByRole('button', {name: '11:20', pressed: false})).toBeInTheDocument();
  });

  it("asks for the specialty's sessions only, and says when there are none", async () => {
    const {fetch} = renderPicker({specialtyId: SPECIALTY.id}, []);
    expect(await screen.findByText('No Synthetic dermatology clinic sessions have a free slot in these weeks.')).toBeInTheDocument();
    expect(calls(fetch)).toContain(`GET ${SESSIONS}?From=2026-11-23&To=2026-12-20&SpecialtyId=${SPECIALTY.id}&WithFreeSlots=true`);
  });

  it('says when there are none at all, or they could not be loaded', async () => {
    const {unmount} = renderPicker({}, []);
    expect(await screen.findByText('No clinic sessions have a free slot in these weeks.')).toBeInTheDocument();
    unmount();
    renderPicker({}, [], 500);
    expect(await screen.findByText('The clinic sessions could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('heads a session with its day, clinician, room and how full it is', () => {
    expect(sessionHeading(testSession({booked: 9}))).toBe('Tue 24, test-user, Room 2, Full');
  });
});
