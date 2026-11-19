import {call, startLightDemo} from '../../test/lightDemo';
import {addDays, weekStart} from '../../utils/calendarSlots';
import {ADMIN, HALLWORTH, STAFF} from '../accounts';
import {PATHWAYS, PATIENTS, ROOMS, TEAMS, USERS} from '../ids';
import {CLINIC_SESSIONS, DAY_TEMPLATES, MONITORING_RULES, PRACTICES, ROLES} from '../slices/setup';
import {db} from '../store';
import {today} from '../time';

const nextTuesday = () => addDays(weekStart(today()), 8);

describe('the light demo’s setup and admin handlers', () => {
  it('keeps the practice directory, refusing a stale change and deleting only what nobody has', async () => {
    startLightDemo(STAFF);
    const list = (await call('GET', '/api/practices?includeArchived=true')).body;
    expect(list.map((p: {name: string}) => p.name)).toContain('Example Lane Surgery (synthetic)');
    const lane = list.find((p: {id: string}) => p.id === PRACTICES.exampleLane);
    expect(lane).toMatchObject({patientCount: 3, canBeDeleted: false});
    expect((await call('PUT', `/api/practices/${lane.id}`, {name: 'Renamed', expectedVersion: 99})).status).toBe(409);
    expect((await call('PUT', `/api/practices/${lane.id}`, {name: 'Renamed', expectedVersion: 1})).body.version).toBe(2);
    expect((await call('DELETE', `/api/practices/${lane.id}`)).status).toBe(409);
    expect((await call('DELETE', `/api/practices/${PRACTICES.oldMill}`)).status).toBe(204);
  });

  it('archives a team only once nothing awaits triage in it, and keeps setup to admins', async () => {
    startLightDemo(STAFF);
    expect((await call('POST', '/api/teams', {name: 'Rheumatology'})).status).toBe(403);
    startLightDemo(ADMIN);
    const archive = await call('POST', `/api/teams/${TEAMS.dermatology}/archive`, {expectedVersion: 1});
    expect(archive.status).toBe(409);
    expect(archive.body.title).toMatch(/awaiting triage/);
    const added = await call('POST', '/api/teams', {name: 'Rheumatology'});
    expect(added.status).toBe(201);
    expect(added.body).toMatchObject({canBeDeleted: true, targetDaysUrgent: 14});
    expect((await call('DELETE', `/api/teams/${added.body.id}`)).status).toBe(204);
    expect((await call('DELETE', `/api/teams/${TEAMS.urology}`)).status).toBe(204);
    expect((await call('DELETE', `/api/teams/${TEAMS.dermatology}`)).status).toBe(409);
  });

  it('changes someone’s teams, and records it in the activity log', async () => {
    startLightDemo(ADMIN);
    const set = await call('PUT', '/api/auth/users/h.marsh/teams', {teams: [{teamId: TEAMS.urology, role: 'ClinicAdmin'}]});
    expect(set.body).toMatchObject([{name: 'Urology', kind: 'Clinic', role: 'ClinicAdmin'}]);
    const log = (await call('GET', '/api/activity/search?Actions=TeamMembershipChanged')).body;
    expect(log.items[0]).toMatchObject({targetUsername: 'h.marsh', details: JSON.stringify({from: [], to: ['Urology (clinic admin)']})});
    expect((await call('GET', '/api/auth/users/h.marsh')).body.accountType).toBe('ClinicAdmin');
  });

  it('keeps an admin out of every team, and the lab without a clinic admin', async () => {
    startLightDemo(ADMIN);
    expect((await call('PUT', '/api/auth/users/admin/teams', {teams: [{teamId: TEAMS.urology, role: 'Member'}]})).status).toBe(409);
    expect((await call('PUT', '/api/auth/users/h.marsh/teams', {teams: [{teamId: TEAMS.lab, role: 'ClinicAdmin'}]})).status).toBe(409);
    expect((await call('PUT', '/api/auth/users/h.marsh/teams', {teams: [{teamId: TEAMS.lab, role: 'Member'}]})).status).toBe(200);
  });

  it('closes a day: not a working day, with no free times', async () => {
    startLightDemo(ADMIN);
    const date = addDays(nextTuesday(), 7);
    expect((await call('POST', '/api/calendar/closed-days', {date, name: 'Staff day'})).status).toBe(201);
    expect((await call('POST', '/api/calendar/closed-days', {date, name: 'Again'})).status).toBe(409);
    const day = (await call('GET', `/api/calendar?StaffUserId=${USERS.marsh}&From=${date}&To=${date}`)).body.days[0];
    expect(day).toMatchObject({isWorkingDay: false, closedDayName: 'Closed: Staff day'});
    const free = (await call('GET', `/api/calendar/availability?StaffUserIds=${USERS.marsh}&From=${date}&To=${date}`)).body;
    expect(free.days[0].free).toEqual([]);
    const holidays = (await call('POST', '/api/calendar/closed-days/bank-holidays', {year: 2100})).body;
    expect(holidays.added.map((h: {name: string}) => h.name)).toContain('Good Friday');
  });

  it('puts the default week’s template blocks on the calendar, and changes one on one day only', async () => {
    startLightDemo(ADMIN);
    const tuesday = nextTuesday();
    const calendar = (await call('GET', `/api/calendar?From=${tuesday}&To=${tuesday}`)).body;
    const triage = calendar.blocks.find((b: {isFromTemplate: boolean}) => b.isFromTemplate);
    expect(triage).toMatchObject({activityName: 'Triage', note: 'Referral triage'});
    expect(calendar.days[0]).toMatchObject({dayTemplateId: DAY_TEMPLATES.adminEarlyTriage});
    const removed = await call('DELETE', `/api/calendar/staff/${ADMIN.id}/days/${tuesday}/template-blocks/${triage.templateBlockId}`);
    expect(removed.status).toBe(204);
    const after = (await call('GET', `/api/calendar?From=${tuesday}&To=${addDays(tuesday, 1)}`)).body;
    expect(after.blocks.filter((b: {isFromTemplate: boolean}) => b.isFromTemplate)).toHaveLength(1);
    expect((await call('DELETE', `/api/calendar/day-templates/${DAY_TEMPLATES.adminEarlyTriage}`)).status).toBe(409);
    startLightDemo(STAFF);
    expect((await call('PUT', `/api/calendar/staff/${ADMIN.id}/week-template`, {weekTemplateId: null})).status).toBe(403);
    expect((await call('DELETE', `/api/calendar/day-templates/${DAY_TEMPLATES.thornburyClinicDay}`)).status).toBe(204);
  });

  it('books into a clinic slot, and cancelling the session lists the patient to tell', async () => {
    startLightDemo(HALLWORTH);
    const session = (await call('GET', `/api/clinics/sessions/${CLINIC_SESSIONS.hallworthNextWeek}`)).body;
    expect(session.session).toMatchObject({slotCount: 9, freeSlotCount: 9, roomName: 'Clinic room 1'});
    const slot = session.slots[0];
    const booked = await call('POST', '/api/appointments', {
      patientId: PATIENTS.tobiasLund,
      staffUserId: USERS.hallworth,
      startUtc: slot.startUtc,
      endUtc: slot.endUtc,
      clinicSessionId: CLINIC_SESSIONS.hallworthNextWeek,
    });
    expect(booked.body).toMatchObject({status: 'Booked', roomId: ROOMS.clinicRoom1, clinicSessionId: CLINIC_SESSIONS.hallworthNextWeek});
    // Booking is the team's; cancelling a session is the admin's (ManageClinics).
    db().user = ADMIN.username;
    const cancel = `/api/clinics/sessions/${CLINIC_SESSIONS.hallworthNextWeek}/cancel`;
    expect((await call('POST', cancel, {reason: 'Off sick', expectedLastUpdated: 'stale'})).status).toBe(409);
    const cancelled = (await call('POST', cancel, {reason: 'Off sick', expectedLastUpdated: session.session.lastUpdated})).body;
    expect(cancelled.patientsToTell).toEqual([expect.objectContaining({patientId: PATIENTS.tobiasLund})]);
    expect((await call('GET', `/api/appointments/${booked.body.id}`)).body.status).toBe('Cancelled');
  });

  it('shows roles, changes only one nobody holds, and never puts admin-only permissions on another role', async () => {
    startLightDemo(ADMIN);
    const roles = (await call('GET', '/api/roles')).body;
    expect(roles[0]).toMatchObject({name: 'Admin', isBuiltIn: true, holderCount: 1});
    const clinician = (await call('GET', `/api/roles/${ROLES.clinician}`)).body;
    expect((await call('PUT', `/api/roles/${ROLES.clinician}`, {...clinician, expectedVersion: clinician.version})).status).toBe(409);
    expect((await call('POST', '/api/roles', {name: 'Auditor', permissions: ['UseApp', 'ViewAudit']})).status).toBe(400);
    const made = await call('POST', '/api/roles', {name: 'Auditor', description: 'Reads', permissions: ['UseApp', 'ViewInsights']});
    expect(made.body).toMatchObject({canEdit: true, canDelete: true, version: '1'});
    expect((await call('DELETE', `/api/roles/${made.body.id}`)).status).toBe(204);
    startLightDemo(STAFF);
    expect((await call('GET', '/api/roles')).status).toBe(403);
    expect((await call('GET', '/api/permissions')).status).toBe(403);
  });

  it('works out the audit log from the pathways, a note by its version and never its text', async () => {
    startLightDemo(ADMIN);
    const audit = (await call('GET', `/api/pathways/audit/search?PathwayId=${PATHWAYS.onHold}&PageSize=50`)).body;
    const actions = audit.items.map((e: {action: string}) => e.action);
    expect(actions).toEqual(expect.arrayContaining(['Created', 'Assigned', 'StageFinished', 'NoteAdded', 'StatusChanged']));
    const note = audit.items.find((e: {action: string}) => e.action === 'NoteAdded');
    expect(JSON.parse(note.details)).toEqual({noteId: expect.any(String), version: 1});
    expect(JSON.stringify(audit)).not.toMatch(/away for a month/);
    startLightDemo(STAFF);
    expect((await call('GET', '/api/pathways/audit/search')).status).toBe(403);
    expect((await call('GET', '/api/activity/search')).status).toBe(403);
  });

  it('restricts a record only once the admin confirms its care stops, and then refuses changes', async () => {
    startLightDemo(ADMIN);
    const restriction = `/api/patients/${PATIENTS.ellisVarden}/restriction`;
    const unconfirmed = await call('PUT', restriction, {set: true, reason: 'Patient asked'});
    expect(unconfirmed.status).toBe(400);
    expect(unconfirmed.body.errors.confirmOpenPathways).toBeDefined();
    expect((await call('PUT', restriction, {set: true, reason: 'Patient asked', confirmOpenPathways: true})).body.isRestricted).toBe(true);
    // The admin is read only (a 403); staff are refused because the record is restricted.
    expect((await call('PATCH', `/api/patients/${PATIENTS.ellisVarden}`, {firstName: 'E'})).status).toBe(403);
    db().user = STAFF.username;
    expect((await call('PATCH', `/api/patients/${PATIENTS.ellisVarden}`, {firstName: 'E'})).status).toBe(409);
    db().user = ADMIN.username;
    const log = (await call('GET', `/api/activity/search?PatientId=${PATIENTS.ellisVarden}`)).body;
    expect(log.items[0]).toMatchObject({action: 'PatientRestricted', patientName: 'Ellis Varden'});
    expect((await call('POST', `/api/patients/${PATIENTS.ellisVarden}/anonymise`, {reason: 'ErasureRequest', confirmation: 'Ellis Varden'})).status).toBe(409);
  });

  it('keeps a GP practice that isn’t listed on the patient only, never with a directory one, and exports it', async () => {
    startLightDemo(STAFF);
    const unlisted = {name: ' Nowhereville Health Centre ', addressLines: ['2 Pretend Road', ' '], postcode: 'zz9 9zz', phone: ''};
    const created = await call('POST', '/api/patients', {firstName: 'Ada', lastName: 'Synthetic', dateOfBirth: '1980-05-17', unlistedPractice: unlisted});
    expect(created.body.unlistedPractice).toEqual({name: 'Nowhereville Health Centre', addressLines: ['2 Pretend Road'], postcode: 'ZZ9 9ZZ', phone: null});
    expect(created.body.practiceId).toBeNull();
    const both = await call('POST', '/api/patients', {firstName: 'B', lastName: 'C', dateOfBirth: '1980-05-17', practiceId: PRACTICES.exampleLane, unlistedPractice: unlisted});
    expect(both.status).toBe(400);
    expect((await call('POST', '/api/patients', {firstName: 'B', lastName: 'C', dateOfBirth: '1980-05-17', unlistedPractice: {name: ' '}})).status).toBe(400);

    const patient = `/api/patients/${created.body.id}`;
    db().user = ADMIN.username;
    expect((await call('GET', `${patient}/export`)).body.patient.unlistedPractice.name).toBe('Nowhereville Health Centre');
    db().user = STAFF.username;
    const directory = (await call('PATCH', patient, {practiceId: PRACTICES.exampleLane})).body;
    expect(directory).toMatchObject({practiceId: PRACTICES.exampleLane});
    expect(directory.unlistedPractice).toBeUndefined();
    const back = (await call('PATCH', patient, {unlistedPractice: unlisted})).body;
    expect(back.practiceId).toBeNull();
    expect(back.unlistedPractice.name).toBe('Nowhereville Health Centre');
    expect(db().activityLog.at(-1)?.details).toBe(JSON.stringify({fields: ['practice', 'unlistedPractice']}));
    expect((await call('PATCH', patient, {practiceId: PRACTICES.exampleLane, unlistedPractice: unlisted})).status).toBe(400);
    expect((await call('PATCH', patient, {unlistedPractice: {name: ''}})).status).toBe(400);
  });

  it('exports what’s held about a patient, and records the export', async () => {
    startLightDemo(ADMIN);
    const exported = (await call('GET', `/api/patients/${PATIENTS.harrietQuill}/export`)).body;
    expect(exported.patient).toMatchObject({lastName: 'Quill', practiceName: 'Example Lane Surgery (synthetic)'});
    expect(exported.pathways[0].notes[0].text).toMatch(/refer to the hospital/);
    expect(exported.appointments.length).toBeGreaterThan(0);
    expect(db().activityLog.at(-1)).toMatchObject({action: 'PatientExported', patientId: PATIENTS.harrietQuill});
    startLightDemo(STAFF);
    expect((await call('GET', `/api/patients/${PATIENTS.harrietQuill}/export`)).status).toBe(403);
  });

  it('lists a record past its retention period, and keeping it takes it off the list', async () => {
    startLightDemo(ADMIN);
    const review = (await call('GET', '/api/patients/retention-review')).body;
    expect(review.items).toEqual([expect.objectContaining({patientId: PATIENTS.graceAbernethy, retentionClass: 'Adult'})]);
    const keep = `/api/patients/${PATIENTS.graceAbernethy}/retention/keep`;
    expect((await call('POST', keep, {keepUntil: addDays(today(), 900), reason: 'Complaint open'})).status).toBe(400);
    expect((await call('POST', keep, {keepUntil: addDays(today(), 365), reason: 'Complaint open'})).status).toBe(204);
    expect((await call('GET', '/api/patients/retention-review')).body.items).toEqual([]);
    expect((await call('GET', '/api/patients/retention-review?IncludeKept=true')).body.items[0].keptReason).toBe('Complaint open');
  });

  it('records a visit after the event, and withdraws it as entered in error', async () => {
    startLightDemo(STAFF);
    const start = new Date(Date.now() - 2 * 86400_000);
    start.setUTCHours(10, 0, 0, 0);
    while ([0, 6].includes(start.getUTCDay())) start.setUTCDate(start.getUTCDate() - 1);
    const end = new Date(start.getTime() + 30 * 60000);
    const past = await call('POST', '/api/appointments/past', {
      patientId: PATIENTS.tobiasLund,
      staffUserId: STAFF.id,
      startUtc: start.toISOString(),
      endUtc: end.toISOString(),
      outcome: 'DidNotAttend',
    });
    expect(past.status).toBe(201);
    expect(past.body).toMatchObject({isRecordedAfterEvent: true, canRecordDnaFollowUp: true, canRecorderMarkEnteredInError: true});
    const followUp = `/api/appointments/${past.body.id}/dna-follow-up`;
    expect((await call('POST', followUp, {decision: 'NoFurtherAppointment'})).status).toBe(400);
    expect((await call('POST', followUp, {decision: 'Rebook'})).body.dnaFollowUp).toBe('Rebook');
    const withdrawn = await call('POST', `/api/appointments/${past.body.id}/entered-in-error`, {reason: 'Wrong patient'});
    expect(withdrawn.body.appointment.status).toBe('EnteredInError');
  });

  it('keeps each admin’s monitoring rules their own, lists the jobs, and won’t require two-factor', async () => {
    startLightDemo(ADMIN);
    const rules = (await call('GET', '/api/monitoring/rules')).body;
    expect(rules[0]).toMatchObject({id: MONITORING_RULES.urgentHistology, templateName: 'Skin lesion pathway', stageName: 'Histology (lab)'});
    expect((await call('PUT', `/api/monitoring/rules/${MONITORING_RULES.hallworthOnHold}/enabled`, {isEnabled: true})).body.isEnabled).toBe(true);
    expect((await call('POST', '/api/monitoring/rules', {event: 'Nonsense'})).status).toBe(400);
    expect((await call('GET', '/api/jobs')).body.map((j: {name: string}) => j.name)).toEqual(['operational-purge', 'patient-reminders']);
    expect((await call('PUT', '/api/auth/security-settings', {requireTwoFactorForAdmins: true})).status).toBe(409);
    expect((await call('GET', '/api/auth/security-settings')).body.adminsWithoutTwoFactorUsernames).toEqual(['admin']);
  });
});
