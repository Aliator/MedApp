/**
 * UK postcodes and mobile numbers, by format only (nothing checks that a postcode exists or a number is in use). The
 * API checks and stores them itself (`UkPostcodes`, `UkPhoneNumbers`): a postcode in upper case with one space before
 * its last three characters, a mobile in international form (`+447…`). These helpers mirror its rules, so the patient
 * form can say what's wrong as it's typed, and show what the API sends back.
 */

/** A postcode's outward code (one or two letters, a digit, then maybe a letter or digit), then its inward code. */
const POSTCODE = /^[A-Z]{1,2}[0-9][A-Z0-9]?[0-9][A-Z]{2}$/;

/** The postcode with every space taken out, in capitals: "zz1 1zz" → "ZZ11ZZ". */
const compactPostcode = (value: string) => value.replace(/\s+/g, '').toUpperCase();

/**
 * A postcode as the API stores it: capitals, with one space before the last three characters ("zz11zz" → "ZZ1 1ZZ").
 * Empty for none; anything three characters or shorter comes back compacted.
 */
export function formatPostcode(value: string): string {
  const compact = compactPostcode(value);
  return compact.length <= 3 ? compact : `${compact.slice(0, -3)} ${compact.slice(-3)}`;
}

/** Whether a value is in a UK postcode's format, in any case and with spaces anywhere (`GIR 0AA` included). */
export function isValidPostcode(value: string): boolean {
  const compact = compactPostcode(value);
  return POSTCODE.test(compact) || compact === 'GIR0AA';
}

/**
 * A phone number's digits in national form (a leading 0 in place of +44 or 0044), or null when it holds anything but
 * digits, a leading +, spaces, dashes, dots and brackets (the API's `UkPhoneNumbers.NationalDigits`).
 */
function nationalDigits(value: string): string | null {
  let text = value.trim();
  if (!text) return null;
  const isInternational = text.startsWith('+');
  if (isInternational) text = text.slice(1);
  if (!/^[0-9 \-.()]*$/.test(text)) return null;
  const digits = text.replace(/[^0-9]/g, '');
  if (isInternational) return digits.startsWith('44') ? `0${digits.slice(2).replace(/^0+/, '')}` : null;
  if (digits.startsWith('0044')) return `0${digits.slice(4).replace(/^0+/, '')}`;
  return digits;
}

/**
 * A UK mobile as typed (`07700 900123`, `+44 7700 900123`, `0044 7700 900123`, `+44 (0)7700 900123`; spaces, dashes,
 * dots and brackets ignored) in international form, as the API stores it (`+447700900123`); null for anything that
 * isn't a UK mobile.
 */
export function normaliseMobile(value: string): string | null {
  const national = nationalDigits(value);
  return national && /^07\d{9}$/.test(national) ? `+44${national.slice(1)}` : null;
}

/** Whether a value, as typed, is a UK mobile: `07` and nine more digits, or the same after `+44` or `0044`. */
export const isValidMobile = (value: string) => normaliseMobile(value) !== null;

/**
 * A mobile as people read it, in national form with a space after the fifth digit: "+447700900123" → "07700 900123".
 * Anything that isn't a UK mobile comes back as it was.
 */
export function formatMobile(value: string): string {
  const international = normaliseMobile(value);
  if (!international) return value;
  const national = `0${international.slice(3)}`;
  return `${national.slice(0, 5)} ${national.slice(5)}`;
}

/** An address's lines and postcode on one line, the postcode last: "1 Test Street, Testbrook, ZZ1 1ZZ"; empty for none. */
export const oneLineAddress = (lines: readonly string[] | null | undefined, postcode?: string | null) =>
  [...(lines ?? []), postcode ?? ''].map((part) => part.trim()).filter(Boolean).join(', ');
