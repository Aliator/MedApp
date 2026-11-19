import {labSortOf, labViewOf, parseLabSearch} from './labSearch';

describe('parseLabSearch', () => {
  it('reads the view and page, whatever their case, ignoring a lab (dropped in MC13)', () => {
    expect(parseLabSearch({view: 'inlab', lab: 'anything', page: '3'})).toEqual({view: 'InLab', page: 3});
    expect(parseLabSearch({view: 'RejectedAndLost'})).toEqual({view: 'RejectedAndLost'});
    expect(parseLabSearch({view: 'all', sort: 'Number', asc: 'false'})).toEqual({view: 'All', sort: 'number', asc: false});
    expect(parseLabSearch({sort: 'time', asc: 'true'})).toEqual({sort: 'time', asc: true});
  });

  it("reads the samples database's filters (MC14), whatever their case", () => {
    const clinic = 'D1000000-0000-0000-0000-000000000001';
    expect(parseLabSearch({source: 'hospital', clinic, test: clinic, status: 'received', due: 'overdue', sort: 'Due'})).toEqual({
      source: 'Hospital',
      clinic: clinic.toLowerCase(),
      test: clinic.toLowerCase(),
      status: 'Received',
      due: 'Overdue',
      sort: 'due',
    });
    expect(parseLabSearch({source: 'Lab', clinic: 'derm', status: 'Open', due: 'soon'})).toEqual({});
  });

  it('drops anything that is not valid', () => {
    expect(parseLabSearch({view: 'Everything', lab: 'northgate', page: '1'})).toEqual({});
    expect(parseLabSearch({page: 'two', lab: 42, sort: 'lab', asc: 'maybe'})).toEqual({});
  });
});

describe('labViewOf', () => {
  it('opens on To receive for everyone (MC14), unless the address says', () => {
    expect(labViewOf({})).toBe('ToReceive');
    expect(labViewOf({view: 'Reported'})).toBe('Reported');
  });
});

describe('labSortOf', () => {
  it("marks the view's own time, oldest first (All newest first), or the sort asked for", () => {
    expect(labSortOf({}, 'Expected')).toEqual({sort: 'time', asc: true});
    expect(labSortOf({}, 'All')).toEqual({sort: 'time', asc: false});
    expect(labSortOf({sort: 'number'}, 'All')).toEqual({sort: 'number', asc: true});
    expect(labSortOf({sort: 'status', asc: false}, 'InLab')).toEqual({sort: 'status', asc: false});
  });
});
