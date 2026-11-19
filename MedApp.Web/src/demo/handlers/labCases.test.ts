import {call, startLightDemo} from '../../test/lightDemo';
import {ADMIN, LAB, STAFF} from '../accounts';
import {HOSPITAL_REQUESTS} from '../slices/hospitals';
import {LAB_TESTS, SPECIMENS} from '../slices/lab';
import {LAB_CASES} from '../slices/labCases';
import {db} from '../store';
import {auditEntries} from './adminLogs';

const caseOf = async (id: string) => (await call('GET', `/api/lab-cases/${id}`)).body;

describe('the light demo’s lab cases (MC15)', () => {
  it('lists the lab’s cases by tab, and a clinic only its own clinic’s', async () => {
    startLightDemo(LAB);
    const open = (await call('POST', '/api/lab-cases/search', {view: 'Open'})).body;
    // Five worked, and three lab stages' cases waiting for their samples (MC16: Priya's Gynaecology Histology, Aisha's two).
    expect(open.totalCount).toBe(8);
    expect(open.items.filter((c: {nextKind: string}) => c.nextKind === 'Receive')).toHaveLength(3);
    const toReport = (await call('POST', '/api/lab-cases/search', {view: 'ToReport'})).body;
    expect(toReport.items.map((c: {id: string}) => c.id).sort()).toEqual([LAB_CASES.ilonaBlood, LAB_CASES.priyaBiopsy].sort());
    expect(toReport.items.find((c: {id: string}) => c.id === LAB_CASES.ilonaBlood)).toMatchObject({nextKind: 'Report', source: 'Clinic', labTestName: 'Full blood count'});
    const hospital = (await call('POST', '/api/lab-cases/search', {view: 'All', source: 'Hospital'})).body;
    expect(hospital.totalCount).toBe(5);

    startLightDemo(STAFF);
    const clinic = (await call('POST', '/api/lab-cases/search', {view: 'All'})).body;
    expect(clinic.items.every((c: {source: string}) => c.source === 'Clinic')).toBe(true);
    expect((await call('GET', `/api/lab-cases/${LAB_CASES.wilfred}`)).status).toBe(404);
  });

  it('shows a case’s steps and what it needs next, and logs viewing it', async () => {
    startLightDemo(LAB);
    const wilfred = await caseOf(LAB_CASES.wilfred);
    expect(wilfred.steps.map((s: {name: string}) => s.name)).toEqual([
      'Received',
      'Accessioned',
      'Sample adequate?',
      'Cut-up',
      'Processing and embedding',
      'Sectioning and staining',
      'Microscopy and report',
    ]);
    expect(wilfred.next).toMatchObject({kind: 'Choice', choice: {choice: 'NeedsMoreWork', outcomes: ['NoMoreWork', 'ExtraTests', 'SecondOpinion']}});
    expect(wilfred).toMatchObject({canRecord: true, canAuthorise: false, source: 'Hospital', hospitalRequestId: HOSPITAL_REQUESTS.wilfredExcision});
    expect(db().activityLog.some((a) => a.action === 'LabCaseViewed' && a.details?.includes(LAB_CASES.wilfred))).toBe(true);

    expect((await caseOf(LAB_CASES.maya)).next).toMatchObject({kind: 'Step', name: 'Accessioned'});
    expect((await caseOf(LAB_CASES.ellis)).next).toMatchObject({kind: 'Step', name: 'Second opinion back'});
  });

  it('works a case through its steps and choices to the report, in order, from the version shown', async () => {
    startLightDemo(LAB);
    let c = await caseOf(LAB_CASES.maya);
    // Out of order, or from an older version: refused.
    expect((await call('POST', `/api/lab-cases/${c.id}/outcomes`, {choice: 'SampleAdequate', outcome: 'Adequate', expectedVersion: c.version})).status).toBe(409);
    expect((await call('POST', `/api/lab-cases/${c.id}/steps`, {fixedStep: 'Accessioned', expectedVersion: c.version - 1})).status).toBe(409);

    c = (await call('POST', `/api/lab-cases/${c.id}/steps`, {fixedStep: 'Accessioned', expectedVersion: c.version})).body;
    c = (await call('POST', `/api/lab-cases/${c.id}/outcomes`, {choice: 'SampleAdequate', outcome: 'Adequate', expectedVersion: c.version})).body;
    while (c.next.kind === 'Step')
      c = (await call('POST', `/api/lab-cases/${c.id}/steps`, {testStepId: c.next.testStepId, expectedVersion: c.version})).body;
    expect(c.next).toMatchObject({kind: 'Choice', name: 'Needs more work?'});

    // Extra tests add the test's extra stains, then ask again.
    c = (await call('POST', `/api/lab-cases/${c.id}/outcomes`, {choice: 'NeedsMoreWork', outcome: 'ExtraTests', expectedVersion: c.version})).body;
    expect(c.next).toMatchObject({kind: 'Step', name: 'Extra stains'});
    c = (await call('POST', `/api/lab-cases/${c.id}/steps`, {testStepId: c.next.testStepId, expectedVersion: c.version})).body;
    expect(c.next).toMatchObject({kind: 'Choice', name: 'Needs more work?'});
    c = (await call('POST', `/api/lab-cases/${c.id}/outcomes`, {choice: 'NeedsMoreWork', outcome: 'NoMoreWork', expectedVersion: c.version})).body;
    expect(c).toMatchObject({next: {kind: 'Report'}, canAuthorise: true});

    expect((await call('POST', `/api/lab-cases/${c.id}/report`, {results: ' ', expectedVersion: c.version})).status).toBe(400);
    c = (await call('POST', `/api/lab-cases/${c.id}/report`, {results: 'Synthetic results.', expectedVersion: c.version})).body;
    // A hospital case's report goes back to the hospital in the same action (MC20).
    expect(c).toMatchObject({status: 'AwaitingSurgeryDetails', reportText: 'Synthetic results.', reportedByUsername: LAB.username, resultId: null});
    expect(c.samples.every((s: {status: string}) => s.status === 'Reported')).toBe(true);
    expect((await call('GET', `/api/hospital-requests/${HOSPITAL_REQUESTS.mayaBiopsy}`)).body.labCaseStatus).toBe('AwaitingSurgeryDetails');
    // The audit log has its entries, with no results text.
    const entries = auditEntries().filter((e) => e.details?.includes(c.id));
    expect(entries.map((e) => e.action)).toEqual(expect.arrayContaining(['LabCaseOpened', 'LabStepRecorded', 'LabReportAuthorised']));
    expect(entries.some((e) => e.details?.includes('Synthetic results'))).toBe(false);
  });

  it('makes a clinic case’s report a result for the requesting clinician to sign off', async () => {
    startLightDemo(LAB);
    const c = await caseOf(LAB_CASES.ilonaBlood);
    const reported = (await call('POST', `/api/lab-cases/${c.id}/report`, {results: 'Synthetic blood results.', expectedVersion: c.version})).body;
    const result = db().results.find((r) => r.id === reported.resultId)!;
    expect(result).toMatchObject({status: 'AwaitingSignOff', reportText: 'Synthetic blood results.', labCaseId: c.id, specimenId: SPECIMENS.ilonaBlood});
    expect(db().notifications.some((n) => n.kind === 'ResultReady' && n.subjectId === result.id && !n.message?.includes('Synthetic'))).toBe(true);
  });

  it('closes an insufficient sample’s case with a repeat requested, and tells the clinic without the reason', async () => {
    startLightDemo(LAB);
    const sample = db().specimens.find((s) => s.id === SPECIMENS.ilonaBlood)!;
    const c = db().labCases.find((x) => x.id === LAB_CASES.ilonaBlood)!;
    // Back to before its adequacy was recorded, to record it insufficient.
    c.steps = c.steps.slice(0, 2);
    expect((await call('POST', `/api/lab-cases/${c.id}/outcomes`, {choice: 'SampleAdequate', outcome: 'Insufficient', expectedVersion: c.version})).status).toBe(400);
    const closed = (
      await call('POST', `/api/lab-cases/${c.id}/outcomes`, {choice: 'SampleAdequate', outcome: 'Insufficient', detail: 'Too little to test.', expectedVersion: c.version})
    ).body;
    expect(closed).toMatchObject({status: 'RepeatRequested', repeatDetail: 'Too little to test.', canRecord: false});
    expect(sample).toMatchObject({status: 'RejectedByLab', rejectionReason: 'InsufficientSample'});
    const told = db().notifications.find((n) => n.kind === 'RepeatSampleRequested' && n.subjectId === sample.id)!;
    expect(told.userId).toBe(STAFF.id);
    expect(told.message).not.toContain('Too little');
  });

  it('marks the latest step entered in error, so the case goes back to it', async () => {
    startLightDemo(LAB);
    let c = await caseOf(LAB_CASES.wilfred);
    const latest = c.steps.at(-1);
    expect(latest.canMarkEnteredInError).toBe(true);
    expect((await call('POST', `/api/lab-cases/${c.id}/steps/${c.steps[1].id}/entered-in-error`, {detail: 'x', expectedVersion: c.version})).status).toBe(409);
    c = (await call('POST', `/api/lab-cases/${c.id}/steps/${latest.id}/entered-in-error`, {detail: 'Recorded on the wrong case.', expectedVersion: c.version})).body;
    expect(c.steps.at(-1)).toMatchObject({isEnteredInError: true, errorDetail: 'Recorded on the wrong case.'});
    expect(c.next).toMatchObject({kind: 'Step', name: 'Microscopy and report'});
  });

  it('opens a case when the lab receives a clinic sample with a test, and by hand for one without', async () => {
    startLightDemo(LAB);
    const samuel = db().specimens.find((s) => s.id === SPECIMENS.samuelScraping)!;
    const received = (await call('POST', `/api/specimens/${samuel.id}/received`, {expectedVersion: samuel.version})).body;
    expect(received.labCaseId).toBeTruthy();
    expect(received.canOpenLabCase).toBe(false);
    const opened = await caseOf(received.labCaseId);
    expect(opened).toMatchObject({status: 'Open', labTestName: 'Skin scraping for fungi', next: {kind: 'Step', name: 'Accessioned'}});

    // Its case marked entered in error (the wrong test, say): the sample can open the right one by hand.
    const wrong = (await call('POST', `/api/lab-cases/${opened.id}/entered-in-error`, {detail: 'Wrong test.', expectedVersion: opened.version})).body;
    expect(wrong.status).toBe('EnteredInError');
    expect(samuel.labCaseId).toBeNull();
    expect((await call('POST', '/api/lab-cases', {specimenId: samuel.id, labTestId: '', expectedVersion: samuel.version})).status).toBe(400);
    const right = (await call('POST', '/api/lab-cases', {specimenId: samuel.id, labTestId: LAB_TESTS.skinHistology, expectedVersion: samuel.version})).body;
    expect(right).toMatchObject({status: 'Open', labTestName: 'Skin histology'});
    expect(samuel.labCaseId).toBe(right.id);
    expect((await call('POST', '/api/lab-cases', {specimenId: samuel.id, labTestId: LAB_TESTS.skinHistology, expectedVersion: samuel.version})).status).toBe(409);
  });

  it('opens a case with each hospital request', async () => {
    startLightDemo(LAB);
    const before = db().labCases.length;
    const request = db().hospitalRequests[0];
    const made = (
      await call('POST', '/api/hospital-requests', {
        hospitalId: request.hospitalId,
        patientId: request.patientId,
        hospitalPatientNumber: 'RG-1',
        surgeonName: 'Mr C. Test (synthetic)',
        priority: 'Routine',
        authoredOn: new Date(Date.now() - 3600_000).toISOString(),
        specimenTypeId: request.specimenTypeId,
        labTestId: request.labTestId,
      })
    ).body;
    expect(db().labCases.length).toBe(before + 1);
    expect((await caseOf(made.labCaseId)).number).toBe(made.labCaseNumber);
  });

  it('lets only the lab work a case, and only a pathologist authorise its report', async () => {
    startLightDemo(ADMIN);
    const asAdmin = await caseOf(LAB_CASES.ilonaBlood);
    expect(asAdmin).toMatchObject({canRecord: false, canAuthorise: false, readOnlyReason: 'Lab cases are worked by the lab team.'});
    expect((await call('POST', `/api/lab-cases/${asAdmin.id}/report`, {results: 'x', expectedVersion: asAdmin.version})).status).toBe(403);

    startLightDemo(STAFF);
    const asClinic = await caseOf(LAB_CASES.ilonaBlood);
    expect(asClinic.canRecord).toBe(false);
    expect((await call('POST', `/api/lab-cases/${asClinic.id}/report`, {results: 'x', expectedVersion: asClinic.version})).status).toBe(403);
  });

  it('seeds clocks on time, at risk and breached, the sooner deadline applying (MC19)', async () => {
    startLightDemo(LAB);
    const ids = async (view: string) => (await call('POST', '/api/lab-cases/search', {view})).body.items.map((c: {id: string}) => c.id);
    expect(await ids('AtRisk')).toEqual([LAB_CASES.ilonaBlood]);
    // Priya's biopsy reached the lab over a week ago, and its report (which finishes her Histology, MC16) isn't in yet.
    expect((await ids('Overdue')).sort()).toEqual([LAB_CASES.ellis, LAB_CASES.priyaBiopsy].sort());
    expect((await caseOf(LAB_CASES.wilfred)).clock).toMatchObject({setBy: 'Hospital', hospitalDueBasis: 'ReportNeededBy', state: 'OnTime', daysLeft: 3});
    expect((await caseOf(LAB_CASES.maya)).clock).toMatchObject({setBy: 'Lab', hospitalDueBasis: 'HospitalDefault', state: 'OnTime'});
    expect((await caseOf(LAB_CASES.callumBiopsy)).clock?.state).toMatch(/^Reported/);
  });

  it('sends a hospital case’s report back to its hospital when authorised, its clock stopped there (MC20)', async () => {
    startLightDemo(LAB);
    const request = db().hospitalRequests.find((r) => r.id === HOSPITAL_REQUESTS.mayaBiopsy)!;
    const c = db().labCases.find((x) => x.id === LAB_CASES.maya)!;
    // Every step done, for the report.
    c.steps.push(
      ...['Accessioned', 'Outcome'].map((kind, i) => ({
        id: `s${i}`,
        kind: kind as 'Accessioned' | 'Outcome',
        testStepId: null,
        choice: kind === 'Outcome' ? ('SampleAdequate' as const) : null,
        outcome: kind === 'Outcome' ? ('Adequate' as const) : null,
        detail: null,
        atUtc: c.receivedAtUtc!,
        byUserId: LAB.id,
        enteredInError: null,
      })),
    );
    let view = await caseOf(c.id);
    while (view.next.kind !== 'Report')
      view =
        view.next.kind === 'Step'
          ? (await call('POST', `/api/lab-cases/${c.id}/steps`, {testStepId: view.next.testStepId, expectedVersion: view.version})).body
          : (await call('POST', `/api/lab-cases/${c.id}/outcomes`, {choice: view.next.choice.choice, outcome: 'NoMoreWork', expectedVersion: view.version})).body;
    view = (await call('POST', `/api/lab-cases/${c.id}/report`, {results: 'Synthetic results.', expectedVersion: view.version})).body;
    expect(view.hospitalReport).toMatchObject({status: 'Final', letterKind: 'LabReport', hospitalId: request.hospitalId, addressedTo: request.surgeonName, sentByUsername: LAB.username});
    expect(view.hospitalReport.sentAtUtc).toBe(view.reportedAtUtc);
    expect(view.clock.state).toMatch(/^Reported/);
    expect(view).toMatchObject({status: 'AwaitingSurgeryDetails', canAddSurgeryDetails: true, canRecord: false});
    const awaiting = (await call('POST', '/api/lab-cases/search', {view: 'AwaitingSurgeryDetails'})).body.items.map((x: {id: string}) => x.id);
    expect(awaiting).toContain(c.id);
    expect(auditEntries().some((e) => e.action === 'LabReportSent' && e.details?.includes(c.id))).toBe(true);
  });

  it('adds the hospital’s surgery details as written, closing a case awaiting them, with no text in the audit log (MC20)', async () => {
    startLightDemo(LAB);
    let c = await caseOf(LAB_CASES.ellisBiopsy);
    expect(c).toMatchObject({status: 'AwaitingSurgeryDetails', canAddSurgeryDetails: true, hospitalReport: {letterKind: 'LabReport'}});
    const url = `/api/lab-cases/${c.id}/surgery-details`;
    expect((await call('POST', url, {expectedVersion: c.version})).status).toBe(400);
    expect((await call('POST', url, {noSurgeryExpected: true, expectedVersion: c.version})).status).toBe(400);
    expect((await call('POST', url, {procedure: 'x', expectedVersion: c.version - 1})).status).toBe(409);
    c = (
      await call('POST', url, {surgeryOn: '2026-11-10', procedure: 'Wide local excision (synthetic)', outcome: 'Completed (synthetic).', expectedVersion: c.version})
    ).body;
    expect(c).toMatchObject({status: 'Closed', canAddSurgeryDetails: false, closedByUsername: LAB.username});
    expect(c.surgeryDetails.at(-1)).toMatchObject({surgeryOn: '2026-11-10', procedure: 'Wide local excision (synthetic)', addedByUsername: LAB.username});
    expect((await call('POST', url, {procedure: 'Again', expectedVersion: c.version})).status).toBe(409);
    const entries = auditEntries().filter((e) => e.details?.includes(c.id));
    expect(entries.map((e) => e.action)).toEqual(expect.arrayContaining(['SurgeryDetailsAdded', 'LabCaseClosed']));
    expect(entries.some((e) => e.details?.includes('excision') || e.details?.includes('Completed'))).toBe(false);
  });

  it('keeps surgery details that arrive before the report, and refuses them on a clinic’s case or from anyone but the lab (MC20)', async () => {
    startLightDemo(LAB);
    let c = await caseOf(LAB_CASES.wilfred);
    expect(c.surgeryDetails).toHaveLength(1);
    expect((await call('POST', `/api/lab-cases/${c.id}/surgery-details`, {noSurgeryExpected: true, detail: 'x', expectedVersion: c.version})).status).toBe(400);
    c = (await call('POST', `/api/lab-cases/${c.id}/surgery-details`, {detail: 'Extra clinical details (synthetic).', expectedVersion: c.version})).body;
    expect(c).toMatchObject({status: 'Open'});
    expect(c.surgeryDetails).toHaveLength(2);

    const clinic = await caseOf(LAB_CASES.ilonaBlood);
    expect(clinic.canAddSurgeryDetails).toBe(false);
    expect((await call('POST', `/api/lab-cases/${clinic.id}/surgery-details`, {procedure: 'x', expectedVersion: clinic.version})).status).toBe(409);

    startLightDemo(ADMIN);
    const asAdmin = await caseOf(LAB_CASES.ellisBiopsy);
    expect(asAdmin.canAddSurgeryDetails).toBe(false);
    expect((await call('POST', `/api/lab-cases/${asAdmin.id}/surgery-details`, {procedure: 'x', expectedVersion: asAdmin.version})).status).toBeGreaterThanOrEqual(403);
  });
});
