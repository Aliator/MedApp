import {call, startLightDemo} from '../../test/lightDemo';
import {ADMIN, LAB, personByUsername, STAFF} from '../accounts';
import {MORE_APPOINTMENTS, PATHWAYS, PATIENTS, REFERRALS, TEAMS, USERS} from '../ids';
import {ukDateOf} from '../time';
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

  it("keeps other teams' referrals from Dermatology: left out of lists and counts, a 404, and gone once sent or redirected away", async () => {
    startLightDemo(STAFF);
    const listed = (await call('GET', '/api/referrals/search?PageSize=100')).body.items.map((r: {teamId: string}) => r.teamId);
    expect(new Set(listed)).toEqual(new Set([TEAMS.dermatology]));
    expect((await call('GET', `/api/referrals/${REFERRALS.ruthGynaecology}`)).status).toBe(404);
    expect((await call('GET', '/api/referrals/awaiting-triage/count')).body).toEqual({count: 2, inYourTeams: true});

    const registered = await call('POST', '/api/referrals', {
      patientId: PATIENTS.priyaHolloway,
      teamId: TEAMS.gynaecology,
      receivedOn: ukDateOf(new Date().toISOString()),
      referrerPractice: 'Synthetic Street Surgery',
      requestedPriority: 'Medium',
      reason: 'A synthetic reason.',
    });
    expect(registered.status).toBe(200);
    expect((await call('GET', `/api/referrals/${registered.body.id}`)).status).toBe(404);

    const referral = (await call('GET', `/api/referrals/${REFERRALS.dermatologyToTriage}`)).body;
    const redirect = {teamId: TEAMS.gynaecology, detail: 'Gynaecological.', expectedVersion: referral.version};
    expect((await call('POST', `/api/referrals/${REFERRALS.dermatologyToTriage}/redirect`, redirect)).status).toBe(200);
    expect((await call('GET', `/api/referrals/${REFERRALS.dermatologyToTriage}`)).status).toBe(404);
  });

  it('lets an admin count and read every team’s referrals', async () => {
    startLightDemo(ADMIN);
    expect((await call('GET', `/api/referrals/${REFERRALS.ruthGynaecology}`)).status).toBe(200);
    expect((await call('GET', '/api/referrals/awaiting-triage/count')).body).toEqual({count: 4, inYourTeams: false});
  });

  it("shows another team's appointment on a calendar as busy time, with no patient", async () => {
    startLightDemo(STAFF);
    const gynaecology = db().appointments.find((a) => a.id === MORE_APPOINTMENTS.priyaGynaecologyBiopsy)!;
    const day = ukDateOf(gynaecology.startUtc!);
    const calendar = (await call('GET', `/api/calendar?StaffUserId=${USERS.hallworth}&From=${day}&To=${day}`)).body;
    expect(calendar.appointments.map((a: {id: string}) => a.id)).not.toContain(gynaecology.id);
    expect(calendar.otherTeamsBusy).toEqual([
      {staffUserId: USERS.hallworth, username: 't.hallworth', startUtc: gynaecology.startUtc, endUtc: gynaecology.endUtc, what: 'Busy'},
    ]);
    expect(JSON.stringify(calendar)).not.toContain(PATIENTS.priyaHolloway);
    expect((await call('GET', `/api/appointments/${gynaecology.id}/messages`)).status).toBe(404);

    startLightDemo(ADMIN);
    const admins = (await call('GET', `/api/calendar?StaffUserId=${USERS.hallworth}&From=${day}&To=${day}`)).body;
    expect(admins.appointments.map((a: {id: string}) => a.id)).toContain(gynaecology.id);
    expect(admins.otherTeamsBusy).toEqual([]);
  });

  it('books only clinicians in the appointment’s team (a 400 on who), and only for the booker’s own team', async () => {
    startLightDemo(STAFF);
    const pathway = db().pathways.find((p) => p.id === PATHWAYS.biopsyToBook)!;
    const start = new Date(Date.now() + 3 * 86400_000).toISOString();
    const end = new Date(Date.parse(start) + 30 * 60000).toISOString();
    const body = {patientId: pathway.patientId, pathwayId: pathway.id, stageId: pathway.stages[1].id, staffUserId: USERS.whitlock, startUtc: start, endUtc: end};
    const refused = await call('POST', '/api/appointments', body);
    expect(refused.status).toBe(400);
    expect(refused.body.errors.staffUserId).toBeDefined();
    expect((await call('POST', '/api/appointments', {...body, staffUserId: USERS.sandiford})).status).toBe(200);

    // A booking coordinator in the lab reads a passed pathway's appointments, but changes none.
    const {state} = startLightDemo(personByUsername('h.marsh')!);
    state.memberships[USERS.marsh] = [{teamId: TEAMS.lab, role: 'Member'}];
    const passed = db().appointments.find((a) => a.pathwayId === PATHWAYS.histologyToFinish)!;
    expect((await call('GET', `/api/appointments/${passed.id}`)).body.canCancel).toBe(false);
    expect((await call('POST', `/api/appointments/${passed.id}/cancel`, {reason: 'x'})).status).toBe(409);
  });

  it("counts Insights for the viewer's teams only (an admin's, every team's)", async () => {
    startLightDemo(STAFF);
    const mine = (await call('GET', '/api/pathways/insights')).body;
    expect((await call('GET', `/api/pathways/insights?TeamId=${TEAMS.gynaecology}`)).body.open.current).toBe(0);
    startLightDemo(ADMIN);
    const all = (await call('GET', '/api/pathways/insights')).body;
    expect(all.open.current).toBeGreaterThan(mine.open.current);
  });
});
