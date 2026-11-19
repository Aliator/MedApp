import type {TemplateStageOptions} from '../../api/pathwaySetup';
import type {OrderLink} from '../../api/pathwayRoutes';
import {MAX_BRANCH_LENGTH} from '../../api/stageSkips';

/**
 * A template stage's options as the editor holds them (docs/centre-phases-3-5-plan.md, decisions 113 and 117):
 * whether staff may skip it, whether it's a branch point (finishing it, the clinician chooses a branch), and the
 * branch it's in ("" for a stage that always runs).
 */
export interface StageOptionsInput {
  isOptional: boolean;
  choosesBranch: boolean;
  branch: string;
  /** The lab works it (the relay, MC7): its owner is the lab team, not the template's own. */
  byLab?: boolean;
}

export const PLAIN_STAGE: StageOptionsInput = {isOptional: false, choosesBranch: false, branch: ''};

// The API's words (`TemplateBranchRules`), so the page says what a save would be refused for.
export const LAST_STAGE_MESSAGE = "The last stage always runs: it can't be optional, a branch point or in a branch.";
export const BRANCH_POINT_OPTIONS_MESSAGE = "A branch point always runs: it can't be optional or in a branch itself.";
export const BRANCH_WITHOUT_POINT_MESSAGE = 'A stage in a branch must come straight after its branch point or another stage in a branch.';
export const TOO_FEW_BRANCHES_MESSAGE = 'A branch point needs at least two branches after it, before the next branch point or the end.';
export const BRANCH_TOO_LONG_MESSAGE = `A branch name must not exceed ${MAX_BRANCH_LENGTH} characters.`;

const branchName = (options: StageOptionsInput) => options.branch.trim();

/** Whether a stage has anything but the defaults. */
export const hasOptions = (options: StageOptionsInput) => options.isOptional || options.choosesBranch || branchName(options) !== '';

/**
 * Whether a stage comes after a branch point, with only stages in a branch between, so it can be put in a branch: a
 * branch's stages sit together after their branch point, and a plain stage ends the branches (their join, MC8).
 */
export function isAfterBranchPoint(rows: readonly StageOptionsInput[], index: number): boolean {
  for (let i = index - 1; i >= 0; i--) {
    if (rows[i].choosesBranch) return true;
    if (branchName(rows[i]) === '') return false;
  }
  return false;
}

/**
 * What's wrong with each stage's options, by its place in the list (the same checks the API makes, in its words): a
 * branch point needs at least two branches (different names, ignoring case) before the next branch point or the end,
 * and can't be optional or in a branch itself; a stage in a branch comes straight after a branch point or another stage
 * in a branch (a plain stage ends the branches); the last stage always
 * runs. The API's check is the last word; these only say so before saving.
 */
export function stageOptionProblems(rows: readonly StageOptionsInput[]): Map<number, string> {
  const problems = new Map<number, string>();
  rows.forEach((row, i) => {
    const name = branchName(row);
    if (name.length > MAX_BRANCH_LENGTH) problems.set(i, BRANCH_TOO_LONG_MESSAGE);
    else if (i === rows.length - 1 && hasOptions(row)) problems.set(i, LAST_STAGE_MESSAGE);
    else if (row.choosesBranch) {
      if (row.isOptional || name !== '') problems.set(i, BRANCH_POINT_OPTIONS_MESSAGE);
      else if (branchesAfter(rows, i).length < 2) problems.set(i, TOO_FEW_BRANCHES_MESSAGE);
    } else if (name !== '' && !isAfterBranchPoint(rows, i)) problems.set(i, BRANCH_WITHOUT_POINT_MESSAGE);
  });
  return problems;
}

/** The branches after a branch point, as first written, up to the next plain stage, branch point or the end. */
export function branchesAfter(rows: readonly StageOptionsInput[], index: number): string[] {
  const names: string[] = [];
  for (const row of rows.slice(index + 1)) {
    const name = branchName(row);
    if (row.choosesBranch || name === '') break;
    if (name && !names.some((n) => n.toLowerCase() === name.toLowerCase())) names.push(name);
  }
  return names;
}

/**
 * What a save sends as `stageOptions`: one for each stage, in order, when any stage has an option or is the lab's
 * (`labTeamId` its owner), or `always` (a template that had a lab's stage, so a cleared one is cleared); nothing when
 * every stage is plain, so a template without branches saves exactly as before (the API reads none as all plain).
 */
export function stageOptionsToSend(
  rows: readonly StageOptionsInput[],
  labTeamId?: string | null,
  always = false,
): TemplateStageOptions[] | undefined {
  const isLabs = (r: StageOptionsInput) => Boolean(r.byLab && labTeamId);
  if (!always && !rows.some((r) => hasOptions(r) || isLabs(r))) return undefined;
  return rows.map((r) => ({
    isOptional: r.isOptional,
    choosesBranch: r.choosesBranch,
    branch: branchName(r) || null,
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
