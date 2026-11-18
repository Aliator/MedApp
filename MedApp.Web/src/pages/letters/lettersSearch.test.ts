import {defaultLetterView, parseLettersSearch} from './lettersSearch';

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
});
