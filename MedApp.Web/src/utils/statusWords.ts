/**
 * The API's names for statuses and priorities, in words. Two pathway statuses are shown by another name than the
 * API's (docs/glossary.md): InReview is "Awaiting review" and Blocked is "On hold"; Completed is shown as itself (never
 * "Discharged", a word tied to a condition). The
 * API's own messages say the same (`PathwayWorkflow.Describe`).
 */
const SHOWN_AS: Record<string, string> = {
  InReview: 'awaiting review',
  Blocked: 'on hold',
};

/** A status or priority in lower-case words: "InProgress" → "in progress", "Blocked" → "on hold". */
export const statusWords = (value: string): string => SHOWN_AS[value] ?? value.replace(/(?<=[a-z])(?=[A-Z])/g, ' ').toLowerCase();

/** A status in words, as a label: "InProgress" → "In progress", "Blocked" → "On hold". */
export function statusLabel(status: string): string {
  const words = statusWords(status);
  return words.charAt(0).toUpperCase() + words.slice(1);
}
