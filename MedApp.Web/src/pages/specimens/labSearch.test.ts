import {labViewOf, parseLabSearch} from './labSearch';

const LAB = '00000051-0000-0000-0000-000000000007';

describe('parseLabSearch', () => {
  it('reads the view, lab and page, whatever their case', () => {
    expect(parseLabSearch({view: 'inlab', lab: LAB.toUpperCase(), page: '3'})).toEqual({view: 'InLab', lab: LAB, page: 3});
    expect(parseLabSearch({view: 'RejectedAndLost'})).toEqual({view: 'RejectedAndLost'});
  });

  it('drops anything that is not valid', () => {
    expect(parseLabSearch({view: 'Everything', lab: 'northgate', page: '1'})).toEqual({});
    expect(parseLabSearch({page: 'two', lab: 42})).toEqual({});
  });
});

describe('labViewOf', () => {
  it("opens on Expected for the lab's staff and To send for everyone else, unless the address says", () => {
    expect(labViewOf({}, true)).toBe('Expected');
    expect(labViewOf({}, false)).toBe('ToSend');
    expect(labViewOf({view: 'Reported'}, false)).toBe('Reported');
  });
});
