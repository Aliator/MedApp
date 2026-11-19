import {call, startLightDemo} from '../../test/lightDemo';
import {addOtherClinic, OTHER_CLINIC} from '../../test/otherClinic';
import {HALLWORTH, LAB, STAFF} from '../accounts';
import {PATHWAYS, PATIENTS, TEAMS} from '../ids';
import {CONSULTS} from '../slices/consults';
import {db} from '../store';
import {auditEntries} from './adminLogs';
import {canSeePathway, grantStageOwner, scopeOf} from './teamWalls';

const PATHWAY = '/api/pathways/patient-pathways';
const asLab = () => (db().user = LAB.username);
const asStaff = () => (db().user = STAFF.username);
const ask = (pathwayId: string, askedTeamId: string, question = 'Synthetic question.') =>
  call('POST', '/api/consults', {pathwayId, askedTeamId, question});

describe('consults in the light demo (MC25)', () => {
  it('lets the lab read a pathway only while its consult is open: it replies, and Dermatology closes it', async () => {
    startLightDemo(LAB);
    // The seeded consult: the lab reads Felix Moreton's pathway, read only, though no sample has gone to it.
    const felix = await call('GET', `${PATHWAY}/${PATHWAYS.branchToChoose}`);
    expect(felix.status).toBe(200);
    expect(felix.body.readOnlyReason).toMatch(/asked a consult/);
    expect(felix.body.canAddNotes).toBe(false);
    const incoming = (await call('GET', '/api/consults/incoming')).body;
    expect(incoming.map((c: {id: string}) => c.id)).toEqual([CONSULTS.felixToLab]);
    expect(incoming[0]).toMatchObject({askingTeamName: 'Dermatology', askedTeamName: 'Lab', canReply: true, canClose: false, canWithdraw: false});
    expect(db().activityLog.some((e) => e.action === 'PatientListed' && e.patientId === PATIENTS.felixMoreton)).toBe(true);

    // The asked team can't close or withdraw; it replies.
    expect((await call('POST', `/api/consults/${CONSULTS.felixToLab}/close`, {expectedVersion: 1})).status).toBe(409);
    expect((await call('POST', `/api/consults/${CONSULTS.felixToLab}/reply`, {reply: ' ', expectedVersion: 1})).status).toBe(400);
    const replied = await call('POST', `/api/consults/${CONSULTS.felixToLab}/reply`, {reply: 'Synthetic reply.', expectedVersion: 1});
    expect(replied.body).toMatchObject({status: 'Replied', reply: 'Synthetic reply.', repliedByUsername: LAB.username, canReply: false});
    expect((await call('POST', `/api/consults/${CONSULTS.felixToLab}/reply`, {reply: 'Again.', expectedVersion: 2})).status).toBe(409);
    // Dermatology is told: a fixed title, no reply, no patient.
    const told = db().notifications.filter((n) => n.kind === 'ConsultReplied');
    expect(told.map((n) => n.userId)).toContain(STAFF.id);
    expect(told[0]).toMatchObject({title: 'Consult replied', message: `${LAB.username} from Lab replied to your team’s consult.`, pathwayId: PATHWAYS.branchToChoose});

    // Still open until Dermatology closes it (withdrawing a replied one is refused).
    asStaff();
    const onPathway = (await call('GET', `/api/consults/pathways/${PATHWAYS.branchToChoose}`)).body;
    expect(onPathway.canRequest).toBe(true);
    expect(onPathway.consults[0]).toMatchObject({status: 'Replied', canClose: true, canWithdraw: false});
    expect((await call('POST', `/api/consults/${CONSULTS.felixToLab}/withdraw`, {expectedVersion: 2})).status).toBe(409);
    expect((await call('POST', `/api/consults/${CONSULTS.felixToLab}/close`, {expectedVersion: 1})).status).toBe(409);
    const closed = await call('POST', `/api/consults/${CONSULTS.felixToLab}/close`, {expectedVersion: 2});
    expect(closed.body).toMatchObject({status: 'Closed', closedByUsername: STAFF.username});

    // Closed: the lab can no longer read the pathway or the consult.
    asLab();
    expect((await call('GET', `${PATHWAY}/${PATHWAYS.branchToChoose}`)).status).toBe(404);
    expect((await call('GET', `/api/consults/pathways/${PATHWAYS.branchToChoose}`)).status).toBe(404);
    expect((await call('GET', '/api/consults/incoming')).body).toEqual([]);

    // The audit log names the teams, never the question or the reply.
    const audit = auditEntries().filter((e) => e.action?.startsWith('Consult'));
    expect(audit.map((e) => e.action).sort()).toEqual(['ConsultClosed', 'ConsultReplied', 'ConsultRequested']);
    for (const e of audit) {
      expect(e.details).not.toMatch(/Synthetic|biopsy/);
      expect(JSON.parse(e.details!)).toMatchObject({askingTeam: 'Dermatology', askedTeam: 'Lab'});
    }
  });

  it('asks only from the pathway’s own team, of another team in use, once at a time; withdrawing ends the grant', async () => {
    startLightDemo(STAFF);
    expect((await ask(PATHWAYS.unassigned, TEAMS.dermatology)).status).toBe(400);
    expect((await ask(PATHWAYS.unassigned, TEAMS.lab, 'x'.repeat(2001))).status).toBe(400);
    const lab = scopeOf(LAB);
    expect(canSeePathway({id: PATHWAYS.unassigned}, lab)).toBe(false);
    const asked = await ask(PATHWAYS.unassigned, TEAMS.lab);
    expect(asked.status).toBe(201);
    expect(asked.body).toMatchObject({status: 'Open', canWithdraw: true, canClose: false});
    expect(canSeePathway({id: PATHWAYS.unassigned}, lab)).toBe(true);
    expect((await ask(PATHWAYS.unassigned, TEAMS.lab)).status).toBe(409);
    const notice = db().notifications.find((n) => n.kind === 'ConsultRequested' && n.pathwayId === PATHWAYS.unassigned);
    expect(notice).toMatchObject({userId: LAB.id, title: 'Consult requested', message: `${STAFF.username} from Dermatology asked your team for a consult.`});

    // The lab can't ask about Dermatology's pathway.
    asLab();
    const own = await call('GET', `/api/consults/pathways/${PATHWAYS.unassigned}`);
    expect(own.body.canRequest).toBe(false);
    expect((await ask(PATHWAYS.unassigned, TEAMS.lab)).status).toBe(409);

    asStaff();
    const withdrawn = await call('POST', `/api/consults/${asked.body.id}/withdraw`, {expectedVersion: 1});
    expect(withdrawn.body).toMatchObject({status: 'Withdrawn', closedByUsername: STAFF.username});
    expect(canSeePathway({id: PATHWAYS.unassigned}, lab)).toBe(false);
    expect(auditEntries().some((e) => e.action === 'ConsultWithdrawn' && e.patientPathwayId === PATHWAYS.unassigned)).toBe(true);
  });

  it('refuses asking and replying on a restricted record, but lets the asking team withdraw or close', async () => {
    startLightDemo(STAFF);
    const asked = (await ask(PATHWAYS.unassigned, TEAMS.lab)).body;
    for (const p of db().patients) if (p.id === PATIENTS.tobiasLund || p.id === PATIENTS.felixMoreton) p.isRestricted = true;
    expect((await ask(PATHWAYS.branchToChoose, TEAMS.lab)).status).toBe(409);
    expect((await call('GET', `/api/consults/pathways/${PATHWAYS.unassigned}`)).body.canRequest).toBe(false);

    asLab();
    const felix = (await call('GET', '/api/consults/incoming')).body.find((c: {id: string}) => c.id === CONSULTS.felixToLab);
    expect(felix.canReply).toBe(false);
    expect((await call('POST', `/api/consults/${CONSULTS.felixToLab}/reply`, {reply: 'Synthetic reply.', expectedVersion: 1})).status).toBe(409);

    asStaff();
    expect((await call('POST', `/api/consults/${asked.id}/withdraw`, {expectedVersion: 1})).status).toBe(200);
    db().consults.find((c) => c.id === CONSULTS.felixToLab)!.status = 'Replied';
    expect((await call('POST', `/api/consults/${CONSULTS.felixToLab}/close`, {expectedVersion: 1})).status).toBe(200);
  });

  it('works for any team: Dermatology asks another clinic, which sees it in its incoming list', async () => {
    startLightDemo(STAFF);
    addOtherClinic(db());
    // Tom Hallworth is in both clinics: only the other clinic's consults are incoming for him there.
    db().memberships[HALLWORTH.id] = [{teamId: OTHER_CLINIC.teamId, role: 'Member'}];
    const asked = await ask(PATHWAYS.unassigned, OTHER_CLINIC.teamId);
    expect(asked.status).toBe(201);
    expect(db().notifications.some((n) => n.kind === 'ConsultRequested' && n.userId === HALLWORTH.id)).toBe(true);

    db().user = HALLWORTH.username;
    expect((await call('GET', `${PATHWAY}/${PATHWAYS.unassigned}`)).status).toBe(200);
    const incoming = (await call('GET', `/api/consults/incoming?teamId=${OTHER_CLINIC.teamId}`)).body;
    expect(incoming.map((c: {id: string}) => c.id)).toEqual([asked.body.id]);
    expect((await call('GET', `/api/consults/incoming?teamId=${TEAMS.lab}`)).status).toBe(400);
    // The other clinic's own pathway is behind Dermatology's wall: no asking about it from there.
    asStaff();
    expect((await ask(OTHER_CLINIC.pathwayId, TEAMS.lab)).status).toBe(404);
  });

  it('keeps the lab’s stage-owner grant when a pathway it was consulted on is passed to it', () => {
    startLightDemo(STAFF);
    const felix = db().pathways.find((p) => p.id === PATHWAYS.branchToChoose)!;
    grantStageOwner(felix, TEAMS.lab);
    expect(db().pathwayAccess.filter((a) => a.pathwayId === felix.id && a.teamId === TEAMS.lab).map((a) => a.reason).sort()).toEqual([
      'Consult',
      'StageOwner',
    ]);
  });
});
