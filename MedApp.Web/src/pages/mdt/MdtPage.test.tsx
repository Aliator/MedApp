import {screen, waitFor} from '@testing-library/react';
import {QueryClient} from '@tanstack/react-query';
import {mdtItem, mdtMeeting} from '../../test/mdt';
import {calls, json, mockApi, renderWithApp, staffUser} from '../../test/render';
import {meetingLabel} from '../../components/mdt/MdtDialogs';
import {MdtPage, preloadMdtPage, shownMeeting} from './MdtPage';
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

describe('MdtPage, choosing and failing', () => {
  const specialtyId = mdtMeeting().specialtyId!;

  it('puts the specialty and the meeting chosen in the address, and every specialty back', async () => {
    mockApi({
      'GET /api/specialties': () => json([{id: specialtyId, name: 'Dermatology'}]),
      'GET /api/mdt/meetings': () => json([mdtMeeting(), later]),
      [`GET /api/mdt/meetings/${mdtMeeting().id}/items`]: () => json({meeting: mdtMeeting(), items: []}),
      'GET /api/mdt/awaiting-meeting': () => json([]),
    });
    const onSearchChange = vi.fn();
    const {user} = renderWithApp(<MdtPage search={{specialty: specialtyId}} onSearchChange={onSearchChange} />, {user: staffUser});

    await user.click(await screen.findByRole('combobox', {name: 'Specialty'}));
    await user.click(await screen.findByRole('option', {name: 'Every specialty'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({specialty: undefined, meeting: undefined});

    await user.click(screen.getByRole('combobox', {name: 'Specialty'}));
    await user.click(await screen.findByRole('option', {name: 'Dermatology'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({specialty: specialtyId, meeting: undefined});

    await user.click(screen.getByRole('combobox', {name: 'Meeting'}));
    await user.click(await screen.findByRole('option', {name: meetingLabel(later)}));
    expect(onSearchChange).toHaveBeenLastCalledWith({specialty: specialtyId, meeting: later.id});
  });

  it('says when there is no MDT, or the MDTs could not be loaded', async () => {
    mockApi({
      'GET /api/specialties': () => json([]),
      'GET /api/mdt/meetings': () => json({title: 'Synthetic failure.', status: 500}, 500),
      'GET /api/mdt/awaiting-meeting': () => json([]),
    });
    renderWithApp(<MdtPage search={{}} onSearchChange={vi.fn()} />, {user: staffUser});

    expect(await screen.findByText(/No MDTs in the last 60 days/)).toBeInTheDocument();
    expect(await screen.findByText(/Synthetic failure|The MDTs could not be loaded/)).toBeInTheDocument();
  });

  it("says when a meeting's list could not be loaded", async () => {
    mockApi({
      'GET /api/specialties': () => json([]),
      'GET /api/mdt/meetings': () => json([mdtMeeting()]),
      [`GET /api/mdt/meetings/${mdtMeeting().id}/items`]: () => json({title: 'Synthetic list failure.', status: 500}, 500),
      'GET /api/mdt/awaiting-meeting': () => json([]),
    });
    renderWithApp(<MdtPage search={{}} onSearchChange={vi.fn()} />, {user: staffUser});

    expect(await screen.findByText(/Synthetic list failure|The list could not be loaded/)).toBeInTheDocument();
  });

  it('preloads the specialties and the MDTs', async () => {
    mockApi({'GET /api/specialties': () => json([]), 'GET /api/mdt/meetings': () => json([])});
    const queryClient = new QueryClient();
    const spy = vi.spyOn(queryClient, 'prefetchQuery');
    await preloadMdtPage(queryClient, {});
    expect(spy).toHaveBeenCalledTimes(2);
  });
});
