import {call, startLightDemo} from '../../test/lightDemo';
import {ADMIN, LAB, STAFF} from '../accounts';
import {PATHWAYS, TEAMS, TEMPLATES} from '../ids';
import {db} from '../store';
import {auditEntries} from './adminLogs';

const PATHWAY = '/api/pathways/patient-pathways';
const viewIds = async (view: string) =>
  (await call('GET', `${PATHWAY}/search?View=${view}&PageSize=100`)).body.items.map((p: {id: string}) => p.id);
const current = (pathway: {stages: {isCurrent: boolean; id: string}[]}) => pathway.stages.find((s) => s.isCurrent)!;

describe('the relay in the light demo', () => {
  it('hands a pathway from the lab back to the clinic when the lab finishes its stage, and the clinic picks it up', async () => {
    startLightDemo(STAFF);
    // Dermatology waits on the lab's Histology: it can't work it, and sees it in Awaiting the lab.
    const priya = (await call('GET', `${PATHWAY}/${PATHWAYS.histologyToFinish}`)).body;
    expect(current(priya)).toMatchObject({ownerTeamName: 'Lab', isWithAnotherTeam: true, canEdit: false});
    expect((await call('POST', `${PATHWAY}/${PATHWAYS.histologyToFinish}/stages/${current(priya).id}/finish`, {})).status).toBe(409);
    expect(await viewIds('AwaitingLab')).toContain(PATHWAYS.histologyToFinish);
    expect(await viewIds('BackFromLab')).toEqual([PATHWAYS.backFromLab]);

    // The lab has Gynaecology's new for it, and finishes Priya's Histology: a handover back to Dermatology.
    db().user = LAB.username;
    expect(await viewIds('NewForUs')).toEqual([PATHWAYS.priyaGynaecology]);
    expect(await viewIds('OurTurn')).toEqual(expect.arrayContaining([PATHWAYS.histologyToFinish, PATHWAYS.priyaGynaecology]));
    const finished = await call('POST', `${PATHWAY}/${PATHWAYS.histologyToFinish}/stages/${current(priya).id}/finish`, {});
    expect(finished.status).toBe(200);
    expect(current(finished.body)).toMatchObject({name: 'Results and team review', handedOverFromTeamName: 'Lab', canPickUp: false});
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

  it('lets a template give a stage only to its own team or the lab', async () => {
    const {state} = startLightDemo(STAFF);
    db().user = ADMIN.username;
    const template = state.templates.find((t) => t.id === TEMPLATES.inflammatorySkin)!;
    const ids = template.stageDefinitionIdsInOrder!;
    const options = (owner: string | null) => ids.map((_, i) => ({isOptional: false, choosesBranch: false, ownerTeamId: i === 1 ? owner : null}));
    const save = (owner: string | null) => call('PATCH', `/api/pathways/patient-pathway-stage/templates/${template.id}`, {stageOptions: options(owner)});
    expect((await save(TEAMS.gynaecology)).status).toBe(400);
    const saved = await save(TEAMS.lab);
    expect(saved.status).toBe(200);
    expect(saved.body.stages[1]).toMatchObject({ownerTeamId: TEAMS.lab, ownerTeamName: 'Lab'});
  });
});
