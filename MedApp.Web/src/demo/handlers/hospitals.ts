import type {Schemas} from '../../api/client';
import {usernameOf} from '../accounts';
import {invalid, notFound, on, problem, refuse} from '../api/http';
import {newId} from '../ids';
import {specimenNumber} from '../slices/lab';
import type {DemoHospital, DemoHospitalRequest, LabRequestPriority} from '../slices/hospitals';
import {db} from '../store';
import {nowUtc, today, ukDateOf} from '../time';
import {logActivity} from './adminLogs';
import {contains, me, paged, pathwayPatient, patientName, patientSortName, requirePermission, sortedBy} from './common';
import {ensureCanChange} from './setupPrivacy';
import {myScope, refuseAcrossWall, type TeamScope} from './teamWalls';

/**
 * Hospitals and hospital requests (MC18 of `docs/multi-clinic-plan.md`), simplified:
 * - **Hospitals** are setup (`ManageSetup`): name, ODS code, contact, how reports go, default deadline days; one a
 *   request names is archived, never deleted.
 * - **A hospital request** is typed in by the lab (`ProcessSpecimens`, in the lab team), shaped on FHIR UK Core
 *   `ServiceRequest-Lab` and `Specimen`, and opens a lab case with no pathway, numbered from the lab's one sequence.
 * - **The patient** is chosen by a person from the matches (`patient-matches`: by NHS number, or last name and date of
 *   birth), never matched automatically (H-72), or created with the request.
 * - **Hospital cases are the lab's** (and admins', read only): anyone else gets a 404 for one, and an empty list.
 * - **A patient whose only link is a hospital case** is hidden from non-lab staff's patient search
 *   (`isHiddenFromSearch`, used by `handlers/patients.ts`), until they have a pathway, referral or appointment.
 */

type Hospital = Schemas['HospitalResponse'];

export const LAB_REQUEST_PRIORITIES: LabRequestPriority[] = ['Routine', 'Urgent', 'Asap', 'Stat'];

const MAX_NAME = 200;
const MAX_ODS = 10;
const MAX_CONTACT = 500;
const MAX_NUMBER = 30;
const MAX_SURGEON = 200;
const DELIVERY_METHODS = ['Post', 'HandedToPatient', 'SecureEmail', 'EmailOutsideMedApp', 'Other'];

/** Whether the viewer sees hospital cases: a member of the lab team, or an admin (every team, read only). */
export function seesHospitalCases(scope: TeamScope = myScope()): boolean {
  if (scope.seesAllTeams) return true;
  const labs = db().teams.filter((t) => t.kind === 'Lab').map((t) => t.id);
  return scope.teamIds.some((id) => labs.includes(id));
}

/**
 * Whether a patient is hidden from the viewer's patient search: their only link to the centre is a hospital case, and
 * the viewer isn't the lab's (or an admin).
 */
export function isHiddenFromSearch(patientId: string | undefined, scope: TeamScope = myScope()): boolean {
  const state = db();
  if (!state.hospitalRequests.some((r) => r.patientId === patientId)) return false;
  if (seesHospitalCases(scope)) return false;
  return !(
    state.pathways.some((p) => p.patientId === patientId) ||
    state.referrals.some((r) => r.patientId === patientId) ||
    state.appointments.some((a) => a.patientId === patientId)
  );
}

function hospitalResponse(hospital: DemoHospital): Hospital {
  return {...hospital, canBeDeleted: !db().hospitalRequests.some((r) => r.hospitalId === hospital.id)};
}

const findHospital = (id: string) => db().hospitals.find((h) => h.id === id) ?? refuse(notFound(`Hospital '${id}'`));

/** The hospital's fields from a request body, checked as the API's validator does (a name taken is a 409). */
function hospitalFields(body: Schemas['HospitalDetailsRequest'] | undefined, id?: string): Omit<DemoHospital, 'id' | 'isArchived'> {
  const name = body?.name?.trim() ?? '';
  const odsCode = body?.odsCode?.trim().toUpperCase() ?? '';
  const errors: Record<string, string[]> = {};
  if (!name) errors.name = ['Give it a name.'];
  else if (name.length > MAX_NAME) errors.name = [`The name must be at most ${MAX_NAME} characters.`];
  if (!/^[A-Z0-9]{3,10}$/.test(odsCode) || odsCode.length > MAX_ODS) errors.odsCode = ['Enter its ODS code: 3 to 10 letters and digits.'];
  if ((body?.contact?.length ?? 0) > MAX_CONTACT) errors.contact = [`The contact must be at most ${MAX_CONTACT} characters.`];
  if (!DELIVERY_METHODS.includes(body?.reportDeliveryMethod ?? '')) errors.reportDeliveryMethod = ['Choose how its reports are sent.'];
  const days = body?.defaultDeadlineDays;
  if (!Number.isInteger(days) || days! < 1 || days! > 90) errors.defaultDeadlineDays = ['The default deadline must be 1 to 90 days.'];
  if (Object.keys(errors).length > 0) refuse(invalid(errors));
  const others = db().hospitals.filter((h) => h.id !== id);
  if (others.some((h) => h.name.toLowerCase() === name.toLowerCase())) refuse(problem(409, `There's already a hospital called '${name}'.`));
  if (others.some((h) => h.odsCode === odsCode)) refuse(problem(409, `There's already a hospital with the ODS code '${odsCode}'.`));
  return {name, odsCode, contact: body?.contact?.trim() || null, reportDeliveryMethod: body!.reportDeliveryMethod!, defaultDeadlineDays: days!};
}

function requestResponse(request: DemoHospitalRequest): Schemas['HospitalRequestResponse'] {
  const state = db();
  const hospital = state.hospitals.find((h) => h.id === request.hospitalId);
  const patient = state.patients.find((p) => p.id === request.patientId);
  return {
    id: request.id,
    hospitalId: request.hospitalId,
    hospitalName: hospital?.name ?? null,
    hospitalOdsCode: hospital?.odsCode ?? null,
    patientId: request.patientId,
    patient: pathwayPatient(request.patientId),
    hospitalPatientNumber: request.hospitalPatientNumber,
    nhsNumber: request.nhsNumber,
    nhsNumbersDiffer: !!request.nhsNumber && !!patient?.nhsNumber && request.nhsNumber !== patient.nhsNumber,
    surgeonName: request.surgeonName,
    priority: request.priority,
    isCancerPathway: request.isCancerPathway,
    authoredOn: request.authoredOn,
    reportNeededBy: request.reportNeededBy,
    collectedAtUtc: request.collectedAtUtc,
    specimenTypeId: request.specimenTypeId,
    specimenTypeName: state.specimenTypes.find((t) => t.id === request.specimenTypeId)?.name ?? null,
    specimenTypeSnomedCode: state.specimenTypes.find((t) => t.id === request.specimenTypeId)?.snomedCode ?? null,
    labTestId: request.labTestId,
    labTestName: state.labTests.find((t) => t.id === request.labTestId)?.name ?? null,
    source: request.source,
    labCaseId: request.labCaseId,
    labCaseNumber: request.labCaseNumber,
    labCaseStatus: request.labCaseStatus,
    registeredAtUtc: request.registeredAtUtc,
    registeredByUsername: usernameOf(request.registeredByUserId),
  };
}

function requestSummary(request: DemoHospitalRequest): Schemas['HospitalRequestSummaryResponse'] {
  const response = requestResponse(request);
  return {
    id: response.id,
    labCaseNumber: response.labCaseNumber,
    labCaseStatus: response.labCaseStatus,
    hospitalName: response.hospitalName,
    patientId: request.patientId,
    patientName: patientName(request.patientId),
    patientNhsNumber: response.patient?.nhsNumber ?? null,
    hospitalPatientNumber: response.hospitalPatientNumber,
    priority: response.priority,
    labTestName: response.labTestName,
    source: response.source,
    isCancerPathway: response.isCancerPathway,
    reportNeededBy: response.reportNeededBy,
    registeredAtUtc: response.registeredAtUtc,
  };
}

/** Refuses intake to anyone outside the lab team: hospital cases are the lab's. */
function requireLabIntake() {
  requirePermission('ProcessSpecimens');
  const scope = myScope();
  if (scope.seesAllTeams || !seesHospitalCases(scope)) refuse(problem(409, 'Hospital requests are registered by the lab team.'));
}

const NHS_NUMBER = /^\d{10}$/;
const compactNumber = (value: string | null | undefined) => value?.replace(/\s/g, '') || null;

/** A moment the request says has happened: a valid time, and not in the future. */
const isPast = (value: string | null | undefined) => !!value && !Number.isNaN(Date.parse(value)) && Date.parse(value) <= Date.now() + 60_000;

/** The patient a request is for: one chosen by a person from the matches, or a new record made with it. */
function requestPatient(body: Schemas['CreateHospitalRequestRequest'], errors: Record<string, string[]>): string | null {
  if (body.patientId && body.newPatient) {
    errors.patientId = ['Choose a patient from the matches, or add a new one, not both.'];
    return null;
  }
  if (body.patientId) {
    const patient = db().patients.find((p) => p.id === body.patientId) ?? refuse(notFound('The patient'));
    ensureCanChange(patient);
    return patient.id!;
  }
  const p = body.newPatient;
  if (!p) {
    errors.patientId = ['Choose the patient from the matches, or add a new one.'];
    return null;
  }
  if (!p.firstName?.trim()) errors['newPatient.firstName'] = ['Enter a first name.'];
  if (!p.lastName?.trim()) errors['newPatient.lastName'] = ['Enter a last name.'];
  if (!p.dateOfBirth || p.dateOfBirth > today()) errors['newPatient.dateOfBirth'] = ['Enter a date of birth, not in the future.'];
  const nhs = compactNumber(p.nhsNumber);
  if (nhs && !NHS_NUMBER.test(nhs)) errors['newPatient.nhsNumber'] = ['An NHS number is 10 digits.'];
  if (Object.keys(errors).length > 0) return null;
  const patient: Schemas['PatientResponse'] = {
    id: newId(),
    firstName: p.firstName!.trim(),
    lastName: p.lastName!.trim(),
    dateOfBirth: p.dateOfBirth,
    email: null,
    nhsNumber: nhs,
    addressLines: [],
    postcode: null,
    mobilePhone: null,
    reminderChannel: 'None',
    practiceId: null,
    isRestricted: false,
    isDisputed: false,
    isAnonymised: false,
  };
  db().patients.push(patient);
  logActivity('PatientCreated', {patientId: patient.id});
  return patient.id!;
}

export function registerHospitals() {
  on('get', '/api/hospitals', ({query}) => {
    me();
    const includeArchived = query.get('includeArchived') === 'true';
    return db()
      .hospitals.filter((h) => includeArchived || !h.isArchived)
      .sort((a, b) => a.name.localeCompare(b.name))
      .map(hospitalResponse);
  });
  on('post', '/api/hospitals', ({body}) => {
    requirePermission('ManageSetup');
    const hospital: DemoHospital = {id: newId(), ...hospitalFields(body), isArchived: false};
    db().hospitals.push(hospital);
    return hospitalResponse(hospital);
  });
  on('put', '/api/hospitals/{id}', ({params, body}) => {
    requirePermission('ManageSetup');
    const hospital = findHospital(params.id);
    Object.assign(hospital, hospitalFields(body, hospital.id));
    return hospitalResponse(hospital);
  });
  on('delete', '/api/hospitals/{id}', ({params}) => {
    requirePermission('ManageSetup');
    const hospital = findHospital(params.id);
    if (!hospitalResponse(hospital).canBeDeleted)
      return problem(409, `'${hospital.name}' has sent requests, so it can't be deleted. Archive it instead.`);
    db().hospitals = db().hospitals.filter((h) => h.id !== hospital.id);
  });
  on('post', '/api/hospitals/{id}/archive', ({params}) => {
    requirePermission('ManageSetup');
    const hospital = findHospital(params.id);
    hospital.isArchived = true;
    return hospitalResponse(hospital);
  });
  on('post', '/api/hospitals/{id}/restore', ({params}) => {
    requirePermission('ManageSetup');
    const hospital = findHospital(params.id);
    hospital.isArchived = false;
    return hospitalResponse(hospital);
  });

  // The patients a request could be for, for a person to choose from: never chosen for them (H-72). In a body, so no
  // NHS number is in a URL.
  on('post', '/api/hospital-requests/patient-matches', ({body}) => {
    requireLabIntake();
    const nhs = compactNumber(body.nhsNumber);
    const lastName = body.lastName?.trim().toLowerCase() || null;
    const dateOfBirth = body.dateOfBirth || null;
    if (!nhs && !(lastName && dateOfBirth)) return invalid({nhsNumber: ['Enter the NHS number, or the last name and date of birth.']});
    const matches = db()
      .patients.filter(
        (p) => !p.isAnonymised && ((nhs && p.nhsNumber === nhs) || (lastName && dateOfBirth && p.lastName?.toLowerCase() === lastName && p.dateOfBirth === dateOfBirth)),
      )
      .sort((a, b) => patientSortName(a.id).localeCompare(patientSortName(b.id)) || (a.id ?? '').localeCompare(b.id ?? ''));
    for (const p of matches) logActivity('PatientListed', {patientId: p.id});
    return matches.map((p) => ({
      patientId: p.id,
      firstName: p.firstName,
      lastName: p.lastName,
      dateOfBirth: p.dateOfBirth,
      nhsNumber: p.nhsNumber,
      isRestricted: p.isRestricted ?? false,
    }));
  });

  on('post', '/api/hospital-requests', ({body}) => {
    requireLabIntake();
    const errors: Record<string, string[]> = {};
    const hospital = db().hospitals.find((h) => h.id === body.hospitalId);
    if (!hospital) errors.hospitalId = ['Choose the hospital.'];
    else if (hospital.isArchived) errors.hospitalId = [`'${hospital.name}' is archived.`];
    const type = db().specimenTypes.find((t) => t.id === body.specimenTypeId);
    if (!type || type.isArchived) errors.specimenTypeId = ['Choose the type of sample.'];
    const test = db().labTests.find((t) => t.id === body.labTestId);
    if (!test || test.isArchived) errors.labTestId = ['Choose the test the request asks for.'];
    const number = body.hospitalPatientNumber?.trim() ?? '';
    if (!number) errors.hospitalPatientNumber = ["Enter the hospital's patient number."];
    else if (number.length > MAX_NUMBER) errors.hospitalPatientNumber = [`At most ${MAX_NUMBER} characters.`];
    const nhs = compactNumber(body.nhsNumber);
    if (nhs && !NHS_NUMBER.test(nhs)) errors.nhsNumber = ['An NHS number is 10 digits.'];
    const surgeon = body.surgeonName?.trim() ?? '';
    if (!surgeon) errors.surgeonName = ["Enter the surgeon's name, as written."];
    else if (surgeon.length > MAX_SURGEON) errors.surgeonName = [`At most ${MAX_SURGEON} characters.`];
    if (!LAB_REQUEST_PRIORITIES.includes(body.priority as LabRequestPriority)) errors.priority = ['Choose the priority the request gives.'];
    if (!isPast(body.authoredOn)) errors.authoredOn = ['Enter when the request was made, not in the future.'];
    if (body.collectedAtUtc && !isPast(body.collectedAtUtc)) errors.collectedAtUtc = ['The collection time can’t be in the future.'];
    if (body.reportNeededBy && body.authoredOn && body.reportNeededBy < ukDateOf(body.authoredOn))
      errors.reportNeededBy = ['The report can’t be needed before the request was made.'];
    if (Object.keys(errors).length > 0) return invalid(errors);
    const patientId = requestPatient(body, errors);
    if (!patientId) return invalid(errors);
    const state = db();
    state.specimenSequence += 1;
    const request: DemoHospitalRequest = {
      id: newId(),
      hospitalId: hospital!.id,
      patientId,
      teamId: state.teams.find((t) => t.kind === 'Lab')!.id!,
      hospitalPatientNumber: number,
      nhsNumber: nhs,
      surgeonName: surgeon,
      priority: body.priority as LabRequestPriority,
      isCancerPathway: body.isCancerPathway === true,
      authoredOn: body.authoredOn!,
      reportNeededBy: body.reportNeededBy || null,
      collectedAtUtc: body.collectedAtUtc || null,
      specimenTypeId: type!.id,
      labTestId: test!.id,
      source: 'Hospital',
      labCaseId: newId(),
      labCaseNumber: specimenNumber(state.specimenSequence),
      labCaseStatus: 'Open',
      registeredAtUtc: nowUtc(),
      registeredByUserId: me().id,
    };
    state.hospitalRequests.push(request);
    return requestResponse(request);
  });

  on('post', '/api/hospital-requests/search', ({body}) => {
    requirePermission('ViewPatients');
    if ((body.search ?? '').length > 200) return invalid({search: ['Search must not exceed 200 characters.']});
    const pages = new URLSearchParams({Page: String(body.page ?? 1), PageSize: String(body.pageSize ?? 10)});
    if (!seesHospitalCases()) return paged<Schemas['HospitalRequestSummaryResponse']>([], pages);
    const term = body.search?.trim() ?? '';
    const compact = term.replace(/\s/g, '');
    const items = db()
      .hospitalRequests.filter(
        (r) =>
          (!body.hospitalId || r.hospitalId === body.hospitalId) &&
          (!body.priority || r.priority === body.priority) &&
          (!term ||
            contains(r.labCaseNumber, compact) ||
            contains(r.hospitalPatientNumber, compact) ||
            contains(patientName(r.patientId), term) ||
            contains(db().patients.find((p) => p.id === r.patientId)?.nhsNumber, compact)),
      )
      .sort((a, b) => b.registeredAtUtc.localeCompare(a.registeredAtUtc) || a.id.localeCompare(b.id));
    const ordered =
      sortedBy(items, body.sort, body.ascending ?? true, {
        case: (r) => r.labCaseNumber,
        patient: (r) => patientSortName(r.patientId),
        hospital: (r) => db().hospitals.find((h) => h.id === r.hospitalId)?.name.toLowerCase() ?? '',
        needed: (r) => r.reportNeededBy ?? '9999-12-31',
        registered: (r) => r.registeredAtUtc,
      }) ?? items;
    const page = paged(ordered.map(requestSummary), pages);
    for (const r of page.items) logActivity('PatientListed', {patientId: r.patientId});
    return page;
  });

  on('get', '/api/hospital-requests/{id}', ({params}) => {
    requirePermission('ViewPatients');
    const scope = myScope();
    const request = db().hospitalRequests.find((r) => r.id === params.id);
    if (!request) return notFound('The hospital request');
    if (!seesHospitalCases(scope)) refuseAcrossWall('HospitalRequest', request.id, 'The hospital request');
    logActivity('LabCaseViewed', {
      patientId: request.patientId,
      details: JSON.stringify({labCaseId: request.labCaseId, access: scope.seesAllTeams ? 'Admin' : 'Owner'}),
    });
    return requestResponse(request);
  });
}
