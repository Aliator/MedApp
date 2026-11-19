import {call, startLightDemo} from '../../test/lightDemo';
import {ADMIN, LAB, STAFF} from '../accounts';
import {PATHWAYS, TEAMS, TEMPLATES} from '../ids';
import {LAB_CASES} from '../slices/labCases';
import {db} from '../store';
import {auditEntries} from './adminLogs';

const PATHWAY = '/api/pathways/patient-pathways';
const viewIds = async (view: string) =>
  (await call('GET', `${PATHWAY}/search?View=${view}&PageSize=100`)).body.items.map((p: {id: string}) => p.id);
const current = (pathway: {stages: {isCurrent: boolean; id: string}[]}) => pathway.stages.find((s) => s.isCurrent)!;

describe('the relay in the light demo', () => {
  it('hands a pathway from the lab back to the clinic when the lab authorises its stage’s case, and the clinic picks it up', async () => {
    startLightDemo(STAFF);
    // Dermatology waits on the lab's Histology: it can't work it, and sees it in Awaiting the lab.
    const priya = (await call('GET', `${PATHWAY}/${PATHWAYS.histologyToFinish}`)).body;
    expect(current(priya)).toMatchObject({ownerTeamName: 'Lab', isWithAnotherTeam: true, canEdit: false});
    expect((await call('POST', `${PATHWAY}/${PATHWAYS.histologyToFinish}/stages/${current(priya).id}/finish`, {})).status).toBe(409);
    expect(await viewIds('AwaitingLab')).toEqual(expect.arrayContaining([PATHWAYS.histologyToFinish, PATHWAYS.forkedTests]));
    expect(await viewIds('BackFromLab')).toEqual([PATHWAYS.backFromLab]);

    // The lab authorises Priya's Histology case: a handover back to Dermatology.
    db().user = LAB.username;
    // Aisha's fork has two of the lab's stages current at once, both handed over (MC8 with the relay).
    expect(await viewIds('NewForUs')).toEqual(expect.arrayContaining([PATHWAYS.forkedTests]));
    expect(await viewIds('OurTurn')).toEqual(expect.arrayContaining([PATHWAYS.histologyToFinish, PATHWAYS.forkedTests]));
    // Its case is open, so the stage isn't finished by hand yet (MC16): its report is authorised first.
    const histology = `${PATHWAY}/${PATHWAYS.histologyToFinish}/stages/${current(priya).id}/finish`;
    const refused = await call('POST', histology, {});
    expect(refused.status).toBe(409);
    expect(refused.body.title).toMatch(/^Lab case MA\d{2}-\d{6} is still open/);
    const labCase = (await call('GET', `/api/lab-cases/${LAB_CASES.priyaBiopsy}`)).body;
    expect(current((await call('GET', `${PATHWAY}/${PATHWAYS.histologyToFinish}`)).body)).toMatchObject({labCaseId: labCase.id, labCaseStatus: 'Open'});
    const report = await call('POST', `/api/lab-cases/${labCase.id}/report`, {results: 'Compound naevus, no atypia (synthetic).', expectedVersion: labCase.version});
    expect(report.status).toBe(200);
    // The results are on the stage, still the lab's, until the lab finishes it by hand (the owner, 19 Nov).
    const reported = (await call('GET', `${PATHWAY}/${PATHWAYS.histologyToFinish}`)).body;
    expect(current(reported)).toMatchObject({name: 'Histology', labCaseStatus: 'Reported', pickedUpByUsername: LAB.username});
    expect(reported.stages.find((s: {name: string}) => s.name === 'Histology').result).toMatchObject({text: 'Compound naevus, no atypia (synthetic).'});
    expect((await call('POST', histology, {})).status).toBe(200);
    const finished = await call('GET', `${PATHWAY}/${PATHWAYS.histologyToFinish}`);
    expect(current(finished.body)).toMatchObject({name: 'Histology result', handedOverFromTeamName: 'Lab', canPickUp: false, needsOutcome: true});
    expect(finished.body.stages.find((s: {name: string}) => s.name === 'Histology').result).toMatchObject({
      text: 'Compound naevus, no atypia (synthetic).',
      enteredByUsername: LAB.username,
    });
    expect(finished.body.stages.find((s: {name: string}) => s.name === 'Histology')).toMatchObject({state: 'Completed', labCaseStatus: 'Reported'});
    expect(finished.body.readOnlyReason).toMatch(/your team can read it/);
    const told = db().notifications.filter((n) => n.kind === 'PathwayHandedOver' && n.pathwayId === PATHWAYS.histologyToFinish);
    expect(told.map((n) => n.userId)).toContain(STAFF.id);
    expect(told[0]).toMatchObject({title: 'Pathway handed over', message: `${LAB.username} passed a pathway to your team.`});
    const audit = auditEntries().find((e) => e.action === 'StageHandedOver' && e.patientPathwayId === PATHWAYS.histologyToFinish);
    expect(JSON.parse(audit!.details!)).toMatchObject({fromTeamId: TEAMS.lab, from: 'Lab', toTeamId: TEAMS.dermatology, to: 'Dermatology'});

    // Back in Dermatology: new for them until picked up (once; again changes nothing).
    db().user = STAFF.username;
    expect(await viewIds('BackFromLab')).toEqual(expect.arrayContaining([PATHWAYS.histologyToFinish, PATHWAYS.backFromLab]));
    const review = current(finished.body).id;
    const picked = await call('POST', `${PATHWAY}/${PATHWAYS.histologyToFinish}/stages/${review}/pick-up`);
    expect(current(picked.body)).toMatchObject({pickedUpByUsername: STAFF.username, canPickUp: false});
    expect((await call('POST', `${PATHWAY}/${PATHWAYS.histologyToFinish}/stages/${review}/pick-up`)).status).toBe(200);
    expect(await viewIds('BackFromLab')).toEqual([PATHWAYS.backFromLab]);
  });

  it('opens a lab stage’s case when it’s reached, links the sample on receipt and picks the stage up (MC16)', async () => {
    startLightDemo(STAFF);
    const ellis = (await call('GET', `${PATHWAY}/${PATHWAYS.biopsyToBook}`)).body;
    const biopsy = current(ellis);
    const sample = await call('POST', `/api/pathways/${PATHWAYS.biopsyToBook}/specimens`, {
      stageId: biopsy.id,
      specimenTypeId: db().specimenTypes[0].id,
      requestingUserId: STAFF.id,
      labTestId: db().labTests[0].id,
    });
    expect(sample.status).toBe(200);
    // His consent is the tour's step before: set aside here, as it's not what this checks.
    db().pathways.find((p) => p.id === PATHWAYS.biopsyToBook)!.stages.find((s) => s.id === biopsy.id)!.needsConsent = false;
    const handed = await call('POST', `${PATHWAY}/${PATHWAYS.biopsyToBook}/stages/${biopsy.id}/finish`, {withoutVisit: true, reason: 'Done in clinic'});
    expect(handed.status).toBe(200);
    const histology = current(handed.body);
    expect(histology).toMatchObject({name: 'Histology', labCaseStatus: 'Open'});
    const opened = db().labCases.find((c) => c.stageId === histology.id)!;
    expect(opened).toMatchObject({status: 'Open', labTestId: db().labTests[0].id, receivedAtUtc: null});

    db().user = LAB.username;
    const specimen = db().specimens.find((s) => s.id === sample.body.id)!;
    await call('POST', `/api/specimens/${specimen.id}/sent`, {expectedVersion: specimen.version});
    await call('POST', `/api/specimens/${specimen.id}/received`, {expectedVersion: specimen.version});
    expect(specimen.labCaseId).toBe(opened.id);
    const after = current((await call('GET', `${PATHWAY}/${PATHWAYS.biopsyToBook}`)).body);
    expect(after).toMatchObject({pickedUpByUsername: LAB.username, canPickUp: false});
  });

  it('reopens the stage that took an insufficient sample, and keeps the lab stage with the lab (MC16)', async () => {
    startLightDemo(LAB);
    const c = db().labCases.find((x) => x.id === LAB_CASES.priyaBiopsy)!;
    // Back to before its adequacy was recorded, to record it insufficient.
    c.steps = c.steps.slice(0, 2);
    const closed = await call('POST', `/api/lab-cases/${c.id}/outcomes`, {choice: 'SampleAdequate', outcome: 'Insufficient', detail: 'Too little.', expectedVersion: c.version});
    expect(closed.body.status).toBe('RepeatRequested');
    const pathway = db().pathways.find((p) => p.id === PATHWAYS.histologyToFinish)!;
    expect(pathway.stages.find((s) => s.name === 'Take biopsy')!.unlocked).toBe(true);
    expect(pathway.stages.find((s) => s.name === 'Histology')!.completedAtUtc).toBeNull();
    const told = db().notifications.find((n) => n.kind === 'RepeatSampleRequested' && n.pathwayId === PATHWAYS.histologyToFinish)!;
    expect(told.message).toBe('The lab asked for a repeat sample (Skin histology). Take biopsy is reopened to take it.');
  });

  it('lets a template give a stage only to its own team or the lab', async () => {
    const {state} = startLightDemo(STAFF);
    db().user = ADMIN.username;
    const template = state.templates.find((t) => t.id === TEMPLATES.inflammatorySkin)!;
    const ids = template.stageDefinitionIdsInOrder!;
    // Its choice nodes and outcomes kept as they are (its routes aren't sent), only an owner changed.
    const options = (owner: string | null) =>
      ids.map((_, i) => ({isOptional: false, choiceKind: template.stages![i].choiceKind, outcomes: template.stages![i].outcomes, ownerTeamId: i === 1 ? owner : null}));
    const save = (owner: string | null) => call('PATCH', `/api/pathways/patient-pathway-stage/templates/${template.id}`, {stageOptions: options(owner)});
    const otherClinic = (await call('POST', '/api/teams', {name: 'Rheumatology'})).body.id;
    expect((await save(otherClinic)).status).toBe(400);
    const saved = await save(TEAMS.lab);
    expect(saved.status).toBe(200);
    expect(saved.body.stages[1]).toMatchObject({ownerTeamId: TEAMS.lab, ownerTeamName: 'Lab'});
  });
});
