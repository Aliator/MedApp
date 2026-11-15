const words = (text: string) => text.split(/[\s\-/]+/).filter((word) => /^[\p{L}\p{N}]/u.test(word));

/** A name's initials, e.g. "Freya Ahmed" → "FA". */
export const initials = (name: string) =>
  words(name)
    .map((word) => word[0].toUpperCase())
    .join('');

/** A title's acronym, from the first letter of each word, e.g. "Total knee replacement" → "TKR". */
export const acronym = (title: string) =>
  words(title)
    .map((word) => word[0].toUpperCase())
    .join('');

/** A procedure's short label in the side nav: the patient's initials, then the procedure's acronym ("FA TKR"). */
export const procedureNavLabel = (patientName: string, title: string) =>
  [initials(patientName), acronym(title)].filter(Boolean).join(' ') || 'Procedure';
