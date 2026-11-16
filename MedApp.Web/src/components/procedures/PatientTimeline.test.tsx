import {screen, waitFor, within} from '@testing-library/react';
import {useState} from 'react';
import {NO_TIMELINE_FILTERS, type TimelineEntry, type TimelineFilters, type TimelineOverview} from '../../api/timeline';
import {adminUser, calls, findAlert, json, mockApi, renderWithApp, staffUser} from '../../test/render';
import {PatientTimeline} from './PatientTimeline';

const patientId = '00000032-0000-0000-0000-000000000001';
const procedureId = '00000032-0000-0000-0000-000000000002';
const otherProcedureId = '00000032-0000-0000-0000-000000000003';
const deletedProcedureId = '00000032-0000-0000-0000-000000000004';
const TIMELINE = `/api/patients/${patientId}/timeline`;
const OVERVIEW = `${TIMELINE}/overview`;

let next = 0;
const entry = (fields: Partial<TimelineEntry>): TimelineEntry => ({
  id: `00000032-0000-0000-0001-${String(++next).padStart(12, '0')}`,
  kind: 'Stages',
  action: 'StageFinished',
  atUtc: '2026-06-16T09:05:00Z',
  username: 'test.staff',
  procedureId,
  procedureTitle: 'Knee assessment',
  isProcedureDeleted: false,
  stageName: 'Consultation',
  changes: [],
  ...fields,
});

const overview: TimelineOverview = {
  upcoming: [
    {procedureId: otherProcedureId, title: 'Overdue scan', status: 'InProgress', priority: 'High', dueDate: '2026-06-10', isOverdue: true},
    {procedureId, title: 'Knee assessment', status: 'Assigned', priority: 'Low', dueDate: '2026-06-30', isOverdue: false},
  ],
  openCount: 4,
  procedures: [
    {id: procedureId, title: 'Knee assessment', isDeleted: false},
    {id: otherProcedureId, title: 'Overdue scan', isDeleted: false},
  ],
};

/** The timeline, with its filters held as a page holds them. */
function Harness({initial = NO_TIMELINE_FILTERS}: {initial?: TimelineFilters}) {
  const [filters, setFilters] = useState(initial);
  return <PatientTimeline patientId={patientId} filters={filters} onFiltersChange={setFilters} />;
}

describe('PatientTimeline', () => {
  it("shows what's due, overdue first and flagged, with a link to the rest", async () => {
    mockApi({[`GET ${OVERVIEW}`]: () => json(overview), [`GET ${TIMELINE}`]: () => json({items: [], nextCursor: null})});
    renderWithApp(<Harness />, {user: staffUser});

    const scan = await screen.findByRole('link', {name: 'Overdue scan'});
    expect(scan).toHaveAttribute('href', `/procedures/${otherProcedureId}`);
    expect(screen.getByText('Due 10 Jun 2026')).toBeInTheDocument();
    expect(screen.getByText('Overdue')).toBeInTheDocument();
    expect(screen.getByText('In progress')).toBeInTheDocument();
    expect(screen.getByText(/And 2 more/)).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'See all procedures'})).toHaveAttribute('href', `/patients/${patientId}/procedures`);
  });

  it('says when nothing is due', async () => {
    mockApi({
      [`GET ${OVERVIEW}`]: () => json({upcoming: [], openCount: 0, procedures: []}),
      [`GET ${TIMELINE}`]: () => json({items: [], nextCursor: null}),
    });
    renderWithApp(<Harness />, {user: staffUser});
    expect(await screen.findByText('Nothing is due: the patient has no open procedures.')).toBeInTheDocument();
  });

  it("says when what's due could not be loaded", async () => {
    mockApi({[`GET ${OVERVIEW}`]: () => json({title: 'Down'}, 500), [`GET ${TIMELINE}`]: () => json({items: [], nextCursor: null})});
    renderWithApp(<Harness />, {user: staffUser});

    expect(await findAlert("What's due could not be loaded.")).toBeInTheDocument();
  });

  it('shows the feed by UK day: what happened, to which procedure, when and by whom', async () => {
    mockApi({
      [`GET ${OVERVIEW}`]: () => json(overview),
      [`GET ${TIMELINE}`]: () =>
        json({
          items: [
            entry({atUtc: '2026-06-16T09:05:00Z'}),
            entry({action: 'Assigned', kind: 'Assignments', assigneeUsername: 'other.staff', atUtc: '2026-06-15T23:30:00Z'}),
            entry({action: 'Created', kind: 'Created', templateName: 'Knee template', atUtc: '2026-06-15T10:00:00Z', username: 'test.admin'}),
          ],
          nextCursor: null,
        }),
    });
    renderWithApp(<Harness />, {user: staffUser});

    // 23:30 UTC on the 15th is 00:30 on the 16th in the UK, so it's with the first.
    const today = within(await screen.findByRole('list', {name: 'Tuesday 16 June 2026'}));
    expect(today.getByText('Finished Consultation')).toBeInTheDocument();
    expect(today.getByText('Assigned other.staff')).toBeInTheDocument();
    expect(today.getByText('10:05 · test.staff')).toBeInTheDocument();
    expect(today.getByText('00:30 · test.staff')).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Monday 15 June 2026'})).toBeInTheDocument();
    expect(screen.getByText('Procedure created')).toBeInTheDocument();
    expect(screen.getByText('From Knee template')).toBeInTheDocument();
    expect(screen.getByText('11:00 · test.admin')).toBeInTheDocument();
    expect(screen.getAllByRole('link', {name: 'Knee assessment'}).every((link) => link.getAttribute('href') === `/procedures/${procedureId}`)).toBe(
      true,
    );
    expect(screen.queryByRole('button', {name: 'Load More'})).not.toBeInTheDocument();
  });

  it("labels a deleted procedure's entries, without a link", async () => {
    mockApi({
      [`GET ${OVERVIEW}`]: () => json({...overview, procedures: [...overview.procedures!, {id: deletedProcedureId, title: 'Old biopsy', isDeleted: true}]}),
      [`GET ${TIMELINE}`]: () =>
        json({
          items: [entry({action: 'Deleted', kind: 'Deleted', procedureId: deletedProcedureId, procedureTitle: 'Old biopsy', isProcedureDeleted: true, fromStatus: 'Cancelled'})],
          nextCursor: null,
        }),
    });
    renderWithApp(<Harness />, {user: adminUser});

    const row = within((await screen.findByText('Procedure deleted')).closest('li')!);
    expect(row.getByText('Was cancelled')).toBeInTheDocument();
    expect(row.getByText('Deleted')).toBeInTheDocument();
    expect(row.getByText('Old biopsy')).toBeInTheDocument();
    expect(screen.queryByRole('link', {name: 'Old biopsy'})).not.toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Old biopsy (deleted)'})).toBeInTheDocument();
  });

  it('loads older entries after the last page, and stops when there are none', async () => {
    const fetch = mockApi({
      [`GET ${OVERVIEW}`]: () => json(overview),
      [`GET ${TIMELINE}`]: (request) =>
        new URL(request.url).searchParams.get('Before') === 'cursor-1'
          ? json({items: [entry({action: 'StageStarted', atUtc: '2026-06-01T09:00:00Z'})], nextCursor: null})
          : json({items: [entry({})], nextCursor: 'cursor-1'}),
    });
    const {user} = renderWithApp(<Harness />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: 'Load More'}));

    expect(await screen.findByText('Started Consultation')).toBeInTheDocument();
    expect(screen.getByText('Finished Consultation')).toBeInTheDocument();
    expect(calls(fetch)).toContain(`GET ${TIMELINE}?PageSize=20&Before=cursor-1`);
    await waitFor(() => expect(screen.queryByRole('button', {name: 'Load More'})).not.toBeInTheDocument());
  });

  it('says when older entries could not be loaded', async () => {
    mockApi({
      [`GET ${OVERVIEW}`]: () => json(overview),
      [`GET ${TIMELINE}`]: (request) =>
        new URL(request.url).searchParams.has('Before') ? json({title: 'Down'}, 500) : json({items: [entry({})], nextCursor: 'cursor-1'}),
    });
    const {user} = renderWithApp(<Harness />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: 'Load More'}));

    expect(await findAlert('Older entries could not be loaded. Please try again.')).toBeInTheDocument();
    expect(screen.getByText('Finished Consultation')).toBeInTheDocument();
  });

  it('filters by kind and by procedure, and shows everything again', async () => {
    const fetch = mockApi({[`GET ${OVERVIEW}`]: () => json(overview), [`GET ${TIMELINE}`]: () => json({items: [entry({})], nextCursor: null})});
    const {user} = renderWithApp(<Harness />, {user: staffUser});

    const notes = await screen.findByRole('button', {name: 'Notes'});
    await user.click(notes);
    await user.click(await screen.findByRole('button', {name: 'Overdue scan'}));

    await waitFor(() => expect(calls(fetch)).toContain(`GET ${TIMELINE}?PageSize=20&Kinds=Notes&ProcedureIds=${otherProcedureId}`));
    expect(notes).toHaveAttribute('aria-pressed', 'true');

    await user.click(screen.getByRole('button', {name: 'Show everything'}));

    await waitFor(() => expect(screen.getByRole('button', {name: 'Notes'})).toHaveAttribute('aria-pressed', 'false'));
    expect(screen.queryByRole('button', {name: 'Show everything'})).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', {name: 'Notes'}));
    await user.click(screen.getByRole('button', {name: 'Notes'}));
    await waitFor(() => expect(screen.getByRole('button', {name: 'Notes'})).toHaveAttribute('aria-pressed', 'false'));
  });

  it('offers the Deleted filter to admins only, and procedure filters only when there is more than one', async () => {
    mockApi({
      [`GET ${OVERVIEW}`]: () => json({...overview, procedures: [overview.procedures![0]]}),
      [`GET ${TIMELINE}`]: () => json({items: [entry({})], nextCursor: null}),
    });
    const staff = renderWithApp(<Harness />, {user: staffUser});
    expect(await screen.findByRole('button', {name: 'Stages'})).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Deleted'})).not.toBeInTheDocument();
    expect(screen.queryByRole('group', {name: 'Procedures'})).not.toBeInTheDocument();
    staff.unmount();

    renderWithApp(<Harness />, {user: adminUser});
    expect(await screen.findByRole('button', {name: 'Deleted'})).toBeInTheDocument();
  });

  it('says when nothing has happened yet', async () => {
    mockApi({[`GET ${OVERVIEW}`]: () => json(overview), [`GET ${TIMELINE}`]: () => json({items: [], nextCursor: null})});
    renderWithApp(<Harness />, {user: staffUser});

    expect(await screen.findByText('Nothing has happened yet')).toBeInTheDocument();
  });

  it('says when nothing matches the filters, and offers to show everything', async () => {
    const fetch = mockApi({[`GET ${OVERVIEW}`]: () => json(overview), [`GET ${TIMELINE}`]: () => json({items: [], nextCursor: null})});
    const {user} = renderWithApp(<Harness initial={{kinds: ['Blocks'], procedureIds: []}} />, {user: staffUser});

    expect(await screen.findByText('Nothing matches these filters')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Show Everything'}));

    await waitFor(() => expect(calls(fetch)).toContain(`GET ${TIMELINE}?PageSize=20`));
    expect(await screen.findByText('Nothing has happened yet')).toBeInTheDocument();
  });

  it('says when the timeline could not be loaded', async () => {
    mockApi({[`GET ${OVERVIEW}`]: () => json(overview), [`GET ${TIMELINE}`]: () => json({title: 'Down'}, 500)});
    renderWithApp(<Harness />, {user: staffUser});

    expect(await findAlert('The timeline could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('gives each kind of entry its own words', async () => {
    mockApi({
      [`GET ${OVERVIEW}`]: () => json(overview),
      [`GET ${TIMELINE}`]: () =>
        json({
          items: [
            entry({action: 'StageUnlocked'}),
            entry({action: 'Unassigned', kind: 'Assignments', assigneeUsername: 'other.staff'}),
            entry({action: 'StatusChanged', kind: 'Blocks', fromStatus: 'InProgress', toStatus: 'Blocked'}),
            entry({action: 'StatusChanged', kind: 'Blocks', fromStatus: 'Blocked', toStatus: 'InProgress'}),
            entry({action: 'StatusChanged', kind: 'Status', fromStatus: 'InReview', toStatus: 'Completed'}),
            entry({action: 'StatusChanged', kind: 'Blocks', fromStatus: 'Blocked', toStatus: 'Cancelled'}),
            entry({action: 'StageNotesUpdated', kind: 'Notes', changes: [{field: 'stageNotes'}]}),
            entry({action: 'DetailsUpdated', kind: 'DueDates', changes: [{field: 'dueDate', from: '2026-06-20', to: '2026-06-27'}]}),
            entry({action: 'DetailsUpdated', kind: 'Details', changes: [{field: 'title'}]}),
            entry({action: 'Exploded', kind: 'Unknown'}),
          ],
          nextCursor: null,
        }),
    });
    renderWithApp(<Harness />, {user: staffUser});

    for (const text of [
      'Unlocked Consultation',
      'Unassigned other.staff',
      'Blocked',
      'Unblocked',
      'Status changed to completed',
      'Status changed to cancelled',
      'Saved notes on Consultation',
      'Due date changed',
      'Due date 20 Jun 2026 → 27 Jun 2026',
      'Details edited',
      'Title changed',
      'Exploded',
    ])
      expect(await screen.findByText(text)).toBeInTheDocument();
  });
});
