import {screen, waitFor} from '@testing-library/react';
import {json, mockApi, renderWithApp, userWithRoles} from '../../test/render';
import {BIOPSY_TYPE, HOSPITAL_ID, HOSPITALS, PATIENT_ID, testRequest} from '../../test/hospitals';
import {matchDescription, NEW_PATIENT, RegisterHospitalRequestPage, requestBody} from './RegisterHospitalRequestPage';

const labUser = userWithRoles('test-lab', 'Lab');
const MATCH = {patientId: PATIENT_ID, firstName: 'Wilfred', lastName: 'Ashdown', dateOfBirth: '1947-11-03', nhsNumber: '9991031375', isRestricted: false};

const FIELDS = {
  hospitalId: HOSPITAL_ID,
  hospitalPatientNumber: ' RG-1 ',
  nhsNumber: '999 103 1375',
  surgeonName: 'Ms A. Example',
  priority: 'Urgent',
  isCancerPathway: true,
  authored: {date: '2026-11-16', time: '16:00'},
  reportNeededBy: '',
  collected: {date: '', time: ''},
  specimenTypeId: BIOPSY_TYPE,
  labTestId: 'test',
};
const NEW = {firstName: ' Iris ', lastName: 'Example', dateOfBirth: '1990-02-02', nhsNumber: ''};

function renderPage() {
  const fetch = mockApi({
    'GET /api/hospitals': () => json(HOSPITALS.filter((h) => !h.isArchived)),
    'GET /api/specimen-types': () =>
      json([{id: BIOPSY_TYPE, name: 'Skin biopsy (punch)', expectedTurnaroundDays: 14, isArchived: false, canBeDeleted: false}]),
    'GET /api/lab-tests': () => json([]),
    'POST /api/hospital-requests/patient-matches': () => json([MATCH]),
    'POST /api/hospital-requests': () => json(testRequest(), 201),
  });
  return {...renderWithApp(<RegisterHospitalRequestPage />, {user: labUser}), fetch};
}

describe('RegisterHospitalRequestPage', () => {
  it('finds matches by the request’s NHS number for the person to choose, choosing none for them', async () => {
    const {user, fetch} = renderPage();

    expect(await screen.findByRole('heading', {name: 'Register Hospital Request', level: 1})).toBeInTheDocument();
    await user.type(screen.getByRole('textbox', {name: /NHS number/i}), '9991031375');
    await user.click(screen.getByRole('button', {name: 'Find Matches'}));

    const match = await screen.findByRole('radio', {name: /Ashdown, Wilfred/});
    expect(match).not.toBeChecked();
    expect(screen.getByRole('radio', {name: /Add a new patient/})).not.toBeChecked();
    const find = fetch.mock.calls.map(([r]) => r as Request).find((r) => new URL(r.url).pathname === '/api/hospital-requests/patient-matches')!;
    expect(new URL(find.url).search).toBe('');
    expect(await find.clone().json()).toEqual({nhsNumber: '9991031375', lastName: null, dateOfBirth: null});
    await user.click(match);
    await waitFor(() => expect(match).toBeChecked());
  });

  it('builds the request from the form: a chosen patient, or a new one, times in UTC', () => {
    expect(requestBody(FIELDS, PATIENT_ID, NEW)).toEqual({
      hospitalId: HOSPITAL_ID,
      patientId: PATIENT_ID,
      newPatient: undefined,
      hospitalPatientNumber: 'RG-1',
      nhsNumber: '9991031375',
      surgeonName: 'Ms A. Example',
      priority: 'Urgent',
      isCancerPathway: true,
      authoredOn: '2026-11-16T16:00:00.000Z',
      reportNeededBy: null,
      collectedAtUtc: null,
      specimenTypeId: BIOPSY_TYPE,
      labTestId: 'test',
    });
    expect(requestBody({...FIELDS, priority: ''}, NEW_PATIENT, NEW)).toMatchObject({
      patientId: null,
      priority: null,
      newPatient: {firstName: 'Iris', lastName: 'Example', dateOfBirth: '1990-02-02', nhsNumber: null},
    });
  });

  it('describes a match by date of birth and NHS number', () => {
    expect(matchDescription({...MATCH, isRestricted: true})).toBe('Born 3 Nov 1947 · NHS No. 999 103 1375 · Restricted');
    expect(matchDescription({...MATCH, nhsNumber: null})).toBe('Born 3 Nov 1947 · No NHS number');
  });
});
