import {QueryClient} from '@tanstack/react-query';
import {calls, json, mockApi} from '../test/render';
import {calendarKeys, calendarQuery, staffWeekTemplateQuery} from './calendar';

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
});
