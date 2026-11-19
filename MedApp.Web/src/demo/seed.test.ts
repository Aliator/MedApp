import {ALL_IDS} from './ids';
import {CONSENT, LETTERS, LETTER_TEMPLATES, MDT_ITEMS, MDT_MEETINGS} from './slices/clinical';
import {LAB_TESTS, RESULTS, SPECIMEN_TYPES, SPECIMENS} from './slices/lab';

const LAB_ID_GROUPS = {LAB_TESTS, RESULTS, SPECIMEN_TYPES, SPECIMENS};
const CLINICAL_ID_GROUPS = {CONSENT, LETTERS, LETTER_TEMPLATES, MDT_ITEMS, MDT_MEETINGS};
import {NHS_NUMBERS, seed} from './seed';
import {thisWeek, today} from './time';

/** The NHS number's check digit (modulus 11), or null when no valid one exists. */
function checkDigit(first9: string): number | null {
  const total = [...first9].reduce((sum, digit, i) => sum + Number(digit) * (10 - i), 0);
  const check = 11 - (total % 11);
  return check === 11 ? 0 : check === 10 ? null : check;
}

describe('the light demo seed', () => {
  it('uses only NHS numbers from the 999 range, each with a valid check digit', () => {
    const state = seed();
    for (const patient of state.patients) {
      expect(patient.nhsNumber).toMatch(/^999\d{7}$/);
      expect(checkDigit(patient.nhsNumber!.slice(0, 9))).toBe(Number(patient.nhsNumber!.slice(9)));
    }
    expect(new Set(NHS_NUMBERS).size).toBe(NHS_NUMBERS.length);
  });

  it('has every fixed ID once', () => {
    const ids = [ALL_IDS, LAB_ID_GROUPS, CLINICAL_ID_GROUPS].flatMap((groups) =>
      Object.values(groups).flatMap((group) => Object.values(group) as string[]),
    );
    expect(new Set(ids).size).toBe(ids.length);
  });

  it('starts signed out, with this week on the calendar', () => {
    const state = seed();
    expect(state.user).toBeNull();
    expect(state.seededOn).toBe(today());
    expect(state.blocks.some((b) => b.date === thisWeek(0))).toBe(true);
    expect(state.meetings[0].date! > today()).toBe(true);
  });

  it('has a pathway on hold, one overdue, and referrals awaiting triage', () => {
    const state = seed();
    expect(state.pathways.some((p) => p.status === 'Blocked')).toBe(true);
    expect(state.pathways.some((p) => p.dueDate < today() && p.status !== 'Completed')).toBe(true);
    expect(state.referrals.filter((r) => r.status === 'AwaitingTriage').length).toBeGreaterThanOrEqual(3);
  });
});
