import {
  hasPatientFilters,
  hasTaskFilters,
  hasUserFilters,
  parseHomeSearch,
  parsePatientsSearch,
  parseUsersSearch,
  taskListSearch,
  withTaskListSearch,
} from './listSearch';

describe('parsePatientsSearch', () => {
  it('reads every setting from the URL', () => {
    expect(
      parsePatientsSearch({
        page: '3',
        sort: 'dateofbirth',
        asc: 'false',
        first: ' Freya ',
        last: 'Ahmed',
        email: 'example.com',
        dobFrom: '1970-01-01',
        dobTo: '1979-12-31',
      }),
    ).toEqual({
      page: 3,
      sort: 'DateOfBirth',
      asc: false,
      first: 'Freya',
      last: 'Ahmed',
      email: 'example.com',
      dobFrom: '1970-01-01',
      dobTo: '1979-12-31',
    });
  });

  it('takes values the router has already parsed', () => {
    expect(parsePatientsSearch({page: 2, asc: true})).toEqual({page: 2, asc: true});
  });

  it('drops invalid and default values', () => {
    expect(
      parsePatientsSearch({page: '1', sort: 'createdAt', asc: 'maybe', first: '  ', dobFrom: '14/03/1980', dobTo: 'soon'}),
    ).toEqual({});
    expect(parsePatientsSearch({page: '-2'})).toEqual({});
    expect(parsePatientsSearch({page: '2.5'})).toEqual({});
  });

  it('tells whether any filter is set', () => {
    expect(hasPatientFilters({page: 2, sort: 'Email'})).toBe(false);
    expect(hasPatientFilters({dobTo: '1980-01-01'})).toBe(true);
  });
});

describe('parseUsersSearch', () => {
  it('reads every setting from the URL', () => {
    expect(parseUsersSearch({page: '2', sort: 'user', asc: 'false', username: 'hart', role: 'Admin'})).toEqual({
      page: 2,
      sort: 'User',
      asc: false,
      username: 'hart',
      role: 'Admin',
    });
  });

  it('drops invalid values and tells whether any filter is set', () => {
    const search = parseUsersSearch({sort: 'Roles', username: ''});
    expect(search).toEqual({});
    expect(hasUserFilters(search)).toBe(false);
    expect(hasUserFilters({role: 'User'})).toBe(true);
  });
});

describe('parseHomeSearch', () => {
  it('reads each task list under its own prefix, dropping anything invalid', () => {
    expect(
      parseHomeSearch({
        show: 'STARTED',
        notStartedPage: '3',
        notStartedSort: 'priority',
        notStartedAsc: 'false',
        notStartedQ: ' knee ',
        startedPriority: 'urgent',
        startedOverdue: 'true',
        startedDueFrom: '2026-12-01',
        startedDueTo: 'soon',
        startedSort: 'colour',
        other: 'x',
      }),
    ).toEqual({
      show: 'started',
      notStartedPage: 3,
      notStartedSort: 'Priority',
      notStartedAsc: false,
      notStartedQ: 'knee',
      startedPriority: 'Urgent',
      startedOverdue: true,
      startedDueFrom: '2026-12-01',
    });
  });

  it('leaves out overdue when it is not true, and an unknown list to show', () => {
    expect(parseHomeSearch({show: 'later', startedOverdue: 'false'})).toEqual({});
  });

  it('splits a list out of the URL state, and replaces it without touching the other', () => {
    const home = parseHomeSearch({show: 'started', notStartedQ: 'colon', startedPage: '2', startedQ: 'knee'});

    expect(taskListSearch(home, 'started')).toEqual({page: 2, q: 'knee'});
    expect(withTaskListSearch(home, 'started', {sort: 'Due', asc: false})).toEqual({
      show: 'started',
      notStartedQ: 'colon',
      startedSort: 'Due',
      startedAsc: false,
    });
  });

  it('knows when a task list is filtered', () => {
    expect(hasTaskFilters({page: 2, sort: 'Due'})).toBe(false);
    expect(hasTaskFilters({overdue: true})).toBe(true);
  });
});
