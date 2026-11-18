import {screen, waitFor} from '@testing-library/react';
import {mdtItem, mdtMeeting} from '../../test/mdt';
import {calls, json, mockApi, renderWithApp, staffUser} from '../../test/render';
import {meetingLabel} from '../../components/mdt/MdtDialogs';
import {MdtPage, shownMeeting} from './MdtPage';
import {parseMdtSearch} from './mdtSearch';

const later = mdtMeeting({id: '00000000-0000-4000-8000-0000000000a2', startUtc: '2099-12-10T13:00:00Z', endUtc: '2099-12-10T14:00:00Z'});

describe('MdtPage', () => {
  it("shows the next MDT's list, with each patient, and items whose meeting was cancelled", async () => {
    const fetch = mockApi({
      'GET /api/specialties': () => json([{id: mdtMeeting().specialtyId, name: 'Dermatology'}]),
      'GET /api/mdt/meetings': () => json([later, mdtMeeting()]),
      [`GET /api/mdt/meetings/${mdtMeeting().id}/items`]: () => json({meeting: mdtMeeting(), items: [mdtItem()]}),
      'GET /api/mdt/awaiting-meeting': () =>
        json([mdtItem({id: 'c2', pathwayTitle: 'Leg ulcer', meetingId: null, meetingStartUtc: null, meetingCancelledStartUtc: '2099-11-26T13:00:00Z'})]),
    });
    renderWithApp(<MdtPage search={{}} onSearchChange={vi.fn()} />, {user: staffUser});

    expect((await screen.findAllByText(meetingLabel(mdtMeeting()))).length).toBeGreaterThan(0);
    expect(await screen.findByRole('link', {name: 'Skin lesion'})).toBeInTheDocument();
    expect(await screen.findByText('Meeting cancelled: list again')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Leg ulcer'})).toBeInTheDocument();
    expect(calls(fetch)).not.toContain(`GET /api/mdt/meetings/${later.id}/items`);
  });

  it('prints the list', async () => {
    mockApi({
      'GET /api/specialties': () => json([]),
      'GET /api/mdt/meetings': () => json([mdtMeeting()]),
      [`GET /api/mdt/meetings/${mdtMeeting().id}/items`]: () => json({meeting: mdtMeeting(), items: []}),
      'GET /api/mdt/awaiting-meeting': () => json([]),
    });
    const print = vi.spyOn(window, 'print').mockImplementation(() => {});
    const {user} = renderWithApp(<MdtPage search={{meeting: mdtMeeting().id}} onSearchChange={vi.fn()} />, {user: staffUser});

    expect(await screen.findByText(/Nobody is listed for this MDT yet/)).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole('button', {name: 'Print'})).not.toHaveAttribute('aria-disabled', 'true'));
    await user.click(screen.getByRole('button', {name: 'Print'}));
    expect(print).toHaveBeenCalled();
    print.mockRestore();
  });
});

describe('shownMeeting', () => {
  it('is the one chosen, or the soonest not yet ended', () => {
    const now = new Date('2026-11-18T12:00:00Z');
    expect(shownMeeting([later, mdtMeeting()], {}, now)?.id).toBe(mdtMeeting().id);
    expect(shownMeeting([later, mdtMeeting()], {meeting: later.id}, now)?.id).toBe(later.id);
    expect(shownMeeting([], {}, now)).toBeUndefined();
  });
});

describe('parseMdtSearch', () => {
  it('keeps IDs and drops anything else', () => {
    expect(parseMdtSearch({specialty: mdtMeeting().specialtyId, meeting: 'nonsense'})).toEqual({specialty: mdtMeeting().specialtyId});
  });
});
