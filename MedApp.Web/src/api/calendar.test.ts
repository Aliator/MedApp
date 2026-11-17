import {QueryClient} from '@tanstack/react-query';
import {calls, json, mockApi} from '../test/render';
import {
  availabilityQuery,
  calendarKeys,
  calendarQuery,
  dayTemplatesQuery,
  meetingQuery,
  staffWeekTemplateQuery,
  teamCalendarQuery,
  weekTemplatesQuery,
} from './calendar';

describe('calendar queries', () => {
  it("fetches a person's default week template", async () => {
    const fetch = mockApi({'GET /api/calendar/staff/u1/week-template': () => json({staffUserId: 'u1', weekTemplateId: 'w1', weekTemplateName: 'Full time'})});

    const query = staffWeekTemplateQuery('u1');
    expect(await new QueryClient().fetchQuery(query)).toEqual({staffUserId: 'u1', weekTemplateId: 'w1', weekTemplateName: 'Full time'});
    expect(query.queryKey).toEqual(calendarKeys.weekTemplateOf('u1'));
    expect(calls(fetch)).toEqual(['GET /api/calendar/staff/u1/week-template']);
  });

  it("keys the signed-in user's calendar as theirs", () => {
    expect(calendarQuery({from: '2026-11-16', to: '2026-11-20'}).queryKey).toEqual(['calendar', 'days', 'me', '2026-11-16', '2026-11-20']);
    expect(calendarQuery({staffUserId: 'u1', from: '2026-11-16', to: '2026-11-16'}).queryKey).toEqual(['calendar', 'days', 'u1', '2026-11-16', '2026-11-16']);
  });

  it("fetches one person's own day and week templates, archived ones included", async () => {
    const fetch = mockApi({
      'GET /api/calendar/staff/u1/day-templates': () => json([]),
      'GET /api/calendar/staff/u1/week-templates': () => json([]),
    });

    await new QueryClient().fetchQuery(dayTemplatesQuery('u1'));
    await new QueryClient().fetchQuery(weekTemplatesQuery('u1'));

    expect(calls(fetch)).toEqual([
      'GET /api/calendar/staff/u1/day-templates?includeArchived=true',
      'GET /api/calendar/staff/u1/week-templates?includeArchived=true',
    ]);
    expect(dayTemplatesQuery('u1').queryKey).toEqual(calendarKeys.dayTemplatesOf('u1'));
    // Under the setup key, so saving any template refreshes every person's lists.
    expect(weekTemplatesQuery('u1').queryKey.slice(0, 2)).toEqual(calendarKeys.weekTemplates);
  });

  it('asks the team view for the people chosen, or everyone when none are', async () => {
    const fetch = mockApi({'GET /api/calendar/team': () => json({date: '2026-11-17', members: [], everyoneFree: []})});

    await new QueryClient().fetchQuery(teamCalendarQuery({date: '2026-11-17', staffUserIds: ['u2', 'u1']}));
    await new QueryClient().fetchQuery(teamCalendarQuery({date: '2026-11-17', staffUserIds: []}));

    expect(calls(fetch)).toEqual([
      'GET /api/calendar/team?Date=2026-11-17&StaffUserIds=u2&StaffUserIds=u1',
      'GET /api/calendar/team?Date=2026-11-17',
    ]);
    // The same people in another order are the same query.
    expect(teamCalendarQuery({date: '2026-11-17', staffUserIds: ['u2', 'u1']}).queryKey)
      .toEqual(teamCalendarQuery({date: '2026-11-17', staffUserIds: ['u1', 'u2']}).queryKey);
  });

  it('asks for availability with who, when, how long and the meeting being moved', async () => {
    const fetch = mockApi({'GET /api/calendar/availability': () => json({days: [], suggestions: [], busy: []})});

    await new QueryClient().fetchQuery(availabilityQuery({staffUserIds: ['u1', 'u2'], from: '2026-11-17', to: '2026-11-30', durationMinutes: 60, exceptMeetingId: 'm1'}));

    expect(calls(fetch)).toEqual([
      'GET /api/calendar/availability?StaffUserIds=u1&StaffUserIds=u2&From=2026-11-17&To=2026-11-30&DurationMinutes=60&ExceptMeetingId=m1',
    ]);
    expect(availabilityQuery({staffUserIds: ['u2', 'u1'], from: 'a', to: 'b', durationMinutes: 30}).queryKey)
      .toEqual(['calendar', 'availability', ['u1', 'u2'], 'a', 'b', 30, null]);
  });

  it('fetches one meeting', async () => {
    const fetch = mockApi({'GET /api/calendar/meetings/m1': () => json({id: 'm1', title: 'Synthetic MDT'})});

    expect(await new QueryClient().fetchQuery(meetingQuery('m1'))).toEqual({id: 'm1', title: 'Synthetic MDT'});
    expect(calls(fetch)).toEqual(['GET /api/calendar/meetings/m1']);
  });
});
