import {call, startLightDemo} from '../../test/lightDemo';
import {ADMIN, CARROW, HALLWORTH, LAB, STAFF} from '../accounts';
import {PATHWAYS, PATIENTS, REFERRALS, TEAMS, TEMPLATES} from '../ids';
import {LAB_IDS, LAB_TESTS, SPECIMEN_TYPES, RESULTS} from '../slices/lab';
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

  it('keeps every sample in one database, with its source: the lab sees the hospital’s, a clinic only its own (MC14)', async () => {
    startLightDemo(LAB);
    type Row = {source: string; hospitalName: string | null; requestingTeamName: string | null; labTestName: string | null; pathwayId: string | null};
    const search = async (body: object) => (await call('POST', '/api/specimens/search', {view: 'All', pageSize: 100, ...body})).body.items as Row[];
    const all = await search({});
    const hospital = all.filter((s) => s.source === 'Hospital');
    expect(hospital.length).toBeGreaterThan(0);
    expect(hospital.every((s) => s.hospitalName && s.pathwayId === null && s.requestingTeamName === null)).toBe(true);
    expect(all.filter((s) => s.source === 'Clinic').every((s) => s.requestingTeamName && s.pathwayId)).toBe(true);
    expect(await search({source: 'Hospital'})).toHaveLength(hospital.length);
    expect((await search({teamId: TEAMS.dermatology})).every((s) => s.requestingTeamName === 'Dermatology')).toBe(true);
    expect((await search({labTestId: LAB_TESTS.fullBloodCount})).every((s) => s.labTestName === 'Full blood count')).toBe(true);
    expect((await search({view: 'ToReceive'})).length).toBeGreaterThan(0);
    expect((await search({due: 'Overdue'})).length).toBeGreaterThan(0);

    startLightDemo(STAFF);
    const clinic = await search({});
    expect(clinic.length).toBeGreaterThan(0);
    expect(clinic.every((s) => s.source === 'Clinic' && s.requestingTeamName === 'Dermatology')).toBe(true);
    const hospitalSample = db().specimens.find((s) => s.source === 'Hospital')!;
    expect((await call('GET', `/api/specimens/${hospitalSample.id}`)).status).toBe(404);
  });

  it('records a sample at the current stage, sends it, and lets only the lab receive it', async () => {
    startLightDemo(STAFF);
    const pathway = (await call('GET', pathwayPath(PATHWAYS.histologyToFinish))).body;
    const stage = current(pathway);
    expect(stage).toMatchObject({name: 'Histology', canRecordSpecimen: true});
    const body = {stageId: stage.id, specimenTypeId: SPECIMEN_TYPES.skinBiopsy, requestingUserId: STAFF.id};
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

  it('tells the requesting clinician when the lab rejects a sample', async () => {
    startLightDemo(LAB);
    const specimen = (await call('GET', `/api/specimens/${LAB_IDS.specimenAtLab}`)).body;
    expect((await call('POST', `/api/specimens/${specimen.id}/rejected`, {reason: 'Other', expectedVersion: specimen.version})).status).toBe(400);
    const rejected = await call('POST', `/api/specimens/${specimen.id}/rejected`, {reason: 'Leaked', expectedVersion: specimen.version});
    expect(rejected.body).toMatchObject({status: 'RejectedByLab', isOpen: false, rejectionReason: 'Leaked'});
    expect(db().notifications.find((n) => n.userId === STAFF.id && n.subjectId === specimen.id && n.kind === 'SpecimenRejectedByLab')).toMatchObject({
      title: 'Sample rejected',
      message: 'The lab rejected a sample (Blood sample).',
    });
  });

  it('holds Callum’s Histology result stage until his result is signed off', async () => {
    startLightDemo(STAFF);
    expect((await call('GET', '/api/results/count')).body).toEqual({awaitingMine: 1});
    // His case's report finished the lab's Histology stage (MC16); the Histology result is Dermatology's again.
    const review = current((await call('GET', pathwayPath(PATHWAYS.backFromLab))).body);
    expect(review.name).toBe('Histology result');
    const held = await call('POST', `${pathwayPath(PATHWAYS.backFromLab)}/stages/${review.id}/finish`, {
      withoutVisit: true,
      reason: 'Reviewed by the team',
      outcome: 'Benign',
    });
    expect(held.status).toBe(409);
    expect(held.body.title).toMatch(/^This stage waits for results: it can't be finished while a result awaits sign-off/);

    const result = (await call('GET', `/api/results/${LAB_IDS.resultToSignOff}`)).body;
    expect(result).toMatchObject({status: 'AwaitingSignOff', responsibleUsername: STAFF.username, canSignOff: true, action: null});
    const seen = (await call('POST', `/api/results/${result.id}/seen`, {expectedVersion: result.version})).body;
    expect((await call('POST', `/api/results/${result.id}/sign-off`, {action: 'Other', expectedVersion: seen.version})).status).toBe(400);
    const signed = await call('POST', `/api/results/${result.id}/sign-off`, {action: 'PatientInformed', expectedVersion: seen.version});
    expect(signed.body).toMatchObject({status: 'SignedOff', signedOffByUsername: STAFF.username, canSignOff: false});
    expect((await call('GET', '/api/results/count')).body).toEqual({awaitingMine: 0});
    const done = await call('POST', `${pathwayPath(PATHWAYS.backFromLab)}/stages/${review.id}/finish`, {
      withoutVisit: true,
      reason: 'Reviewed by the team',
      outcome: 'Benign',
    });
    expect(done.status).toBe(200);
  });

  it('holds the last stage while a sample is open or a result awaits, and nothing of the lab’s holds Harriet’s referral', () => {
    const {state} = startLightDemo(ADMIN);
    const ilona = state.pathways.find((p) => p.id === PATHWAYS.followUpBooked)!;
    const last = ilona.stages.at(-1)!;
    expect(labGate(ilona, last, true)).toMatch(/^The pathway's last stage can't be finished while 2 samples are still with the lab: MA\d{2}-\d{6} \(Blood sample, received\)/);
    expect(labGate(ilona, ilona.stages[2], false)).toBeNull();
    const daniyal = state.pathways.find((p) => p.id === PATHWAYS.notStarted)!;
    expect(labGate(daniyal, daniyal.stages.at(-1)!, true)).toMatch(/^The pathway's last stage can't be finished while a result awaits sign-off/);
    const harriet = state.pathways.find((p) => p.id === PATHWAYS.referredToHospital)!;
    expect(labGate(harriet, harriet.stages.at(-1)!, true)).toBeNull();
  });

  it('lists results by view, and tells the responsible clinician when someone else signs one off', async () => {
    startLightDemo(CARROW);
    expect((await call('GET', '/api/results/search?View=AwaitingMine')).body.items.map((r: {id: string}) => r.id)).toEqual([RESULTS.daniyalExternal]);
    startLightDemo(ADMIN);
    expect((await call('GET', '/api/results/search?View=AwaitingMine')).body.totalCount).toBe(0);
    expect((await call('GET', '/api/results/search?View=Awaiting')).body.totalCount).toBe(2);
    expect((await call('GET', '/api/results/search?View=SignedOff')).body.totalCount).toBe(2);
    startLightDemo(HALLWORTH);
    const callum = (await call('GET', `/api/results/${RESULTS.callumBiopsy}`)).body;
    await call('POST', `/api/results/${callum.id}/sign-off`, {action: 'ForMdtDiscussion', expectedVersion: callum.version});
    expect(db().notifications.find((n) => n.userId === STAFF.id && n.kind === 'ResultSignedOffByOther')?.message).toBe(
      "t.hallworth signed off a result you're responsible for.",
    );
  });

  it('keeps the sample setup lists for admins', async () => {
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
    expect((await call('POST', '/api/specimen-types', {name: 'Nail clipping 2', expectedTurnaroundDays: 7, snomedCode: 'abc'})).status).toBe(400);
    expect((await call('GET', '/api/laboratories')).status).toBe(501);
  });

  it('keeps the lab test catalogue for admins, checking its times and steps (MC13)', async () => {
    const test = {name: 'Nail clipping for fungi', kind: 'Microbiology', targetDays: 14, limitDays: 21, atRiskDays: 3, steps: [{name: 'Culture', kind: 'Standard'}]};
    startLightDemo(STAFF);
    expect((await call('POST', '/api/lab-tests', test)).status).toBe(403);
    startLightDemo(ADMIN);
    expect((await call('GET', '/api/lab-tests')).body).toHaveLength(3);
    expect((await call('GET', '/api/lab-tests?includeArchived=true')).body).toHaveLength(4);
    expect((await call('POST', '/api/lab-tests', {...test, name: 'skin histology'})).status).toBe(409);
    expect((await call('POST', '/api/lab-tests', {...test, limitDays: 7})).body.errors).toHaveProperty('limitDays');
    expect((await call('POST', '/api/lab-tests', {...test, atRiskDays: 14})).body.errors).toHaveProperty('atRiskDays');
    expect((await call('POST', '/api/lab-tests', {...test, steps: []})).body.errors).toHaveProperty('steps');
    expect((await call('POST', '/api/lab-tests', {...test, kind: 'Astrology'})).body.errors).toHaveProperty('kind');
    const added = (await call('POST', '/api/lab-tests', test)).body;
    expect(added).toMatchObject({name: 'Nail clipping for fungi', snomedCode: null, canBeDeleted: true, steps: [{name: 'Culture', kind: 'Standard', isOptional: false}]});
    const histology = (await call('PUT', `/api/lab-tests/${LAB_TESTS.skinHistology}`, {...test, name: 'Skin histology', kind: 'Histology', limitDays: 14})).body;
    expect(histology).toMatchObject({limitDays: 14, steps: [{name: 'Culture'}]});
    expect((await call('POST', `/api/lab-tests/${LAB_TESTS.skinHistology}/archive`)).body).toMatchObject({isArchived: true});
    expect((await call('DELETE', `/api/lab-tests/${LAB_TESTS.liverFunction}`)).status).toBe(204);
  });

  it('registers, edits and withdraws a referral, showing a possible duplicate first', async () => {
    startLightDemo(STAFF);
    const details = {
      patientId: PATIENTS.aishaKhoury,
      teamId: TEAMS.dermatology,
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

    const pathway = (await call('POST', `/api/referrals/${REFERRALS.dermatologyToTriage}/accept`, {templateId: TEMPLATES.suspectedSkinCancer, priority: 'Urgent', expectedVersion: 1})).body;
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
