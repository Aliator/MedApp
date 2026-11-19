import {call, startLightDemo} from '../../test/lightDemo';
import {ADMIN, STAFF} from '../accounts';
import {MEETINGS, PATHWAYS, PATIENTS, REFERRALS, TEMPLATES, USERS} from '../ids';
import {db} from '../store';

const isCurrent = (stage: {isCurrent: boolean}) => stage.isCurrent;

/** A UTC time a few days ahead, on the hour. */
function daysAhead(days: number, hour: number): Date {
  const date = new Date(Date.now() + days * 86400_000);
  date.setUTCHours(hour, 0, 0, 0);
  return date;
}

describe('the light demo handlers', () => {
  it('signs in only as one of the two demo accounts, and out again', async () => {
    startLightDemo(null);
    expect((await call('GET', '/api/auth/whoami')).body.isAuthenticated).toBe(false);
    expect((await call('POST', '/api/auth/login', {username: 'j.whitlock', password: 'x'})).status).toBe(401);
    expect((await call('POST', '/api/auth/login', {username: STAFF.username, password: 'x'})).status).toBe(200);
    const whoami = (await call('GET', '/api/auth/whoami')).body;
    expect(whoami).toMatchObject({isAuthenticated: true, name: STAFF.username, roles: ['Clinician']});
    expect(whoami.permissions).not.toContain('ViewInsights');
    await call('POST', '/api/auth/logout');
    expect((await call('GET', '/api/pathways/patient-pathways/search')).status).toBe(401);
  });

  it('finishes the current stage, and moves on to the next', async () => {
    startLightDemo(STAFF);
    const before = (await call('GET', `/api/pathways/patient-pathways/${PATHWAYS.histologyToFinish}`)).body;
    const current = before.stages.find(isCurrent);
    expect(current.name).toBe('Histology (lab)');
    const after = await call('POST', `/api/pathways/patient-pathways/${PATHWAYS.histologyToFinish}/stages/${current.id}/finish`, {});
    expect(after.status).toBe(200);
    expect(after.body.stages.find(isCurrent).name).toBe('Results and team review');
    expect(after.body.completedStageCount).toBe(3);
  });

  it('asks for a reason to finish a stage whose visit wasn’t attended', async () => {
    startLightDemo(STAFF);
    const pathway = (await call('GET', `/api/pathways/patient-pathways/${PATHWAYS.biopsyToBook}`)).body;
    const finish = `/api/pathways/patient-pathways/${PATHWAYS.biopsyToBook}/stages/${pathway.currentStageId}/finish`;
    expect((await call('POST', finish, {})).status).toBe(409);
    expect((await call('POST', finish, {withoutVisit: true, reason: 'Done at the GP'})).status).toBe(200);
  });

  it('puts a pathway on hold with a reason, and takes it off again', async () => {
    startLightDemo(STAFF);
    const status = `/api/pathways/patient-pathways/${PATHWAYS.followUpBooked}/status`;
    expect((await call('POST', status, {status: 'Blocked'})).status).toBe(400);
    const held = (await call('POST', status, {status: 'Blocked', reason: 'Waiting for results'})).body;
    expect(held).toMatchObject({status: 'Blocked', blockedFromStatus: 'InProgress', allowedStatusChanges: ['InProgress', 'Cancelled']});
    expect((await call('POST', status, {status: 'InProgress'})).body.status).toBe('InProgress');
  });

  it('accepts a referral into a new pathway, once, and only for a member of its team', async () => {
    startLightDemo(STAFF);
    const gynae = await call('POST', `/api/referrals/${REFERRALS.ruthGynaecology}/accept`, {
      templateId: TEMPLATES.postMenopausalBleeding,
      priority: 'Urgent',
    });
    expect(gynae.status).toBe(409);
    const accepted = await call('POST', `/api/referrals/${REFERRALS.dermatologyToTriage}/accept`, {
      templateId: TEMPLATES.skinLesion,
      priority: 'Urgent',
      expectedVersion: 1,
    });
    expect(accepted.status).toBe(200);
    expect(accepted.body).toMatchObject({patientId: PATIENTS.oliverMaddox, templateName: 'Skin lesion pathway', status: 'Unassigned'});
    const referral = (await call('GET', `/api/referrals/${REFERRALS.dermatologyToTriage}`)).body;
    expect(referral).toMatchObject({status: 'Accepted', pathwayId: accepted.body.id, canTriage: false});
    const again = await call('POST', `/api/referrals/${REFERRALS.dermatologyToTriage}/accept`, {templateId: TEMPLATES.skinLesion, priority: 'High'});
    expect(again.status).toBe(409);
  });

  it('puts a meeting on the calendars of those invited, and tells them', async () => {
    startLightDemo(ADMIN);
    const start = daysAhead(2, 10);
    const created = await call('POST', '/api/calendar/meetings', {
      title: 'Demo planning',
      startUtc: start.toISOString(),
      endUtc: new Date(start.getTime() + 3600_000).toISOString(),
      attendeeUserIds: [STAFF.id],
    });
    expect(created.status).toBe(200);
    db().user = STAFF.username;
    const date = created.body.date;
    const calendar = (await call('GET', `/api/calendar?From=${date}&To=${date}`)).body;
    expect(calendar.meetings.map((m: {title: string}) => m.title)).toContain('Demo planning');
    const notifications = (await call('GET', '/api/notifications')).body;
    expect(notifications.items[0]).toMatchObject({kind: 'MeetingInvited', meetingId: created.body.id});
  });

  it('refuses a meeting to someone who can’t arrange them, and lets them reply to one', async () => {
    startLightDemo(STAFF);
    expect((await call('POST', '/api/calendar/meetings', {title: 'x'})).status).toBe(403);
    expect((await call('GET', `/api/calendar/meetings/${MEETINGS.skinMdt}`)).body.myReply).toBe('Accepted');
    const declined = await call('PUT', `/api/calendar/meetings/${MEETINGS.skinMdt}/reply`, {reply: 'Declined'});
    expect(declined.body.myReply).toBe('Declined');
  });

  it('books a stage’s visit, which shows on the pathway and the timeline', async () => {
    startLightDemo(STAFF);
    const pathway = (await call('GET', `/api/pathways/patient-pathways/${PATHWAYS.biopsyToBook}`)).body;
    const start = daysAhead(3, 11);
    const booked = await call('POST', '/api/appointments', {
      patientId: PATIENTS.ellisVarden,
      pathwayId: PATHWAYS.biopsyToBook,
      stageId: pathway.currentStageId,
      staffUserId: STAFF.id,
      startUtc: start.toISOString(),
      endUtc: new Date(start.getTime() + 30 * 60000).toISOString(),
    });
    expect(booked.body).toMatchObject({status: 'Booked', stageName: 'Biopsy or sample', staffUsername: STAFF.username});
    const onPathway = (await call('GET', `/api/appointments/search?PathwayId=${PATHWAYS.biopsyToBook}&PageSize=100`)).body;
    expect(onPathway.items.map((a: {id: string}) => a.id)).toContain(booked.body.id);
    const timeline = (await call('GET', `/api/patients/${PATIENTS.ellisVarden}/timeline?PageSize=50`)).body;
    expect(timeline.items.map((e: {action: string}) => e.action)).toContain('AppointmentBooked');
  });

  it('pages a patient’s timeline, newest first', async () => {
    startLightDemo(ADMIN);
    const first = (await call('GET', `/api/patients/${PATIENTS.priyaHolloway}/timeline?PageSize=3`)).body;
    expect(first.items).toHaveLength(3);
    expect(first.items[0].atUtc >= first.items[2].atUtc).toBe(true);
    const next = (await call('GET', `/api/patients/${PATIENTS.priyaHolloway}/timeline?PageSize=3&Before=${encodeURIComponent(first.nextCursor)}`)).body;
    expect(next.items[0].id).not.toBe(first.items[0].id);
  });

  it('lists my work, and counts referrals awaiting triage in my teams', async () => {
    startLightDemo(STAFF);
    const mine = (await call('GET', '/api/pathways/patient-pathways/search?AssignedToMe=true&PageSize=50')).body;
    expect(mine.items.length).toBeGreaterThan(0);
    expect(mine.items.every((p: {assignees: string[]}) => p.assignees.includes(STAFF.username))).toBe(true);
    expect((await call('GET', '/api/referrals/awaiting-triage/count')).body).toEqual({count: 2, inYourTeams: true});
  });

  it('keeps each account’s notepad and settings apart', async () => {
    startLightDemo(ADMIN);
    await call('PUT', '/api/me/notepad', {text: 'Admin’s note'});
    await call('PUT', '/api/auth/me/settings', {settings: '{"medapp.colorMode":"light"}'});
    db().user = STAFF.username;
    expect((await call('GET', '/api/me/notepad')).body.text).toBe('');
    expect((await call('GET', '/api/auth/me/settings')).body.settings).toBeNull();
    expect(db().notepads[USERS.admin].text).toBe('Admin’s note');
  });

  it('shows Insights from the demo’s pathways', async () => {
    startLightDemo(ADMIN);
    const insights = (await call('GET', '/api/pathways/insights?Weeks=4')).body;
    expect(insights.overdue.current).toBe(1);
    expect(insights.throughput).toHaveLength(4);
    expect(insights.stageTimes.length).toBeGreaterThan(0);
  });

  it('asks for a branch at a branch point, and skips the branch not chosen', async () => {
    startLightDemo(STAFF);
    const pathway = (await call('GET', `/api/pathways/patient-pathways/${PATHWAYS.branchToChoose}`)).body;
    const point = pathway.stages.find(isCurrent);
    expect(point).toMatchObject({choosesBranch: true, needsBranchChoice: true});
    expect(point.branches.map((b: {name: string}) => b.name)).toEqual(['Biopsy first', 'Treat directly']);
    const finish = `/api/pathways/patient-pathways/${PATHWAYS.branchToChoose}/stages/${point.id}/finish`;
    expect((await call('POST', finish, {})).status).toBe(400);
    const after = (await call('POST', finish, {branch: 'treat directly'})).body;
    expect(after.stages.find(isCurrent).name).toBe('Excision');
    expect(after.skippedStageCount).toBe(2);
    expect(after.stages.filter((s: {state: string}) => s.state === 'Skipped').every((s: {skipReason: string}) => s.skipReason === 'BranchNotChosen')).toBe(true);
  });

  it('runs a fork’s routes side by side, and starts the join only when each is done (MC8)', async () => {
    startLightDemo(STAFF);
    const url = `/api/pathways/patient-pathways/${PATHWAYS.forkedTests}`;
    let pathway = (await call('GET', url)).body;
    const [histology, labTests] = pathway.stages.filter(isCurrent);
    expect([histology.name, labTests.name]).toEqual(['Histology (lab)', 'Lab tests (lab)']);
    expect(pathway.currentStageIds).toEqual([histology.id, labTests.id]);
    const review = pathway.stages.find((s: {name: string}) => s.name === 'Results and team review');
    expect(review.predecessorIds).toEqual([histology.id, labTests.id]);
    expect((await call('POST', `${url}/stages/${review.id}/finish`, {withoutVisit: true})).status).toBe(409);
    pathway = (await call('POST', `${url}/stages/${histology.id}/finish`, {})).body;
    expect(pathway.stages.filter(isCurrent).map((s: {id: string}) => s.id)).toEqual([labTests.id]);
    pathway = (await call('POST', `${url}/stages/${labTests.id}/finish`, {})).body;
    expect(pathway.stages.filter(isCurrent).map((s: {id: string}) => s.id)).toEqual([review.id]);
  });

  it('skips an optional stage with a reason, and reinstates it', async () => {
    startLightDemo(STAFF);
    const url = `/api/pathways/patient-pathways/${PATHWAYS.branchToChoose}`;
    let pathway = (await call('POST', `${url}/stages/${(await call('GET', url)).body.currentStageId}/finish`, {branch: 'Treat directly'})).body;
    // Excision needs consent first: the consent gate refuses until a record is made.
    const excision = pathway.stages.find(isCurrent);
    db().pathways.find((p) => p.id === PATHWAYS.branchToChoose)!.stages.find((s) => s.id === excision.id)!.completedAtUtc = new Date().toISOString();
    pathway = (await call('GET', url)).body;
    const followUp = pathway.stages.find(isCurrent);
    expect(followUp).toMatchObject({name: 'Follow-up', isOptional: true, canSkip: true});
    expect((await call('POST', `${url}/stages/${followUp.id}/skip`, {})).status).toBe(400);
    pathway = (await call('POST', `${url}/stages/${followUp.id}/skip`, {reason: 'NotNeeded'})).body;
    expect(pathway.stages.find(isCurrent).name).toBe('Discharge letter to GP');
    pathway = (await call('POST', `${url}/stages/${followUp.id}/reinstate`, {reason: 'Wanted after all'})).body;
    expect(pathway.stages.find(isCurrent).id).toBe(followUp.id);
  });

  it('refuses to finish a stage that needs consent until it’s recorded', async () => {
    startLightDemo(STAFF);
    const url = `/api/pathways/patient-pathways/${PATHWAYS.consentToRecord}`;
    const treatment = (await call('GET', url)).body.stages.find(isCurrent);
    expect(treatment).toMatchObject({name: 'Treatment', needsConsent: true});
    const refused = await call('POST', `${url}/stages/${treatment.id}/finish`, {});
    expect(refused.status).toBe(409);
  });

  it('keeps the stage library and templates, and refuses deleting what’s in use', async () => {
    startLightDemo(ADMIN);
    const definitions = (await call('GET', '/api/pathways/patient-pathway-stage/definitions')).body;
    const histology = definitions.find((d: {name: string}) => d.name === 'Histology (lab)');
    expect(histology.canBeDeleted).toBe(false);
    expect((await call('DELETE', `/api/pathways/patient-pathway-stage/definitions/${histology.id}`)).status).toBe(409);
    const made = await call('POST', '/api/pathways/patient-pathway-stage/definitions', {name: 'Phone review', visitRequirement: 'None'});
    expect(made.status).toBe(201);
    const badBranch = await call('POST', '/api/pathways/patient-pathway-stage/templates', {
      name: 'Phone pathway',
      teamId: definitions.length && db().teams[0].id,
      stageDefinitionIdsInOrder: [made.body.id, histology.id],
      stageOptions: [{choosesBranch: true}, {}],
    });
    expect(badBranch.status).toBe(400);
    const template = await call('POST', '/api/pathways/patient-pathway-stage/templates', {
      name: 'Phone pathway',
      teamId: db().teams[0].id,
      stageDefinitionIdsInOrder: [made.body.id, histology.id],
    });
    expect(template.status).toBe(201);
    expect((await call('DELETE', `/api/pathways/patient-pathway-stage/templates/${TEMPLATES.skinLesion}`)).status).toBe(409);
    expect((await call('DELETE', `/api/pathways/patient-pathway-stage/templates/${template.body.id}`)).status).toBe(204);
  });

  it('lets staff ask for a note to be deleted, and an admin delete it', async () => {
    startLightDemo(STAFF);
    const url = `/api/pathways/patient-pathways/${PATHWAYS.biopsyToBook}`;
    const note = (await call('GET', url)).body.noteList[0];
    expect(note).toMatchObject({canDelete: false, canRequestDeletion: true});
    const asked = await call('POST', `${url}/notes/${note.id}/deletion-requests`, {reason: 'WrongPatient'});
    expect(asked.status).toBe(201);
    db().user = ADMIN.username;
    const request = (await call('GET', `/api/pathways/note-deletion-requests/${asked.body.deletionRequestId}`)).body;
    expect(request).toMatchObject({status: 'Pending', canDecide: true});
    const decided = (await call('POST', `/api/pathways/note-deletion-requests/${request.id}/decision`, {deleteNote: true, expectedVersion: 1})).body;
    expect(decided.status).toBe('Approved');
    expect((await call('GET', url)).body.noteList).toHaveLength(0);
  });

  it('logs a data request, checks identity, records the review, refuses the pack and records it sent', async () => {
    startLightDemo(ADMIN);
    const patientId = db().pathways.find((p) => p.notes.length > 0)?.patientId ?? db().patients[0].id;
    const created = await call('POST', '/api/data-requests', {patientId, receivedOn: '2026-01-31', requestedByPatient: true});
    expect(created.status).toBe(201);
    expect(created.body).toMatchObject({status: 'Open', firstDueOn: '2026-02-28', dueOn: '2026-02-28', canManage: true, canMakePack: false});
    expect(created.body.packBlockedReason).toBe('Record the identity check first.');
    const id = created.body.id;

    const list = (await call('GET', '/api/data-requests/search?AwaitingReview=true')).body;
    expect(list.items.map((r: {id: string}) => r.id)).toContain(id);

    const checked = await call('POST', `/api/data-requests/${id}/identity-check`, {checkedOn: '2026-02-01', method: 'InPerson', expectedVersion: 1});
    expect(checked.body).toMatchObject({identityCheckMethod: 'InPerson', version: 2});
    expect((await call('POST', `/api/data-requests/${id}/extension`, {months: 1, reason: 'Complex', expectedVersion: 1})).status).toBe(409);

    const items = (await call('GET', `/api/data-requests/${id}/review-items`)).body;
    const withheld = items.slice(0, 1).map((item: {kind: string; itemId: string}) => ({kind: item.kind, itemId: item.itemId, reason: 'Serious harm'}));
    const reviewed = await call('PUT', `/api/data-requests/${id}/review`, {withheld, expectedVersion: 2});
    expect(reviewed.body).toMatchObject({canMakePack: true, packBlockedReason: null, withheld});

    const pack = await call('GET', `/api/data-requests/${id}/pack`);
    expect(pack.status).toBe(501);
    expect(pack.body.title).toBe('The data-request pack isn’t in the light demo.');

    const sent = await call('POST', `/api/data-requests/${id}/sent`, {sentOn: '2026-02-10', method: 'Post', expectedVersion: 3});
    expect(sent.body).toMatchObject({status: 'Sent', canManage: false, canReview: false});
    expect((await call('GET', '/api/data-requests/search?Status=Sent')).body.totalCount).toBe(1);
  });

  it('keeps data requests to admins: a clinician can neither see, log nor review one', async () => {
    // Only ManagePrivacy deals with data requests (the owner's decision), as in the API.
    startLightDemo(STAFF);
    expect((await call('GET', '/api/data-requests/search')).status).toBe(403);
    expect((await call('POST', '/api/data-requests', {patientId: PATIENTS.ellisVarden, receivedOn: '2026-01-01', requestedByPatient: true})).status).toBe(403);
    expect((await call('PUT', '/api/data-requests/any/review', {withheld: [], expectedVersion: 1})).status).toBe(403);
  });
});
