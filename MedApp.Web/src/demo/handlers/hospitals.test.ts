import {call, startLightDemo} from '../../test/lightDemo';
import {ADMIN, LAB, STAFF} from '../accounts';
import {PATIENTS} from '../ids';
import {HOSPITAL_PATIENTS, HOSPITAL_REQUESTS, HOSPITALS} from '../slices/hospitals';
import {LAB_TESTS, SPECIMEN_TYPES} from '../slices/lab';
import {db} from '../store';
import {daysFrom, ukTime} from '../time';

const newRequest = (more: Record<string, unknown> = {}) => ({
  hospitalId: HOSPITALS.riverside,
  hospitalPatientNumber: 'RG-900001',
  nhsNumber: '9991034110',
  surgeonName: 'Mr C. Test (synthetic)',
  priority: 'Stat',
  isCancerPathway: true,
  authoredOn: ukTime(daysFrom(-1), 15, 0),
  reportNeededBy: daysFrom(3),
  collectedAtUtc: ukTime(daysFrom(-1), 11, 0),
  specimenTypeId: SPECIMEN_TYPES.skinBiopsy,
  labTestId: LAB_TESTS.skinHistology,
  ...more,
});

describe('the light demo’s hospitals and hospital requests (MC18)', () => {
  it('lists the lab’s hospital requests, and none to clinic staff', async () => {
    startLightDemo(LAB);
    const page = (await call('POST', '/api/hospital-requests/search', {page: 1, pageSize: 10})).body;
    expect(page.totalCount).toBe(5);
    expect(page.items[0]).toMatchObject({labCaseNumber: expect.stringMatching(/^MA\d{2}-\d{6}$/), labCaseStatus: 'Open'});
    const urgent = (await call('POST', '/api/hospital-requests/search', {priority: 'Urgent'})).body.items;
    expect(urgent.map((r: {id: string}) => r.id)).toEqual([HOSPITAL_REQUESTS.wilfredExcision, HOSPITAL_REQUESTS.wilfredBiopsy]);
    const byNumber = (await call('POST', '/api/hospital-requests/search', {search: 'RG-311402'})).body.items;
    expect(byNumber.map((r: {id: string}) => r.id)).toEqual([HOSPITAL_REQUESTS.mayaBiopsy]);

    startLightDemo(STAFF);
    expect((await call('POST', '/api/hospital-requests/search', {})).body.totalCount).toBe(0);
    expect((await call('GET', `/api/hospital-requests/${HOSPITAL_REQUESTS.wilfredExcision}`)).status).toBe(404);
    expect(db().activityLog.some((a) => a.action === 'TeamBoundaryRefused')).toBe(true);
  });

  it('shows a request with the record’s patient and the request’s own numbers, and logs viewing the case', async () => {
    startLightDemo(LAB);
    const request = (await call('GET', `/api/hospital-requests/${HOSPITAL_REQUESTS.wilfredExcision}`)).body;
    expect(request).toMatchObject({
      hospitalName: 'Riverside General (demo)',
      hospitalPatientNumber: 'RG-204817',
      nhsNumber: '9991031375',
      nhsNumbersDiffer: false,
      priority: 'Urgent',
      isCancerPathway: true,
      source: 'Hospital',
      specimenTypeSnomedCode: '309066003',
      labTestName: expect.any(String),
      patient: {id: HOSPITAL_PATIENTS.wilfredAshdown, lastName: 'Ashdown', nhsNumber: '9991031375'},
    });
    expect(db().activityLog.some((a) => a.action === 'LabCaseViewed' && a.patientId === HOSPITAL_PATIENTS.wilfredAshdown)).toBe(true);
  });

  it('hides a patient whose only link is a hospital case from clinic staff’s search, not the lab’s or the admin’s', async () => {
    const found = async () => (await call('GET', '/api/patients/search?LastName=Ashdown')).body.totalCount;
    startLightDemo(STAFF);
    expect(await found()).toBe(0);
    // Ellis Varden is the centre's patient too, so he stays findable.
    expect((await call('GET', '/api/patients/search?LastName=Varden')).body.totalCount).toBe(1);
    startLightDemo(LAB);
    expect(await found()).toBe(1);
    startLightDemo(ADMIN);
    expect(await found()).toBe(1);
  });

  it('finds matches for a person to choose, by NHS number or last name and date of birth', async () => {
    startLightDemo(LAB);
    expect((await call('POST', '/api/hospital-requests/patient-matches', {lastName: 'Ashdown'})).status).toBe(400);
    const byNhs = (await call('POST', '/api/hospital-requests/patient-matches', {nhsNumber: '999 103 1375'})).body;
    expect(byNhs.map((m: {patientId: string}) => m.patientId)).toEqual([HOSPITAL_PATIENTS.wilfredAshdown]);
    const byName = (await call('POST', '/api/hospital-requests/patient-matches', {lastName: 'thistlewood', dateOfBirth: '1986-05-21'})).body;
    expect(byName.map((m: {patientId: string}) => m.patientId)).toEqual([HOSPITAL_PATIENTS.mayaThistlewood]);
    startLightDemo(STAFF);
    expect((await call('POST', '/api/hospital-requests/patient-matches', {nhsNumber: '9991031375'})).status).toBe(403);
  });

  it('registers a request for a chosen patient or a new one, opening a lab case numbered from the lab’s sequence', async () => {
    startLightDemo(LAB);
    const before = db().specimenSequence;
    const missing = await call('POST', '/api/hospital-requests', newRequest({priority: null, surgeonName: ''}));
    expect(missing.status).toBe(400);
    expect(Object.keys(missing.body.errors)).toEqual(expect.arrayContaining(['priority', 'surgeonName']));
    expect((await call('POST', '/api/hospital-requests', newRequest({labTestId: null, patientId: PATIENTS.ellisVarden}))).body.errors).toHaveProperty('labTestId');
    expect((await call('POST', '/api/hospital-requests', newRequest())).body.errors).toHaveProperty('patientId');

    const forEllis = await call('POST', '/api/hospital-requests', newRequest({patientId: PATIENTS.ellisVarden}));
    expect(forEllis.status).toBe(200);
    expect(forEllis.body).toMatchObject({labCaseStatus: 'Open', priority: 'Stat', nhsNumbersDiffer: true});
    expect(db().specimenSequence).toBe(before + 1);

    const created = await call('POST', '/api/hospital-requests', newRequest({newPatient: {firstName: 'Iris', lastName: 'Example', dateOfBirth: '1990-02-02', nhsNumber: '9991034110'}}));
    expect(created.status).toBe(200);
    expect(db().patients.find((p) => p.id === created.body.patientId)).toMatchObject({lastName: 'Example', nhsNumber: '9991034110'});
    // Only the hospital links her here, so clinic staff don't find her.
    startLightDemo(STAFF);
    expect((await call('GET', '/api/patients/search?LastName=Example')).body.totalCount).toBe(0);
  });

  it('refuses intake outside the lab, and a new request for a restricted record', async () => {
    startLightDemo(STAFF);
    expect((await call('POST', '/api/hospital-requests', newRequest({patientId: PATIENTS.ellisVarden}))).status).toBe(403);
    startLightDemo(LAB);
    db().patients.find((p) => p.id === PATIENTS.ellisVarden)!.isRestricted = true;
    expect((await call('POST', '/api/hospital-requests', newRequest({patientId: PATIENTS.ellisVarden}))).status).toBe(409);
  });

  it('keeps hospitals as setup: a name or ODS code taken is a 409, and one that sent requests is archived, not deleted', async () => {
    startLightDemo(ADMIN);
    const body = {name: 'Northfield (demo)', odsCode: 'zzn03', contact: null, reportDeliveryMethod: 'Post', defaultDeadlineDays: 7};
    const added = await call('POST', '/api/hospitals', body);
    expect(added.body).toMatchObject({odsCode: 'ZZN03', canBeDeleted: true});
    expect((await call('POST', '/api/hospitals', {...body, name: 'Other'})).status).toBe(409);
    expect((await call('POST', '/api/hospitals', {...body, odsCode: 'ZZ9', defaultDeadlineDays: 0})).status).toBe(400);
    expect((await call('DELETE', `/api/hospitals/${HOSPITALS.riverside}`)).status).toBe(409);
    expect((await call('POST', `/api/hospitals/${HOSPITALS.riverside}/archive`)).body).toMatchObject({isArchived: true});
    expect((await call('DELETE', `/api/hospitals/${HOSPITALS.hillcrest}`)).status).toBe(204);
    startLightDemo(LAB);
    expect((await call('POST', '/api/hospitals', {...body, name: 'Lab’s', odsCode: 'ZZL04'})).status).toBe(403);
  });
});
