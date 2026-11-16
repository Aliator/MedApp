import {QueryClient} from '@tanstack/react-query';
import {calls, json, mockApi} from '../test/render';
import {
  describeTimelineEntry,
  groupByDay,
  hasTimelineFilters,
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
      'Notes',
      'Assignments',
      'Blocks',
      'Status',
      'Due dates',
      'Other edits',
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

  it('says when the workflow changed the status itself', () => {
    expect(describeTimelineEntry(entry({action: 'StatusChanged', fromStatus: 'InProgress', toStatus: 'InReview', isAutomatic: true}))).toEqual({
      title: 'Status changed to in review',
      detail: 'Was in progress, changed automatically',
    });
  });
});
