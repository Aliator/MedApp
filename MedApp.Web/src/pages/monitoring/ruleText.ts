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
  return parts;
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
