import {
  choiceValues,
  hasAuditFilters,
  hasPatientFilters,
  hasProcedureFilters,
  hasTaskFilters,
  hasUserFilters,
  parseAuditSearch,
  parseCalendarTemplatesSearch,
  parseTasksSearch,
  parsePatientsSearch,
  parseProceduresSearch,
  parseSetupSearch,
  parseChoice,
  parseUsersSearch,
  procedureStatuses,
} from './listSearch';

describe('parseChoice', () => {
  const options = ['Low', 'Medium', 'High'];

  it('reads one value, a comma list, or a comma list after ! ("not any of"), matching options whatever their case', () => {
    expect(parseChoice('high', options)).toBe('High');
    expect(parseChoice('low,HIGH,low', options)).toBe('Low,High');
    expect(parseChoice('!medium', options)).toBe('!Medium');
  });

  it('drops values that are not options, and a choice left with none', () => {
    expect(parseChoice('High,Whenever', options)).toBe('High');
    expect(parseChoice('Whenever', options)).toBeUndefined();
    expect(parseChoice('', options)).toBeUndefined();
  });
});

describe('choiceValues', () => {
  const options = ['Low', 'Medium', 'High'];

  it('gives the values listed, or for "not any of" every other option', () => {
    expect(choiceValues('Low,High', options)).toEqual(['Low', 'High']);
    expect(choiceValues('!Medium', options)).toEqual(['Low', 'High']);
  });

  it('gives nothing for no choice, or one that leaves none', () => {
    expect(choiceValues(undefined, options)).toBeUndefined();
    expect(choiceValues('!Low,Medium,High', options)).toBeUndefined();
  });
});

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

describe('parseTasksSearch', () => {
  it('reads the page, sort and filters, dropping anything invalid', () => {
    expect(
      parseTasksSearch({
        page: '3',
        sort: 'priority',
        asc: 'false',
        title: ' knee ',
        patient: 'freya',
        progress: 'STARTED',
        status: 'blocked,Completed',
        priority: '!urgent',
        overdue: 'true',
        dueFrom: '2026-12-01',
        dueTo: 'soon',
        other: 'x',
      }),
    ).toEqual({
      page: 3,
      sort: 'Priority',
      asc: false,
      title: 'knee',
      patient: 'freya',
      progress: 'started',
      // Only open statuses: a user's tasks are unfinished.
      status: 'Blocked',
      priority: '!Urgent',
      overdue: true,
      dueFrom: '2026-12-01',
    });
  });

  it('starts on Not started: no progress in the URL, and an unknown one is dropped', () => {
    expect(parseTasksSearch({progress: 'later', overdue: 'false'})).toEqual({});
    expect(parseTasksSearch({progress: 'any'})).toEqual({progress: 'any'});
  });

  it('knows when the list is filtered beyond its progress', () => {
    expect(hasTaskFilters({page: 2, sort: 'Due', progress: 'started'})).toBe(false);
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
        title: ' colon ',
        template: 'scope',
        status: 'inreview',
        priority: 'HIGH,low',
        overdue: 'true',
        mine: 'false',
        closed: 'true',
        dueFrom: '2026-12-01',
        dueTo: 'later',
      }),
    ).toEqual({
      page: 2,
      sort: 'Status',
      asc: false,
      title: 'colon',
      template: 'scope',
      status: 'InReview',
      priority: 'High,Low',
      overdue: true,
      // Kept: false says not to start on "Assigned to me".
      mine: false,
      closed: true,
      dueFrom: '2026-12-01',
    });
  });

  it('ignores an unknown status and sort', () => {
    expect(parseProceduresSearch({status: 'Finished', sort: 'colour'})).toEqual({});
  });

  it('knows when the list is filtered', () => {
    expect(hasProcedureFilters({page: 3, sort: 'Due'})).toBe(false);
    expect(hasProcedureFilters({closed: true})).toBe(true);
  });

  it('asks for the statuses chosen; for "not any of", the other open ones (or all, with closed ones included)', () => {
    expect(procedureStatuses({})).toBeUndefined();
    expect(procedureStatuses({status: 'Blocked,Completed'})).toEqual(['Blocked', 'Completed']);
    expect(procedureStatuses({status: '!Blocked'})).toEqual(['Unassigned', 'Assigned', 'InProgress', 'InReview']);
    expect(procedureStatuses({status: '!Blocked', closed: true})).toEqual([
      'Unassigned',
      'Assigned',
      'InProgress',
      'InReview',
      'Completed',
      'Cancelled',
      'Archived',
    ]);
  });
});

describe('parseSetupSearch', () => {
  it('reads the page, name and status (active ones unless it says otherwise)', () => {
    expect(parseSetupSearch({page: '2', name: ' consent ', status: 'archived'})).toEqual({page: 2, name: 'consent', status: 'archived'});
    expect(parseSetupSearch({page: '1', status: 'gone', other: 'x'})).toEqual({});
  });

  it('reads the old show archived switch as both', () => {
    expect(parseSetupSearch({archived: 'true'})).toEqual({status: 'any'});
  });
});

describe('parseCalendarTemplatesSearch', () => {
  it('reads the tab (Day templates unless it says otherwise) and its search', () => {
    expect(parseCalendarTemplatesSearch({tab: 'Week', name: 'standard', status: 'any', page: '2'})).toEqual({
      tab: 'week',
      name: 'standard',
      status: 'any',
      page: 2,
    });
    expect(parseCalendarTemplatesSearch({tab: 'day'})).toEqual({});
    expect(parseCalendarTemplatesSearch({tab: 'staff'})).toEqual({tab: 'staff'});
  });
});

describe('parseAuditSearch', () => {
  it('reads the page, order and filters, dropping anything invalid', () => {
    const procedureId = '3f2a1b4c-5d6e-4f70-8a9b-0c1d2e3f4a5b';
    expect(
      parseAuditSearch({page: '3', asc: 'true', q: ' knee ', action: 'stagefinished,created', procedureId, from: '2026-11-01', to: '2026-11-30'}),
    ).toEqual({page: 3, asc: true, q: 'knee', action: 'StageFinished,Created', procedureId, from: '2026-11-01', to: '2026-11-30'});
    expect(parseAuditSearch({action: 'Exploded', procedureId: 'not-a-guid', from: 'yesterday'})).toEqual({});
  });

  it('knows when the log is filtered', () => {
    expect(hasAuditFilters({page: 2, asc: true})).toBe(false);
    expect(hasAuditFilters({procedureId: 'x'})).toBe(true);
  });
});
