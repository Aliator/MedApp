import {formatMobile, formatPostcode, isValidMobile, isValidPostcode, normaliseMobile, oneLineAddress} from './contactDetails';

describe('postcodes', () => {
  it.each(['ZZ1 1ZZ', 'zz1 1zz', 'ZZ11ZZ', ' Z Z 1 1 Z Z ', 'Z1 1ZZ', 'ZZ1Z 1ZZ', 'Z1Z 1ZZ', 'ZZ12 3ZZ', 'GIR 0AA', 'gir0aa'])(
    'accepts %s',
    (value) => expect(isValidPostcode(value)).toBe(true),
  );

  it.each(['', 'ZZ1', 'ZZ1 1Z', 'ZZZ1 1ZZ', '1ZZ 1ZZ', 'ZZ1 ZZZ', 'ZZ1-1ZZ', 'ZZ1 1ZZZ'])('refuses %j', (value) =>
    expect(isValidPostcode(value)).toBe(false),
  );

  it('writes one in capitals with one space before its last three characters, as the API stores it', () => {
    expect(formatPostcode('zz11zz')).toBe('ZZ1 1ZZ');
    expect(formatPostcode(' zz1z   1zz ')).toBe('ZZ1Z 1ZZ');
    expect(formatPostcode('zz1')).toBe('ZZ1');
    expect(formatPostcode('')).toBe('');
  });
});

describe('mobiles', () => {
  it.each([
    ['07700 900123', '+447700900123'],
    ['07700900123', '+447700900123'],
    ['+44 7700 900123', '+447700900123'],
    ['+44 (0)7700 900123', '+447700900123'],
    ['0044 7700 900123', '+447700900123'],
    ['07700-900-123', '+447700900123'],
    ['(07700) 900.123', '+447700900123'],
  ])('reads %s as %s', (typed, stored) => {
    expect(normaliseMobile(typed)).toBe(stored);
    expect(isValidMobile(typed)).toBe(true);
  });

  it.each(['', '   ', '01632 960123', '0770090012', '077009001234', '+33 7700 900123', '07700 9OO123', '+447700 900123 ext 2', '7700900123'])(
    'refuses %j',
    (typed) => {
      expect(normaliseMobile(typed)).toBeNull();
      expect(isValidMobile(typed)).toBe(false);
    },
  );

  it('shows one in national form, with a space after the fifth digit', () => {
    expect(formatMobile('+447700900123')).toBe('07700 900123');
    expect(formatMobile('07700900999')).toBe('07700 900999');
    expect(formatMobile('not a number')).toBe('not a number');
  });
});

describe('oneLineAddress', () => {
  it('puts the lines and the postcode on one line, leaving out blanks', () => {
    expect(oneLineAddress(['1 Test Street', ' ', 'Testbrook'], 'ZZ1 1ZZ')).toBe('1 Test Street, Testbrook, ZZ1 1ZZ');
    expect(oneLineAddress(null, null)).toBe('');
    expect(oneLineAddress([], 'ZZ1 1ZZ')).toBe('ZZ1 1ZZ');
  });
});
