import {call, startLightDemo} from '../../test/lightDemo';
import {addOtherClinic, OTHER_CLINIC} from '../../test/otherClinic';
import {ADMIN, LAB, personByUsername, STAFF} from '../accounts';
import {id, MEETINGS, PATHWAYS, PATIENTS, REFERRALS, TEAMS, USERS} from '../ids';
import type {DemoState} from '../state';
import {ukDateOf} from '../time';
import {db} from '../store';

const PATHWAY = '/api/pathways/patient-pathways';
const OTHER = OTHER_CLINIC.pathwayId;

describe('the team walls in the light demo', () => {
  it("keeps another clinic's pathway from Dermatology's staff: a 404, left out of lists, a summary line on the patient", async () => {
    addOtherClinic(startLightDemo(STAFF).state);
    expect((await call('GET', `${PATHWAY}/${OTHER}`)).status).toBe(404);
    expect((await call('POST', `${PATHWAY}/${OTHER}/notes`, {text: 'x'})).status).toBe(404);
    expect(db().activityLog.some((e) => e.action === 'TeamBoundaryRefused' && e.details?.includes(OTHER))).toBe(true);

    const search = (await call('GET', `${PATHWAY}/search?PatientId=${PATIENTS.priyaHolloway}&IncludeClosed=true`)).body;
    expect(search.items.map((p: {id: string}) => p.id)).toEqual([PATHWAYS.histologyToFinish]);
    const patient = (await call('GET', `/api/patients/${PATIENTS.priyaHolloway}`)).body;
    expect(patient.otherTeamPathways).toEqual([expect.objectContaining({teamName: OTHER_CLINIC.name, state: 'InProgress'})]);
    const timeline = (await call('GET', `/api/patients/${PATIENTS.priyaHolloway}/timeline?PageSize=100`)).body;
    expect(timeline.items.some((e: {pathwayId?: string}) => e.pathwayId === OTHER)).toBe(false);
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
    const back = (await call('GET', `${PATHWAY}/${PATHWAYS.referredToHospital}`)).body;
    expect(back).toMatchObject({canWorkOnStages: false, canAddNotes: false});
    expect((await call('POST', `${PATHWAY}/${PATHWAYS.referredToHospital}/notes`, {text: 'x'})).status).toBe(409);
    expect((await call('GET', `${PATHWAY}/${PATHWAYS.unassigned}`)).status).toBe(404);
  });

  it('lets an admin read every team’s pathways, with no summaries', async () => {
    addOtherClinic(startLightDemo(ADMIN).state);
    expect((await call('GET', `${PATHWAY}/${OTHER}`)).status).toBe(200);
    expect((await call('GET', `/api/patients/${PATIENTS.priyaHolloway}`)).body.otherTeamPathways).toEqual([]);
  });

  it("keeps other teams' referrals from Dermatology: left out of lists and counts, a 404, and gone once sent or redirected away", async () => {
    addOtherClinic(startLightDemo(STAFF).state);
    const listed = (await call('GET', '/api/referrals/search?PageSize=100')).body.items.map((r: {teamId: string}) => r.teamId);
    expect(new Set(listed)).toEqual(new Set([TEAMS.dermatology]));
    expect((await call('GET', `/api/referrals/${OTHER_CLINIC.referralId}`)).status).toBe(404);
    expect((await call('GET', '/api/referrals/awaiting-triage/count')).body).toEqual({count: 2, inYourTeams: true});

    const registered = await call('POST', '/api/referrals', {
      patientId: PATIENTS.tobiasLund,
      teamId: OTHER_CLINIC.teamId,
      receivedOn: ukDateOf(new Date().toISOString()),
      referrerPractice: 'Synthetic Street Surgery',
      requestedPriority: 'Medium',
      reason: 'A synthetic reason.',
    });
    expect(registered.status).toBe(200);
    expect((await call('GET', `/api/referrals/${registered.body.id}`)).status).toBe(404);

    const referral = (await call('GET', `/api/referrals/${REFERRALS.dermatologyToTriage}`)).body;
    const redirect = {teamId: OTHER_CLINIC.teamId, detail: 'For the other clinic.', expectedVersion: referral.version};
    expect((await call('POST', `/api/referrals/${REFERRALS.dermatologyToTriage}/redirect`, redirect)).status).toBe(200);
    expect((await call('GET', `/api/referrals/${REFERRALS.dermatologyToTriage}`)).status).toBe(404);
  });

  it('lets an admin count and read every team’s referrals', async () => {
    addOtherClinic(startLightDemo(ADMIN).state);
    expect((await call('GET', `/api/referrals/${OTHER_CLINIC.referralId}`)).status).toBe(200);
    expect((await call('GET', '/api/referrals/awaiting-triage/count')).body).toEqual({count: 4, inYourTeams: false});
  });

  it("shows another team's appointment on a calendar as busy time, with no patient", async () => {
    addOtherClinic(startLightDemo(STAFF).state);
    const other = db().appointments.find((a) => a.id === OTHER_CLINIC.appointmentId)!;
    const day = ukDateOf(other.startUtc!);
    const calendar = (await call('GET', `/api/calendar?StaffUserId=${USERS.hallworth}&From=${day}&To=${day}`)).body;
    expect(calendar.appointments.map((a: {id: string}) => a.id)).not.toContain(other.id);
    expect(calendar.otherTeamsBusy).toContainEqual(
      {staffUserId: USERS.hallworth, username: 't.hallworth', startUtc: other.startUtc, endUtc: other.endUtc, what: 'Busy'},
    );
    expect(calendar.otherTeamsBusy.every((busy: {what: string}) => busy.what === 'Busy')).toBe(true);
    expect(JSON.stringify(calendar)).not.toContain(PATIENTS.priyaHolloway);
    expect((await call('GET', `/api/appointments/${other.id}/messages`)).status).toBe(404);

    addOtherClinic(startLightDemo(ADMIN).state);
    const admins = (await call('GET', `/api/calendar?StaffUserId=${USERS.hallworth}&From=${day}&To=${day}`)).body;
    expect(admins.appointments.map((a: {id: string}) => a.id)).toContain(other.id);
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
    addOtherClinic(startLightDemo(STAFF).state);
    const mine = (await call('GET', '/api/pathways/insights')).body;
    // Another team picked is a 400 (MC6's clinic picker).
    expect((await call('GET', `/api/pathways/insights?TeamId=${OTHER_CLINIC.teamId}`)).status).toBe(400);
    addOtherClinic(startLightDemo(ADMIN).state);
    const all = (await call('GET', '/api/pathways/insights')).body;
    expect(all.open.current).toBeGreaterThan(mine.open.current);
  });

  it("narrows the grids to the clinic picked: an admin's any team, staff's own only (another is a 400)", async () => {
    addOtherClinic(startLightDemo(ADMIN).state);
    const ids = (body: {items: {id: string}[]}) => body.items.map((x) => x.id);
    const other = `TeamId=${OTHER_CLINIC.teamId}&PageSize=100`;
    const pathways = (await call('GET', `${PATHWAY}/search?${other}&IncludeClosed=true`)).body;
    expect(ids(pathways)).toEqual([OTHER]);
    expect(ids((await call('GET', `/api/patients/search?${other}`)).body)).toEqual([PATIENTS.priyaHolloway]);
    expect(ids((await call('GET', `/api/patients/search?TeamId=${TEAMS.lab}&PageSize=100`)).body)).not.toContain(PATIENTS.priyaHolloway);
    for (const path of ['/api/referrals/search', '/api/appointments/search', '/api/results/search', '/api/letters/search?View=All&'])
      expect((await call('GET', `${path}${path.endsWith('&') ? '' : '?'}${other}`)).status).toBe(200);

    startLightDemo(STAFF);
    expect((await call('GET', `${PATHWAY}/search?TeamId=${TEAMS.dermatology}`)).status).toBe(200);
    const refused = await call('GET', `/api/patients/search?TeamId=${TEAMS.lab}`);
    expect(refused.status).toBe(400);
    expect(refused.body.errors.teamId).toBeDefined();
  });
});

describe('cross-clinic MDTs in the light demo (MC26)', () => {
  const MEETING = id(0xd8, 0x7e);
  const ITEM = id(0xe9, 0x7e);

  /** The other clinic's MDT, ahead, with n.thornbury (Dermatology) invited, and its pathway listed on it. */
  function addOtherClinicMdt(state: DemoState, endUtc = new Date(Date.now() + 3 * 86400_000).toISOString()) {
    addOtherClinic(state);
    const startUtc = new Date(Date.parse(endUtc) - 3600_000).toISOString();
    state.meetings.push({
      id: MEETING,
      title: 'Other clinic MDT',
      date: endUtc.slice(0, 10),
      startUtc,
      endUtc,
      organiserUserId: USERS.hallworth,
      organiserUsername: 't.hallworth',
      attendees: [{userId: STAFF.id, username: STAFF.username, reply: 'Accepted', repliedAtUtc: null}],
      isMdt: true,
      teamId: OTHER_CLINIC.teamId,
    });
    state.mdtItems.push({
      id: ITEM,
      patientId: PATIENTS.priyaHolloway,
      pathwayId: OTHER,
      teamId: OTHER_CLINIC.teamId,
      meetingId: MEETING,
      meetingStartUtc: startUtc,
      question: 'Agree the plan.',
      status: 'Listed',
      listedByUserId: USERS.hallworth,
      listedAtUtc: new Date().toISOString(),
      outcome: null,
      outcomeByUserId: null,
      outcomeAtUtc: null,
      presentUserIds: [],
      withdrawnByUserId: null,
      withdrawnAtUtc: null,
      presenterUserId: null,
      version: 1,
    });
  }

  it("lets an attendee from another clinic read the listed case only, read only, until it's withdrawn and the meeting has ended", async () => {
    addOtherClinicMdt(startLightDemo(STAFF).state);
    const list = (await call('GET', `/api/mdt/meetings/${MEETING}/items`)).body;
    expect(list.readOnlyReason).toMatch(/^You attend .*’s MDT/);
    expect(list.items).toEqual([expect.objectContaining({id: ITEM, canWithdraw: false, canDefer: false, canChoosePresenter: false})]);
    expect(list.items[0].readOnlyReason).toMatch(/^Other clinic \(test\)’s case/);
    const pathway = await call('GET', `${PATHWAY}/${OTHER}`);
    expect(pathway.status).toBe(200);
    expect(pathway.body).toMatchObject({canEditDetails: false, canAddNotes: false, canWorkOnStages: false});
    expect(pathway.body.readOnlyReason).toMatch(/^You attend .*’s MDT, so you can read this pathway/);
    expect((await call('POST', `/api/mdt/items/${ITEM}/withdraw`, {expectedVersion: 1})).status).toBe(409);
    expect((await call('POST', `${PATHWAY}/${OTHER}/notes`, {text: 'x'})).status).toBe(409);
    // Withdrawn, the grant lasts until the meeting ends; then it's gone.
    const item = db().mdtItems.find((i) => i.id === ITEM)!;
    item.status = 'Withdrawn';
    expect((await call('GET', `${PATHWAY}/${OTHER}`)).status).toBe(200);
    db().meetings.find((m) => m.id === MEETING)!.endUtc = new Date(Date.now() - 60_000).toISOString();
    expect((await call('GET', `${PATHWAY}/${OTHER}`)).status).toBe(404);
  });

  it('shows someone from another team who is not invited none of the list, and no counts', async () => {
    addOtherClinicMdt(startLightDemo(personByUsername('b.sandiford')!).state);
    const list = (await call('GET', `/api/mdt/meetings/${MEETING}/items`)).body;
    expect(list.items).toEqual([]);
    expect(list.readOnlyReason).toMatch(/you aren’t invited/);
    expect(list.meeting).toMatchObject({itemCount: 0, openItemCount: 0});
    expect((await call('GET', `${PATHWAY}/${OTHER}`)).status).toBe(404);
    expect((await call('GET', `/api/mdt/pathways/${OTHER}/items`)).status).toBe(404);
  });
});

describe('team calendars and the meeting rule in the light demo (MC11)', () => {
  it("shows another team's meeting as just busy \"Meeting\": no title, attendees or ID, and a 404 when opened", async () => {
    startLightDemo(LAB);
    const meeting = db().meetings.find((m) => m.id === MEETINGS.skinMdt)!;
    const day = meeting.date!;
    const team = (await call('GET', `/api/calendar/team?Date=${day}&TeamId=${TEAMS.dermatology}`)).body;
    const nadia = team.members.find((m: {staffUserId: string}) => m.staffUserId === STAFF.id);
    expect(nadia.meetings).toEqual([]);
    expect(nadia.otherTeamsBusy).toContainEqual({staffUserId: STAFF.id, username: STAFF.username, startUtc: meeting.startUtc, endUtc: meeting.endUtc, what: 'Meeting'});
    expect(JSON.stringify(team)).not.toContain(meeting.title!);
    expect(JSON.stringify(team)).not.toContain(MEETINGS.skinMdt);
    expect((await call('GET', `/api/calendar/meetings/${MEETINGS.skinMdt}`)).status).toBe(404);

    startLightDemo(STAFF);
    const own = (await call('GET', `/api/calendar?StaffUserId=${USERS.sandiford}&From=${day}&To=${day}`)).body;
    expect(own.meetings.map((m: {id: string}) => m.id)).toContain(MEETINGS.skinMdt);
  });

  it("shows anyone any team's calendar: their first team's by default, with its members", async () => {
    startLightDemo(STAFF);
    const mine = (await call('GET', '/api/calendar/team')).body;
    expect(mine.teamId).toBe(TEAMS.dermatology);
    expect(mine.teamMembers.map((m: {username: string}) => m.username)).toEqual(['b.sandiford', 'n.thornbury', 't.hallworth']);
    expect(mine.members).toHaveLength(3);

    const lab = (await call('GET', `/api/calendar/team?TeamId=${TEAMS.lab}&StaffUserIds=${STAFF.id}`)).body;
    expect(lab.teamName).toBe('Lab');
    // Who is narrowed to the team's own members.
    expect(lab.members.map((m: {staffUserId: string}) => m.staffUserId)).toEqual([LAB.id]);
  });

  it('books meetings only with your own team, except an MDT; admins arrange any', async () => {
    const {state} = startLightDemo(personByUsername('j.whitlock')!);
    state.memberships[USERS.whitlock] = [{teamId: TEAMS.dermatology, role: 'Member'}];
    const startUtc = new Date(Date.now() + 3 * 86400_000).toISOString();
    const endUtc = new Date(Date.parse(startUtc) + 30 * 60000).toISOString();
    const meeting = {title: 'Planning', startUtc, endUtc, attendeeUserIds: [STAFF.id, LAB.id]};
    const refused = await call('POST', '/api/calendar/meetings', meeting);
    expect(refused.status).toBe(400);
    expect(refused.body.errors.attendeeUserIds[0]).toMatch(/^r\.aldous isn’t in your team/);
    expect((await call('POST', '/api/calendar/meetings', {...meeting, attendeeUserIds: [STAFF.id]})).status).toBe(200);
    expect((await call('POST', '/api/calendar/meetings', {...meeting, isMdt: true, teamId: TEAMS.dermatology})).status).toBe(200);

    startLightDemo(ADMIN);
    expect((await call('POST', '/api/calendar/meetings', meeting)).status).toBe(200);
  });
});
