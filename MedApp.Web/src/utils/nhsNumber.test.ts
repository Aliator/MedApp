import {formatNhsNumber, isPlaceholderNhsNumber, isValidNhsNumber, normaliseNhsNumber, spokenNhsNumber} from './nhsNumber';

// Synthetic test numbers only (the 999 range is never issued to real patients).
const VALID = ['9990000018', '9990000026', '9990000034', '9990000042', '9990000050', '9990000069', '9990000077'];

describe('normaliseNhsNumber', () => {
  it('takes out spaces and other whitespace', () => {
    expect(normaliseNhsNumber(' 999 000\t0018 ')).toBe('9990000018');
    expect(normaliseNhsNumber('')).toBe('');
  });
});

describe('isValidNhsNumber', () => {
  it.each(VALID)('accepts %s, with or without spaces', (number) => {
    expect(isValidNhsNumber(number)).toBe(true);
    expect(isValidNhsNumber(formatNhsNumber(number))).toBe(true);
  });

  it('refuses a wrong check digit', () => {
    expect(isValidNhsNumber('9990000019')).toBe(false);
  });

  it('refuses a number whose check digit would be 10', () => {
    for (let last = 0; last <= 9; last++) expect(isValidNhsNumber(`999000014${last}`)).toBe(false);
  });

  it('accepts a check digit of 0, where the sum leaves no remainder', () => {
    // 999000005: 9×10 + 9×9 + 9×8 + 5×2 = 253 = 23 × 11, so 11 − 0 = 11, which means 0.
    expect(isValidNhsNumber('9990000050')).toBe(true);
    expect(isValidNhsNumber('9990000051')).toBe(false);
  });

  it('refuses anything but ten digits', () => {
    expect(isValidNhsNumber('')).toBe(false);
    expect(isValidNhsNumber('999000001')).toBe(false);
    expect(isValidNhsNumber('99900000188')).toBe(false);
    expect(isValidNhsNumber('999-000-0018')).toBe(false);
    expect(isValidNhsNumber('99900000a8')).toBe(false);
  });
});

describe('isPlaceholderNhsNumber', () => {
  const PLACEHOLDERS = [...'0123456789'].map((d) => d.repeat(10)).concat(['0123456789', '9876543210']);

  it.each(PLACEHOLDERS)('treats %s as a placeholder, which the check digit alone would let through', (number) => {
    expect(isPlaceholderNhsNumber(number)).toBe(true);
    expect(isPlaceholderNhsNumber(formatNhsNumber(number))).toBe(true);
    expect(isValidNhsNumber(number)).toBe(false);
  });

  it('passes real-looking numbers', () => {
    for (const number of VALID) expect(isPlaceholderNhsNumber(number)).toBe(false);
    expect(isPlaceholderNhsNumber('1111111112')).toBe(false);
    expect(isPlaceholderNhsNumber('')).toBe(false);
  });
});

describe('formatNhsNumber', () => {
  it('groups ten digits 3, 3 and 4', () => {
    expect(formatNhsNumber('9990000018')).toBe('999 000 0018');
    expect(formatNhsNumber('999 0000 018')).toBe('999 000 0018');
  });

  it('leaves anything else as it was', () => {
    expect(formatNhsNumber('12345')).toBe('12345');
    expect(formatNhsNumber('')).toBe('');
  });
});

describe('spokenNhsNumber', () => {
  it('reads the number a digit at a time, in its groups', () => {
    expect(spokenNhsNumber('9990000018')).toBe('9 9 9, 0 0 0, 0 0 1 8');
  });

  it('leaves anything else as it was', () => {
    expect(spokenNhsNumber('123')).toBe('123');
  });
});
