import {QueryClient} from '@tanstack/react-query';
import {calls, json, mockApi} from '../test/render';
import {
  describeTimelineEntry,
  groupByDay,
  hasTimelineFilters,
  isPatientEntry,
  NO_TIMELINE_FILTERS,
  patientTimelineOverviewQuery,
  patientTimelineQuery,
  TIMELINE_KINDS,
  timelineKindLabel,
  type TimelineEntry,
} from './timeline';

const patientId = '00000031-0000-0000-0000-000000000001';
const procedureId = '00000031-0000-0000-0000-000000000002';

const entry = (fields: Partial<TimelineEntry>): TimelineEntry => ({
  id: '00000031-0000-0000-0000-000000000003',
  kind: 'Stages',
  action: 'StageFinished',
  atUtc: '2026-06-15T09:00:00Z',
  username: 'test.staff',
  procedureId,
  procedureTitle: 'Test procedure',
  isProcedureDeleted: false,
  changes: [],
  ...fields,
});

describe('patientTimelineQuery', () => {
  it('asks for a page with the filters, sorted, then the next page after its cursor', async () => {
    const fetch = mockApi({
      [`GET /api/patients/${patientId}/timeline`]: (request) =>
        json({items: [], nextCursor: new URL(request.url).searchParams.get('Before') ? null : 'next-page'}),
    });
    const queryClient = new QueryClient();
    const query = patientTimelineQuery(patientId, {kinds: ['Notes', 'Blocks'], procedureIds: [procedureId]});

    await queryClient.fetchInfiniteQuery(query);
    await queryClient.fetchInfiniteQuery({...query, pages: 2});

    expect(query.queryKey).toEqual(['procedures', 'timeline', patientId, {kinds: ['Blocks', 'Notes'], procedureIds: [procedureId]}]);
    // Fetching two pages starts again from the first.
    expect(new Set(calls(fetch))).toEqual(
      new Set([
        `GET /api/patients/${patientId}/timeline?PageSize=20&Kinds=Blocks&Kinds=Notes&ProcedureIds=${procedureId}`,
        `GET /api/patients/${patientId}/timeline?PageSize=20&Before=next-page&Kinds=Blocks&Kinds=Notes&ProcedureIds=${procedureId}`,
      ]),
    );
  });

  it('asks for everything when nothing is filtered', async () => {
    const fetch = mockApi({[`GET /api/patients/${patientId}/timeline`]: () => json({items: [], nextCursor: null})});

    await new QueryClient().fetchInfiniteQuery(patientTimelineQuery(patientId));

    expect(calls(fetch)).toEqual([`GET /api/patients/${patientId}/timeline?PageSize=20`]);
  });
});

describe('patientTimelineOverviewQuery', () => {
  it("fetches the patient's overview", async () => {
    const overview = {upcoming: [], openCount: 0, procedures: []};
    mockApi({[`GET /api/patients/${patientId}/timeline/overview`]: () => json(overview)});

    expect(await new QueryClient().fetchQuery(patientTimelineOverviewQuery(patientId))).toEqual(overview);
  });
});

describe('timeline filters', () => {
  it('names every kind', () => {
    expect(TIMELINE_KINDS.map(timelineKindLabel)).toEqual([
      'Created',
      'Stages',
      'Appointments',
      'Notes',
      'Assignments',
      'Blocks',
      'Status',
      'Due dates',
      'Other edits',
      'Files',
      'Deleted',
    ]);
  });

  it('knows when anything is filtered', () => {
    expect(hasTimelineFilters(NO_TIMELINE_FILTERS)).toBe(false);
    expect(hasTimelineFilters({kinds: ['Notes'], procedureIds: []})).toBe(true);
    expect(hasTimelineFilters({kinds: [], procedureIds: [procedureId]})).toBe(true);
  });
});

describe('groupByDay', () => {
  it('groups entries by their UK day, in order', () => {
    // 23:30 UTC in June is the next morning in the UK.
    const late = entry({id: 'a', atUtc: '2026-06-15T23:30:00Z'});
    const morning = entry({id: 'b', atUtc: '2026-06-16T08:00:00Z'});
    const dayBefore = entry({id: 'c', atUtc: '2026-06-15T12:00:00Z'});

    expect(groupByDay([morning, late, dayBefore])).toEqual([
      {day: '2026-06-16', entries: [morning, late]},
      {day: '2026-06-15', entries: [dayBefore]},
    ]);
  });
});

describe('describeTimelineEntry', () => {
  it("gives a note's text (admins only) as Markdown to show formatted, never in the detail line", () => {
    const added = describeTimelineEntry(entry({action: 'NoteAdded', changes: [{field: 'note', from: null, to: 'Prefers **mornings**.'}]}));
    expect(added).toEqual({title: 'Note added', detail: '', noteText: 'Prefers **mornings**.'});

    const edited = describeTimelineEntry(entry({action: 'NoteEdited', changes: [{field: 'note', from: 'Old.', to: '- Bloods taken'}]}));
    expect(edited).toEqual({title: 'Note edited', detail: 'It now reads:', noteText: '- Bloods taken'});

    // Staff get no text: just that it was added or edited.
    expect(describeTimelineEntry(entry({action: 'NoteEdited', changes: [{field: 'note', from: null, to: null}]}))).toEqual({
      title: 'Note edited',
      detail: '',
      noteText: undefined,
    });
  });

  it("says a note was deleted: to admins with its reason and the text it had, to staff just that it was", () => {
    const forAdmin = describeTimelineEntry(
      entry({action: 'NoteDeleted', reason: 'Synthetic: on the wrong patient.', changes: [{field: 'note', from: 'Prefers **mornings**.', to: null}]}),
    );
    expect(forAdmin).toEqual({title: 'Note deleted', detail: 'Reason: Synthetic: on the wrong patient. It read:', noteText: 'Prefers **mornings**.'});

    expect(describeTimelineEntry(entry({action: 'NoteDeleted', reason: null, changes: [{field: 'note', from: null, to: null}]}))).toEqual({
      title: 'Note deleted',
      detail: '',
      noteText: undefined,
    });
  });

  it.each([
    [{action: 'StageStarted', stageName: 'Consultation'}, 'Started Consultation', ''],
    [{action: 'StageFinished', stageName: 'Consultation'}, 'Finished Consultation', ''],
    [{action: 'StageUnlocked', stageName: null}, 'Unlocked a stage', ''],
    [{action: 'Assigned', assigneeUsername: 'other.staff'}, 'Assigned other.staff', ''],
    [{action: 'Unassigned', assigneeUsername: null}, 'Unassigned someone', ''],
    [{action: 'Deleted', fromStatus: 'Cancelled'}, 'Procedure deleted', 'Was cancelled'],
    [{action: 'Deleted', fromStatus: null}, 'Procedure deleted', ''],
    [{action: 'Exploded'}, 'Exploded', ''],
    [{action: null}, 'Changed', ''],
  ] as [Partial<TimelineEntry>, string, string][])('words %o', (fields, title, detail) => {
    expect(describeTimelineEntry(entry(fields))).toEqual({title, detail});
  });

  it('says where a procedure came from', () => {
    const created = entry({action: 'Created', templateName: 'Test template', stageCount: 1, dueDate: '2026-06-22', priority: 'High'});

    expect(describeTimelineEntry(created)).toEqual({title: 'Procedure created', detail: 'From Test template, 1 stage, due 22 Jun 2026, high priority'});
    expect(describeTimelineEntry(entry({action: 'Created', stageCount: 3})).detail).toBe('3 stages');
  });

  it("gives an admin the notes saved, and staff only that they were", () => {
    const admin = entry({action: 'StageNotesUpdated', stageName: 'Consultation', changes: [{field: 'stageNotes', from: '', to: 'Synthetic notes.'}]});
    const cleared = entry({action: 'StageNotesUpdated', stageName: 'Consultation', changes: [{field: 'stageNotes', from: 'Old', to: ''}]});
    const staff = entry({action: 'StageNotesUpdated', stageName: 'Consultation', changes: [{field: 'stageNotes', from: null, to: null}]});

    expect(describeTimelineEntry(admin)).toEqual({title: 'Saved notes on Consultation', detail: 'Notes: Synthetic notes.'});
    expect(describeTimelineEntry(cleared).detail).toBe('Notes cleared');
    expect(describeTimelineEntry(staff)).toEqual({title: 'Saved notes on Consultation', detail: ''});
  });

  it('words each edited detail, with the values the API sent', () => {
    const admin = entry({
      action: 'DetailsUpdated',
      kind: 'DueDates',
      changes: [
        {field: 'title', from: 'Old title', to: 'New title'},
        {field: 'notes', from: '', to: ''},
        {field: 'dueDate', from: '2026-06-15', to: null},
        {field: 'priority', from: 'Medium', to: 'High'},
        {field: 'colour', from: 'red', to: 'blue'},
      ],
    });
    const staff = entry({
      action: 'DetailsUpdated',
      kind: 'Details',
      changes: [
        {field: 'title', from: null, to: null},
        {field: 'notes', from: null, to: null},
      ],
    });

    expect(describeTimelineEntry(admin)).toEqual({
      title: 'Due date changed',
      detail: 'Title "Old title" → "New title"; Notes cleared; Due date 15 Jun 2026 → none; Priority medium → high; Colour: blue',
    });
    expect(describeTimelineEntry(staff)).toEqual({title: 'Details edited', detail: 'Title changed; Notes changed'});
    expect(describeTimelineEntry(entry({action: 'DetailsUpdated', changes: [{field: 'title', from: '', to: 'Named'}]})).detail).toBe(
      'Title nothing → "Named"',
    );
  });

  it('words a block, with its reason for an admin', () => {
    expect(describeTimelineEntry(entry({action: 'StatusChanged', fromStatus: 'InProgress', toStatus: 'Blocked', reason: 'Waiting for results.'}))).toEqual({
      title: 'Blocked',
      detail: 'Was in progress. Waiting for results.',
    });
    expect(describeTimelineEntry(entry({action: 'StatusChanged', fromStatus: 'Assigned', toStatus: 'Blocked'})).detail).toBe('Was assigned');
  });

  it('words an unblock, and cancelling a blocked procedure as a status change', () => {
    expect(describeTimelineEntry(entry({action: 'StatusChanged', fromStatus: 'Blocked', toStatus: 'InReview'}))).toEqual({
      title: 'Unblocked',
      detail: 'Back to in review',
    });
    expect(describeTimelineEntry(entry({action: 'StatusChanged', fromStatus: 'Blocked', toStatus: 'Cancelled', reason: 'No longer needed.'}))).toEqual({
      title: 'Status changed to cancelled',
      detail: 'Was blocked. No longer needed.',
    });
  });

  it('says a stage was finished without a visit, what it asked for, and (to admins) why', () => {
    const required = entry({action: 'StageFinished', stageName: 'Review', finishedWithoutVisit: true, visitRequirement: 'Required', reason: 'Seen elsewhere.'});
    const suggested = entry({action: 'StageFinished', stageName: 'Review', finishedWithoutVisit: true, visitRequirement: 'Suggested'});

    expect(describeTimelineEntry(required)).toEqual({title: 'Finished Review without a visit', detail: 'A visit was required. Seen elsewhere.'});
    expect(describeTimelineEntry(suggested)).toEqual({title: 'Finished Review without a visit', detail: 'A visit was suggested'});
    expect(describeTimelineEntry(entry({action: 'StageFinished', finishedWithoutVisit: true})).detail).toBe('');
  });

  describe('appointments', () => {
    const appointment = (fields: Partial<TimelineEntry>) =>
      entry({
        kind: 'Appointments',
        stageName: 'Review',
        appointmentStartUtc: '2026-11-23T10:00:00Z',
        appointmentEndUtc: '2026-11-23T10:30:00Z',
        appointmentStaffUsername: 'test.doctor',
        ...fields,
      });
    const slot = 'Mon 23 Nov 2026, 10:00 to 10:30 with test.doctor';

    it.each([
      [{action: 'AppointmentRequested', isAutomatic: true, appointmentStartUtc: null, appointmentStaffUsername: null}, 'Appointment requested for Review', 'Requested automatically, as the stage involves a visit. Needs booking'],
      [{action: 'AppointmentRequested', stageName: null, appointmentStartUtc: null, appointmentStaffUsername: null}, 'Appointment requested', 'Needs booking'],
      [{action: 'AppointmentBooked'}, 'Appointment booked for Review', slot],
      [{action: 'AppointmentAttended'}, 'Marked Attended for Review', slot],
      [{action: 'AppointmentAttended', fromStatus: 'DidNotAttend'}, 'Marked Attended for Review', `${slot}. Was marked Did not attend`],
      [{action: 'AppointmentDidNotAttend', fromStatus: 'Attended'}, 'Marked Did not attend for Review', `${slot}. Was marked Attended`],
      [{action: 'AppointmentDidNotAttend', fromStatus: 'Booked'}, 'Marked Did not attend for Review', slot],
      [{action: 'AppointmentCancelled', reason: 'Patient asked to cancel.'}, 'Appointment cancelled for Review', `${slot}. Patient asked to cancel.`],
      [{action: 'AppointmentCancelled', isAutomatic: true, appointmentStartUtc: null, appointmentStaffUsername: null}, 'Appointment cancelled for Review', 'Cancelled automatically'],
      [{action: 'AppointmentSomethingNew', appointmentStartUtc: null}, 'Appointment changed for Review', 'With test.doctor'],
    ] as [Partial<TimelineEntry>, string, string][])('words %o', (fields, title, detail) => {
      expect(describeTimelineEntry(appointment(fields))).toEqual({title, detail});
    });

    it('says where a rescheduled appointment moved from, and to', () => {
      const moved = appointment({action: 'AppointmentRescheduled', previousStartUtc: '2026-11-20T14:00:00Z', previousStaffUsername: 'other.doctor'});

      expect(describeTimelineEntry(moved)).toEqual({
        title: 'Appointment rescheduled for Review',
        detail: `From Fri 20 Nov 2026, 14:00 with other.doctor, to ${slot}`,
      });
      expect(describeTimelineEntry(appointment({action: 'AppointmentRescheduled'})).detail).toBe(`To ${slot}`);
    });

    it('gives an admin the notes, and staff only that they changed', () => {
      const admin = appointment({action: 'AppointmentNotesUpdated', changes: [{field: 'appointmentNotes', from: '', to: 'Synthetic note.'}]});
      const staff = appointment({action: 'AppointmentNotesUpdated', changes: [{field: 'appointmentNotes', from: null, to: null}]});

      expect(describeTimelineEntry(admin)).toEqual({title: 'Appointment notes changed for Review', detail: 'Notes: Synthetic note.'});
      expect(describeTimelineEntry(staff)).toEqual({title: 'Appointment notes changed for Review', detail: ''});
    });
  });

  it('says when the workflow changed the status itself', () => {
    expect(describeTimelineEntry(entry({action: 'StatusChanged', fromStatus: 'InProgress', toStatus: 'InReview', isAutomatic: true}))).toEqual({
      title: 'Status changed to in review',
      detail: 'Was in progress, changed automatically',
    });
  });

  it('words a file attached or deleted: its name only for admins, and what it was attached to', () => {
    const file = (fields: Partial<TimelineEntry>) =>
      describeTimelineEntry(entry({kind: 'Files', attachmentId: '00000031-0000-0000-0000-000000000009', ...fields}));

    // Staff get no names (free text).
    expect(file({action: 'AttachmentAdded', attachedTo: 'Procedure', fileName: null})).toEqual({title: 'Attached a file', detail: 'To the procedure'});
    expect(file({action: 'AttachmentAdded', attachedTo: 'Stage', stageName: 'Consultation', fileName: 'scan.pdf'})).toEqual({
      title: 'Attached scan.pdf',
      detail: 'To the Consultation stage',
    });
    expect(file({action: 'AttachmentDeleted', attachedTo: 'Stage', stageName: null, fileName: '  '})).toEqual({
      title: 'Deleted a file',
      detail: 'From a stage',
    });
    expect(file({action: 'AttachmentDeleted', attachedTo: 'Patient', procedureId: NO_PROCEDURE_ID, procedureTitle: '', fileName: 'letter.docx'})).toEqual({
      title: 'Deleted letter.docx',
      detail: 'From the patient',
    });
  });
});

const NO_PROCEDURE_ID = '00000000-0000-0000-0000-000000000000';

describe('isPatientEntry', () => {
  it("knows an entry about the patient's own file, which has no procedure", () => {
    expect(isPatientEntry(entry({attachedTo: 'Patient', procedureId: NO_PROCEDURE_ID}))).toBe(true);
    expect(isPatientEntry(entry({attachedTo: null, procedureId: NO_PROCEDURE_ID}))).toBe(true);
    expect(isPatientEntry(entry({attachedTo: 'Procedure'}))).toBe(false);
    expect(isPatientEntry(entry({}))).toBe(false);
  });
});
