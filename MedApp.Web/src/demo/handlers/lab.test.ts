import {call, startLightDemo} from '../../test/lightDemo';
import {ADMIN, STAFF} from '../accounts';
import {PATHWAYS, PATIENTS, REFERRALS, SPECIALTIES, TEMPLATES} from '../ids';
import {LAB_IDS, SPECIMEN_TYPES, LABORATORIES, RESULTS} from '../slices/lab';
import {db} from '../store';
import {daysFrom, today} from '../time';
import {labGate} from './lab';

const pathwayPath = (id: string) => `/api/pathways/patient-pathways/${id}`;
const current = (pathway: {stages: {isCurrent: boolean; id: string; name: string}[]}) => pathway.stages.find((s) => s.isCurrent)!;

describe('the light demo’s lab, results and referrals', () => {
  it('has work in each of the Lab page’s views', async () => {
    startLightDemo(ADMIN);
    for (const view of ['ToSend', 'Expected', 'InLab', 'Overdue', 'RejectedAndLost', 'Reported']) {
      const page = (await call('GET', `/api/specimens/search?View=${view}`)).body;
      expect(page.totalCount, view).toBeGreaterThan(0);
    }
    const toSend = (await call('GET', '/api/specimens/search?View=ToSend')).body.items;
    expect(toSend.map((s: {id: string}) => s.id)).toContain(LAB_IDS.specimenToSend);
    const found = await call('POST', '/api/specimens/find', {number: toSend[0].number.toLowerCase()});
    expect(found.body).toMatchObject({id: toSend[0].id, label: {qrPath: null}});
  });

  it('records a specimen at the current stage, sends it, and lets only the lab receive it', async () => {
    startLightDemo(STAFF);
    const pathway = (await call('GET', pathwayPath(PATHWAYS.histologyToFinish))).body;
    const stage = current(pathway);
    expect(stage).toMatchObject({name: 'Histology (lab)', canRecordSpecimen: true});
    const body = {stageId: stage.id, specimenTypeId: SPECIMEN_TYPES.skinBiopsy, laboratoryId: LABORATORIES.valley, requestingUserId: STAFF.id};
    expect((await call('POST', `/api/pathways/${PATHWAYS.histologyToFinish}/specimens`, {...body, specimenTypeId: SPECIMEN_TYPES.skinSwab})).status).toBe(400);
    const taken = await call('POST', `/api/pathways/${PATHWAYS.histologyToFinish}/specimens`, body);
    expect(taken.status).toBe(200);
    expect(taken.body).toMatchObject({status: 'Taken', canMarkSent: true, canMarkReceived: false, canMarkEnteredInError: true});
    expect(taken.body.number).toMatch(/^MA\d{2}-\d{6}$/);
    const id = taken.body.id;
    expect((await call('POST', `/api/specimens/${id}/sent`, {expectedVersion: 0})).status).toBe(409);
    const sent = await call('POST', `/api/specimens/${id}/sent`, {expectedVersion: 1, reference: 'BATCH-1'});
    expect(sent.body).toMatchObject({status: 'Sent', courierReference: 'BATCH-1', sentByUsername: STAFF.username});
    expect((await call('POST', `/api/specimens/${id}/received`, {expectedVersion: 2})).status).toBe(403);
    expect((await call('POST', `/api/specimens/${id}/sent`, {expectedVersion: 2})).status).toBe(409);
  });

  it('tells the requesting clinician when the lab rejects a specimen', async () => {
    startLightDemo(ADMIN);
    const specimen = (await call('GET', `/api/specimens/${LAB_IDS.specimenAtLab}`)).body;
    expect((await call('POST', `/api/specimens/${specimen.id}/rejected`, {reason: 'Other', expectedVersion: specimen.version})).status).toBe(400);
    const rejected = await call('POST', `/api/specimens/${specimen.id}/rejected`, {reason: 'Leaked', expectedVersion: specimen.version});
    expect(rejected.body).toMatchObject({status: 'RejectedByLab', isOpen: false, rejectionReason: 'Leaked'});
    expect(db().notifications.find((n) => n.userId === STAFF.id && n.subjectId === specimen.id && n.kind === 'SpecimenRejectedByLab')).toMatchObject({
      title: 'Specimen rejected',
      message: 'The lab rejected a Blood sample specimen.',
    });
  });

  it('holds Priya’s results stage until her result is signed off, but not her Histology stage', async () => {
    startLightDemo(STAFF);
    expect((await call('GET', '/api/results/count')).body).toEqual({awaitingMine: 1});
    const histology = current((await call('GET', pathwayPath(PATHWAYS.histologyToFinish))).body);
    const finished = await call('POST', `${pathwayPath(PATHWAYS.histologyToFinish)}/stages/${histology.id}/finish`, {});
    expect(finished.status).toBe(200);
    const review = current(finished.body);
    expect(review.name).toBe('Results and team review');
    const held = await call('POST', `${pathwayPath(PATHWAYS.histologyToFinish)}/stages/${review.id}/finish`, {withoutVisit: true, reason: 'Reviewed by the team'});
    expect(held.status).toBe(409);
    expect(held.body.title).toMatch(/^This stage waits for results: it can't be finished while a result awaits sign-off/);

    const result = (await call('GET', `/api/results/${LAB_IDS.priyaResult}`)).body;
    expect(result).toMatchObject({status: 'AwaitingSignOff', responsibleUsername: STAFF.username, canSignOff: true, action: null});
    const seen = (await call('POST', `/api/results/${result.id}/seen`, {expectedVersion: result.version})).body;
    expect((await call('POST', `/api/results/${result.id}/sign-off`, {action: 'Other', expectedVersion: seen.version})).status).toBe(400);
    const signed = await call('POST', `/api/results/${result.id}/sign-off`, {action: 'PatientInformed', expectedVersion: seen.version});
    expect(signed.body).toMatchObject({status: 'SignedOff', signedOffByUsername: STAFF.username, canSignOff: false});
    expect((await call('GET', '/api/results/count')).body).toEqual({awaitingMine: 0});
    const done = await call('POST', `${pathwayPath(PATHWAYS.histologyToFinish)}/stages/${review.id}/finish`, {withoutVisit: true, reason: 'Reviewed by the team'});
    expect(done.status).toBe(200);
  });

  it('holds the last stage while a specimen is open or a result awaits, and nothing of the lab’s holds Harriet’s Treatment', () => {
    const {state} = startLightDemo(ADMIN);
    const ilona = state.pathways.find((p) => p.id === PATHWAYS.followUpBooked)!;
    const last = ilona.stages.at(-1)!;
    expect(labGate(ilona, last, true)).toMatch(/^The pathway's last stage can't be finished while 2 specimens are still with the lab: MA\d{2}-\d{6} \(Blood sample, received\)/);
    expect(labGate(ilona, ilona.stages[2], false)).toBeNull();
    const daniyal = state.pathways.find((p) => p.id === PATHWAYS.notStarted)!;
    expect(labGate(daniyal, daniyal.stages.at(-1)!, true)).toMatch(/^The pathway's last stage can't be finished while a result awaits sign-off/);
    const harriet = state.pathways.find((p) => p.id === PATHWAYS.consentToRecord)!;
    expect(labGate(harriet, harriet.stages.find((s) => s.name === 'Treatment')!, false)).toBeNull();
    expect(labGate(harriet, harriet.stages.at(-1)!, true)).toBeNull();
  });

  it('lists results by view, and tells the responsible clinician when someone else signs one off', async () => {
    startLightDemo(ADMIN);
    expect((await call('GET', '/api/results/search?View=AwaitingMine')).body.items.map((r: {id: string}) => r.id)).toEqual([RESULTS.daniyalExternal]);
    expect((await call('GET', '/api/results/search?View=Awaiting')).body.totalCount).toBe(2);
    expect((await call('GET', '/api/results/search?View=SignedOff')).body.totalCount).toBe(2);
    const priya = (await call('GET', `/api/results/${RESULTS.priyaBiopsy}`)).body;
    await call('POST', `/api/results/${priya.id}/sign-off`, {action: 'ForMdtDiscussion', expectedVersion: priya.version});
    expect(db().notifications.find((n) => n.userId === STAFF.id && n.kind === 'ResultSignedOffByOther')?.message).toBe(
      "admin signed off a result you're responsible for.",
    );
  });

  it('keeps the specimen setup lists for admins', async () => {
    startLightDemo(STAFF);
    expect((await call('POST', '/api/specimen-types', {name: 'Nail clipping', expectedTurnaroundDays: 14})).status).toBe(403);
    startLightDemo(ADMIN);
    expect((await call('GET', '/api/specimen-types')).body).toHaveLength(3);
    expect((await call('GET', '/api/specimen-types?includeArchived=true')).body).toHaveLength(4);
    expect((await call('POST', '/api/specimen-types', {name: 'blood sample', expectedTurnaroundDays: 3})).status).toBe(409);
    expect((await call('POST', '/api/specimen-types', {name: 'Nail clipping', expectedTurnaroundDays: 91})).status).toBe(400);
    expect((await call('POST', '/api/specimen-types', {name: 'Nail clipping', expectedTurnaroundDays: 21})).body).toMatchObject({canBeDeleted: true});
    expect((await call('DELETE', `/api/specimen-types/${SPECIMEN_TYPES.bloodSample}`)).status).toBe(409);
    expect((await call('DELETE', `/api/specimen-types/${SPECIMEN_TYPES.skinSwab}`)).status).toBe(204);
    expect((await call('POST', `/api/laboratories/${LABORATORIES.centre}/archive`)).body).toMatchObject({isArchived: true, canBeDeleted: false});
  });

  it('registers, edits and withdraws a referral, showing a possible duplicate first', async () => {
    startLightDemo(STAFF);
    const details = {
      patientId: PATIENTS.aishaKhoury,
      specialtyId: SPECIALTIES.dermatology,
      receivedOn: today(),
      referrerPractice: 'Example Lane Surgery (synthetic)',
      requestedPriority: 'High',
      reason: 'Second letter about the rash.',
    };
    const duplicate = await call('POST', '/api/referrals', details);
    expect(duplicate.status).toBe(400);
    expect(duplicate.body.errors.confirmPossibleDuplicate[0]).toMatch(/^This patient already has an open Dermatology referral: received/);
    const registered = (await call('POST', '/api/referrals', {...details, confirmPossibleDuplicate: true})).body;
    expect(registered).toMatchObject({status: 'AwaitingTriage', canEdit: true, canClose: true, registeredByUsername: STAFF.username});
    const edited = (await call('PUT', `/api/referrals/${registered.id}`, {...details, requestedPriority: 'Urgent', expectedVersion: 1})).body;
    const event = (referral: {history: {action: string}[]}, action: string) => referral.history.find((e) => e.action === action);
    expect(event(edited, 'DetailsEdited')).toMatchObject({changedFields: ['requestedPriority']});
    expect((await call('POST', `/api/referrals/${registered.id}/withdraw`, {expectedVersion: edited.version})).status).toBe(400);
    const withdrawn = (await call('POST', `/api/referrals/${registered.id}/withdraw`, {detail: 'Sent twice', expectedVersion: edited.version})).body;
    expect(withdrawn).toMatchObject({status: 'Withdrawn', isOpen: false, canEdit: false, clock: {state: 'Stopped', stopReason: 'Withdrawn'}});
    expect(event(withdrawn, 'Withdrawn')).toMatchObject({toStatus: 'Withdrawn'});
    expect(JSON.stringify(withdrawn.history)).not.toContain('Sent twice');
  });

  it('asks the referrer for more information, and records it came', async () => {
    startLightDemo(STAFF);
    const asked = (await call('POST', `/api/referrals/${REFERRALS.aishaDermatology}/ask-for-information`, {expectedVersion: 1})).body;
    expect(asked).toMatchObject({status: 'InformationRequested', isOpen: true, canTriage: true});
    expect((await call('POST', `/api/referrals/${REFERRALS.aishaDermatology}/ask-for-information`, {expectedVersion: 2})).status).toBe(409);
    const received = (await call('POST', `/api/referrals/${REFERRALS.aishaDermatology}/information-received`, {expectedVersion: 2})).body;
    expect(received.status).toBe('AwaitingTriage');
  });

  it('pauses an accepted referral’s clock, moving its pathway’s target date, and resumes it', async () => {
    startLightDemo(STAFF);
    const ellis = (await call('GET', `/api/referrals/${REFERRALS.ellisAccepted}`)).body;
    expect(ellis.clock).toMatchObject({state: 'Stopped', stopReason: 'StopStageFinished'});
    expect((await call('POST', `/api/referrals/${REFERRALS.ellisAccepted}/clock/pause`, {reason: 'PatientChoseToWait', reviewOn: daysFrom(14), expectedVersion: ellis.version})).status).toBe(409);

    const pathway = (await call('POST', `/api/referrals/${REFERRALS.dermatologyToTriage}/accept`, {templateId: TEMPLATES.skinLesion, priority: 'Urgent', expectedVersion: 1})).body;
    const accepted = (await call('GET', `/api/referrals/${REFERRALS.dermatologyToTriage}`)).body;
    expect(accepted).toMatchObject({clock: {state: 'Running', targetDays: 14}, canPauseClock: true});
    const pause = `/api/referrals/${REFERRALS.dermatologyToTriage}/clock/pause`;
    expect((await call('POST', pause, {reason: 'PatientChoseToWait', reviewOn: today(), expectedVersion: accepted.version})).status).toBe(400);
    const paused = (await call('POST', pause, {reason: 'PatientChoseToWait', reviewOn: daysFrom(21), expectedVersion: accepted.version})).body;
    expect(paused).toMatchObject({clock: {state: 'Paused', pauseReviewOn: daysFrom(21)}, canPauseClock: false, canResumeClock: true});
    expect(db().pathways.find((p) => p.id === pathway.id)?.dueDate).toBe(daysFrom(21));
    const resumed = (await call('POST', `/api/referrals/${REFERRALS.dermatologyToTriage}/clock/resume`, {expectedVersion: paused.version})).body;
    expect(resumed.clock).toMatchObject({state: 'Running', pausedDays: 0});
    expect(db().pathways.find((p) => p.id === pathway.id)?.dueDate).toBe(resumed.clock.targetOn);
    const paging = (await call('GET', '/api/referrals/search?Paused=true')).body;
    expect(paging.totalCount).toBe(0);
  });
});
