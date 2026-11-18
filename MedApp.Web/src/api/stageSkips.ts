/**
 * Skipping a pathway's stages (docs/centre-phases-3-5-plan.md, decisions 113 to 117): the reasons staff choose from
 * when they skip an optional stage, and the one the app gives the stages of a branch the clinician didn't choose.
 * The API stores them by number and sends and reads them by name.
 */
export const STAGE_SKIP_REASONS = [
  {value: 'NotNeeded', label: 'Not needed'},
  {value: 'PatientDeclined', label: 'Patient declined'},
  {value: 'Other', label: 'Other'},
] as const;
export type StageSkipReason = (typeof STAGE_SKIP_REASONS)[number]['value'];

/** Only the app sets this one: another branch was chosen at a branch point. */
export const BRANCH_NOT_CHOSEN = 'BranchNotChosen';

/** The most a skip's detail can hold, as the API allows. */
export const MAX_SKIP_DETAIL_LENGTH = 500;

/** The most a reinstatement's reason can hold, as the API allows. */
export const MAX_REINSTATE_REASON_LENGTH = 500;

/** The most a branch's name can hold, as the API allows. */
export const MAX_BRANCH_LENGTH = 50;

/** A skip reason in words: "PatientDeclined" → "Patient declined"; anything unknown as it came. */
export function skipReasonLabel(reason: string | null | undefined): string {
  if (reason === BRANCH_NOT_CHOSEN) return 'Another branch was chosen';
  return STAGE_SKIP_REASONS.find((r) => r.value === reason)?.label ?? reason ?? '';
}

/** A reason and its detail in words: "Other: seen at another hospital", or just the reason. */
export function describeSkipReason(reason: string | null | undefined, detail?: string | null): string {
  const label = skipReasonLabel(reason);
  const more = detail?.trim();
  return label && more ? `${label}: ${more}` : label || more || '';
}

/** Whether a skip reason needs a detail (Other). */
export const skipNeedsDetail = (reason: string | null | undefined) => reason === 'Other';

/** Whether a skip can be sent: a reason chosen, and a detail that's short enough, and given for Other. */
export const skipFieldsComplete = (reason: string, detail: string) =>
  reason !== '' && detail.length <= MAX_SKIP_DETAIL_LENGTH && (!skipNeedsDetail(reason) || detail.trim() !== '');
