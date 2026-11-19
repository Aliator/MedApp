import type { Schemas } from "./client";
import { MAX_BRANCH_LENGTH } from "./stageSkips";

/**
 * Tree pathways (MC8, `docs/multi-clinic-plan.md`): a template's and a pathway's stages are linked into a directed graph
 * with no cycles. A stage with several successors is a fork (every route runs, side by side) or a **choice node** (an
 * assessment or a lab result: the clinician records its outcome, and only that outcome's stages follow); a stage with
 * several predecessors is a join, waiting until each is finished or skipped.
 *
 * **Pathways grow** (the owner's model, 19 Nov): a template is the full tree of every possibility, and a patient's
 * pathway holds only the stages up to the next choice node not yet recorded (`grownStageOrders`). Recording an outcome
 * adds that outcome's stages, up to the next choice node; an outcome not chosen never appears.
 */

export type TemplateLink = Schemas["PathwayTemplateLinkResponse"];
export type TemplateOutcome = Schemas["PathwayTemplateOutcomeResponse"];

/** The kinds of choice node: a clinician's assessment or review, or a lab result coming back to the clinic. */
export const CHOICE_KINDS = [
  { value: "Assessment", label: "Assessment" },
  { value: "LabResult", label: "Lab result" },
] as const;

/** A choice node's kind in words ("Lab result"), or "" for a plain stage. */
export const choiceKindLabel = (kind: string | null | undefined) =>
  CHOICE_KINDS.find((k) => k.value === kind)?.label ?? "";

/** The links of a template written as a list: each stage to the next (a choice node needs routes, MC9). */
export function chainLinks(stages: readonly { stageOrder?: number }[]): TemplateLink[] {
  const orders = stages.map((s) => s.stageOrder ?? 0).sort((a, b) => a - b);
  return orders.slice(1).map((to, i) => ({ fromStageOrder: orders[i], toStageOrder: to }));
}

/** Why a template with routes can't have its stages changed as a list: a save must send its links (MC9). */
export const PARALLEL_ROUTES_LOCKED =
  "This template has routes (side by side, or a choice node's outcomes), so its stages can only be changed with their routes (links).";

const key = (l: TemplateLink) => `${l.fromStageOrder}>${l.toStageOrder}`;

/**
 * Whether a template is more than a list: routes side by side, or a choice node (whose outcomes are routes), which only
 * the route editor changes.
 */
export function hasRoutes(
  template: Pick<Schemas["PathwayTemplateResponse"], "stages" | "links">,
): boolean {
  if ((template.stages ?? []).some((s) => s.choiceKind)) return true;
  if (!template.links) return false;
  const chain = new Set(chainLinks(template.stages ?? []).map(key));
  return (
    template.links.length !== chain.size ||
    template.links.some((l) => !chain.has(key(l)))
  );
}

/** What growing reads of a template stage: its place, and a choice node's kind and outcomes. */
export interface GrowStage {
  stageOrder?: number;
  choiceKind?: string | null;
  outcomes?: readonly TemplateOutcome[] | null;
}

/** Whether a link is one a choice node's recorded outcome takes (any link from a plain stage is taken). */
function takes(stage: GrowStage | undefined, to: number, recorded: ReadonlyMap<number, string>): boolean {
  if (!stage?.choiceKind) return true;
  const outcome = recorded.get(stage.stageOrder ?? 0);
  if (outcome === undefined) return false;
  return (stage.outcomes ?? []).some(
    (o) => (o.name ?? "").toLowerCase() === outcome.toLowerCase() && (o.nextStageOrders ?? []).includes(to),
  );
}

/**
 * The template stages a pathway has, by order (the owner's model): the first, then every stage a taken link leads to,
 * where a choice node's links are taken only for its recorded outcome. A stage waits while a stage that could still lead
 * into it isn't settled: a choice node not yet recorded with a link to it, or a stage still reachable through one (so a
 * join after a fork never starts before every route that may reach it is known). Never chosen by MedApp: `recorded` is
 * each choice node's outcome, by its order, as the clinician recorded it.
 */
export function grownStageOrders(
  stages: readonly GrowStage[],
  links: readonly TemplateLink[],
  recorded: ReadonlyMap<number, string>,
): number[] {
  if (stages.length === 0) return [];
  const byOrder = new Map(stages.map((s) => [s.stageOrder ?? 0, s]));
  const present = new Set([Math.min(...byOrder.keys())]);
  for (let changed = true; changed; ) {
    changed = false;
    // Every stage that may yet be reached: through taken links, and through an unrecorded choice node's every link.
    const reachable = new Set(present);
    const queue = [...present];
    while (queue.length > 0) {
      const from = queue.shift()!;
      const stage = byOrder.get(from);
      const isOpen = Boolean(stage?.choiceKind) && !recorded.has(from);
      for (const l of links) {
        const to = l.toStageOrder ?? 0;
        if (l.fromStageOrder === from && !reachable.has(to) && (isOpen || takes(stage, to, recorded))) {
          reachable.add(to);
          queue.push(to);
        }
      }
    }
    for (const l of links) {
      const [from, to] = [l.fromStageOrder ?? 0, l.toStageOrder ?? 0];
      if (present.has(to) || !present.has(from) || !takes(byOrder.get(from), to, recorded)) continue;
      const waits = links.some((other) => {
        if (other.toStageOrder !== to) return false;
        const before = other.fromStageOrder ?? 0;
        if (present.has(before)) return Boolean(byOrder.get(before)?.choiceKind) && !recorded.has(before);
        return reachable.has(before);
      });
      if (waits) continue;
      present.add(to);
      changed = true;
    }
  }
  return [...present].sort((a, b) => a - b);
}

/** The links among a pathway's template stages: the template's, less those a recorded choice node's outcome didn't take. */
export function grownLinks(
  stages: readonly GrowStage[],
  links: readonly TemplateLink[],
  present: readonly number[],
  recorded: ReadonlyMap<number, string>,
): TemplateLink[] {
  const byOrder = new Map(stages.map((s) => [s.stageOrder ?? 0, s]));
  const has = new Set(present);
  return links.filter(
    (l) =>
      has.has(l.fromStageOrder ?? 0) &&
      has.has(l.toStageOrder ?? 0) &&
      takes(byOrder.get(l.fromStageOrder ?? 0), l.toStageOrder ?? 0, recorded),
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

/** What `templateGraphProblems` reads of a stage: its options, and a choice node's kind and outcomes. */
export interface GraphStage {
  isOptional?: boolean;
  choiceKind?: string | null;
  outcomes?: readonly TemplateOutcome[] | null;
}

// `TemplateGraphRules`' words (MC9, choice nodes), so the editor says what a save would be refused for.
export const BACKWARD_LINK_MESSAGE = "A stage can only come after a stage above it.";
export const NO_PREDECESSOR_MESSAGE = "Choose what it comes after: only the first stage starts the pathway.";
export const GRAPH_LAST_STAGE_MESSAGE = "The last stage ends a route: it can’t be optional or a choice node.";
export const CHOICE_OPTIONAL_MESSAGE = "A choice node always runs: it can’t be optional.";
export const NO_OUTCOMES_MESSAGE = "A choice node needs at least two outcomes, each leading to a stage after it.";
export const DEAD_END_CHOICE_MESSAGE = "A choice node can’t end a route: make the stages its outcomes lead to come after it.";
export const UNNAMED_OUTCOME_MESSAGE = "Name the outcome that leads to this stage: the clinician records one of the choice node’s outcomes.";
export const SAME_OUTCOME_NAME_MESSAGE = "Each outcome of a choice node needs a different name.";
export const OUTCOME_NOT_AFTER_MESSAGE = "An outcome can lead only to a stage that comes straight after its choice node.";
export const OUTCOME_TOO_LONG_MESSAGE = `An outcome's name must not exceed ${MAX_BRANCH_LENGTH} characters.`;

/**
 * What's wrong with a template's routes (`TemplateGraphRules`, MC9 and choice nodes), by each stage's 0-based index:
 * one start (only the first stage has nothing before it), links only forward (so no cycles, and every stage is reached
 * from the first), and a choice node (never optional, never an end) with at least two named outcomes, every stage
 * straight after it led to by one of them. Routes may end anywhere else (a referral, a discharge). The last stage
 * can't be optional. The API's check is the last word.
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
  const named = (o: TemplateOutcome) => (o.name?.trim() ?? "") !== "";
  stages.forEach((stage, i) => {
    const order = i + 1;
    const after = forward.filter((l) => l.fromStageOrder === order).map((l) => l.toStageOrder ?? 0);
    if (order === last && (stage.isOptional || stage.choiceKind)) say(i, GRAPH_LAST_STAGE_MESSAGE);
    if (order > 1 && !forward.some((l) => l.toStageOrder === order)) say(i, NO_PREDECESSOR_MESSAGE);
    if (!stage.choiceKind) return;
    if (stage.isOptional) say(i, CHOICE_OPTIONAL_MESSAGE);
    if (order < last && after.length === 0) say(i, DEAD_END_CHOICE_MESSAGE);
    const outcomes = stage.outcomes ?? [];
    const names = new Set<string>();
    for (const outcome of outcomes) {
      const name = (outcome.name?.trim() ?? "").toLowerCase();
      if (name.length > MAX_BRANCH_LENGTH) say(i, OUTCOME_TOO_LONG_MESSAGE);
      if (names.has(name)) say(i, SAME_OUTCOME_NAME_MESSAGE);
      names.add(name);
      if ((outcome.nextStageOrders ?? []).some((next) => !after.includes(next))) say(i, OUTCOME_NOT_AFTER_MESSAGE);
    }
    for (const next of [...after].sort((a, b) => a - b))
      if (!outcomes.some((o) => named(o) && (o.nextStageOrders ?? []).includes(next))) say(next - 1, UNNAMED_OUTCOME_MESSAGE);
    if (outcomes.filter((o) => named(o) && (o.nextStageOrders ?? []).length > 0).length < 2) say(i, NO_OUTCOMES_MESSAGE);
  });
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
