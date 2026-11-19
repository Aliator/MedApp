import type {TemplateStageOptions} from '../../api/pathwaySetup';
import type {OrderLink, TemplateOutcome} from '../../api/pathwayRoutes';

/**
 * One outcome of a choice node as the editor holds it: its name, and the stages it leads to (by row key; none: it ends
 * the route, as a referral recorded there does).
 */
export interface OutcomeRow {
  key: string;
  name: string;
  nextKeys: string[];
}

/**
 * A template stage's options as the editor holds them: whether staff may skip it, whether it's a choice node (its kind:
 * an assessment or a lab result, "" for a plain stage) and, on a choice node, its outcomes in order, each naming the
 * stages after it that it leads to (one stage may be led to by several outcomes, and one outcome may lead to several).
 */
export interface StageOptionsInput {
  isOptional: boolean;
  choiceKind: string;
  outcomes: OutcomeRow[];
  /** The lab works it (the relay, MC7): its owner is the lab team, not the template's own. */
  byLab?: boolean;
}

let outcomeCount = 0;
/** A new outcome, with a key of its own. */
export const outcomeRow = (name = '', nextKeys: string[] = []): OutcomeRow => ({key: `outcome-${++outcomeCount}`, name, nextKeys});

export const PLAIN_STAGE: StageOptionsInput = {isOptional: false, choiceKind: '', outcomes: []};

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
 * A choice node's outcomes as a save sends them (by its 0-based index): each named one, in order, leading to the stages
 * it names that come straight after the choice node (`links`), in stage order. An unnamed outcome isn't sent: a stage
 * only it leads to says so (`templateGraphProblems`).
 */
export function outcomesFromRows(
  rows: readonly (Pick<StageOptionsInput, 'outcomes'> & {key: string})[],
  links: readonly OrderLink[],
  index: number,
): TemplateOutcome[] {
  const order = new Map(rows.map((r, i) => [r.key, i + 1]));
  const after = new Set(links.filter((l) => l.fromStageOrder === index + 1).map((l) => l.toStageOrder ?? 0));
  return (rows[index]?.outcomes ?? [])
    .filter((o) => o.name.trim() !== '')
    .map((o) => ({
      name: o.name.trim(),
      nextStageOrders: [...new Set(o.nextKeys.map((key) => order.get(key) ?? 0))].filter((next) => after.has(next)).sort((x, y) => x - y),
    }));
}

/** A saved choice node's outcomes onto the editor's rows: each with the rows it leads to, by key. */
export function outcomeRows(outcomes: readonly TemplateOutcome[] | null | undefined, rows: readonly {key: string}[]): OutcomeRow[] {
  return (outcomes ?? []).map((o) =>
    outcomeRow(o.name ?? '', (o.nextStageOrders ?? []).flatMap((next) => (rows[next - 1] ? [rows[next - 1].key] : []))),
  );
}

/**
 * What a save sends as `stageOptions`: one for each stage, in order, when any stage has an option or is the lab's
 * (`labTeamId` its owner), or `always` (a template that had a lab's stage, so a cleared one is cleared); nothing when
 * every stage is plain, so a plain list saves exactly as before. A choice node's outcomes come from `links`.
 */
export function stageOptionsToSend(
  rows: readonly (StageOptionsInput & {key: string})[],
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

/** What a save sends as `links`: each row's predecessors, by 1-based order, sorted by the stage they come from, then to. */
export function linksFromRoutes(rows: readonly {key: string}[], routes: Readonly<Routes>): OrderLink[] {
  const order = new Map(rows.map((r, i) => [r.key, i + 1]));
  return rows
    .flatMap((row, i) =>
      [...new Set(routes[row.key] ?? [])]
        .map((key) => order.get(key) ?? 0)
        .filter((from) => from > 0)
        .map((from) => ({fromStageOrder: from, toStageOrder: i + 1})),
    )
    .sort((a, b) => a.fromStageOrder - b.fromStageOrder || a.toStageOrder - b.toStageOrder);
}
