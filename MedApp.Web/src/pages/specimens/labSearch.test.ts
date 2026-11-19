import {labSortOf, labViewOf, parseLabSearch} from './labSearch';

describe('parseLabSearch', () => {
  it('reads the view and page, whatever their case, ignoring a lab (dropped in MC13)', () => {
    expect(parseLabSearch({view: 'inlab', lab: 'anything', page: '3'})).toEqual({view: 'InLab', page: 3});
    expect(parseLabSearch({view: 'RejectedAndLost'})).toEqual({view: 'RejectedAndLost'});
    expect(parseLabSearch({view: 'all', sort: 'Number', asc: 'false'})).toEqual({view: 'All', sort: 'number', asc: false});
    expect(parseLabSearch({sort: 'time', asc: 'true'})).toEqual({sort: 'time', asc: true});
  });

  it('drops anything that is not valid', () => {
    expect(parseLabSearch({view: 'Everything', lab: 'northgate', page: '1'})).toEqual({});
    expect(parseLabSearch({page: 'two', lab: 42, sort: 'lab', asc: 'maybe'})).toEqual({});
  });
});

describe('labViewOf', () => {
  it("opens on Expected for the lab's staff and To send for everyone else, unless the address says", () => {
    expect(labViewOf({}, true)).toBe('Expected');
    expect(labViewOf({}, false)).toBe('ToSend');
    expect(labViewOf({view: 'Reported'}, false)).toBe('Reported');
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
