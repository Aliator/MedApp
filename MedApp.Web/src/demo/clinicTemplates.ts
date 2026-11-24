import type {Schemas} from '../api/client';
import {DERMATOLOGY_REVIEWED, type ReviewedStage, type ReviewedTemplate} from './dermatologyReviewed';
import {id, TEAM_NAMES, TEAMS, TEMPLATE_BRANCHES, TEMPLATES} from './ids';
import {LOWER_LIMB_TEMPLATES} from './lowerLimb';
import {OPHTHALMOLOGY_REVIEWED} from './ophthalmologyReviewed';
import type {DemoStageDefinition} from './state';

/**
 * Each clinic's templates from its reviewed file, mapped onto the model the same way, for the light demo's seed:
 * Dermatology's (the owner's reviewed `docs/pathways/dermatology.yaml`, copied in `dermatologyReviewed.ts`, with the
 * tour's relay) and the Eye clinic's (the draft `docs/pathways/ophthalmology.yaml`, copied in `ophthalmologyReviewed.ts`):
 * - `kind`: assessment and labResult are choice nodes (`Assessment`, `LabResult`), their `outcomes` the outcomes.
 * - `owner: lab` makes a lab stage; `visit`, `needsLetter` (the letter kind: a final letter to the GP is `DischargeSummary`, an onward referral to hospital, else a clinic letter)
 *   and `needsConsent` go to the stage definition; `snomed` to its code and term; `retentionClass` to the template.
 * - `repeat`, `endOutcome` and the letter's own name have no field: they're written into the stage's description.
 * - **Routes only go forward** (no cycles). A route back is left out: an outcome looping to its own stage (another
 *   review) is dropped, as the stage stays current while it repeats; one going back to take a sample again becomes a
 *   repeatable addable route ("Repeat biopsy") after the lab's stage; any other ends the route there, still recorded.
 *   The description says where the reviewed file goes.
 * Stage definitions are shared across templates by name (the stage library), with the existing library's where the
 * names match.
 */

/** Each template's fixed ID, by its name in the reviewed file (template names are unique across the clinics). */
const TEMPLATE_IDS: Record<string, string> = {
  'Suspected skin cancer': TEMPLATES.suspectedSkinCancer,
  'Changing mole or lesion check': TEMPLATES.changingMole,
  'Skin cancer surveillance': TEMPLATES.skinCancerSurveillance,
  'Inflammatory skin assessment': TEMPLATES.inflammatorySkin,
  'Eczema assessment': TEMPLATES.eczema,
  'Psoriasis assessment': TEMPLATES.psoriasis,
  'Acne assessment': TEMPLATES.acne,
  'Suspected skin infection': TEMPLATES.skinInfection,
  'Hair loss assessment': TEMPLATES.hairLoss,
  'Skin pigmentation assessment': TEMPLATES.pigmentation,
  'Skin sample to the lab': TEMPLATES.skinSample,
  'Cataract assessment': TEMPLATES.cataract,
  'Glaucoma assessment': TEMPLATES.glaucoma,
  'Red eye assessment': TEMPLATES.redEye,
  'Macular assessment': TEMPLATES.macular,
  'Lower limb biomechanics assessment': TEMPLATES.lowerLimb,
};

/** The reviewed file's addable routes' fixed IDs, by name. */
const ROUTE_IDS: Record<string, string> = {'Biopsy another lesion': TEMPLATE_BRANCHES.biopsyAnother};

/** A library description for a stage several templates share (each template's own notes are on its stage). */
const SHARED_DESCRIPTIONS: Record<string, string> = {
  Triage: 'Record the referral, the history and what has been tried, and decide when to see the patient.',
  'Dermoscopy assessment': 'Skin check and dermoscopy of the lesion, with photographs; record its size and site.',
  'Take biopsy': 'Take a skin biopsy for the lab, with the patient’s consent; record the site.',
  Histology: 'The lab processes the sample and enters the histology report.',
  'Histology result': 'The clinician records the histology diagnosis.',
  'Skin cancer MDT': 'Local skin cancer MDT (dermatology and pathology). Record the team’s decision.',
  'Final letter to GP': 'Write to the GP with the diagnosis and care plan, as the centre’s care ends.',
  'Refer to GP': 'Refer to the GP for prescribing or ongoing care (the centre doesn’t prescribe).',
  'Refer to hospital': 'Refer to the hospital for specialist care or treatment.',
  'Urgent hospital referral': 'Urgent referral to the hospital; phone the team first when it’s needed the same day.',
  'Refer to psychology': 'Refer for psychological support.',
  'Take bloods': 'Take blood for the tests the clinician lists.',
  'Blood tests': 'The lab runs the requested tests and enters the results.',
  'Blood results': 'The clinician reads the results and records the outcome.',
  'Take skin sample': 'Take scrapings, clippings or a swab for the lab; record the sample type and site.',
  Microbiology: 'Microscopy first, then culture; fungal culture can take 2 to 4 weeks.',
  'Microbiology result': 'The clinician reads the report and records the outcome.',
};

const VISITS = {none: 'None', suggested: 'Suggested', required: 'Required'} as const;

const END_OUTCOMES: Record<string, string> = {
  discharge: 'care completed',
  gpReferral: 'a referral to the GP',
  hospitalReferral: 'a referral to the hospital',
  otherClinic: 'a referral to another service',
  monitoring: 'monitoring',
};

/** The letter kind a stage's letter is (`LetterKind` by name): a final letter to the GP (`DischargeSummary`), an onward referral to the hospital (MC28), or a clinic letter. */
const letterKind = (letter: string | undefined) =>
  !letter ? null : /^final letter/i.test(letter) ? 'DischargeSummary' : /to hospital$/i.test(letter) ? 'OnwardReferral' : 'ClinicLetter';

const sentence = (text: string) => (/[.!?]$/.test(text.trim()) ? text.trim() : `${text.trim()}.`);

/**
 * Not one of the owner's reviewed ten: the tour's relay, kept as short as the owner asked (19 November 2026). Triage
 * (the seeded pathway's needs a visit, where the sample was taken, and the patient's consent recorded), the lab's Testing, a
 * Results appointment back in the clinic, then a referral back to the GP for medication (the owner, 19 November 2026).
 */
const TOUR_RELAY: ReviewedTemplate = {
  template: 'Skin sample to the lab',
  retentionClass: 'Standard',
  summary: 'A short route for the tour: triage, where a sample is taken, the lab’s test, the results at an appointment, then a referral back to the GP.',
  start: 'triage',
  stages: [
    {id: 'triage', name: 'Triage', kind: 'step', visit: 'none', next: ['testing'], snomed: '225390008 | Triage', notes: 'Triage the referral. Where a skin scraping is needed, see the patient and take it for the lab, with their consent.'},
    {id: 'testing', name: 'Testing', owner: 'lab', kind: 'step', visit: 'none', next: ['results'], notes: 'The lab tests the sample and enters the results.'},
    {id: 'results', name: 'Results appointment', kind: 'step', visit: 'required', next: ['refer-gp'], notes: 'Go through the results with the patient.'},
    {id: 'refer-gp', name: 'Refer to GP for medication', kind: 'step', visit: 'none', next: [], endOutcome: 'gpReferral', needsLetter: 'Referral letter to GP', snomed: '306206005 | Referral to service', notes: 'Refer back to the GP for the medication the results call for (the centre doesn’t prescribe).'},
  ],
  addableRoutes: [],
};

/** Each clinic's templates, by team: Dermatology's reviewed ten then the tour's relay, then the Eye clinic's four. */
const CLINICS: {teamId: string; templates: ReviewedTemplate[]}[] = [
  {teamId: TEAMS.dermatology, templates: [...DERMATOLOGY_REVIEWED, TOUR_RELAY]},
  {teamId: TEAMS.eyeClinic, templates: OPHTHALMOLOGY_REVIEWED},
  {teamId: TEAMS.lowerLimb, templates: LOWER_LIMB_TEMPLATES},
];

/** Every stage of the templates, their addable routes' too, in order (Dermatology's first, so their IDs stay). */
const everyStage = (): ReviewedStage[] =>
  CLINICS.flatMap((c) => c.templates).flatMap((t) => [...t.stages, ...t.addableRoutes.flatMap((r) => r.stages)]);

/** The first new definition's number (`id(0xd3, n)`): after the existing library's. */
const FIRST_DEFINITION = 0x20;

/**
 * The stage definitions the clinics' templates add to the library, one per stage name not already in it, in the
 * files' order, each with a fixed ID.
 */
export function reviewedStageDefinitions(existing: readonly DemoStageDefinition[]): DemoStageDefinition[] {
  const added: DemoStageDefinition[] = [];
  for (const stage of everyStage()) {
    if (existing.some((d) => d.name === stage.name) || added.some((d) => d.name === stage.name)) continue;
    const [code, term] = stage.snomed?.split('|').map((part) => part.trim()) ?? [null, null];
    added.push({
      id: id(0xd3, FIRST_DEFINITION + added.length),
      name: stage.name,
      description: SHARED_DESCRIPTIONS[stage.name] ?? stage.notes,
      instructions: null,
      snomedCode: code ?? null,
      snomedTerm: term ?? null,
      visitRequirement: VISITS[stage.visit],
      needsLetterKind: letterKind(stage.needsLetter),
      needsConsent: stage.needsConsent ?? false,
      waitsForResults: stage.kind === 'labResult',
      isArchived: false,
    });
  }
  return added;
}

/** A stage definition's ID, by name (the library's, after `reviewedStageDefinitions`). */
export const reviewedStageId = (definitions: readonly DemoStageDefinition[], name: string) => {
  const found = definitions.find((d) => d.name === name);
  if (!found) throw new Error(`No stage definition named '${name}'.`);
  return found.id;
};

const successorsOf = (stage: ReviewedStage) => (stage.outcomes ? stage.outcomes.flatMap(([, next]) => next) : (stage.next ?? []));

/**
 * The template's stages in an order where every route goes forward: a depth-first walk from the start marks the links
 * back (to a stage still on the walk), and the rest are sorted, keeping the file's order where they allow it.
 */
function forwardOrder(template: ReviewedTemplate): {order: ReviewedStage[]; isBack: (from: string, to: string) => boolean} {
  const byId = new Map(template.stages.map((s) => [s.id, s]));
  const back = new Set<string>();
  const seen = new Set<string>();
  const onWalk = new Set<string>();
  const walk = (stageId: string) => {
    seen.add(stageId);
    onWalk.add(stageId);
    for (const next of successorsOf(byId.get(stageId)!)) {
      if (onWalk.has(next)) back.add(`${stageId}>${next}`);
      else if (!seen.has(next)) walk(next);
    }
    onWalk.delete(stageId);
  };
  walk(template.start);
  const isBack = (from: string, to: string) => back.has(`${from}>${to}`);
  const incoming = new Map(template.stages.map((s) => [s.id, 0]));
  for (const stage of template.stages)
    for (const next of new Set(successorsOf(stage))) if (!isBack(stage.id, next)) incoming.set(next, (incoming.get(next) ?? 0) + 1);
  const order: ReviewedStage[] = [];
  const left = [...template.stages];
  while (left.length > 0) {
    const index = Math.max(0, left.findIndex((s) => incoming.get(s.id) === 0));
    const [stage] = left.splice(index, 1);
    order.push(stage);
    for (const next of new Set(successorsOf(stage))) if (!isBack(stage.id, next)) incoming.set(next, (incoming.get(next) ?? 0) - 1);
  }
  return {order, isBack};
}

let nextRouteNumber = 0x10;

/** One reviewed template as the API answers it (`PathwayTemplateResponse`), its team's, its stages' definitions from `definitions`. */
function toTemplate(template: ReviewedTemplate, teamId: string, definitions: readonly DemoStageDefinition[]): Schemas['PathwayTemplateResponse'] {
  const {order, isBack} = forwardOrder(template);
  const orderOf = new Map(order.map((s, i) => [s.id, i + 1]));
  const byId = new Map(template.stages.map((s) => [s.id, s]));
  const definitionOf = (name: string) => definitions.find((d) => d.name === name)!;
  const links: Schemas['PathwayTemplateLinkResponse'][] = [];
  const link = (from: string, to: string) => {
    const [fromStageOrder, toStageOrder] = [orderOf.get(from)!, orderOf.get(to)!];
    if (!links.some((l) => l.fromStageOrder === fromStageOrder && l.toStageOrder === toStageOrder)) links.push({fromStageOrder, toStageOrder});
  };
  // A route back to take a sample again: the sampling stage and the lab's stage after it, added on the result.
  const isSampling = (stage: ReviewedStage) => (stage.next ?? []).some((n) => byId.get(n)?.owner === 'lab');
  const repeatRoutes: Schemas['PathwayTemplateBranchResponse'][] = [];
  const repeatRoute = (sampling: ReviewedStage) => {
    const lab = byId.get(sampling.next!.find((n) => byId.get(n)?.owner === 'lab')!)!;
    const name = `Repeat ${sampling.name.replace(/^Take /, '').toLowerCase()}`;
    if (!repeatRoutes.some((r) => r.name === name))
      repeatRoutes.push({
        id: id(0xfc, nextRouteNumber++),
        name,
        stages: [sampling, lab].map((s) => ({stageDefinitionId: definitionOf(s.name).id, name: s.name})),
        attachAfterStageOrders: [orderOf.get(lab.id)!],
        isRepeatable: true,
      });
    return name;
  };

  const stages = order.map((stage, i): Schemas['PathwayTemplateStageResponse'] => {
    const definition = definitionOf(stage.name);
    const said = [sentence(stage.notes)];
    if (stage.repeat) said.push(`Repeats: ${sentence(stage.repeat)}`);
    if (stage.needsLetter) said.push(`Needs a letter: ${sentence(stage.needsLetter)}`);
    if (stage.endOutcome) said.push(`Ends with ${END_OUTCOMES[stage.endOutcome] ?? stage.endOutcome}.`);
    let outcomes: Schemas['PathwayTemplateOutcomeResponse'][] | null = null;
    if (stage.outcomes) {
      outcomes = [];
      for (const [name, next] of stage.outcomes) {
        const ahead = next.filter((n) => !isBack(stage.id, n));
        ahead.forEach((n) => link(stage.id, n));
        if (ahead.length > 0 || next.length === 0) {
          outcomes.push({name, nextStageOrders: ahead.map((n) => orderOf.get(n)!).sort((a, b) => a - b)});
          continue;
        }
        const target = byId.get(next[0])!;
        if (target.id === stage.id) said.push(`${name}: stays at this stage, with the next review booked on it.`);
        else if (isSampling(target)) said.push(`${name}: add the ${repeatRoute(target)} route, and record the outcome when its result is back.`);
        else {
          outcomes.push({name, nextStageOrders: []});
          said.push(`${name}: in practice this goes back to ${target.name}; here the route ends: start a new pathway if needed.`);
        }
      }
    } else
      for (const next of stage.next ?? []) {
        if (!isBack(stage.id, next)) link(stage.id, next);
        else said.push(`In practice this goes back to ${byId.get(next)!.name}; here the route ends: start a new pathway if needed.`);
      }
    return {
      stageOrder: i + 1,
      stageDefinitionId: definition.id,
      name: stage.name,
      description: said.join(' '),
      snomedCode: definition.snomedCode,
      snomedTerm: definition.snomedTerm,
      isArchived: false,
      visitRequirement: VISITS[stage.visit],
      isOptional: stage.optional ?? false,
      choiceKind: stage.kind === 'assessment' ? 'Assessment' : stage.kind === 'labResult' ? 'LabResult' : null,
      outcomes,
      stopsClock: i === 0,
      ownerTeamId: stage.owner === 'lab' ? TEAMS.lab : null,
      ownerTeamName: stage.owner === 'lab' ? 'Lab' : null,
    };
  });

  // The reviewed file's addable routes: their stages, then the lab's stage they lead into (a biopsy's histology).
  const reviewedRoutes = template.addableRoutes.map((route): Schemas['PathwayTemplateBranchResponse'] => {
    const last = route.stages.at(-1)!;
    const into = (last.next ?? []).map((n) => byId.get(n)).filter((s): s is ReviewedStage => s?.owner === 'lab');
    return {
      id: ROUTE_IDS[route.name] ?? id(0xfc, nextRouteNumber++),
      name: route.name,
      stages: [...route.stages, ...into].map((s) => ({stageDefinitionId: definitionOf(s.name).id, name: s.name})),
      attachAfterStageOrders: [orderOf.get(route.attachesAfter)!],
      isRepeatable: false,
    };
  });

  return {
    id: TEMPLATE_IDS[template.template],
    name: template.template,
    isArchived: false,
    teamId,
    teamName: TEAM_NAMES.find(([key]) => key === teamId)![1],
    retentionClass: template.retentionClass,
    pathwayCount: 0,
    clockStopStageOrder: 1,
    stageDefinitionIdsInOrder: stages.map((s) => s.stageDefinitionId!),
    links: links.sort((a, b) => a.fromStageOrder! - b.fromStageOrder! || a.toStageOrder! - b.toStageOrder!),
    addableBranches: [...reviewedRoutes, ...repeatRoutes],
    stages,
  };
}

/** Every clinic's templates, each owned by its clinic's team, as the API answers them. */
export function reviewedTemplates(definitions: readonly DemoStageDefinition[]): Schemas['PathwayTemplateResponse'][] {
  nextRouteNumber = 0x10;
  return CLINICS.flatMap((clinic) => clinic.templates.map((template) => toTemplate(template, clinic.teamId, definitions)));
}
