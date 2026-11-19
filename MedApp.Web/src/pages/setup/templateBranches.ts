import type {TemplateStageOptions} from '../../api/pathwaySetup';
import type {OrderLink, TemplateOutcome} from '../../api/pathwayRoutes';
import type {PathwayTemplate} from '../../api/pathways';

/**
 * A template stage's options as the editor holds them: whether staff may skip it, whether it's a choice node (its kind:
 * an assessment or a lab result, "" for a plain stage), and, for a stage straight after a choice node, the outcome that
 * leads to it ("" when none does yet). Outcomes are saved on their choice node (`outcomesFromRows`).
 */
export interface StageOptionsInput {
  isOptional: boolean;
  choiceKind: string;
  outcome: string;
  /** The lab works it (the relay, MC7): its owner is the lab team, not the template's own. */
  byLab?: boolean;
}

export const PLAIN_STAGE: StageOptionsInput = {isOptional: false, choiceKind: '', outcome: ''};

// The API's words for a list (a template without routes), so the page says what a save would be refused for.
export const LAST_STAGE_MESSAGE = "The last stage can't be optional.";

/** Whether a stage has anything but the defaults. */
export const hasOptions = (options: StageOptionsInput) => options.isOptional || options.choiceKind !== '';

/** What's wrong with a list's options (choice nodes need routes, so a list has only the last stage's rule). */
export function stageOptionProblems(rows: readonly StageOptionsInput[]): Map<number, string> {
  const problems = new Map<number, string>();
  if (rows.length > 0 && rows[rows.length - 1].isOptional) problems.set(rows.length - 1, LAST_STAGE_MESSAGE);
  return problems;
}

/**
 * A choice node's outcomes from the rows (by its 0-based index): one per outcome name given on the stages straight after
 * it (ignoring case; the first spelling kept), each leading to every such stage, in order. Stages after it with no
 * name are in none (`templateGraphProblems` says so).
 */
export function outcomesFromRows(rows: readonly StageOptionsInput[], links: readonly OrderLink[], index: number): TemplateOutcome[] {
  const outcomes: TemplateOutcome[] = [];
  const nexts = links.filter((l) => l.fromStageOrder === index + 1).map((l) => l.toStageOrder ?? 0).sort((a, b) => a - b);
  for (const next of nexts) {
    const name = rows[next - 1]?.outcome.trim() ?? '';
    if (!name) continue;
    const same = outcomes.find((o) => (o.name ?? '').toLowerCase() === name.toLowerCase());
    if (same) same.nextStageOrders = [...(same.nextStageOrders ?? []), next];
    else outcomes.push({name, nextStageOrders: [next]});
  }
  return outcomes;
}

/** Each row's outcome name from a saved template: the name of the outcome of a choice node that leads to it. */
export function rowOutcomes(template: Pick<PathwayTemplate, 'stages'> | null, count: number): string[] {
  const names = new Array<string>(count).fill('');
  for (const stage of template?.stages ?? [])
    for (const outcome of stage.outcomes ?? [])
      for (const next of outcome.nextStageOrders ?? []) if (next >= 1 && next <= count) names[next - 1] = outcome.name ?? '';
  return names;
}

/**
 * What a save sends as `stageOptions`: one for each stage, in order, when any stage has an option or is the lab's
 * (`labTeamId` its owner), or `always` (a template that had a lab's stage, so a cleared one is cleared); nothing when
 * every stage is plain, so a plain list saves exactly as before. A choice node's outcomes come from `links`.
 */
export function stageOptionsToSend(
  rows: readonly StageOptionsInput[],
  labTeamId?: string | null,
  always = false,
  links: readonly OrderLink[] | null = null,
): TemplateStageOptions[] | undefined {
  const isLabs = (r: StageOptionsInput) => Boolean(r.byLab && labTeamId);
  if (!always && !rows.some((r) => hasOptions(r) || isLabs(r))) return undefined;
  return rows.map((r, i) => ({
    isOptional: r.isOptional,
    choiceKind: r.choiceKind || null,
    outcomes: r.choiceKind && links ? outcomesFromRows(rows, links, i) : null,
    ...(isLabs(r) ? {ownerTeamId: labTeamId} : {}),
  }));
}

/** The route editor's links (MC9): each row's predecessors, by row key. */
export type Routes = Record<string, string[]>;

/** Routes from a template's links (by 1-based order) onto the editor's rows. */
export function routesFromLinks(rows: readonly {key: string}[], links: readonly OrderLink[]): Routes {
  return Object.fromEntries(
    rows.map((row, i) => [
      row.key,
      links.filter((l) => l.toStageOrder === i + 1).flatMap((l) => (rows[(l.fromStageOrder ?? 0) - 1] ? [rows[(l.fromStageOrder ?? 0) - 1].key] : [])),
    ]),
  );
}

/**
 * Routes kept valid as rows move, come and go: a row follows only rows above it (so there are no cycles), and one left
 * following none (but the first) follows the row above, as a new stage at the end does.
 */
export function tidyRoutes(rows: readonly {key: string}[], routes: Readonly<Routes>): Routes {
  return Object.fromEntries(
    rows.map((row, i) => {
      const above = new Set(rows.slice(0, i).map((r) => r.key));
      const kept = (routes[row.key] ?? []).filter((key) => above.has(key));
      return [row.key, i === 0 ? [] : kept.length > 0 ? kept : [rows[i - 1].key]];
    }),
  );
}

/** What a save sends as `links`: each row's predecessors, by 1-based order, in order. */
export function linksFromRoutes(rows: readonly {key: string}[], routes: Readonly<Routes>): OrderLink[] {
  const order = new Map(rows.map((r, i) => [r.key, i + 1]));
  return rows.flatMap((row, i) =>
    (routes[row.key] ?? [])
      .map((key) => order.get(key) ?? 0)
      .filter((from) => from > 0)
      .sort((a, b) => a - b)
      .map((from) => ({fromStageOrder: from, toStageOrder: i + 1})),
  );
}
