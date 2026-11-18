import {
  choiceValues,
  hasActivityFilters,
  parseActivitySearch,
  parseRetentionSearch,
  hasAuditFilters,
  hasPatientFilters,
  hasPathwayFilters,
  hasTaskFilters,
  hasUserFilters,
  idChoiceValues,
  parseAuditSearch,
  parseIdChoice,
  parseTasksSearch,
  parsePatientsSearch,
  parsePathwaysSearch,
  parseSetupSearch,
  parseChoice,
  parseInsightsSearch,
  parseUsersSearch,
  pathwayStatuses,
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

describe('parseIdChoice', () => {
  const a = '0000005A-0000-0000-0000-0000000000AA';
  const b = '0000005a-0000-0000-0000-0000000000bb';

  it('reads one ID or a comma list, in lower case, without repeats', () => {
    expect(parseIdChoice(a)).toBe(a.toLowerCase());
    expect(parseIdChoice(` ${a} , ${b},${a.toLowerCase()}`)).toBe(`${a.toLowerCase()},${b}`);
  });

  it('drops what is not an ID, and "not any of", which the API has no way to ask', () => {
    expect(parseIdChoice(`${a},Dermatology`)).toBe(a.toLowerCase());
    expect(parseIdChoice('Dermatology')).toBeUndefined();
    expect(parseIdChoice(`!${a}`)).toBeUndefined();
    expect(parseIdChoice(undefined)).toBeUndefined();
  });

  it('gives the IDs for the API, or none', () => {
    expect(idChoiceValues(`${a},${b}`)).toEqual([a, b]);
    expect(idChoiceValues(undefined)).toBeUndefined();
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
        nhs: ' 999 000 ',
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
      nhs: '999 000',
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
    expect(hasPatientFilters({nhs: '999'})).toBe(true);
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
    expect(hasTaskFilters({team: 'x'})).toBe(true);
    expect(hasTaskFilters({stage: 'x'})).toBe(true);
  });

  it('reads Breached only and the sort by days waited', () => {
    expect(parseTasksSearch({breached: 'true', sort: 'wait'})).toEqual({breached: true, sort: 'Wait'});
    expect(parseTasksSearch({breached: 'false'})).toEqual({});
    expect(hasTaskFilters({breached: true})).toBe(true);
  });

  it('reads teams and the stage reached as IDs', () => {
    const id = '0000000D-0000-0000-0000-000000000001';
    expect(parseTasksSearch({team: id, stage: `${id},lab`})).toEqual({team: id.toLowerCase(), stage: id.toLowerCase()});
  });
});

describe('parsePathwaysSearch', () => {
  it('reads the page, sort and filters, dropping anything invalid', () => {
    expect(
      parsePathwaysSearch({
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
    expect(parsePathwaysSearch({status: 'Finished', sort: 'colour'})).toEqual({});
  });

  it('reads an assignee: an ID, or nobody (none), dropping anything else', () => {
    const userId = '0000000A-0000-0000-0000-000000000001';
    expect(parsePathwaysSearch({assignee: userId})).toEqual({assignee: userId.toLowerCase()});
    expect(parsePathwaysSearch({assignee: 'NONE'})).toEqual({assignee: 'none'});
    expect(parsePathwaysSearch({assignee: 'k.ashford'})).toEqual({});
    expect(hasPathwayFilters({assignee: 'none'})).toBe(true);
  });

  it('reads a template ID, dropping anything that is not one', () => {
    const templateId = '0000000E-0000-0000-0000-000000000001';
    expect(parsePathwaysSearch({templateId})).toEqual({templateId: templateId.toLowerCase()});
    expect(parsePathwaysSearch({templateId: 'Knee review'})).toEqual({});
    expect(hasPathwayFilters({templateId: 'x'})).toBe(true);
  });

  it('knows when the list is filtered', () => {
    expect(hasPathwayFilters({page: 3, sort: 'Due'})).toBe(false);
    expect(hasPathwayFilters({closed: true})).toBe(true);
    expect(hasPathwayFilters({team: 'x'})).toBe(true);
    expect(hasPathwayFilters({stage: 'x'})).toBe(true);
  });

  it('reads Breached only and the sort by days waited', () => {
    expect(parsePathwaysSearch({breached: 'true', sort: 'Wait', asc: 'false'})).toEqual({breached: true, sort: 'Wait', asc: false});
    expect(parsePathwaysSearch({breached: 'no'})).toEqual({});
    expect(hasPathwayFilters({breached: true})).toBe(true);
  });

  it('reads teams and the stage reached as IDs ("Waiting for histology")', () => {
    const histology = '0000000D-0000-0000-0000-000000000001';
    const skin = '0000005a-0000-0000-0000-0000000000aa';
    expect(parsePathwaysSearch({stage: histology, team: `${skin},Dermatology`})).toEqual({
      stage: histology.toLowerCase(),
      team: skin,
    });
    expect(parsePathwaysSearch({stage: `!${histology}`})).toEqual({});
  });

  it('asks for the statuses chosen; for "not any of", the other open ones (or all, with closed ones included)', () => {
    expect(pathwayStatuses({})).toBeUndefined();
    expect(pathwayStatuses({status: 'Blocked,Completed'})).toEqual(['Blocked', 'Completed']);
    expect(pathwayStatuses({status: '!Blocked'})).toEqual(['Unassigned', 'Assigned', 'InProgress', 'InReview']);
    expect(pathwayStatuses({status: '!Blocked', closed: true})).toEqual([
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

describe('parseAuditSearch', () => {
  it('reads the page, order and filters, dropping anything invalid', () => {
    const pathwayId = '3f2a1b4c-5d6e-4f70-8a9b-0c1d2e3f4a5b';
    expect(
      parseAuditSearch({page: '3', asc: 'true', q: ' knee ', action: 'stagefinished,created', pathwayId, from: '2026-11-01', to: '2026-11-30'}),
    ).toEqual({page: 3, asc: true, q: 'knee', action: 'StageFinished,Created', pathwayId, from: '2026-11-01', to: '2026-11-30'});
    expect(parseAuditSearch({action: 'Exploded', pathwayId: 'not-a-guid', from: 'yesterday'})).toEqual({});
  });

  it('knows when the log is filtered', () => {
    expect(hasAuditFilters({page: 2, asc: true})).toBe(false);
    expect(hasAuditFilters({pathwayId: 'x'})).toBe(true);
  });
});

describe('parseActivitySearch', () => {
  it('reads the page, order and filters, dropping anything invalid', () => {
    const patientId = '3f2a1b4c-5d6e-4f70-8a9b-0c1d2e3f4a5b';
    expect(
      parseActivitySearch({page: '2', asc: 'true', q: ' 192.0.2 ', action: 'patientviewed,SignInFailed', patientId, from: '2026-11-01', to: '2026-11-30'}),
    ).toEqual({page: 2, asc: true, q: '192.0.2', action: 'PatientViewed,SignInFailed', patientId, from: '2026-11-01', to: '2026-11-30'});
    expect(parseActivitySearch({action: 'Exploded', patientId: 'not-a-guid', from: 'yesterday'})).toEqual({});
  });

  it('knows when the log is filtered', () => {
    expect(hasActivityFilters({page: 2, asc: true})).toBe(false);
    expect(hasActivityFilters({patientId: 'x'})).toBe(true);
    expect(hasActivityFilters({action: 'SignedIn'})).toBe(true);
  });
});

describe('parseRetentionSearch', () => {
  it('reads the page, and whether kept records are listed too', () => {
    expect(parseRetentionSearch({page: '3', kept: 'true'})).toEqual({page: 3, kept: true});
    expect(parseRetentionSearch({kept: 'false', page: 'x'})).toEqual({});
  });
});

describe('parseInsightsSearch', () => {
  it('reads the period and template, keeping 12 weeks (the default) out', () => {
    const template = '0000000E-0000-0000-0000-000000000001';
    expect(parseInsightsSearch({weeks: '26', template})).toEqual({weeks: 26, template: template.toLowerCase()});
    expect(parseInsightsSearch({weeks: 4})).toEqual({weeks: 4});
    expect(parseInsightsSearch({weeks: '12'})).toEqual({});
  });

  it('reads a team by its ID', () => {
    const team = '0000005A-0000-0000-0000-0000000000AA';
    expect(parseInsightsSearch({team})).toEqual({team: team.toLowerCase()});
  });

  it('drops a period it does not offer, and a template or team that is not an ID', () => {
    expect(parseInsightsSearch({weeks: '5', template: 'knee', team: 'Dermatology'})).toEqual({});
    expect(parseInsightsSearch({})).toEqual({});
  });
});
