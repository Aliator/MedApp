import type {Schemas} from '../../api/client';
import {invalid, notFound, on, problem, refuse} from '../api/http';
import {newId} from '../ids';
import type {DemoLabTest, LabStepKind, LabTestKind} from '../slices/lab';
import {db} from '../store';
import {me, requirePermission} from './common';
import {byName, setupName, snomedCode} from './lab';

/**
 * The lab's test catalogue (MC13, `api/lab-tests`, ManageSetup), a simplified copy of the API's rules: a unique name,
 * a kind, an optional SNOMED CT procedure code, the target and limit in calendar days (1 to 90, target ≤ limit), at risk
 * 0 to 30 days before the due date (under the target), and 1 to 20 named steps in order. Lab cases come with MC15, so
 * nothing uses a test yet and every one can be deleted; the API archives one a case used instead.
 */

const KINDS: LabTestKind[] = ['Histology', 'Cytology', 'Haematology', 'ClinicalChemistry', 'Microbiology', 'Other'];
const STEP_KINDS: LabStepKind[] = ['Standard', 'ExtraTests', 'SentForSecondOpinion', 'SecondOpinionReturned'];
const MAX_STEPS = 20;
const MAX_NAME = 100;

function labTestResponse(test: DemoLabTest): Schemas['LabTestResponse'] {
  return {...test, steps: test.steps.map((s) => ({...s})), canBeDeleted: true};
}

/** The request checked as the API's validator does; a 400 naming each field otherwise. */
function checked(body: Schemas['LabTestRequest'], id: string | undefined) {
  const errors: Record<string, string[]> = {};
  const kind = KINDS.find((k) => k === body.kind);
  if (!kind) errors.kind = ['Choose the kind of test.'];
  const target = body.targetDays ?? 0;
  const limit = body.limitDays ?? 0;
  const atRisk = body.atRiskDays ?? 0;
  if (!Number.isInteger(target) || target < 1 || target > 90) errors.targetDays = ['The target must be 1 to 90 days.'];
  if (!Number.isInteger(limit) || limit < 1 || limit > 90) errors.limitDays = ['The limit must be 1 to 90 days.'];
  else if (limit < target) errors.limitDays = ["The limit can't be sooner than the target."];
  if (!Number.isInteger(atRisk) || atRisk < 0 || atRisk > 30) errors.atRiskDays = ['At risk must be 0 to 30 days before the due date.'];
  else if (atRisk >= target) errors.atRiskDays = ['At risk must start before the target, so fewer days than it.'];
  const steps = body.steps ?? [];
  if (steps.length === 0) errors.steps = ['Add at least one step.'];
  if (steps.length > MAX_STEPS) errors.steps = [`A test has at most ${MAX_STEPS} steps.`];
  steps.forEach((step, i) => {
    const name = step.name?.trim() ?? '';
    if (!name || name.length > MAX_NAME) errors[`steps[${i}].name`] = [`Give step ${i + 1} a name of at most ${MAX_NAME} characters.`];
    if (!STEP_KINDS.some((k) => k === step.kind)) errors[`steps[${i}].kind`] = [`Choose step ${i + 1}'s kind.`];
  });
  const code = snomedCode(body.snomedCode);
  if (Object.keys(errors).length > 0) refuse(invalid(errors));
  const name = setupName(body.name, db().labTests, id, (taken) => `There's already a lab test called '${taken}'.`);
  return {name, kind: kind!, snomedCode: code, targetDays: target, limitDays: limit, atRiskDays: atRisk, steps};
}

export function registerLabTests() {
  const findTest = (id: string) => db().labTests.find((t) => t.id === id) ?? refuse(notFound(`Lab test '${id}'`));
  const stepsOf = (steps: Schemas['LabTestStepRequest'][], existing?: DemoLabTest): DemoLabTest['steps'] =>
    steps.map((step, i) => ({
      // A step kept in its place keeps its ID (as the API matches them by position).
      id: existing?.steps[i]?.id ?? newId(),
      name: step.name!.trim(),
      kind: step.kind as LabStepKind,
      isOptional: step.isOptional === true,
    }));

  on('get', '/api/lab-tests', ({query}) => {
    me();
    const includeArchived = query.get('includeArchived') === 'true';
    return db().labTests.filter((t) => includeArchived || !t.isArchived).sort(byName).map(labTestResponse);
  });
  on('post', '/api/lab-tests', ({body}) => {
    requirePermission('ManageSetup');
    const {steps, ...fields} = checked(body, undefined);
    const test: DemoLabTest = {id: newId(), ...fields, steps: stepsOf(steps), isArchived: false};
    db().labTests.push(test);
    return labTestResponse(test);
  });
  on('put', '/api/lab-tests/{id}', ({params, body}) => {
    requirePermission('ManageSetup');
    const test = findTest(params.id);
    const {steps, ...fields} = checked(body, test.id);
    Object.assign(test, fields, {steps: stepsOf(steps, test)});
    return labTestResponse(test);
  });
  on('delete', '/api/lab-tests/{id}', ({params}) => {
    requirePermission('ManageSetup');
    const test = findTest(params.id);
    if (!labTestResponse(test).canBeDeleted) return problem(409, `Lab cases have used '${test.name}', so it can't be deleted. Archive it instead.`);
    db().labTests = db().labTests.filter((t) => t.id !== test.id);
  });
  on('post', '/api/lab-tests/{id}/archive', ({params}) => {
    requirePermission('ManageSetup');
    const test = findTest(params.id);
    test.isArchived = true;
    return labTestResponse(test);
  });
  on('post', '/api/lab-tests/{id}/restore', ({params}) => {
    requirePermission('ManageSetup');
    const test = findTest(params.id);
    test.isArchived = false;
    return labTestResponse(test);
  });
}
