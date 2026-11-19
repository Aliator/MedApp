import {call, startLightDemo} from '../../test/lightDemo';
import {ADMIN, LAB, STAFF} from '../accounts';
import {PATHWAYS, PATIENTS} from '../ids';
import {db} from '../store';

const PATHWAY = '/api/pathways/patient-pathways';

describe('the team walls in the light demo', () => {
  it("keeps another clinic's pathway from Dermatology's staff: a 404, left out of lists, a summary line on the patient", async () => {
    startLightDemo(STAFF);
    expect((await call('GET', `${PATHWAY}/${PATHWAYS.priyaGynaecology}`)).status).toBe(404);
    expect((await call('POST', `${PATHWAY}/${PATHWAYS.priyaGynaecology}/notes`, {text: 'x'})).status).toBe(404);
    expect(db().activityLog.some((e) => e.action === 'TeamBoundaryRefused' && e.details?.includes(PATHWAYS.priyaGynaecology))).toBe(true);

    const search = (await call('GET', `${PATHWAY}/search?PatientId=${PATIENTS.priyaHolloway}&IncludeClosed=true`)).body;
    expect(search.items.map((p: {id: string}) => p.id)).toEqual([PATHWAYS.histologyToFinish]);
    const patient = (await call('GET', `/api/patients/${PATIENTS.priyaHolloway}`)).body;
    expect(patient.otherTeamPathways).toEqual([expect.objectContaining({teamName: 'Gynaecology', state: 'InProgress'})]);
    const timeline = (await call('GET', `/api/patients/${PATIENTS.priyaHolloway}/timeline?PageSize=100`)).body;
    expect(timeline.items.some((e: {pathwayId?: string}) => e.pathwayId === PATHWAYS.priyaGynaecology)).toBe(false);
  });

  it('lets the lab read only the pathways passed to it, and change them only while it has a stage of theirs', async () => {
    startLightDemo(LAB);
    // At the lab's Histology stage: the lab works it and adds notes, and changes nothing else.
    const passed = await call('GET', `${PATHWAY}/${PATHWAYS.histologyToFinish}`);
    expect(passed.status).toBe(200);
    expect(passed.body).toMatchObject({canWorkOnStages: true, canAddNotes: true, canEditDetails: false, readOnlyReason: null});
    expect((await call('POST', `${PATHWAY}/${PATHWAYS.histologyToFinish}/notes`, {text: 'x'})).status).toBe(200);
    expect((await call('PATCH', `${PATHWAY}/${PATHWAYS.histologyToFinish}`, {title: 'x'})).status).toBe(409);
    // Handed back (Harriet's is at Treatment): read only.
    const back = (await call('GET', `${PATHWAY}/${PATHWAYS.consentToRecord}`)).body;
    expect(back).toMatchObject({canWorkOnStages: false, canAddNotes: false});
    expect((await call('POST', `${PATHWAY}/${PATHWAYS.consentToRecord}/notes`, {text: 'x'})).status).toBe(409);
    expect((await call('GET', `${PATHWAY}/${PATHWAYS.biopsyToBook}`)).status).toBe(404);
  });

  it('lets an admin read every team’s pathways, with no summaries', async () => {
    startLightDemo(ADMIN);
    expect((await call('GET', `${PATHWAY}/${PATHWAYS.priyaGynaecology}`)).status).toBe(200);
    expect((await call('GET', `/api/patients/${PATIENTS.priyaHolloway}`)).body.otherTeamPathways).toEqual([]);
  });
});
