import {defaultLetterView, lettersSortOf, parseLettersSearch} from './lettersSearch';

describe('lettersSearch', () => {
  it('reads the view (any case) and page, dropping anything invalid', () => {
    expect(parseLettersSearch({view: 'tosend', page: '3'})).toEqual({view: 'ToSend', page: 3});
    expect(parseLettersSearch({view: 'AwaitingMyApproval', page: 2})).toEqual({view: 'AwaitingMyApproval', page: 2});
    expect(parseLettersSearch({view: 'Everything', page: '1'})).toEqual({});
    expect(parseLettersSearch({view: '', page: 'x'})).toEqual({});
    expect(parseLettersSearch({view: {}, page: '-2'})).toEqual({});
  });

  it('opens on drafts for a writer, approvals for an approver, otherwise what is to send', () => {
    expect(defaultLetterView(['ViewPatients', 'WriteLetters', 'ApproveLetters'])).toBe('MyDrafts');
    expect(defaultLetterView(['ViewPatients', 'ApproveLetters'])).toBe('AwaitingMyApproval');
    expect(defaultLetterView(['ViewPatients', 'SendLetters'])).toBe('ToSend');
  });

  it('reads every letter (the view removed), the patient searched for and the sort', () => {
    expect(parseLettersSearch({view: 'all', patient: ' Freya ', sort: 'Latest', asc: 'false'})).toEqual({view: 'All', patient: 'Freya', sort: 'latest', asc: false});
    expect(parseLettersSearch({sort: 'status', asc: 'true'})).toEqual({sort: 'status', asc: true});
    expect(parseLettersSearch({sort: 'author', asc: 'maybe'})).toEqual({});
  });

  it("marks each view's own order where a column shows it, or the sort asked for", () => {
    expect(lettersSortOf({}, 'MyDrafts')).toEqual({sort: 'latest', asc: false});
    expect(lettersSortOf({}, 'Sent')).toEqual({sort: 'latest', asc: false});
    expect(lettersSortOf({}, 'AwaitingMyApproval')).toEqual({sort: 'latest', asc: true});
    expect(lettersSortOf({}, 'ToSend')).toEqual({sort: 'latest', asc: true});
    expect(lettersSortOf({}, 'ReturnedToMe')).toBeNull();
    expect(lettersSortOf({}, 'All')).toBeNull();
    expect(lettersSortOf({sort: 'patient'}, 'All')).toEqual({sort: 'patient', asc: true});
    expect(lettersSortOf({sort: 'status', asc: false}, 'Sent')).toEqual({sort: 'status', asc: false});
  });
});
