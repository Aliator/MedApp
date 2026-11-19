import type { Schemas } from "./client";
import { MAX_BRANCH_LENGTH } from "./stageSkips";

/**
 * Tree pathways (MC8, `docs/multi-clinic-plan.md`): a template's and a pathway's stages are linked into a directed graph
 * with no cycles. A stage with several successors is a fork (every route runs, side by side) or a branch point (the
 * clinician chooses one); a stage with several predecessors is a join, waiting until each is finished or skipped.
 */

export type TemplateLink = Schemas["PathwayTemplateLinkResponse"];

/** What `chainLinks` reads of a template stage: its place and its branch options. */
interface ChainStage {
  stageOrder?: number;
  choosesBranch?: boolean;
  branch?: string | null;
}

/**
 * The links a template written as a list has (today's templates, and the editor until the route editor, MC9): each
 * stage to the next; a branch point to each branch's first stage, each branch's stages in order, and each branch's
 * last stage to the stage after the branches (their join). A branch's stages sit straight after their branch point
 * (or another branch's stages): a plain stage ends the branches, so a branch name after it is read as a plain stage.
 */
export function chainLinks(stages: readonly ChainStage[]): TemplateLink[] {
  const ordered = [...stages].sort(
    (a, b) => (a.stageOrder ?? 0) - (b.stageOrder ?? 0),
  );
  const links: TemplateLink[] = [];
  const link = (from: number, to: number) =>
    links.push({ fromStageOrder: from, toStageOrder: to });
  let tails: number[] = [];
  let branchPoint: number | null = null;
  const branchEnds = new Map<string, number>();
  for (const stage of ordered) {
    const order = stage.stageOrder ?? 0;
    const branch = stage.branch?.trim().toLowerCase();
    if (branchPoint !== null && branch && !stage.choosesBranch) {
      link(branchEnds.get(branch) ?? branchPoint, order);
      branchEnds.set(branch, order);
      continue;
    }
    if (branchPoint !== null) {
      tails = branchEnds.size > 0 ? [...branchEnds.values()] : [branchPoint];
      branchPoint = null;
      branchEnds.clear();
    }
    for (const tail of tails) link(tail, order);
    tails = [order];
    if (stage.choosesBranch) branchPoint = order;
  }
  return links;
}

/** Why a template with routes side by side can't have its stages changed as a list: a save must send its links (MC9). */
export const PARALLEL_ROUTES_LOCKED =
  "This template has routes that run side by side, so its stages can only be changed with their routes (links).";

const key = (l: TemplateLink) => `${l.fromStageOrder}>${l.toStageOrder}`;

/** Whether a template's links are more than its list says: routes that run side by side, which only the route editor changes. */
export function hasParallelRoutes(
  template: Pick<Schemas["PathwayTemplateResponse"], "stages" | "links">,
): boolean {
  if (!template.links) return false;
  const chain = new Set(chainLinks(template.stages ?? []).map(key));
  return (
    template.links.length !== chain.size ||
    template.links.some((l) => !chain.has(key(l)))
  );
}

/** "A", "A and B", "A, B and C". */
export const andList = (words: readonly string[]) =>
  words.length < 2
    ? (words[0] ?? "")
    : `${words.slice(0, -1).join(", ")} and ${words[words.length - 1]}`;

/**
 * What a join says, under its title: which stages it waits for ("3. Histology (lab) and 5. Lab tests (lab)"); nothing
 * for a stage with one predecessor or none.
 */
export function waitsForWords(
  stage: Pick<Schemas["PatientPathwayStageResponse"], "predecessorIds">,
  stages: readonly Pick<
    Schemas["PatientPathwayStageResponse"],
    "id" | "stageOrder" | "name"
  >[],
): string | null {
  const before = stages.filter((s) =>
    stage.predecessorIds?.includes(s.id ?? ""),
  );
  if (before.length < 2) return null;
  return `Starts when each of these is finished or skipped: ${andList(before.map((s) => `${s.stageOrder}. ${s.name}`))}.`;
}

/** A template stage's predecessors, by order ("After 1. First clinic visit"), for a template with parallel routes. */
export function afterWords(
  order: number,
  template: Pick<Schemas["PathwayTemplateResponse"], "stages" | "links">,
): string | null {
  const before = (template.links ?? [])
    .filter((l) => l.toStageOrder === order)
    .map((l) => l.fromStageOrder ?? 0);
  if (before.length === 0) return null;
  const names = [...before]
    .sort((a, b) => a - b)
    .map(
      (o) =>
        `${o}. ${template.stages?.find((s) => s.stageOrder === o)?.name ?? "Unknown stage"}`,
    );
  return `After ${andList(names)}`;
}

/** The longest reason for adding a branch (MC10): the API's limit. */
export const MAX_BRANCH_REASON_LENGTH = 500;

/** A link by 1-based stage order, as a template sends and keeps it. */
export interface OrderLink {
  fromStageOrder?: number;
  toStageOrder?: number;
}

/**
 * How far each stage sits in from the main line, by its 1-based order (index 0 is unused): 0 on the main line, one
 * more on each route after a fork or branch point, one less again at their join. The tree view's outline (MC9) and the
 * route editor indent by it.
 */
export function routeDepths(count: number, links: readonly OrderLink[]): number[] {
  const depths = new Array<number>(count + 1).fill(0);
  const successors = (order: number) => links.filter((l) => l.fromStageOrder === order).length;
  // What a link carries into the stage it leads to: deeper by one out of a stage with several routes after it.
  const carried = (from: number) => depths[from] + (successors(from) > 1 ? 1 : 0);
  for (let order = 1; order <= count; order++) {
    const before = links.filter((l) => l.toStageOrder === order && (l.fromStageOrder ?? 0) < order).map((l) => carried(l.fromStageOrder ?? 0));
    depths[order] = before.length === 0 ? 0 : before.length === 1 ? before[0] : Math.max(0, Math.min(...before) - 1);
  }
  return depths;
}

/** What `templateGraphProblems` reads of a stage: its options. */
export interface GraphStage {
  isOptional?: boolean;
  choosesBranch?: boolean;
  branch?: string | null;
}

// `TemplateGraphRules`' words (MC9), so the editor says what a save would be refused for.
export const BACKWARD_LINK_MESSAGE = "A stage can only come after a stage above it.";
export const NO_PREDECESSOR_MESSAGE = "Choose what it comes after: only the first stage starts the pathway.";
export const NO_SUCCESSOR_MESSAGE = "Nothing comes after it: only the last stage ends the pathway. Make a later stage come after it.";
export const GRAPH_LAST_STAGE_MESSAGE = "The last stage always runs: it can’t be optional, a branch point or start a branch.";
export const GRAPH_BRANCH_POINT_OPTIONAL_MESSAGE = "A branch point always runs: it can’t be optional.";
export const GRAPH_TOO_FEW_BRANCHES_MESSAGE = "A branch point needs at least two stages straight after it, one starting each branch.";
export const UNNAMED_BRANCH_MESSAGE = "Name the branch this stage starts: the clinician chooses between the names.";
export const SAME_BRANCH_NAME_MESSAGE = "Each branch after a branch point needs a different name.";
export const GRAPH_BRANCH_TOO_LONG_MESSAGE = `A branch name must not exceed ${MAX_BRANCH_LENGTH} characters.`;

/**
 * What's wrong with a template's routes (`TemplateGraphRules`, MC9), by each stage's 0-based index: one start (only the
 * first stage has nothing before it), one end (only the last has nothing after it), links only forward (so no cycles,
 * and every stage is reached from the first), and a branch point with at least two stages straight after it, each
 * starting a branch with a name of its own. The last stage always runs. The API's check is the last word.
 */
export function templateGraphProblems(stages: readonly GraphStage[], links: readonly OrderLink[]): Map<number, string> {
  const problems = new Map<number, string>();
  const say = (index: number, message: string) => {
    if (!problems.has(index)) problems.set(index, message);
  };
  const last = stages.length;
  for (const link of links) {
    const [from, to] = [link.fromStageOrder ?? 0, link.toStageOrder ?? 0];
    if (from < 1 || to > last || from >= to) say(Math.max(0, Math.min(to, last) - 1), BACKWARD_LINK_MESSAGE);
  }
  const forward = links.filter((l) => (l.fromStageOrder ?? 0) >= 1 && (l.toStageOrder ?? 0) <= last && (l.fromStageOrder ?? 0) < (l.toStageOrder ?? 0));
  const branchName = (stage: GraphStage) => stage.branch?.trim() ?? "";
  stages.forEach((stage, i) => {
    const order = i + 1;
    const after = forward.filter((l) => l.fromStageOrder === order).map((l) => l.toStageOrder ?? 0);
    if (branchName(stage).length > MAX_BRANCH_LENGTH) say(i, GRAPH_BRANCH_TOO_LONG_MESSAGE);
    if (order === last && (stage.isOptional || stage.choosesBranch)) say(i, GRAPH_LAST_STAGE_MESSAGE);
    if (order > 1 && !forward.some((l) => l.toStageOrder === order)) say(i, NO_PREDECESSOR_MESSAGE);
    if (order < last && after.length === 0) say(i, NO_SUCCESSOR_MESSAGE);
    if (!stage.choosesBranch) return;
    if (stage.isOptional) say(i, GRAPH_BRANCH_POINT_OPTIONAL_MESSAGE);
    else if (after.length < 2) say(i, GRAPH_TOO_FEW_BRANCHES_MESSAGE);
    const seen = new Set<string>();
    for (const next of [...after].sort((a, b) => a - b)) {
      const name = branchName(stages[next - 1]).toLowerCase();
      if (!name) say(next - 1, UNNAMED_BRANCH_MESSAGE);
      else if (seen.has(name)) say(next - 1, SAME_BRANCH_NAME_MESSAGE);
      seen.add(name);
    }
  });
  if (last > 0 && branchName(stages[last - 1]) !== "") say(last - 1, GRAPH_LAST_STAGE_MESSAGE);
  return problems;
}

/** The orders of the stages straight after a stage, by its 1-based order. */
export const successorOrders = (order: number, links: readonly OrderLink[]) =>
  links.filter((l) => l.fromStageOrder === order).map((l) => l.toStageOrder ?? 0);

/**
 * How far each of a pathway's stages sits in from the main line (`routeDepths`), by stage ID, from each stage's
 * `predecessorIds`: the tree view's outline (MC9). A chain's stages are all 0.
 */
export function stageDepths(
  stages: readonly Pick<Schemas["PatientPathwayStageResponse"], "id" | "stageOrder" | "predecessorIds">[],
): Map<string, number> {
  const ordered = [...stages].sort((a, b) => (a.stageOrder ?? 0) - (b.stageOrder ?? 0));
  const place = new Map(ordered.map((s, i) => [s.id ?? "", i + 1]));
  const links = ordered.flatMap((s) =>
    (s.predecessorIds ?? []).map((from) => ({ fromStageOrder: place.get(from) ?? 0, toStageOrder: place.get(s.id ?? "") ?? 0 })),
  );
  const depths = routeDepths(ordered.length, links);
  return new Map(ordered.map((s, i) => [s.id ?? "", depths[i + 1]]));
}
