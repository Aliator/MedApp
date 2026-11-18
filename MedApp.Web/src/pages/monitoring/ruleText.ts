/** A piece of a rule's sentence; the parts an admin chose are `strong`. */
export interface SentencePart {
  text: string;
  strong?: boolean;
}

/**
 * What a sentence needs to know about a rule: each choice's ID (null for "any") and its name now (null for one
 * since deleted, when its ID is set).
 */
export interface RuleDescription {
  event: string;
  subjectUserId?: string | null;
  subjectUsername?: string | null;
  minimumPriority?: string | null;
  templateId?: string | null;
  templateName?: string | null;
  stageDefinitionId?: string | null;
  stageName?: string | null;
  thresholdDays?: number | null;
  teamId?: string | null;
  teamName?: string | null;
}

/** The choices for "only pathways of this priority or higher", and how each reads in a sentence. */
export const MINIMUM_PRIORITIES = [
  {value: 'Medium', label: 'Medium or higher', reads: 'Medium, High or Urgent'},
  {value: 'High', label: 'High or higher', reads: 'High or Urgent'},
  {value: 'Urgent', label: 'Urgent only', reads: 'Urgent'},
] as const;

const named = (id: string | null | undefined, name: string | null | undefined, deleted: string): SentencePart | null =>
  id ? (name ? {text: name, strong: true} : {text: deleted}) : null;

/** "a pathway", "an Urgent priority pathway", "a High or Urgent priority pathway from the Knee template". */
function pathway(rule: RuleDescription): SentencePart[] {
  // Low or higher is every pathway, so it reads as no condition.
  const priority = MINIMUM_PRIORITIES.find((p) => p.value === rule.minimumPriority)?.reads;
  const parts: SentencePart[] = priority
    ? [{text: /^[AEIOU]/.test(priority) ? 'an ' : 'a '}, {text: priority, strong: true}, {text: ' priority pathway'}]
    : [{text: 'a pathway'}];
  const template = named(rule.templateId, rule.templateName, 'a deleted template');
  if (template) parts.push({text: template.strong ? ' from the ' : ' from '}, template, ...(template.strong ? [{text: ' template'}] : []));
  return [...parts, ...team(rule)];
}

/** " in Dermatology" (strong), " in a deleted team", or nothing for any. */
function team(rule: RuleDescription): SentencePart[] {
  const chosen = named(rule.teamId, rule.teamName, 'a deleted team');
  return chosen ? [{text: ' in '}, chosen] : [];
}

/** "a High or Urgent priority referral in Dermatology, on the Knee template": a referral alert's subject. */
function referral(rule: RuleDescription): SentencePart[] {
  const parts = pathway(rule);
  // pathway() says "pathway"; a referral alert is about the referral (its pathway's template, once accepted).
  return parts.map((p) => (p.strong ? p : {...p, text: p.text.replace('pathway', 'referral')}));
}

/** "2 days" or "1 day" (strong). */
const days = (rule: RuleDescription): SentencePart => {
  const n = rule.thresholdDays ?? 0;
  return {text: `${n} ${n === 1 ? 'day' : 'days'}`, strong: true};
};

/** A service alert's sentence: what waits, and for how long. */
function alert(rule: RuleDescription): SentencePart[] | null {
  const zero = (rule.thresholdDays ?? 0) === 0;
  const who = named(rule.subjectUserId, rule.subjectUsername, 'a deleted user');
  switch (rule.event) {
    case 'ReferralNotTriaged':
      return [{text: 'Notify me when '}, ...referral(rule), {text: ' has awaited triage more than '}, days(rule)];
    case 'ResultNotSignedOff':
      return [
        {text: 'Notify me when a result'},
        ...(who ? [{text: ' for '}, who] : []),
        {text: ' on '},
        ...pathway(rule),
        {text: ' has awaited sign-off more than '},
        days(rule),
      ];
    case 'VisitNotBooked':
      return [{text: 'Notify me when a visit for '}, ...stage(rule), {text: ' on '}, ...pathway(rule), {text: ' has awaited booking more than '}, days(rule)];
    case 'SpecimenOverdue':
      return [
        {text: 'Notify me when a specimen from '},
        ...stage(rule),
        {text: ' on '},
        ...pathway(rule),
        ...(zero ? [{text: ' is past its expected turnaround'}] : [{text: ' is more than '}, days(rule), {text: ' past its expected turnaround'}]),
      ];
    case 'WaitBreached':
      return [
        {text: 'Notify me when the waiting-time target of '},
        ...referral(rule),
        ...(zero ? [{text: ' has passed'}] : [{text: ' is less than '}, days(rule), {text: ' away, or passed'}]),
      ];
    case 'PauseReviewPassed':
      return [
        {text: 'Notify me when the review date of a paused clock on '},
        ...referral(rule),
        ...(zero ? [{text: ' has passed'}] : [{text: ' passed more than '}, days(rule), {text: ' ago'}]),
      ];
    default:
      return null;
  }
}

/** "Bloods" (strong), "a stage", or "a deleted stage". */
function stage(rule: RuleDescription): SentencePart[] {
  return [named(rule.stageDefinitionId, rule.stageName, 'a deleted stage') ?? {text: 'a stage'}];
}

/**
 * A rule as a plain sentence, e.g. "Notify me when **Sam** finishes **Bloods** on a **High or Urgent** priority
 * pathway". Used for the rules list and for the preview while one is edited, so both always agree.
 */
export function describeRule(rule: RuleDescription): SentencePart[] {
  const alertSentence = alert(rule);
  if (alertSentence) return alertSentence;
  const who = named(rule.subjectUserId, rule.subjectUsername, 'a deleted user') ?? {text: 'anyone'};
  const what = pathway(rule);
  const action: SentencePart[] = (() => {
    switch (rule.event) {
      case 'PathwayCreated':
        return [{text: ' creates '}, ...what];
      case 'StageFinished':
        return [{text: ' finishes '}, ...stage(rule), {text: ' on '}, ...what];
      case 'PathwayCompleted':
        return [{text: ' discharges '}, ...what];
      case 'PathwayBlocked':
        return [{text: ' puts '}, ...what, {text: ' on hold'}];
      case 'PathwayCancelled':
        return [{text: ' cancels '}, ...what];
      case 'PathwayAssigned':
        return [{text: ' is assigned to '}, ...what];
      case 'StageUnlocked':
        return [{text: ' unlocks '}, ...stage(rule), {text: ' on '}, ...what];
      case 'StageFinishedWithoutVisit':
        return [{text: ' finishes '}, ...stage(rule), {text: ' without its required visit on '}, ...what];
      case 'DueDateChanged':
        return [{text: ' changes the target date of '}, ...what];
      default:
        return [{text: ` does something to `}, ...what];
    }
  })();
  return [{text: 'Notify me when '}, who, ...action];
}

/** The sentence's words alone, e.g. for a screen reader announcement or a label. */
export const sentenceText = (parts: SentencePart[]) => parts.map((p) => p.text).join('');
