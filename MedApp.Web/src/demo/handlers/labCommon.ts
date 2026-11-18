import type {Schemas} from '../../api/client';
import {problem, refuse, invalid} from '../api/http';
import {newId} from '../ids';
import {db} from '../store';
import {nowUtc} from '../time';
import {pathwayPatient} from './common';

/** What the lab's handlers (`lab.ts`, `labResults.ts`) and referrals share: the patient's flags, times and notifications. */

const DAY_MS = 86400_000;

/** The patient's record flags, as stored (an admin may restrict a record in the full version). */
export function patientFlags(patientId: string): Schemas['PatientFlagsResponse'] {
  const patient = db().patients.find((p) => p.id === patientId);
  return {isRestricted: patient?.isRestricted ?? false, isDisputed: patient?.isDisputed ?? false, isAnonymised: patient?.isAnonymised ?? false};
}

/** The patient as a specimen, result or referral carries them, with their flags. */
export const patientBanner = (patientId: string): Schemas['PathwayPatientResponse'] => ({...pathwayPatient(patientId), ...patientFlags(patientId)});

/** `PatientRules.WhyCantChange`: a restricted or anonymised record refuses changes. */
export function whyRecordCantChange(patientId: string): string | null {
  const flags = patientFlags(patientId);
  if (flags.isAnonymised) return "This patient's record has been anonymised, so it can't be changed.";
  if (flags.isRestricted) return "This patient's record is restricted, so it can't be changed until an admin lifts the restriction.";
  return null;
}

/** A 409 unless the change starts from the version saved now. */
export function ensureVersion(expected: number | undefined, actual: number, message: string) {
  if (expected !== undefined && expected !== actual) refuse(problem(409, message));
}

/** Whole days between two UTC moments (date arithmetic, nothing clinical). */
export const wholeDaysBetween = (fromUtc: string, toUtc: string) => Math.max(0, Math.floor((Date.parse(toUtc) - Date.parse(fromUtc)) / DAY_MS));

export const addDaysUtc = (isoUtc: string, days: number) => new Date(Date.parse(isoUtc) + days * DAY_MS).toISOString();

const UK_DATE = new Intl.DateTimeFormat('en-GB', {day: 'numeric', month: 'short', year: 'numeric', timeZone: 'Europe/London'});

/** A UTC moment's UK date as messages show it: "16 Nov 2026". */
export const ukShortDate = (isoUtc: string) => UK_DATE.format(new Date(isoUtc));

/**
 * A step's time (`SpecimenRules.StepTime`): now when none is given, else at most 7 days back, never in the future and
 * never before the step before. A 400 on `field` otherwise.
 */
export function stepTime(atUtc: string | null | undefined, previousUtc: string | null, step: string, field: string): string {
  const now = nowUtc();
  if (!atUtc) return now;
  const at = Date.parse(atUtc);
  if (Number.isNaN(at)) refuse(invalid({[field]: [`Give the time it was ${step}.`]}));
  if (at > Date.parse(now) + 60_000) refuse(invalid({[field]: [`The time it was ${step} can't be in the future.`]}));
  if (at < Date.parse(now) - 7 * DAY_MS) refuse(invalid({[field]: [`The time it was ${step} can be at most 7 days ago.`]}));
  if (previousUtc && at < Date.parse(previousUtc)) refuse(invalid({[field]: [`The time it was ${step} can't be before the step before it.`]}));
  return new Date(at).toISOString();
}

/** Free text: trimmed, or null when empty. A 400 on `field` when it's longer than `max`. */
export function optionalText(value: string | null | undefined, max: number, field: string, what: string): string | null {
  const text = value?.trim() || null;
  if (text && text.length > max) refuse(invalid({[field]: [`${what} must be at most ${max.toLocaleString('en-GB')} characters.`]}));
  return text;
}

/** "a Skin biopsy (punch)", or "an Endometrial biopsy". */
export const withArticle = (name: string) => (/^[AEIOU]/i.test(name) ? 'an ' : 'a ') + name;

/** Tells each person (but never the actor, and each once) with a fixed title and a message holding no patient details. */
export function notify(
  userIds: (string | null | undefined)[],
  actorId: string,
  notification: {kind: string; title: string; message: string; pathwayId?: string | null; subjectId: string},
) {
  const now = nowUtc();
  for (const userId of new Set(userIds))
    if (userId && userId !== actorId) db().notifications.push({id: newId(), userId, createdAtUtc: now, isRead: false, ...notification});
}
