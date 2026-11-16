import {insights, STAFF_ID} from '../../test/insights';
import {
  assigneeHref,
  assigneeName,
  formatHours,
  formatWeek,
  overdueByAssigneeSummary,
  overdueByPrioritySummary,
  priorityHref,
  proceduresHref,
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

  it('links to the procedures list, filtered, and on the template when there is one', () => {
    expect(proceduresHref({})).toBe('/procedures');
    expect(proceduresHref({status: 'InProgress'}, 'Knee review')).toBe('/procedures?status=InProgress&template=Knee+review');
    expect(assigneeHref({userId: STAFF_ID, count: 1})).toBe(`/procedures?overdue=true&assignee=${STAFF_ID}`);
    expect(assigneeHref({userId: null, count: 1})).toBe('/procedures?overdue=true&assignee=none');
    expect(priorityHref({priority: 'Urgent', count: 1}, 'Knee review')).toBe('/procedures?overdue=true&priority=Urgent&template=Knee+review');
    expect(priorityHref({count: 1})).toBe('/procedures?overdue=true&priority=');
  });

  it('sums up the overdue work by assignee', () => {
    expect(overdueByAssigneeSummary(insights)).toBe('5 procedures are overdue. k.ashford has the most (3). 2 have nobody assigned.');
    expect(
      overdueByAssigneeSummary({...insights, overdue: {current: 1}, overdueByAssignee: [{userId: null, count: 1}]}),
    ).toBe('1 procedure is overdue. 1 has nobody assigned.');
    expect(overdueByAssigneeSummary({...insights, overdue: {current: 1}, overdueByAssignee: undefined})).toBe('1 procedure is overdue.');
    expect(overdueByAssigneeSummary({...insights, overdue: {current: 0}})).toBe('Nothing is overdue.');
    expect(overdueByAssigneeSummary({...insights, overdue: undefined})).toBe('Nothing is overdue.');
  });

  it('sums up the overdue work by priority', () => {
    expect(overdueByPrioritySummary(insights)).toBe('Of 5 overdue, 1 is urgent and 2 high priority.');
    expect(overdueByPrioritySummary({...insights, overdueByPriority: undefined})).toBe('Of 5 overdue, 0 are urgent and 0 high priority.');
    expect(overdueByPrioritySummary({...insights, overdue: {current: 0}})).toBe('Nothing is overdue.');
  });

  it('sums up what was created and completed', () => {
    expect(throughputSummary(insights.throughput!)).toBe(
      'Over 3 weeks, 8 procedures created and 10 completed: 2 more completed than created. This week so far: 1 created, 3 completed.',
    );
    expect(throughputSummary([{weekStart: '2026-11-16', created: 2, completed: 1}])).toBe(
      'Over 1 week, 2 procedures created and 1 completed: 1 more created than completed. This week so far: 2 created, 1 completed.',
    );
    expect(throughputSummary([{weekStart: '2026-11-16'}])).toBe(
      'Over 1 week, 0 procedures created and 0 completed: as many completed as created. This week so far: 0 created, 0 completed.',
    );
    expect(throughputSummary([])).toBe('Over 0 weeks, 0 procedures created and 0 completed: as many completed as created.');
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
