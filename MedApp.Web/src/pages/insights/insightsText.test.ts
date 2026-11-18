import {insights, STAFF_ID, TEMPLATE_ID} from '../../test/insights';
import {
  assigneeHref,
  assigneeName,
  assigneeNames,
  formatHours,
  formatWeek,
  overdueByAssigneeSummary,
  overdueByPrioritySummary,
  priorityHref,
  pathwaysHref,
  stageTimesSummary,
  throughputSummary,
} from './insightsText';

describe('insights in words', () => {
  it.each([
    [0.25, '15 min'],
    [1, '1 h'],
    [5.5, '5 h 30 min'],
    [47.99, '47 h 59 min'],
    [48, '2 d'],
    [76.4, '3 d 4 h'],
  ])('formats %f hours as %s', (hours, text) => {
    expect(formatHours(hours)).toBe(text);
  });

  it('formats a week by its Monday, and leaves anything else as it is', () => {
    expect(formatWeek('2026-11-09')).toBe('9 Nov');
    expect(formatWeek('soon')).toBe('soon');
  });

  it('names assignees: the account, nobody, or a deleted account', () => {
    expect(assigneeName({userId: STAFF_ID, username: 'k.ashford', count: 1})).toBe('k.ashford');
    expect(assigneeName({userId: null, count: 1})).toBe('Nobody');
    expect(assigneeName({userId: STAFF_ID, username: null, count: 1})).toBe('Deleted user');
  });

  it('links to the pathways list, filtered, and on the template when there is one', () => {
    // mine=false: never narrowed to the admin's own pathways by where Account Settings has lists start.
    expect(pathwaysHref({})).toBe('/pathways?mine=false');
    expect(pathwaysHref({status: 'InProgress'}, {templateId: TEMPLATE_ID})).toBe(`/pathways?status=InProgress&mine=false&templateId=${TEMPLATE_ID}`);
    expect(pathwaysHref({overdue: 'true'}, {templateId: null, teamId: null})).toBe('/pathways?overdue=true&mine=false');
    expect(assigneeHref({userId: STAFF_ID, count: 1})).toBe(`/pathways?overdue=true&assignee=${STAFF_ID}&mine=false`);
    expect(assigneeHref({userId: null, count: 1})).toBe('/pathways?overdue=true&assignee=none&mine=false');
    expect(priorityHref({priority: 'Urgent', count: 1}, {templateId: TEMPLATE_ID})).toBe(`/pathways?overdue=true&priority=Urgent&mine=false&templateId=${TEMPLATE_ID}`);
    expect(priorityHref({count: 1})).toBe('/pathways?overdue=true&priority=&mine=false');
  });

  it('carries the team to the pathways list too, as its team filter', () => {
    const teamId = '0000005a-0000-0000-0000-0000000000aa';

    expect(pathwaysHref({}, {teamId})).toBe(`/pathways?mine=false&team=${teamId}`);
    expect(assigneeHref({userId: null, count: 1}, {templateId: TEMPLATE_ID, teamId})).toBe(
      `/pathways?overdue=true&assignee=none&mine=false&templateId=${TEMPLATE_ID}&team=${teamId}`,
    );
  });

  it('numbers deleted accounts when there are several, so their bars can be told apart', () => {
    const deleted = (n: number) => ({userId: `0000000a-0000-0000-0000-00000000000${n}`, username: null, count: 1});
    expect(assigneeNames([deleted(2), {userId: null, count: 4}, {userId: STAFF_ID, username: 'k.ashford', count: 1}, deleted(3)])).toEqual([
      'Deleted user 1',
      'Nobody',
      'k.ashford',
      'Deleted user 2',
    ]);
    expect(assigneeNames([deleted(2), {userId: STAFF_ID, username: 'k.ashford', count: 1}])).toEqual(['Deleted user', 'k.ashford']);
  });

  it('sums up the overdue work by assignee', () => {
    expect(overdueByAssigneeSummary(insights)).toBe('5 pathways are overdue. k.ashford has the most (3). 2 have nobody assigned.');
    expect(
      overdueByAssigneeSummary({...insights, overdue: {current: 1}, overdueByAssignee: [{userId: null, count: 1}]}),
    ).toBe('1 pathway is overdue. 1 has nobody assigned.');
    expect(overdueByAssigneeSummary({...insights, overdue: {current: 1}, overdueByAssignee: undefined})).toBe('1 pathway is overdue.');
    expect(overdueByAssigneeSummary({...insights, overdue: {current: 0}})).toBe('Nothing is overdue.');
    expect(overdueByAssigneeSummary({...insights, overdue: undefined})).toBe('Nothing is overdue.');
  });

  it('sums up the overdue work by priority', () => {
    expect(overdueByPrioritySummary(insights)).toBe('Of 5 overdue, 1 is urgent and 2 high priority.');
    expect(overdueByPrioritySummary({...insights, overdueByPriority: undefined})).toBe('Of 5 overdue, 0 are urgent and 0 high priority.');
    expect(overdueByPrioritySummary({...insights, overdue: {current: 0}})).toBe('Nothing is overdue.');
  });

  it('sums up what was created and discharged', () => {
    expect(throughputSummary(insights.throughput!)).toBe(
      'Over 3 weeks, 8 pathways created and 10 discharged: 2 more discharged than created. This week so far: 1 created, 3 discharged.',
    );
    expect(throughputSummary([{weekStart: '2026-11-16', created: 2, completed: 1}])).toBe(
      'Over 1 week, 2 pathways created and 1 discharged: 1 more created than discharged. This week so far: 2 created, 1 discharged.',
    );
    expect(throughputSummary([{weekStart: '2026-11-16'}])).toBe(
      'Over 1 week, 0 pathways created and 0 discharged: as many discharged as created. This week so far: 0 created, 0 discharged.',
    );
    expect(throughputSummary([])).toBe('Over 0 weeks, 0 pathways created and 0 discharged: as many discharged as created.');
  });

  it('names the stage that takes longest, and its template when there are several', () => {
    expect(stageTimesSummary(insights.stageTimes!)).toBe('The longest is Procedure: 2 d 6 h on average, 2 d 2 h of it waiting to be started.');
    const other = {templateId: null, templateName: 'Allergy check', stages: [{stageOrder: 1, stageName: 'Test', averageWorkingHours: 0.5}]};
    expect(stageTimesSummary([other, ...insights.stageTimes!])).toBe(
      'The longest is Procedure of Knee review: 2 d 6 h on average, 2 d 2 h of it waiting to be started.',
    );
    expect(stageTimesSummary([{...other, templateName: null, stages: undefined}, other])).toBe(
      'The longest is Test of Allergy check: 30 min on average, 0 min of it waiting to be started.',
    );
    expect(stageTimesSummary([])).toBe('No stages were finished in this period.');
  });
});
