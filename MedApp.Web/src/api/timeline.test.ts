import {QueryClient} from '@tanstack/react-query';
import {calls, json, mockApi} from '../test/render';
import {
  describeTimelineEntry,
  groupByDay,
  hasTimelineFilters,
  isPatientEntry,
  isReferralEntry,
  isReferralFileEntry,
  NO_TIMELINE_FILTERS,
  patientTimelineOverviewQuery,
  patientTimelineQuery,
  TIMELINE_KINDS,
  timelineKindLabel,
  type TimelineEntry,
} from './timeline';

const patientId = '00000031-0000-0000-0000-000000000001';
const pathwayId = '00000031-0000-0000-0000-000000000002';

const entry = (fields: Partial<TimelineEntry>): TimelineEntry => ({
  id: '00000031-0000-0000-0000-000000000003',
  kind: 'Stages',
  action: 'StageFinished',
  atUtc: '2026-06-15T09:00:00Z',
  username: 'test.staff',
  pathwayId,
  pathwayTitle: 'Test pathway',
  isPathwayDeleted: false,
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
    const query = patientTimelineQuery(patientId, {kinds: ['Notes', 'Blocks'], pathwayIds: [pathwayId]});

    await queryClient.fetchInfiniteQuery(query);
    await queryClient.fetchInfiniteQuery({...query, pages: 2});

    expect(query.queryKey).toEqual(['pathways', 'timeline', patientId, {kinds: ['Blocks', 'Notes'], pathwayIds: [pathwayId]}]);
    // Fetching two pages starts again from the first.
    expect(new Set(calls(fetch))).toEqual(
      new Set([
        `GET /api/patients/${patientId}/timeline?PageSize=20&Kinds=Blocks&Kinds=Notes&PathwayIds=${pathwayId}`,
        `GET /api/patients/${patientId}/timeline?PageSize=20&Before=next-page&Kinds=Blocks&Kinds=Notes&PathwayIds=${pathwayId}`,
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
    const overview = {upcoming: [], openCount: 0, pathways: []};
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
      'Referrals',
      'Waiting time',
      'Notes',
      'Assignments',
      'On hold',
      'Status',
      'Target dates',
      'Other edits',
      'Files',
      'Deleted',
    ]);
  });

  it('knows when anything is filtered', () => {
    expect(hasTimelineFilters(NO_TIMELINE_FILTERS)).toBe(false);
    expect(hasTimelineFilters({kinds: ['Notes'], pathwayIds: []})).toBe(true);
    expect(hasTimelineFilters({kinds: [], pathwayIds: [pathwayId]})).toBe(true);
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
    [{action: 'Deleted', fromStatus: 'Cancelled'}, 'Pathway deleted', 'Was cancelled'],
    [{action: 'Deleted', fromStatus: null}, 'Pathway deleted', ''],
    [{action: 'Exploded'}, 'Exploded', ''],
    [{action: null}, 'Changed', ''],
  ] as [Partial<TimelineEntry>, string, string][])('words %o', (fields, title, detail) => {
    expect(describeTimelineEntry(entry(fields))).toEqual({title, detail});
  });

  it('says where a pathway came from', () => {
    const created = entry({action: 'Created', templateName: 'Test template', stageCount: 1, dueDate: '2026-06-22', priority: 'High'});

    expect(describeTimelineEntry(created)).toEqual({title: 'Pathway created', detail: 'From Test template, 1 stage, due 22 Jun 2026, high priority'});
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
      title: 'Target date changed',
      detail: 'Title "Old title" → "New title"; Notes cleared; Target date 15 Jun 2026 → none; Priority medium → high; Colour: blue',
    });
    expect(describeTimelineEntry(staff)).toEqual({title: 'Details edited', detail: 'Title changed; Notes changed'});
    expect(describeTimelineEntry(entry({action: 'DetailsUpdated', changes: [{field: 'title', from: '', to: 'Named'}]})).detail).toBe(
      'Title nothing → "Named"',
    );
  });

  it('words a block, with its reason for an admin', () => {
    expect(describeTimelineEntry(entry({action: 'StatusChanged', fromStatus: 'InProgress', toStatus: 'Blocked', reason: 'Waiting for results.'}))).toEqual({
      title: 'Put on hold',
      detail: 'Was in progress. Waiting for results.',
    });
    expect(describeTimelineEntry(entry({action: 'StatusChanged', fromStatus: 'Assigned', toStatus: 'Blocked'})).detail).toBe('Was assigned');
  });

  it('words taking a pathway off hold, and cancelling one on hold as a status change', () => {
    expect(describeTimelineEntry(entry({action: 'StatusChanged', fromStatus: 'Blocked', toStatus: 'InReview'}))).toEqual({
      title: 'Taken off hold',
      detail: 'Back to awaiting review',
    });
    expect(describeTimelineEntry(entry({action: 'StatusChanged', fromStatus: 'Blocked', toStatus: 'Cancelled', reason: 'No longer needed.'}))).toEqual({
      title: 'Status changed to cancelled',
      detail: 'Was on hold. No longer needed.',
    });
  });

  it('says a stage was finished without a visit, what it asked for, and (to admins) why', () => {
    const required = entry({action: 'StageFinished', stageName: 'Review', finishedWithoutVisit: true, visitRequirement: 'Required', reason: 'Seen elsewhere.'});
    const suggested = entry({action: 'StageFinished', stageName: 'Review', finishedWithoutVisit: true, visitRequirement: 'Suggested'});

    expect(describeTimelineEntry(required)).toEqual({title: 'Finished Review without a visit', detail: 'A visit was required. Seen elsewhere.'});
    expect(describeTimelineEntry(suggested)).toEqual({title: 'Finished Review without a visit', detail: 'A visit was suggested'});
    expect(describeTimelineEntry(entry({action: 'StageFinished', finishedWithoutVisit: true})).detail).toBe('');
  });

  it('words a skipped stage: the reason to everyone, its detail only to admins', () => {
    expect(describeTimelineEntry(entry({action: 'StageSkipped', stageName: 'Scan', skipReason: 'PatientDeclined'}))).toEqual({title: 'Skipped Scan', detail: 'Patient declined'});
    expect(describeTimelineEntry(entry({action: 'StageSkipped', stageName: 'Scan', skipReason: 'Other', reason: 'Seen at another hospital'}))).toEqual({
      title: 'Skipped Scan',
      detail: 'Other. Seen at another hospital',
    });
  });

  it('words a branch chosen, with the stages it skipped, and a stage reinstated, with why only for admins', () => {
    expect(
      describeTimelineEntry(entry({action: 'BranchChosen', stageName: 'Biopsy', branch: 'Treatment', skippedStageNames: ['Discharge letter', 'Follow-up']})),
    ).toEqual({title: 'Chose the Treatment branch at Biopsy', detail: 'Skipped: Discharge letter, Follow-up'});
    expect(describeTimelineEntry(entry({action: 'BranchChosen', stageName: 'Biopsy', branch: null, skippedStageNames: []}))).toEqual({
      title: 'Chose a branch at Biopsy',
      detail: '',
    });
    expect(describeTimelineEntry(entry({action: 'StageReinstated', stageName: 'Follow-up', skipReason: 'BranchNotChosen', reason: 'Wrong branch'}))).toEqual({
      title: 'Reinstated Follow-up',
      detail: 'Had been skipped: Another branch was chosen. Wrong branch',
    });
    expect(describeTimelineEntry(entry({action: 'StageReinstated', stageName: 'Follow-up'}))).toEqual({title: 'Reinstated Follow-up', detail: ''});
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
      [{action: 'AppointmentRequested', isAutomatic: true, appointmentStartUtc: null, appointmentStaffUsername: null}, 'Appointment requested for Review', 'Requested automatically, as the stage involves a visit. Requires appointment'],
      [{action: 'AppointmentRequested', stageName: null, appointmentStartUtc: null, appointmentStaffUsername: null}, 'Appointment requested', 'Requires appointment'],
      [{action: 'AppointmentBooked'}, 'Appointment booked for Review', slot],
      [{action: 'AppointmentAttended'}, 'Marked Attended for Review', slot],
      [{action: 'AppointmentAttended', fromStatus: 'DidNotAttend'}, 'Marked Attended for Review', `${slot}. Was marked Did not attend`],
      [{action: 'AppointmentDidNotAttend', fromStatus: 'Attended'}, 'Marked Did not attend for Review', `${slot}. Was marked Attended`],
      [{action: 'AppointmentDidNotAttend', fromStatus: 'Booked'}, 'Marked Did not attend for Review', slot],
      [{action: 'AppointmentRecordedAfterEvent', toStatus: 'Attended'}, 'Appointment recorded after the event for Review: attended', slot],
      [{action: 'AppointmentRecordedAfterEvent', toStatus: 'DidNotAttend', fromStatus: 'Requested'}, 'Appointment recorded after the event for Review: did not attend', `${slot}. Was in Requires Appointment`],
      [{action: 'AppointmentRecordedAfterEvent', toStatus: 'Attended', stageName: null}, 'Appointment recorded after the event: attended', slot],
      [{action: 'AppointmentCancelled', reason: 'Patient asked to cancel.'}, 'Appointment cancelled for Review', `${slot}. Patient asked to cancel.`],
      [{action: 'AppointmentCancelled', isAutomatic: true, appointmentStartUtc: null, appointmentStaffUsername: null}, 'Appointment cancelled for Review', 'Cancelled automatically'],
      // Why comes to admins only; staff see just when and with whom.
      [{action: 'AppointmentEnteredInError', reason: 'Recorded on the wrong patient.'}, 'Appointment marked entered in error for Review', `${slot}. Recorded on the wrong patient.`],
      [{action: 'AppointmentEnteredInError', reason: null}, 'Appointment marked entered in error for Review', slot],
      // A missed appointment's follow-up: the decision for everyone, its detail (the reason) for admins only.
      [{action: 'AppointmentDnaFollowUpRecorded', dnaFollowUp: 'ClinicianToReview', reason: 'Dr Test to call.'}, 'Did not attend follow-up recorded for Review: Clinician to review', `${slot}. Dr Test to call.`],
      [{action: 'AppointmentDnaFollowUpRecorded', dnaFollowUp: 'Rebook', reason: null}, 'Did not attend follow-up recorded for Review: Rebook', slot],
      [{action: 'AppointmentDnaFollowUpRecorded', dnaFollowUp: null}, 'Did not attend follow-up recorded for Review: unknown', slot],
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
      title: 'Status changed to awaiting review',
      detail: 'Was in progress, changed automatically',
    });
  });

  it('words a file attached or deleted: its name only for admins, and what it was attached to', () => {
    const file = (fields: Partial<TimelineEntry>) =>
      describeTimelineEntry(entry({kind: 'Files', attachmentId: '00000031-0000-0000-0000-000000000009', ...fields}));

    // Staff get no names (free text).
    expect(file({action: 'AttachmentAdded', attachedTo: 'Pathway', fileName: null})).toEqual({title: 'Attached a file', detail: 'To the pathway'});
    expect(file({action: 'AttachmentAdded', attachedTo: 'Stage', stageName: 'Consultation', fileName: 'scan.pdf'})).toEqual({
      title: 'Attached scan.pdf',
      detail: 'To the Consultation stage',
    });
    expect(file({action: 'AttachmentDeleted', attachedTo: 'Stage', stageName: null, fileName: '  '})).toEqual({
      title: 'Deleted a file',
      detail: 'From a stage',
    });
    expect(file({action: 'AttachmentDeleted', attachedTo: 'Patient', pathwayId: NO_PATHWAY_ID, pathwayTitle: '', fileName: 'letter.docx'})).toEqual({
      title: 'Deleted letter.docx',
      detail: 'From the patient',
    });
  });
});

const NO_PATHWAY_ID = '00000000-0000-0000-0000-000000000000';

describe('isPatientEntry', () => {
  it("knows an entry about the patient's own file, which has no pathway", () => {
    expect(isPatientEntry(entry({attachedTo: 'Patient', pathwayId: NO_PATHWAY_ID}))).toBe(true);
    expect(isPatientEntry(entry({attachedTo: null, pathwayId: NO_PATHWAY_ID}))).toBe(true);
    expect(isPatientEntry(entry({attachedTo: 'Pathway'}))).toBe(false);
    expect(isPatientEntry(entry({}))).toBe(false);
    // A referral's letter has no pathway either, but isn't the patient's own file.
    expect(isPatientEntry(entry({attachedTo: 'Referral', pathwayId: NO_PATHWAY_ID}))).toBe(false);
  });
});

const referralId = '00000031-0000-0000-0000-00000000000a';

/** A referral history entry, as the API sends it: no pathway, its referral and specialty. */
const referralEntry = (fields: Partial<TimelineEntry>) =>
  entry({kind: 'Referrals', pathwayId: null, pathwayTitle: null, referralId, specialtyName: 'Dermatology', ...fields});

describe('referral entries', () => {
  it('knows a referral entry, and a file attached to a referral', () => {
    expect(isReferralEntry(referralEntry({action: 'ReferralRegistered'}))).toBe(true);
    expect(isReferralEntry(entry({kind: 'Other', action: 'ReferralWithdrawn'}))).toBe(true);
    expect(isReferralEntry(entry({kind: 'WaitingTime', action: 'Unknown'}))).toBe(true);
    expect(isReferralEntry(entry({}))).toBe(false);
    expect(isReferralFileEntry(entry({kind: 'Files', attachedTo: 'Referral', referralId}))).toBe(true);
    expect(isReferralFileEntry(entry({kind: 'Files', attachedTo: 'Patient'}))).toBe(false);
  });

  it('words a registration: the specialty and priority to staff, the practice and reason only to admins', () => {
    expect(describeTimelineEntry(referralEntry({action: 'ReferralRegistered', priority: 'High'}))).toEqual({
      title: 'Referral to Dermatology registered',
      detail: 'high priority asked for',
    });
    expect(
      describeTimelineEntry(
        referralEntry({action: 'ReferralRegistered', priority: 'Urgent', referrerPractice: 'Synthetic Surgery', reason: 'A synthetic reason'}),
      ),
    ).toEqual({title: 'Referral to Dermatology registered', detail: 'From Synthetic Surgery, urgent priority asked for. Reason: A synthetic reason'});
  });

  it('words an edit: each field, with the values the API sent', () => {
    expect(
      describeTimelineEntry(
        referralEntry({
          action: 'ReferralDetailsEdited',
          specialtyName: 'Urology',
          changes: [
            {field: 'reason'},
            {field: 'receivedOn', from: '2026-11-02', to: '2026-11-03'},
            {field: 'requestedPriority', from: 'Low', to: 'High'},
            {field: 'specialty', from: 'Dermatology', to: 'Urology'},
            {field: 'referrerOdsCode', from: null, to: null},
          ],
        }),
      ),
    ).toEqual({
      title: 'Referral to Urology: details edited',
      detail: 'Reason changed; Received date 2 Nov 2026 → 3 Nov 2026; Requested priority low → high; Specialty Dermatology → Urology; ODS code changed',
    });
  });

  it('words a withdrawal and an entry in error, with the detail only for admins', () => {
    expect(describeTimelineEntry(referralEntry({action: 'ReferralWithdrawn', toStatus: 'Withdrawn'}))).toEqual({
      title: 'Referral to Dermatology withdrawn',
      detail: '',
    });
    expect(describeTimelineEntry(referralEntry({action: 'ReferralEnteredInError', reason: 'Registered twice'}))).toEqual({
      title: 'Referral to Dermatology marked entered in error',
      detail: 'Registered twice',
    });
  });

  it('words the waiting-time clock: the pause reason and review date to everyone, its detail only to admins', () => {
    const clock = (fields: Partial<TimelineEntry>) => referralEntry({kind: 'WaitingTime', ...fields});
    expect(describeTimelineEntry(clock({action: 'ReferralClockPaused', pauseReason: 'PatientChoseToWait', reviewOn: '2027-02-03'}))).toEqual({
      title: 'Waiting-time clock paused',
      detail: 'Patient chose to wait, review 3 Feb 2027',
    });
    expect(describeTimelineEntry(clock({action: 'ReferralClockPaused', pauseReason: 'Other', reason: 'On holiday abroad'}))).toEqual({
      title: 'Waiting-time clock paused',
      detail: 'Other. On holiday abroad',
    });
    expect(describeTimelineEntry(clock({action: 'ReferralClockResumed'}))).toEqual({title: 'Waiting-time clock resumed', detail: ''});
    expect(describeTimelineEntry(clock({action: 'ReferralClockResumed', isAutomatic: true}))).toEqual({
      title: 'Waiting-time clock resumed',
      detail: 'Resumed automatically, as the clock stopped',
    });
    expect(
      describeTimelineEntry(clock({action: 'ReferralClockStopped', stopReason: 'StopStageFinished', daysWaited: 130, targetDays: 126, breached: true})),
    ).toEqual({title: 'Waiting-time clock stopped', detail: 'Clock-stop stage finished, day 130 of 126, breached'});
    expect(describeTimelineEntry(clock({action: 'ReferralClockStopped', stopReason: 'PathwayDischarged', daysWaited: 40}))).toEqual({
      title: 'Waiting-time clock stopped',
      detail: 'Discharged, day 40',
    });
    expect(describeTimelineEntry(clock({action: 'ReferralClockStopped'}))).toEqual({title: 'Waiting-time clock stopped', detail: ''});
    expect(describeTimelineEntry(clock({action: 'ReferralTargetChanged', previousTargetDays: 126, targetDays: 28, priority: 'High'}))).toEqual({
      title: 'Waiting-time target changed',
      detail: '126 → 28 days (high priority)',
    });
    expect(describeTimelineEntry(clock({action: 'ReferralTargetChanged', targetDays: 14}))).toEqual({
      title: 'Waiting-time target changed',
      detail: '14 days',
    });
    expect(describeTimelineEntry(clock({action: 'ReferralTargetChanged'}))).toEqual({title: 'Waiting-time target changed', detail: ''});
  });

  it('says when the waiting-time clock set the target date', () => {
    expect(
      describeTimelineEntry(entry({action: 'DetailsUpdated', kind: 'DueDates', fromClock: true, changes: [{field: 'dueDate', from: '2026-06-20', to: '2026-07-01'}]})),
    ).toEqual({title: 'Target date set by the waiting-time clock', detail: 'Target date 20 Jun 2026 → 1 Jul 2026'});
  });

  it('words a referral change it does not know yet by the status after it', () => {
    expect(describeTimelineEntry(referralEntry({action: 'ReferralPaused', toStatus: 'AwaitingTriage', specialtyName: null}))).toEqual({
      title: 'Referral changed',
      detail: 'Now awaiting triage',
    });
  });

  it('words triage: the template, priority and reason to staff; the detail and where else only to admins', () => {
    expect(describeTimelineEntry(referralEntry({action: 'ReferralAccepted', templateName: 'Skin check', priority: 'High'}))).toEqual({
      title: 'Referral to Dermatology accepted',
      detail: 'Pathway from Skin check, high priority',
    });
    expect(describeTimelineEntry(referralEntry({action: 'ReferralRejected', rejectionReason: 'Duplicate'}))).toEqual({
      title: 'Referral to Dermatology rejected',
      detail: 'Duplicate',
    });
    expect(describeTimelineEntry(referralEntry({action: 'ReferralRejected', rejectionReason: 'Other', reason: 'Seen privately'}))).toEqual({
      title: 'Referral to Dermatology rejected',
      detail: 'Other. Seen privately',
    });
    expect(describeTimelineEntry(referralEntry({action: 'ReferralRedirectedElsewhere'}))).toEqual({
      title: 'Referral to Dermatology redirected elsewhere',
      detail: '',
    });
    expect(describeTimelineEntry(referralEntry({action: 'ReferralRedirectedElsewhere', redirectedTo: 'Synthetic Service', reason: 'Closer'}))).toEqual({
      title: 'Referral to Dermatology redirected elsewhere',
      detail: 'To Synthetic Service. Closer',
    });
    expect(
      describeTimelineEntry(referralEntry({action: 'ReferralSpecialtyChanged', previousSpecialtyName: 'Urology', specialtyName: 'Dermatology'})),
    ).toEqual({title: 'Referral redirected to another specialty', detail: 'Specialty Urology → Dermatology'});
    expect(describeTimelineEntry(referralEntry({action: 'ReferralSpecialtyChanged', specialtyName: null, reason: 'Wrong clinic'}))).toEqual({
      title: 'Referral redirected to another specialty',
      detail: 'Wrong clinic',
    });
  });

  it('says a file was attached to the referral', () => {
    expect(
      describeTimelineEntry(entry({kind: 'Files', action: 'AttachmentAdded', attachedTo: 'Referral', referralId, pathwayId: NO_PATHWAY_ID, fileName: null})),
    ).toEqual({title: 'Attached a file', detail: 'To the referral'});
  });
});
