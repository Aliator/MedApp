/**
 * NHS numbers: ten digits, the last a Modulus 11 check digit (NHS Data Dictionary, "NHS Number"). The API stores
 * them as ten digits with no spaces and checks them itself; these helpers are for typing and showing them.
 */

/** The number as typed with its spaces (and any other whitespace) taken out: "999 000 0018" → "9990000018". */
export const normaliseNhsNumber = (value: string) => value.replace(/\s+/g, '');

/** A digit ten times over (`0000000000` to `9999999999`). */
const ONE_DIGIT_REPEATED = /^(\d)\1{9}$/;

/**
 * Whether a value (spaces allowed) is one of the numbers people type when a form insists on one they don't have: a
 * digit ten times over (`0000000000` to `9999999999`), `0123456789` or `9876543210`. They all pass the check digit,
 * so they're refused separately. The API refuses the same ones.
 */
export function isPlaceholderNhsNumber(value: string): boolean {
  const digits = normaliseNhsNumber(value);
  return ONE_DIGIT_REPEATED.test(digits) || digits === '0123456789' || digits === '9876543210';
}

/**
 * Whether a value (spaces allowed) is a valid NHS number: ten digits whose check digit is right, and not a
 * placeholder (`isPlaceholderNhsNumber`). The first nine digits are weighted 10 down to 2 and summed; the check digit
 * is 11 minus the sum's remainder mod 11, with 11 meaning 0, and 10 never valid.
 */
export function isValidNhsNumber(value: string): boolean {
  const digits = normaliseNhsNumber(value);
  if (!/^\d{10}$/.test(digits) || isPlaceholderNhsNumber(digits)) return false;
  let sum = 0;
  for (let i = 0; i < 9; i++) sum += Number(digits[i]) * (10 - i);
  const check = 11 - (sum % 11);
  if (check === 10) return false;
  return (check === 11 ? 0 : check) === Number(digits[9]);
}

/**
 * An NHS number as people read it, in groups of 3, 3 and 4: "9990000018" → "999 000 0018". Anything that isn't ten
 * digits (after taking out spaces) comes back as it was.
 */
export function formatNhsNumber(value: string): string {
  const digits = normaliseNhsNumber(value);
  return /^\d{10}$/.test(digits) ? `${digits.slice(0, 3)} ${digits.slice(3, 6)} ${digits.slice(6)}` : value;
}

/**
 * The number for a screen reader, a digit at a time ("9 9 9, 0 0 0, 0 0 1 8"), so it isn't read as one big number.
 * Anything that isn't ten digits comes back as it was.
 */
export function spokenNhsNumber(value: string): string {
  const digits = normaliseNhsNumber(value);
  if (!/^\d{10}$/.test(digits)) return value;
  return [digits.slice(0, 3), digits.slice(3, 6), digits.slice(6)].map((group) => group.split('').join(' ')).join(', ');
}
