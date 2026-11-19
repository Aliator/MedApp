import type { Schemas } from "./client";

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

/** Why a template with routes side by side can't have its stages changed as a list (until the route editor, MC9). */
export const PARALLEL_ROUTES_LOCKED =
  "This template has routes that run side by side, so its stages can’t be changed as a list yet: that comes with the route editor. Its name, team, retention class and clock stop can change.";

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
