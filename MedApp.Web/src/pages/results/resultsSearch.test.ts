import {parseResultsSearch, resultsViewOf, resultViewsFor} from './resultsSearch';

const SPECIALTY = '00000061-0000-0000-0000-000000000008';

describe('parseResultsSearch', () => {
  it('reads the view, specialty and page, whatever their case', () => {
    expect(parseResultsSearch({view: 'needsclinician', specialty: SPECIALTY.toUpperCase(), page: '3'})).toEqual({
      view: 'NeedsClinician',
      specialty: SPECIALTY,
      page: 3,
    });
    expect(parseResultsSearch({view: 'SignedOff'})).toEqual({view: 'SignedOff'});
  });

  it('drops anything that is not valid, including the API-only All view', () => {
    expect(parseResultsSearch({view: 'All', specialty: 'cardiology', page: '1'})).toEqual({});
    expect(parseResultsSearch({page: 'two', specialty: 42})).toEqual({});
  });
});

describe('resultsViewOf', () => {
  it('opens on Awaiting my sign-off for those who sign off, and Awaiting sign-off for everyone else', () => {
    expect(resultsViewOf({}, true)).toBe('AwaitingMine');
    expect(resultsViewOf({}, false)).toBe('Awaiting');
    expect(resultsViewOf({view: 'SignedOff'}, false)).toBe('SignedOff');
  });

  it("never shows Awaiting my sign-off to someone who can't sign off", () => {
    expect(resultsViewOf({view: 'AwaitingMine'}, false)).toBe('Awaiting');
    expect(resultViewsFor(false).map((v) => v.value)).toEqual(['Awaiting', 'NeedsClinician', 'SignedOff']);
    expect(resultViewsFor(true)).toHaveLength(4);
  });
});
