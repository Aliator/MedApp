import {parseResultsSearch, resultsSortOf, resultsViewOf, resultViewsFor} from './resultsSearch';

const TEAM = '00000061-0000-0000-0000-000000000008';

describe('parseResultsSearch', () => {
  it('reads the view, team and page, whatever their case', () => {
    expect(parseResultsSearch({view: 'needsclinician', team: TEAM.toUpperCase(), page: '3'})).toEqual({
      view: 'NeedsClinician',
      team: TEAM,
      page: 3,
    });
    expect(parseResultsSearch({view: 'SignedOff'})).toEqual({view: 'SignedOff'});
  });

  it('reads every result (the view removed), the patient searched for and the sort', () => {
    expect(parseResultsSearch({view: 'all', patient: ' Freya ', sort: 'Title', asc: 'false'})).toEqual({view: 'All', patient: 'Freya', sort: 'title', asc: false});
    expect(parseResultsSearch({sort: 'reported', asc: 'true'})).toEqual({sort: 'reported', asc: true});
  });

  it('drops anything that is not valid', () => {
    expect(parseResultsSearch({view: 'Everything', team: 'cardiology', page: '1', sort: 'content', asc: 'maybe'})).toEqual({});
    expect(parseResultsSearch({page: 'two', team: 42})).toEqual({});
  });
});

describe('resultsViewOf', () => {
  it('opens on Awaiting my sign-off for those who sign off, and Awaiting sign-off for everyone else', () => {
    expect(resultsViewOf({}, true)).toBe('AwaitingMine');
    expect(resultsViewOf({}, false)).toBe('Awaiting');
    expect(resultsViewOf({view: 'SignedOff'}, false)).toBe('SignedOff');
  });

  it('keeps every result (the view removed) for everyone', () => {
    expect(resultsViewOf({view: 'All'}, false)).toBe('All');
  });

  it("never shows Awaiting my sign-off to someone who can't sign off", () => {
    expect(resultsViewOf({view: 'AwaitingMine'}, false)).toBe('Awaiting');
    expect(resultViewsFor(false).map((v) => v.value)).toEqual(['Awaiting', 'NeedsClinician', 'SignedOff']);
    expect(resultViewsFor(true)).toHaveLength(4);
  });
});

describe('resultsSortOf', () => {
  it("marks each view's own order, or the sort asked for", () => {
    expect(resultsSortOf({}, 'AwaitingMine')).toEqual({sort: 'reported', asc: true});
    expect(resultsSortOf({}, 'SignedOff')).toEqual({sort: 'status', asc: false});
    expect(resultsSortOf({}, 'All')).toEqual({sort: 'reported', asc: false});
    expect(resultsSortOf({sort: 'patient'}, 'All')).toEqual({sort: 'patient', asc: true});
    expect(resultsSortOf({sort: 'title', asc: false}, 'Awaiting')).toEqual({sort: 'title', asc: false});
  });
});
