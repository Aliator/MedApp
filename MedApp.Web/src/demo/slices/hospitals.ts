import type {Schemas} from '../../api/client';
import {LAB} from '../accounts';
import {PATIENTS, TEAMS, id} from '../ids';
import type {BaseState} from '../state';
import {daysFrom, ukTime} from '../time';
import {LAB_TESTS, SPECIMEN_TYPES, specimenNumber, type LabSlice} from './lab';

/**
 * The light demo's hospitals and hospital requests (MC18 of `docs/multi-clinic-plan.md`): a nearby hospital sends the
 * lab samples with a request shaped on FHIR UK Core `ServiceRequest-Lab` and `Specimen`, typed in by the lab; each opens
 * a lab case of its own, with no pathway. Handlers: `handlers/hospitals.ts`. A patient whose only link is a hospital
 * case is hidden from non-lab staff's patient search.
 */

/** FHIR's `ServiceRequest.priority` codes, as `LabRequestPriority` names them. */
export type LabRequestPriority = 'Routine' | 'Urgent' | 'Asap' | 'Stat';

export interface DemoHospital {
  id: string;
  name: string;
  odsCode: string;
  contact: string | null;
  /** How its reports go (`LetterDispatchMethod` by name). */
  reportDeliveryMethod: string;
  /** Calendar days for its report when a request names no date (MC19's clock uses it). */
  defaultDeadlineDays: number;
  isArchived: boolean;
}

/** A hospital's request, as typed in by the lab, and the lab case it opened (no pathway: the lab's own). */
export interface DemoHospitalRequest {
  id: string;
  hospitalId: string;
  patientId: string;
  /** The lab team's: hospital cases are the lab's (decision 200). */
  teamId: string;
  hospitalPatientNumber: string;
  /** The NHS number as the request gave it (the record's may differ: both are shown). */
  nhsNumber: string | null;
  /** The surgeon's name, as written on the request. */
  surgeonName: string;
  priority: LabRequestPriority;
  isCancerPathway: boolean;
  authoredOn: string;
  /** FHIR's `occurrence`: the UK date the hospital needs the report by, or null (the lab's own limit applies). */
  reportNeededBy: string | null;
  collectedAtUtc: string | null;
  specimenTypeId: string;
  /** The test asked for, from the lab's catalogue (MC13). */
  labTestId: string;
  /** Where the sample came from (MC14's `SpecimenSource`): always the hospital here. */
  source: 'Hospital';
  labCaseId: string;
  labCaseNumber: string;
  registeredAtUtc: string;
  registeredByUserId: string;
}

export interface HospitalSlice {
  hospitals: DemoHospital[];
  hospitalRequests: DemoHospitalRequest[];
}

export const HOSPITALS = {
  /** The nearby hospital (invented, as the full demo's). */
  riverside: id(0xc1, 1),
  /** Never sent anything: the setup page can delete it. */
  hillcrest: id(0xc1, 2),
} as const;

export const HOSPITAL_REQUESTS = {
  /** Wilfred Ashdown: a cancer pathway excision, urgent, the report needed in 5 days. Only the hospital links him here. */
  wilfredExcision: id(0xc2, 1),
  /** Maya Thistlewood: routine, no date given. Only the hospital links her here. */
  mayaBiopsy: id(0xc2, 2),
  /** Ellis Varden, a centre patient too (matched by the lab to his record): routine. */
  ellisExcision: id(0xc2, 3),
} as const;

/** Each request's lab case (MC15 seeds them: `slices/labCases.ts`). */
export const HOSPITAL_LAB_CASES = {
  wilfred: id(0xc3, 1),
  maya: id(0xc3, 2),
  ellis: id(0xc3, 3),
} as const;

/** Patients the hospital sent and the centre doesn't otherwise see (hidden from non-lab staff's search). */
export const HOSPITAL_PATIENTS = {
  wilfredAshdown: id(0xc4, 1),
  mayaThistlewood: id(0xc4, 2),
} as const;

/** The IDs the Read Me's tour opens. */
export const HOSPITAL_IDS = {
  /** A hospital request to open as the lab (`r.aldous`). */
  urgentRequest: HOSPITAL_REQUESTS.wilfredExcision,
  hospitalOnlyPatient: HOSPITAL_PATIENTS.wilfredAshdown,
} as const;

const ago = (days: number, hours = 10, minutes = 0) => ukTime(daysFrom(-days), hours, minutes);

/** The hospital patients' records: synthetic, with 999-range NHS numbers (valid check digits). */
export const HOSPITAL_PATIENT_RECORDS: Schemas['PatientResponse'][] = [
  [HOSPITAL_PATIENTS.wilfredAshdown, 'Wilfred', 'Ashdown', '1947-11-03', '9991031375'],
  [HOSPITAL_PATIENTS.mayaThistlewood, 'Maya', 'Thistlewood', '1986-05-21', '9991032746'],
].map(([patientId, firstName, lastName, dateOfBirth, nhsNumber], i) => ({
  id: patientId,
  firstName,
  lastName,
  dateOfBirth,
  email: null,
  nhsNumber,
  addressLines: [`${40 + i} Riverside Walk`, 'Exampleton'],
  postcode: null,
  mobilePhone: null,
  reminderChannel: 'None',
  practiceId: null,
  isRestricted: false,
  isDisputed: false,
  isAnonymised: false,
}));

/**
 * Seeds the slice after the base and the lab's (it adds the hospital's patients to the base, and numbers its cases from
 * the lab's one sequence, as specimens are numbered).
 */
export function seedHospitals(state: BaseState, lab: Pick<LabSlice, 'specimenSequence'>): HospitalSlice {
  state.patients.push(...HOSPITAL_PATIENT_RECORDS.map((p) => ({...p})));
  const ellis = state.patients.find((p) => p.id === PATIENTS.ellisVarden);
  const request = (
    requestId: string,
    caseId: string,
    days: number,
    fields: Pick<DemoHospitalRequest, 'patientId' | 'hospitalPatientNumber' | 'nhsNumber' | 'surgeonName' | 'priority' | 'isCancerPathway' | 'reportNeededBy'>,
  ): DemoHospitalRequest => {
    lab.specimenSequence += 1;
    const registeredAtUtc = ago(days, 9, 30);
    return {
      id: requestId,
      hospitalId: HOSPITALS.riverside,
      teamId: TEAMS.lab,
      authoredOn: ago(days + 1, 16, 0),
      collectedAtUtc: ago(days + 1, 11, 15),
      specimenTypeId: SPECIMEN_TYPES.skinBiopsy,
      labTestId: LAB_TESTS.skinHistology,
      source: 'Hospital',
      labCaseId: caseId,
      labCaseNumber: specimenNumber(lab.specimenSequence, registeredAtUtc.slice(0, 10)),
      registeredAtUtc,
      registeredByUserId: LAB.id,
      ...fields,
    };
  };
  return {
    hospitals: [
      {
        id: HOSPITALS.riverside,
        name: 'Riverside General (demo)',
        odsCode: 'ZZR01',
        contact: 'Histopathology requests desk (synthetic)',
        reportDeliveryMethod: 'Post',
        defaultDeadlineDays: 10,
        isArchived: false,
      },
      {
        id: HOSPITALS.hillcrest,
        name: 'Hillcrest Community Hospital (demo)',
        odsCode: 'ZZH02',
        contact: null,
        reportDeliveryMethod: 'EmailOutsideMedApp',
        defaultDeadlineDays: 14,
        isArchived: false,
      },
    ],
    hospitalRequests: [
      request(HOSPITAL_REQUESTS.wilfredExcision, HOSPITAL_LAB_CASES.wilfred, 2, {
        patientId: HOSPITAL_PATIENTS.wilfredAshdown,
        hospitalPatientNumber: 'RG-204817',
        nhsNumber: '9991031375',
        surgeonName: 'Ms A. Example (synthetic)',
        priority: 'Urgent',
        isCancerPathway: true,
        reportNeededBy: daysFrom(5),
      }),
      request(HOSPITAL_REQUESTS.mayaBiopsy, HOSPITAL_LAB_CASES.maya, 1, {
        patientId: HOSPITAL_PATIENTS.mayaThistlewood,
        hospitalPatientNumber: 'RG-311402',
        nhsNumber: '9991032746',
        surgeonName: 'Mr B. Sample (synthetic)',
        priority: 'Routine',
        isCancerPathway: false,
        reportNeededBy: null,
      }),
      request(HOSPITAL_REQUESTS.ellisExcision, HOSPITAL_LAB_CASES.ellis, 4, {
        patientId: PATIENTS.ellisVarden,
        hospitalPatientNumber: 'RG-118953',
        nhsNumber: ellis?.nhsNumber ?? null,
        surgeonName: 'Ms A. Example (synthetic)',
        priority: 'Routine',
        isCancerPathway: false,
        reportNeededBy: daysFrom(6),
      }),
    ],
  };
}
