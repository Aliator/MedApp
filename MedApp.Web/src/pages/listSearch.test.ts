import {
  hasAuditFilters,
  hasPatientFilters,
  hasProcedureFilters,
  hasTaskFilters,
  hasUserFilters,
  parseAuditSearch,
  parseHomeSearch,
  parsePatientsSearch,
  parseProceduresSearch,
  parseSetupSearch,
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

describe('parseProceduresSearch', () => {
  it('reads the page, sort and filters, dropping anything invalid', () => {
    expect(
      parseProceduresSearch({
        page: '2',
        sort: 'status',
        asc: 'false',
        q: ' colon ',
        status: 'inreview',
        priority: 'HIGH',
        overdue: 'true',
        mine: 'false',
        closed: 'true',
        dueFrom: '2026-12-01',
        dueTo: 'later',
      }),
    ).toEqual({page: 2, sort: 'Status', asc: false, q: 'colon', status: 'InReview', priority: 'High', overdue: true, closed: true, dueFrom: '2026-12-01'});
  });

  it('ignores an unknown status and sort', () => {
    expect(parseProceduresSearch({status: 'Finished', sort: 'colour'})).toEqual({});
  });

  it('knows when the list is filtered', () => {
    expect(hasProcedureFilters({page: 3, sort: 'Due'})).toBe(false);
    expect(hasProcedureFilters({closed: true})).toBe(true);
  });
});

describe('parseSetupSearch', () => {
  it('reads the page and whether archived ones show', () => {
    expect(parseSetupSearch({page: '2', archived: 'true'})).toEqual({page: 2, archived: true});
    expect(parseSetupSearch({page: '1', archived: 'false', other: 'x'})).toEqual({});
  });
});

describe('parseAuditSearch', () => {
  it('reads the page, order and filters, dropping anything invalid', () => {
    const procedureId = '3f2a1b4c-5d6e-4f70-8a9b-0c1d2e3f4a5b';
    expect(
      parseAuditSearch({page: '3', asc: 'true', q: ' knee ', action: 'stagefinished', procedureId, from: '2026-11-01', to: '2026-11-30'}),
    ).toEqual({page: 3, asc: true, q: 'knee', action: 'StageFinished', procedureId, from: '2026-11-01', to: '2026-11-30'});
    expect(parseAuditSearch({action: 'Exploded', procedureId: 'not-a-guid', from: 'yesterday'})).toEqual({});
  });

  it('knows when the log is filtered', () => {
    expect(hasAuditFilters({page: 2, asc: true})).toBe(false);
    expect(hasAuditFilters({procedureId: 'x'})).toBe(true);
  });
});
