import {ADMIN, LAB, STAFF} from './accounts';
import {workingDayOf} from './handlers/setupCalendar';
import {seed} from './seed';
import {replaceState} from './store';
import {thisWeek, today} from './time';

const DAYS = {
  wednesday: '2026-11-18T10:00:00Z',
  saturday: '2026-11-21T10:00:00Z',
  sunday: '2026-11-22T10:00:00Z',
};

describe('the light demo’s calendars', () => {
  afterEach(() => vi.useRealTimers());

  for (const [name, at] of Object.entries(DAYS)) {
    it(`gives every account something today and this week, seeded on a ${name}`, () => {
      vi.useFakeTimers({toFake: ['Date']});
      vi.setSystemTime(new Date(at));
      const state = replaceState(seed());
      const on = today();
      const week = Array.from({length: 7}, (_, day) => thisWeek(day, on));

      for (const person of [ADMIN, STAFF, LAB]) {
        const mine = state.blocks.filter((b) => b.staffUserId === person.id);
        expect(mine.some((b) => b.date === on), `${person.username} today`).toBe(true);
        expect(mine.filter((b) => b.date !== undefined && week.includes(b.date)).length, `${person.username} this week`).toBeGreaterThan(3);
        expect(workingDayOf(person.id, on).working, `${person.username} working today`).toBe(true);
      }
    });
  }

  it('keeps weekends off when seeded on a weekday', () => {
    vi.useFakeTimers({toFake: ['Date']});
    vi.setSystemTime(new Date(DAYS.wednesday));
    replaceState(seed());
    expect(workingDayOf(STAFF.id, thisWeek(5, today())).working).toBe(false);
  });
});
