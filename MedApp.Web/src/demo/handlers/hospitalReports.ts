import type {Schemas} from '../../api/client';
import {usernameOf} from '../accounts';
import {invalid, problem, refuse} from '../api/http';
import {newId} from '../ids';
import type {DemoLabCase} from '../slices/labCases';
import {db} from '../store';
import {nowUtc} from '../time';
import {ensureVersion, optionalText, patientFlags} from './labCommon';

/**
 * Reports to the hospital and surgery details (MC20 of `docs/multi-clinic-plan.md`), for hospital cases only (a clinic
 * case's report goes to its pathway: MC15 and MC16).
 * - **Sending:** authorising a hospital case's report sends it back to its request's hospital, addressed to the request's
 *   surgeon (never chosen, H-72), shaped on FHIR UK Core `DiagnosticReport-Lab` (status final, issued when authorised).
 *   The full version renders a `LabReport` letter (recipient `Hospital`) with the report attached and records it as
 *   sent (`LabReportSent`); the light demo records it as sent. The clock (MC19) stops at the authorising. The case
 *   then awaits the hospital's surgery details.
 * - **Surgery details:** what the hospital sends after its request (the date, the procedure, the outcome, extra
 *   details), typed by the lab as written (`SurgeryDetailsAdded`, no text in the audit log). Added to a case awaiting
 *   them, they close it (`LabCaseClosed`); "No surgery expected" closes it with a detail. Arriving before the report,
 *   they're kept and the case stays open. Nothing is worked out from them.
 */

type Details = Schemas['AddSurgeryDetailsRequest'];

const MAX_PROCEDURE = 200;
const MAX_TEXT = 500;
const CHANGED_MEANWHILE = 'Someone else changed this lab case while you had it open. Reload it to see their change, then try again.';

/** Whether surgery details can still arrive: a hospital case, open or awaiting them. */
export const takesSurgeryDetails = (c: DemoLabCase) => c.source === 'Hospital' && (c.status === 'Open' || c.status === 'AwaitingSurgeryDetails');

/** Sends an authorised hospital case's report back to its request's hospital: recorded as sent, now awaiting surgery details. */
export function sendReportToHospital(c: DemoLabCase, userId: string, atUtc: string) {
  const request = db().hospitalRequests.find((r) => r.id === c.hospitalRequestId);
  const hospital = db().hospitals.find((h) => h.id === (request?.hospitalId ?? c.hospitalId));
  c.reportSent = {
    atUtc,
    byUserId: userId,
    hospitalId: hospital?.id ?? c.hospitalId ?? '',
    addressedTo: request?.surgeonName ?? 'The requesting surgeon',
    method: hospital?.reportDeliveryMethod ?? 'Post',
  };
  c.status = 'AwaitingSurgeryDetails';
}

/** The fields MC20 adds to a case's answer. */
export function hospitalReportFields(c: DemoLabCase, worksCases: boolean): Pick<
  Schemas['LabCaseResponse'],
  'hospitalReport' | 'surgeryDetails' | 'closedAtUtc' | 'closedByUsername' | 'canAddSurgeryDetails'
> {
  const sent = c.reportSent;
  return {
    hospitalReport: sent
      ? {
          status: 'Final',
          letterKind: 'LabReport',
          hospitalId: sent.hospitalId,
          hospitalName: db().hospitals.find((h) => h.id === sent.hospitalId)?.name ?? 'The hospital',
          addressedTo: sent.addressedTo,
          deliveryMethod: sent.method,
          issuedAtUtc: c.report?.atUtc ?? sent.atUtc,
          sentAtUtc: sent.atUtc,
          sentByUsername: usernameOf(sent.byUserId),
        }
      : undefined,
    surgeryDetails: (c.surgeryDetails ?? []).map((d) => ({
      id: d.id,
      surgeryOn: d.surgeryOn,
      procedure: d.procedure,
      outcome: d.outcome,
      noSurgeryExpected: d.noSurgeryExpected,
      detail: d.detail,
      addedAtUtc: d.addedAtUtc,
      addedByUsername: usernameOf(d.addedByUserId),
    })),
    closedAtUtc: c.closed?.atUtc ?? null,
    closedByUsername: usernameOf(c.closed?.byUserId),
    canAddSurgeryDetails: worksCases && takesSurgeryDetails(c) && !patientFlags(c.patientId).isAnonymised,
  };
}

/** The audit entries MC20 adds (IDs, names and enums only; never the surgery details typed). */
export function hospitalReportAuditEntries(c: DemoLabCase, add: (key: string, action: string, atUtc: string, userId: string, details: Record<string, unknown>) => void) {
  const sent = c.reportSent;
  if (sent)
    add('sent', 'LabReportSent', sent.atUtc, sent.byUserId, {
      hospitalId: sent.hospitalId,
      hospital: db().hospitals.find((h) => h.id === sent.hospitalId)?.name ?? null,
      letterKind: 'LabReport',
      recipientKind: 'Hospital',
      method: sent.method,
    });
  for (const d of c.surgeryDetails ?? [])
    add(`${d.id}:surgery`, 'SurgeryDetailsAdded', d.addedAtUtc, d.addedByUserId, {surgeryDetailsId: d.id, noSurgeryExpected: d.noSurgeryExpected});
  if (c.closed) add('closed', 'LabCaseClosed', c.closed.atUtc, c.closed.byUserId, {});
}

/** A UK date as `YYYY-MM-DD`, or a 400. */
function dateOrNull(value: string | null | undefined): string | null {
  const text = value?.trim() || null;
  if (text && (!/^\d{4}-\d{2}-\d{2}$/.test(text) || Number.isNaN(Date.parse(`${text}T00:00:00Z`))))
    refuse(invalid({surgeryOn: ['Give the date of the surgery, as the hospital wrote it.']}));
  return text;
}

/** Adds the hospital's surgery details, as written (`POST api/lab-cases/{id}/surgery-details`); closes a case awaiting them. */
export function addSurgeryDetails(c: DemoLabCase, body: Details, userId: string): DemoLabCase {
  if (patientFlags(c.patientId).isAnonymised) refuse(problem(409, "This patient's record has been anonymised, so the case can't be changed."));
  ensureVersion(body.expectedVersion, c.version, CHANGED_MEANWHILE);
  if (c.source !== 'Hospital') refuse(problem(409, 'Surgery details come only with a hospital’s case.'));
  if (!takesSurgeryDetails(c)) refuse(problem(409, 'This case is closed, so no more surgery details can be added.'));
  const noSurgeryExpected = body.noSurgeryExpected === true;
  const surgeryOn = dateOrNull(body.surgeryOn);
  const procedure = optionalText(body.procedure, MAX_PROCEDURE, 'procedure', 'The procedure');
  const outcome = optionalText(body.outcome, MAX_TEXT, 'outcome', 'The outcome');
  const detail = optionalText(body.detail, MAX_TEXT, 'detail', 'The detail');
  if (noSurgeryExpected) {
    if (surgeryOn || procedure || outcome) refuse(invalid({noSurgeryExpected: ['With no surgery expected, leave the surgery’s date, procedure and outcome empty.']}));
    if (!detail) refuse(invalid({detail: ['Say why no surgery is expected, as the hospital wrote it.']}));
    if (c.status !== 'AwaitingSurgeryDetails')
      refuse(invalid({noSurgeryExpected: ['“No surgery expected” closes the case: record it once the report has been sent.']}));
  } else if (!surgeryOn && !procedure && !outcome && !detail) refuse(invalid({procedure: ['Enter what the hospital sent: the date, the procedure, the outcome or other details.']}));
  const now = nowUtc();
  (c.surgeryDetails ??= []).push({id: newId(), surgeryOn, procedure, outcome, noSurgeryExpected, detail, addedAtUtc: now, addedByUserId: userId});
  if (c.status === 'AwaitingSurgeryDetails') {
    c.status = 'Closed';
    c.closed = {atUtc: now, byUserId: userId};
  }
  c.version++;
  return c;
}
