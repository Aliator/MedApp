import {screen, waitFor, within} from '@testing-library/react';
import {useState} from 'react';
import {NO_TIMELINE_FILTERS, type TimelineEntry, type TimelineFilters, type TimelineOverview} from '../../api/timeline';
import {adminUser, calls, findAlert, json, mockApi, renderWithApp, staffUser} from '../../test/render';
import {PatientTimeline, pathwayChipLabel, PATHWAY_CHIPS_SHOWN} from './PatientTimeline';

const patientId = '00000032-0000-0000-0000-000000000001';
const pathwayId = '00000032-0000-0000-0000-000000000002';
const otherPathwayId = '00000032-0000-0000-0000-000000000003';
const deletedPathwayId = '00000032-0000-0000-0000-000000000004';
const TIMELINE = `/api/patients/${patientId}/timeline`;
const OVERVIEW = `${TIMELINE}/overview`;

let next = 0;
const entry = (fields: Partial<TimelineEntry>): TimelineEntry => ({
  id: `00000032-0000-0000-0001-${String(++next).padStart(12, '0')}`,
  kind: 'Stages',
  action: 'StageFinished',
  atUtc: '2026-06-16T09:05:00Z',
  username: 'test.staff',
  pathwayId,
  pathwayTitle: 'Knee assessment',
  isPathwayDeleted: false,
  stageName: 'Consultation',
  changes: [],
  ...fields,
});

const overview: TimelineOverview = {
  upcoming: [
    {pathwayId: otherPathwayId, title: 'Overdue scan', status: 'InProgress', priority: 'High', dueDate: '2026-06-10', isOverdue: true},
    {pathwayId, title: 'Knee assessment', status: 'Assigned', priority: 'Low', dueDate: '2026-06-30', isOverdue: false},
  ],
  openCount: 4,
  pathways: [
    {id: pathwayId, title: 'Knee assessment', isDeleted: false},
    {id: otherPathwayId, title: 'Overdue scan', isDeleted: false},
  ],
};

/** The screen reader announcements' live region (Account Settings, Accessibility). */
const announcer = () => screen.getByTestId('announcer');

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
    expect(scan).toHaveAttribute('href', `/pathways/${otherPathwayId}`);
    expect(screen.getByText('Due 10 Jun 2026')).toBeInTheDocument();
    expect(screen.getByText('Overdue')).toBeInTheDocument();
    expect(screen.getByText('In progress')).toBeInTheDocument();
    expect(screen.getByText(/And 2 more/)).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'See all pathways'})).toHaveAttribute('href', `/patients/${patientId}/pathways`);
  });

  it('says when nothing is due', async () => {
    mockApi({
      [`GET ${OVERVIEW}`]: () => json({upcoming: [], openCount: 0, pathways: []}),
      [`GET ${TIMELINE}`]: () => json({items: [], nextCursor: null}),
    });
    renderWithApp(<Harness />, {user: staffUser});
    expect(await screen.findByText('Nothing is due: the patient has no open pathways.')).toBeInTheDocument();
  });

  it("says when what's due could not be loaded", async () => {
    mockApi({[`GET ${OVERVIEW}`]: () => json({title: 'Down'}, 500), [`GET ${TIMELINE}`]: () => json({items: [], nextCursor: null})});
    renderWithApp(<Harness />, {user: staffUser});

    expect(await findAlert("What's due could not be loaded.")).toBeInTheDocument();
  });

  it('shows the feed by UK day: what happened, to which pathway, when and by whom', async () => {
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
    expect(screen.getByText('Pathway created')).toBeInTheDocument();
    expect(screen.getByText('From Knee template')).toBeInTheDocument();
    expect(screen.getByText('11:00 · test.admin')).toBeInTheDocument();
    expect(screen.getAllByRole('link', {name: 'Knee assessment'}).every((link) => link.getAttribute('href') === `/pathways/${pathwayId}`)).toBe(
      true,
    );
    expect(screen.queryByRole('button', {name: 'Load More'})).not.toBeInTheDocument();
  });

  it("labels a deleted pathway's entries, without a link", async () => {
    mockApi({
      [`GET ${OVERVIEW}`]: () => json({...overview, pathways: [...overview.pathways!, {id: deletedPathwayId, title: 'Old biopsy', isDeleted: true}]}),
      [`GET ${TIMELINE}`]: () =>
        json({
          items: [entry({action: 'Deleted', kind: 'Deleted', pathwayId: deletedPathwayId, pathwayTitle: 'Old biopsy', isPathwayDeleted: true, fromStatus: 'Cancelled'})],
          nextCursor: null,
        }),
    });
    renderWithApp(<Harness />, {user: adminUser});

    const row = within((await screen.findByText('Pathway deleted')).closest('li')!);
    expect(row.getByText('Was cancelled')).toBeInTheDocument();
    expect(row.getByText('Deleted')).toBeInTheDocument();
    expect(row.getByText('Old biopsy')).toBeInTheDocument();
    expect(screen.queryByRole('link', {name: 'Old biopsy'})).not.toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Old biopsy (deleted)'})).toBeInTheDocument();
  });

  it("shows appointments' history, with the pathway only when the appointment is for one, and filters to them", async () => {
    const fetch = mockApi({
      [`GET ${OVERVIEW}`]: () => json(overview),
      [`GET ${TIMELINE}`]: () =>
        json({
          items: [
            entry({
              kind: 'Appointments',
              action: 'AppointmentBooked',
              appointmentStartUtc: '2026-06-22T09:00:00Z',
              appointmentEndUtc: '2026-06-22T09:30:00Z',
              appointmentStaffUsername: 'test.doctor',
            }),
            entry({kind: 'Appointments', action: 'AppointmentDidNotAttend', pathwayId: null, pathwayTitle: null, stageName: null}),
            entry({kind: 'Appointments', action: 'AppointmentRequested', isAutomatic: true}),
            entry({kind: 'Appointments', action: 'AppointmentCancelled'}),
            entry({kind: 'Appointments', action: 'AppointmentAttended'}),
            entry({kind: 'Appointments', action: 'AppointmentNotesUpdated'}),
            entry({kind: 'Appointments', action: 'AppointmentRecordedAfterEvent', toStatus: 'DidNotAttend', fromStatus: 'Requested'}),
            entry({kind: 'Appointments', action: 'AppointmentEnteredInError'}),
            entry({action: 'StageFinished', finishedWithoutVisit: true, visitRequirement: 'Suggested'}),
          ],
          nextCursor: null,
        }),
    });
    renderWithApp(<Harness />, {user: staffUser});

    expect(await screen.findByText('Appointment booked for Consultation')).toBeInTheDocument();
    expect(screen.getByText('Mon 22 Jun 2026, 10:00 to 10:30 with test.doctor')).toBeInTheDocument();
    expect(screen.getByText('Finished Consultation without a visit')).toBeInTheDocument();
    expect(screen.getByText('Appointment recorded after the event for Consultation: did not attend')).toBeInTheDocument();
    expect(screen.getByText('Appointment marked entered in error for Consultation')).toBeInTheDocument();
    // Not for a pathway: no link, just when and who.
    const missed = within(screen.getByText('Marked Did not attend').closest('li')!);
    expect(missed.queryByRole('link')).not.toBeInTheDocument();
    expect(missed.getByText('10:05 · test.staff')).toBeInTheDocument();

    screen.getByRole('button', {name: 'Appointments'}).click();
    await waitFor(() => expect(calls(fetch)).toContain(`GET ${TIMELINE}?PageSize=20&Kinds=Appointments`));
  });

  it("shows specimens' steps, linking to the specimen by its number beside its pathway, and filters to them", async () => {
    const specimenId = '00000051-0000-0000-0000-000000000001';
    const specimen = (fields: Partial<TimelineEntry>) =>
      entry({kind: 'Specimens', specimenId, specimenNumber: 'MA26-000123', specimenTypeName: 'Skin biopsy (punch)', laboratoryName: 'Northgate pathology', ...fields});
    const fetch = mockApi({
      [`GET ${OVERVIEW}`]: () => json(overview),
      [`GET ${TIMELINE}`]: () =>
        json({
          items: [
            specimen({action: 'SpecimenTaken'}),
            specimen({action: 'SpecimenSent'}),
            specimen({action: 'SpecimenReceived'}),
            specimen({action: 'SpecimenReported'}),
            specimen({action: 'SpecimenRejected', fromStatus: 'Received'}),
            specimen({action: 'SpecimenLost'}),
            specimen({action: 'SpecimenEnteredInError'}),
            specimen({action: 'SpecimenDetailsEdited', changes: [{field: 'site'}]}),
          ],
          nextCursor: null,
        }),
    });
    renderWithApp(<Harness />, {user: staffUser});

    const taken = within((await screen.findByText('Skin biopsy (punch) specimen taken')).closest('li')!);
    expect(taken.getByRole('link', {name: 'Specimen MA26-000123'})).toHaveAttribute('href', `/specimens/${specimenId}`);
    expect(taken.getByRole('link', {name: 'Knee assessment'})).toBeInTheDocument();
    expect(screen.getByText('Skin biopsy (punch) specimen rejected by Northgate pathology')).toBeInTheDocument();
    expect(screen.getByText('Site changed')).toBeInTheDocument();
    expect(screen.getAllByRole('link', {name: 'Specimen MA26-000123'})).toHaveLength(8);

    screen.getByRole('button', {name: 'Specimens'}).click();
    await waitFor(() => expect(calls(fetch)).toContain(`GET ${TIMELINE}?PageSize=20&Kinds=Specimens`));
  });

  it('loads older entries after the last page, announces them, focuses the first, and stops when there are none', async () => {
    localStorage.setItem('medapp.accessibility', JSON.stringify({announcements: true}));
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
    // Not left on the button, which has gone: on the first entry that came in.
    await waitFor(() => expect(document.activeElement).toHaveTextContent('Started Consultation'));
    await waitFor(() => expect(announcer()).toHaveTextContent('1 older entry loaded.'));
  });

  it('says when older entries could not be loaded, without announcing any as loaded', async () => {
    localStorage.setItem('medapp.accessibility', JSON.stringify({announcements: true}));
    mockApi({
      [`GET ${OVERVIEW}`]: () => json(overview),
      [`GET ${TIMELINE}`]: (request) =>
        new URL(request.url).searchParams.has('Before') ? json({title: 'Down'}, 500) : json({items: [entry({})], nextCursor: 'cursor-1'}),
    });
    const {user} = renderWithApp(<Harness />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: 'Load More'}));

    expect(await findAlert('Older entries could not be loaded. Please try again.')).toBeInTheDocument();
    expect(screen.getByText('Finished Consultation')).toBeInTheDocument();
    // Longer than the announcer waits before it speaks.
    await new Promise((resolve) => setTimeout(resolve, 300));
    expect(announcer()).toHaveTextContent('');
    expect(screen.getByRole('button', {name: 'Load More'})).toHaveFocus();
  });

  it('labels pathway chips with the day each was created, or deleted, so the same titles can be told apart', () => {
    expect(pathwayChipLabel({id: pathwayId, title: 'Physio review', createdOn: '2026-03-03'})).toBe('Physio review, 3 Mar 2026');
    expect(pathwayChipLabel({id: pathwayId, title: 'Physio review', isDeleted: true, deletedOn: '2026-05-01'})).toBe(
      'Physio review, deleted 1 May 2026',
    );
    expect(pathwayChipLabel({id: pathwayId, title: 'Physio review', isDeleted: true})).toBe('Physio review (deleted)');
    expect(pathwayChipLabel({id: pathwayId, title: 'Physio review'})).toBe('Physio review');
    expect(pathwayChipLabel({id: pathwayId})).toBe('');
  });

  it('shows the newest pathway chips, any chosen, and the rest on request', async () => {
    const many = Array.from({length: 12}, (_, i) => ({
      id: `00000032-0000-0000-0002-${String(i).padStart(12, '0')}`,
      title: 'Physio review',
      isDeleted: false,
      createdOn: `2026-03-${String(20 - i).padStart(2, '0')}`,
    }));
    mockApi({[`GET ${OVERVIEW}`]: () => json({...overview, pathways: many}), [`GET ${TIMELINE}`]: () => json({items: [entry({})], nextCursor: null})});
    const {user} = renderWithApp(<Harness initial={{kinds: [], pathwayIds: [many[11].id]}} />, {user: staffUser});

    const group = within(await screen.findByRole('group', {name: 'Pathways'}));
    // The ten newest, and the oldest because it's chosen.
    await waitFor(() => expect(group.getAllByRole('button', {pressed: false})).toHaveLength(PATHWAY_CHIPS_SHOWN));
    expect(group.getByRole('button', {name: 'Physio review, 9 Mar 2026'})).toHaveAttribute('aria-pressed', 'true');
    expect(group.queryByRole('button', {name: 'Physio review, 10 Mar 2026'})).not.toBeInTheDocument();

    await user.click(group.getByRole('button', {name: 'Show all 12'}));
    expect(group.getByRole('button', {name: 'Physio review, 10 Mar 2026'})).toBeInTheDocument();

    await user.click(group.getByRole('button', {name: 'Show fewer'}));
    expect(group.queryByRole('button', {name: 'Physio review, 10 Mar 2026'})).not.toBeInTheDocument();
  });

  it('stops at the most pathways the API takes', async () => {
    const chosen = Array.from({length: 50}, (_, i) => `00000032-0000-0000-0003-${String(i).padStart(12, '0')}`);
    mockApi({[`GET ${OVERVIEW}`]: () => json(overview), [`GET ${TIMELINE}`]: () => json({items: [entry({})], nextCursor: null})});
    renderWithApp(<Harness initial={{kinds: [], pathwayIds: chosen}} />, {user: staffUser});

    const chip = await screen.findByRole('button', {name: 'Overdue scan'});
    expect(chip).toHaveAttribute('aria-disabled', 'true');
  });

  it('filters by kind and by pathway, and shows everything again', async () => {
    const fetch = mockApi({[`GET ${OVERVIEW}`]: () => json(overview), [`GET ${TIMELINE}`]: () => json({items: [entry({})], nextCursor: null})});
    const {user} = renderWithApp(<Harness />, {user: staffUser});

    const notes = await screen.findByRole('button', {name: 'Notes'});
    await user.click(notes);
    await user.click(await screen.findByRole('button', {name: 'Overdue scan'}));

    await waitFor(() => expect(calls(fetch)).toContain(`GET ${TIMELINE}?PageSize=20&Kinds=Notes&PathwayIds=${otherPathwayId}`));
    expect(notes).toHaveAttribute('aria-pressed', 'true');

    await user.click(screen.getByRole('button', {name: 'Show everything'}));

    await waitFor(() => expect(screen.getByRole('button', {name: 'Notes'})).toHaveAttribute('aria-pressed', 'false'));
    expect(screen.queryByRole('button', {name: 'Show everything'})).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', {name: 'Notes'}));
    await user.click(screen.getByRole('button', {name: 'Notes'}));
    await waitFor(() => expect(screen.getByRole('button', {name: 'Notes'})).toHaveAttribute('aria-pressed', 'false'));
  });

  it('offers the Deleted filter to admins only, and pathway filters only when there is more than one', async () => {
    mockApi({
      [`GET ${OVERVIEW}`]: () => json({...overview, pathways: [overview.pathways![0]]}),
      [`GET ${TIMELINE}`]: () => json({items: [entry({})], nextCursor: null}),
    });
    const staff = renderWithApp(<Harness />, {user: staffUser});
    expect(await screen.findByRole('button', {name: 'Stages'})).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Deleted'})).not.toBeInTheDocument();
    expect(screen.queryByRole('group', {name: 'Pathways'})).not.toBeInTheDocument();
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
    const {user} = renderWithApp(<Harness initial={{kinds: ['Blocks'], pathwayIds: []}} />, {user: staffUser});

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
            entry({action: 'StatusChanged', kind: 'Status', fromStatus: 'Blocked', toStatus: 'Cancelled'}),
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
      'Put on hold',
      'Taken off hold',
      'Status changed to discharged',
      'Status changed to cancelled',
      'Saved notes on Consultation',
      'Target date changed',
      'Target date 20 Jun 2026 → 27 Jun 2026',
      'Details edited',
      'Title changed',
      'Exploded',
    ])
      expect(await screen.findByText(text)).toBeInTheDocument();
  });

  describe('files', () => {
    const NO_PATHWAY_ID = '00000000-0000-0000-0000-000000000000';
    const attachmentId = '00000032-0000-0000-0000-000000000009';
    const fileEntries = (fileName: (name: string) => string | null) => [
      entry({kind: 'Files', action: 'AttachmentAdded', attachmentId, attachedTo: 'Patient', pathwayId: NO_PATHWAY_ID, pathwayTitle: '', stageName: null, fileName: fileName('letter.docx')}),
      entry({kind: 'Files', action: 'AttachmentAdded', attachmentId, attachedTo: 'Pathway', stageName: null, fileName: fileName('referral.pdf')}),
      entry({kind: 'Files', action: 'AttachmentDeleted', attachmentId, attachedTo: 'Stage', stageName: 'Consultation', fileName: fileName('scan.png')}),
    ];

    it('offers a Files filter', async () => {
      const fetch = mockApi({[`GET ${OVERVIEW}`]: () => json(overview), [`GET ${TIMELINE}`]: () => json({items: [], nextCursor: null})});
      const {user} = renderWithApp(<Harness />, {user: staffUser});

      await user.click(await screen.findByRole('button', {name: 'Files'}));

      await waitFor(() => expect(calls(fetch)).toContain(`GET ${TIMELINE}?PageSize=20&Kinds=Files`));
    });

    it('tells staff a file was attached or deleted, and to what, without its name', async () => {
      mockApi({[`GET ${OVERVIEW}`]: () => json(overview), [`GET ${TIMELINE}`]: () => json({items: fileEntries(() => null), nextCursor: null})});
      renderWithApp(<Harness />, {user: staffUser});

      const patientRow = within((await screen.findByText('To the patient')).closest('li')!);
      expect(patientRow.getByText('Attached a file')).toBeInTheDocument();
      // A patient's own file has no pathway: no link, and no empty title.
      expect(patientRow.queryByRole('link')).not.toBeInTheDocument();
      expect(patientRow.getByText('10:05 · test.staff')).toBeInTheDocument();

      const pathwayRow = within(screen.getByText('To the pathway').closest('li')!);
      expect(pathwayRow.getByText('Attached a file')).toBeInTheDocument();
      expect(pathwayRow.getByRole('link', {name: 'Knee assessment'})).toHaveAttribute('href', `/pathways/${pathwayId}`);

      const stageRow = within(screen.getByText('From the Consultation stage').closest('li')!);
      expect(stageRow.getByText('Deleted a file')).toBeInTheDocument();
      expect(screen.queryByText(/letter\.docx|referral\.pdf|scan\.png/)).not.toBeInTheDocument();
    });

    it('tells admins which file', async () => {
      mockApi({[`GET ${OVERVIEW}`]: () => json(overview), [`GET ${TIMELINE}`]: () => json({items: fileEntries((name) => name), nextCursor: null})});
      renderWithApp(<Harness />, {user: adminUser});

      expect(await screen.findByText('Attached letter.docx')).toBeInTheDocument();
      expect(screen.getByText('Attached referral.pdf')).toBeInTheDocument();
      expect(screen.getByText('Deleted scan.png')).toBeInTheDocument();
    });
  });

  it("shows a note's text to admins formatted, not as its Markdown marks", async () => {
    const note = entry({id: 'n-1', kind: 'Notes', action: 'NoteAdded', changes: [{field: 'note', from: null, to: 'Prefers **mornings**.'}]});
    mockApi({[`GET ${OVERVIEW}`]: () => json(overview), [`GET ${TIMELINE}`]: () => json({items: [note], nextCursor: null})});
    renderWithApp(<Harness />, {user: adminUser});

    const row = within((await screen.findByText('Note added')).closest('li')!);
    expect(row.getByText('mornings').closest('strong, b')).not.toBeNull();
    expect(row.queryByText(/\*\*/)).not.toBeInTheDocument();
  });

  describe('referrals', () => {
    const referralId = '00000032-0000-0000-0000-00000000000a';

    it('lists the referrals awaiting triage beside the open pathways, each linking to its page', async () => {
      mockApi({
        [`GET ${OVERVIEW}`]: () =>
          json({...overview, awaitingReferrals: [{referralId, specialtyName: 'Dermatology', receivedOn: '2026-06-01', requestedPriority: 'Urgent'}]}),
        [`GET ${TIMELINE}`]: () => json({items: [], nextCursor: null}),
      });
      renderWithApp(<Harness />, {user: staffUser});

      const list = await screen.findByRole('list', {name: 'Referrals awaiting triage'});
      expect(within(list).getByRole('link', {name: 'Referral to Dermatology'})).toHaveAttribute('href', `/referrals/${referralId}`);
      expect(within(list).getByText('Received 1 Jun 2026')).toBeInTheDocument();
      expect(within(list).getByText('Urgent')).toBeInTheDocument();
    });

    it('shows referral entries and their letters, linking to the referral rather than a pathway', async () => {
      mockApi({
        [`GET ${OVERVIEW}`]: () => json(overview),
        [`GET ${TIMELINE}`]: () =>
          json({
            items: [
              entry({kind: 'Referrals', action: 'ReferralWithdrawn', pathwayId: null, pathwayTitle: null, referralId, specialtyName: 'Dermatology'}),
              entry({
                kind: 'Files',
                action: 'AttachmentAdded',
                attachedTo: 'Referral',
                referralId,
                pathwayId: '00000000-0000-0000-0000-000000000000',
                pathwayTitle: '',
                fileName: null,
              }),
            ],
            nextCursor: null,
          }),
      });
      renderWithApp(<Harness />, {user: staffUser});

      const withdrawn = within((await screen.findByText('Referral to Dermatology withdrawn')).closest('li')!);
      expect(withdrawn.getByRole('link', {name: 'Referral to Dermatology'})).toHaveAttribute('href', `/referrals/${referralId}`);
      const letter = within(screen.getByText('To the referral').closest('li')!);
      expect(letter.getByRole('link', {name: 'The referral'})).toHaveAttribute('href', `/referrals/${referralId}`);
      expect(letter.queryByRole('link', {name: ''})).not.toBeInTheDocument();
    });

    it('links an acceptance to its referral and its pathway, and a pathway created from a referral back to it', async () => {
      const pathwayId = '00000031-0000-0000-0000-0000000000bb';
      mockApi({
        [`GET ${OVERVIEW}`]: () => json(overview),
        [`GET ${TIMELINE}`]: () =>
          json({
            items: [
              entry({
                id: 'accepted',
                kind: 'Referrals',
                action: 'ReferralAccepted',
                pathwayId,
                pathwayTitle: 'Skin check',
                referralId,
                specialtyName: 'Dermatology',
                templateName: 'Skin check',
                priority: 'High',
              }),
              entry({id: 'created', kind: 'Created', action: 'Created', pathwayId, pathwayTitle: 'Skin check', referralId, templateName: 'Skin check'}),
              entry({id: 'rejected', kind: 'Referrals', action: 'ReferralRejected', pathwayId: null, pathwayTitle: null, referralId, specialtyName: 'Urology', rejectionReason: 'Duplicate'}),
            ],
            nextCursor: null,
          }),
      });
      renderWithApp(<Harness />, {user: staffUser});

      const accepted = within((await screen.findByText('Referral to Dermatology accepted')).closest('li')!);
      expect(accepted.getByText('Pathway from Skin check, high priority')).toBeInTheDocument();
      expect(accepted.getByRole('link', {name: 'Referral to Dermatology'})).toHaveAttribute('href', `/referrals/${referralId}`);
      expect(accepted.getByRole('link', {name: 'Skin check'})).toHaveAttribute('href', `/pathways/${pathwayId}`);
      const created = within(screen.getByText('Pathway created').closest('li')!);
      expect(created.getByRole('link', {name: 'Skin check'})).toHaveAttribute('href', `/pathways/${pathwayId}`);
      expect(created.getByRole('link', {name: 'From a referral'})).toHaveAttribute('href', `/referrals/${referralId}`);
      const rejected = within(screen.getByText('Referral to Urology rejected').closest('li')!);
      expect(rejected.getByText('Duplicate')).toBeInTheDocument();
    });

    it('can be filtered to', async () => {
      const fetch = mockApi({[`GET ${OVERVIEW}`]: () => json(overview), [`GET ${TIMELINE}`]: () => json({items: [], nextCursor: null})});
      const {user} = renderWithApp(<Harness />, {user: staffUser});

      await user.click(await screen.findByRole('button', {name: 'Referrals'}));
      await waitFor(() => expect(calls(fetch)).toContain(`GET ${TIMELINE}?PageSize=20&Kinds=Referrals`));
    });
  });
});
