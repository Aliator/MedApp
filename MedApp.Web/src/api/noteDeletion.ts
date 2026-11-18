/** Why a note is deleted, or asked to be: the API's fixed choices (stored by number there; sent and read by name). */
export const NOTE_DELETION_REASONS = [
  {value: 'WrongPatient', label: 'Wrong patient'},
  {value: 'WrittenInError', label: 'Written in error'},
  {value: 'Duplicate', label: 'Duplicate'},
  {value: 'Other', label: 'Other'},
] as const;
export type NoteDeletionReason = (typeof NOTE_DELETION_REASONS)[number]['value'];

/** The most a reason's detail can hold, as the API allows. */
export const MAX_DELETION_DETAIL_LENGTH = 500;

/** Whether a value is one of the reasons. */
export const isNoteDeletionReason = (value: string | null | undefined): value is NoteDeletionReason =>
  NOTE_DELETION_REASONS.some((r) => r.value === value);

/** A reason in words: "WrongPatient" → "Wrong patient"; anything unknown as it came. */
export const noteDeletionReasonLabel = (reason: string | null | undefined) =>
  NOTE_DELETION_REASONS.find((r) => r.value === reason)?.label ?? reason ?? '';

/** A reason and its detail in words: "Wrong patient: belongs to the other pathway", or just the reason. */
export function describeNoteDeletionReason(reason: string | null | undefined, detail?: string | null): string {
  const label = noteDeletionReasonLabel(reason);
  const more = detail?.trim();
  return label && more ? `${label}: ${more}` : label || more || '';
}

/** Whether a reason needs a detail (Other). */
export const needsDetail = (reason: string | null | undefined) => reason === 'Other';
