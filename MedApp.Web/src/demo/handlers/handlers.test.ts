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

  it('accepts a referral into a new pathway, once, and only for a member of its specialty', async () => {
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

  it('lists my work, and counts referrals awaiting triage in my specialties', async () => {
    startLightDemo(STAFF);
    const mine = (await call('GET', '/api/pathways/patient-pathways/search?AssignedToMe=true&PageSize=50')).body;
    expect(mine.items.length).toBeGreaterThan(0);
    expect(mine.items.every((p: {assignees: string[]}) => p.assignees.includes(STAFF.username))).toBe(true);
    expect((await call('GET', '/api/referrals/awaiting-triage/count')).body).toEqual({count: 2, inYourSpecialties: true});
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
});
