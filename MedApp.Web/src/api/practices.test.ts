import {EMPTY_PRACTICE, practiceAddress, practiceChoices, practiceInput, practiceLabel, toPracticeRequest} from './practices';

const practices = [
  {id: 'b', name: 'Testbrook Surgery', odsCode: 'ZZZ123', isArchived: false},
  {id: 'o', name: 'Oldfield Practice', isArchived: true},
  {id: 'a', name: 'avonmere Health Centre', isArchived: false},
];

describe('practices', () => {
  it('offers those not archived by name, and the one already chosen even if archived', () => {
    expect(practiceChoices(practices).map((p) => p.id)).toEqual(['a', 'b']);
    expect(practiceChoices(practices, 'o').map((p) => p.id)).toEqual(['a', 'o', 'b']);
    expect(practiceChoices(practices, '').map((p) => p.id)).toEqual(['a', 'b']);
  });

  it('labels one with its code, and marks an archived one', () => {
    expect(practiceLabel(practices[0])).toBe('Testbrook Surgery (ZZZ123)');
    expect(practiceLabel(practices[1])).toBe('Oldfield Practice (archived)');
  });

  it('starts the form from a practice, always with three lines', () => {
    expect(practiceInput(null)).toBe(EMPTY_PRACTICE);
    expect(practiceInput({name: 'Testbrook Surgery', addressLines: ['1 Test Street'], postcode: 'ZZ1 1ZZ'})).toEqual({
      name: 'Testbrook Surgery',
      odsCode: '',
      addressLines: ['1 Test Street', '', ''],
      postcode: 'ZZ1 1ZZ',
      phone: '',
      secureEmail: '',
    });
  });

  it('sends blank details as none, without blank lines, trimmed', () => {
    expect(toPracticeRequest({...EMPTY_PRACTICE, name: ' Testbrook Surgery ', addressLines: [' ', '1 Test Street ', ''], phone: ' 01632 960001'})).toEqual({
      name: 'Testbrook Surgery',
      odsCode: null,
      addressLines: ['1 Test Street'],
      postcode: null,
      phone: '01632 960001',
      secureEmail: null,
    });
  });

  it('gives the address on one line', () => {
    expect(practiceAddress({addressLines: ['1 Test Street'], postcode: 'ZZ1 1ZZ'})).toBe('1 Test Street, ZZ1 1ZZ');
    expect(practiceAddress({})).toBe('');
  });
});
