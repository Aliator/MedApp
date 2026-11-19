import type {Schemas} from '../../api/client';
import {ADMIN_PERMISSIONS, type Permission} from '../../auth/permissions';
import {addDays, dayOfWeek, weekStart} from '../../utils/calendarSlots';
import {ADMIN, CARROW, PATHOLOGIST_ROLE, PEOPLE, STAFF, type DemoMembership} from '../accounts';
import {ACTIVITY_TYPES, id, PATHWAYS, PATIENTS, REFERRALS, ROOMS, SITES, TEAMS, STAGES, TEMPLATES, USERS} from '../ids';
import {ACTIVITY_TYPE_LIST, ROOM_LIST, SITE} from '../seed';
import type {BaseState} from '../state';
import {daysFrom, today, ukTime} from '../time';

/**
 * The light demo's state for setup and admin: practices, sites and rooms with their hours, activity types, closed days,
 * calendar templates, clinic sessions and patterns, staff working hours, team memberships, roles, monitoring
 * rules, the activity log, scheduled jobs, the security settings and the retention review. Fixed IDs are kinds 0xf0
 * to 0xf8 (`ids.ts`). Everything is synthetic: invented practices and sites, `ZZ` postcodes, Ofcom's drama numbers.
 */

/** GP practices in the directory. */
export const PRACTICES = {
  exampleLane: id(0xf0, 1),
  harbourView: id(0xf0, 2),
  /** Archived and nobody's practice, so it can be restored or deleted. */
  oldMill: id(0xf0, 3),
} as const;

/** The second site, beside the seed's `SITES.centre`, and its rooms. */
export const MORE_SITES = {
  northfield: id(0xf1, 1),
} as const;

export const MORE_ROOMS = {
  roomA: id(0xf1, 101),
  roomB: id(0xf1, 102),
} as const;

/** The roles (the API's starting roles, with fixed IDs of the demo's own). */
export const ROLES = {
  admin: id(0xf2, 1),
  clinician: id(0xf2, 2),
  seniorClinician: id(0xf2, 3),
  nurse: id(0xf2, 4),
  booking: id(0xf2, 5),
  lab: id(0xf2, 6),
  manager: id(0xf2, 7),
  pathologist: id(0xf2, 8),
} as const;

/** Clinic patterns; the sessions they made are in `CLINIC_SESSIONS`. */
export const CLINIC_PATTERNS = {
  /** Tom Hallworth's Tuesday morning skin clinic in Clinic room 1, 20-minute slots. */
  hallworthSkin: id(0xf3, 1),
  /** The admin's Tuesday afternoon diabetes clinic at Northfield, Room A, 15-minute slots. */
  adminDiabetes: id(0xf3, 2),
} as const;

export const CLINIC_SESSIONS = {
  /** Tom Hallworth's skin clinic next Tuesday (made by its pattern). */
  hallworthNextWeek: id(0xf3, 102),
  /** The admin's diabetes clinic next Tuesday (made by its pattern). */
  adminNextWeek: id(0xf3, 202),
  /** A one-off minor surgery session for Tom Hallworth next Thursday afternoon, in the Procedure room. */
  hallworthMinorSurgery: id(0xf3, 301),
} as const;

/** Day templates (their blocks are `id(0xf4, 100 * n + k)`). */
export const DAY_TEMPLATES = {
  /** The admin's early referral triage: used by the admin's usual week (so archived, never deleted). */
  adminEarlyTriage: id(0xf4, 1),
  /** Nadia Thornbury's skin clinic day (clinic, lunch, letters): used by no week, so it can be deleted. */
  thornburyClinicDay: id(0xf4, 2),
} as const;

export const WEEK_TEMPLATES = {
  /** The admin's usual week, their default: early triage on Tuesday, Wednesday and Friday. */
  adminUsualWeek: id(0xf5, 1),
} as const;

export const CLOSED_DAYS = {
  christmasDay: id(0xf6, 1),
  boxingDay: id(0xf6, 2),
  newYearsDay: id(0xf6, 3),
  /** Northfield only: a staff training day in about three weeks. */
  northfieldTraining: id(0xf6, 4),
} as const;

/** The admin's monitoring rules. */
export const MONITORING_RULES = {
  /** Anyone finishing Histology on a High or Urgent skin lesion pathway. */
  urgentHistology: id(0xf7, 1),
  /** Tom Hallworth putting a pathway on hold (switched off). */
  hallworthOnHold: id(0xf7, 2),
  /** A service alert: Dermatology referrals waiting more than two days for triage (nothing runs it in the demo). */
  dermatologyTriage: id(0xf7, 3),
  /** A service alert (MC19): any lab case past its due date (nothing runs it in the demo; one alert is seeded). */
  labCaseOverdue: id(0xf7, 4),
} as const;

export interface DemoSite {
  id: string;
  name: string;
  addressLines: string[];
  postcode: string | null;
  phone: string | null;
  isArchived: boolean;
  createdAt: string;
  lastUpdated: string;
  /** Its own opening hours, or null for the default (Monday to Friday, 08:00 to 18:00). */
  hours: {dayOfWeek: string; opens: string; closes: string}[] | null;
  hoursSince: string | null;
}

export interface DemoRoom {
  id: string;
  siteId: string;
  name: string;
  isArchived: boolean;
}

export interface DemoClosedDay {
  id: string;
  date: string;
  name: string;
  /** Null for everywhere. */
  siteId: string | null;
}

export interface DemoStaffHours {
  since: string;
  days: {dayOfWeek: string; starts: string; ends: string; siteId: string}[];
}

export interface DemoDayTemplate {
  id: string;
  ownerUserId: string;
  name: string;
  isArchived: boolean;
  blocks: {id: string; activityTypeId: string; startTime: string; endTime: string; note: string | null}[];
}

export const WEEKDAYS = ['monday', 'tuesday', 'wednesday', 'thursday', 'friday'] as const;
export type Weekday = (typeof WEEKDAYS)[number];

export interface DemoWeekTemplate {
  id: string;
  ownerUserId: string;
  name: string;
  isArchived: boolean;
  days: Record<Weekday, string | null>;
}

export interface DemoClinicPattern {
  id: string;
  staffUserId: string;
  roomId: string;
  teamId: string;
  dayOfWeek: string;
  starts: string;
  ends: string;
  slotMinutes: number;
  everyWeeks: number;
  fromDate: string;
  untilDate: string | null;
  lastUpdated: string;
}

export interface DemoClinicSession {
  id: string;
  staffUserId: string;
  roomId: string;
  teamId: string;
  patternId: string | null;
  date: string;
  startUtc: string;
  endUtc: string;
  slotMinutes: number;
  status: 'Planned' | 'Cancelled';
  cancelReason: string | null;
  cancelledAtUtc: string | null;
  lastUpdated: string;
  /** The appointments cancelled with it, for its list of patients to tell. */
  cancelledAppointmentIds: string[];
}

export interface DemoRole {
  id: string;
  name: string;
  description: string;
  isBuiltIn: boolean;
  isArchived: boolean;
  permissions: string[];
  version: number;
  createdAt: string;
  lastUpdated: string;
}

export type DemoMonitoringRule = Omit<Schemas['MonitoringRuleResponse'], 'subjectUsername' | 'templateName' | 'stageName'> & {
  id: string;
  ownerUserId: string;
};

/** An activity log entry as stored (the patient's name is looked up as it's answered). */
export type DemoActivityEntry = Omit<Schemas['ActivityEntryResponse'], 'patientName'> & {id: string; atUtc: string};

/** A record past its retention period (made up: the demo's own records are all recent), and any decision to keep it. */
export interface DemoRetention {
  patientId: string;
  conclusionOfTreatment: string;
  retentionClass: string;
  keptUntil: string | null;
  keptReason: string | null;
}

export interface SetupSlice {
  /** The GP practice directory (counts worked out per answer). */
  practices: Omit<Schemas['PracticeResponse'], 'patientCount' | 'referralCount' | 'canBeDeleted'>[];
  sites: DemoSite[];
  rooms: DemoRoom[];
  activityTypes: Schemas['ActivityTypeResponse'][];
  closedDays: DemoClosedDay[];
  /** Each person's own working hours, by user ID (none: the default). */
  staffHours: Record<string, DemoStaffHours>;
  dayTemplates: DemoDayTemplate[];
  weekTemplates: DemoWeekTemplate[];
  /** Each person's default week, by user ID. */
  defaultWeeks: Record<string, {weekTemplateId: string; since: string}>;
  /** Apply Template's day overrides, keyed `userId|date`: that day's day template, or null for none. */
  dayOverrides: Record<string, string | null>;
  /** Template blocks hidden on one day (changed or removed there), keyed `userId|date|templateBlockId`. */
  templateExceptions: string[];
  /** Day and week templates once used (by a week, an override or as someone's default): archived, never deleted. */
  usedTemplateIds: string[];
  clinicPatterns: DemoClinicPattern[];
  clinicSessions: DemoClinicSession[];
  /** Each person's teams and their role in each, by user ID. */
  memberships: Record<string, DemoMembership[]>;
  roles: DemoRole[];
  monitoringRules: DemoMonitoringRule[];
  activityLog: DemoActivityEntry[];
  jobs: Schemas['ScheduledJobResponse'][];
  securitySettings: {requireTwoFactorForAdmins: boolean; updatedAtUtc: string | null; updatedBy: string | null};
  retention: DemoRetention[];
}

/** A moment `days` before today at a UK time of day. */
const ago = (days: number, hours = 10, minutes = 0) => ukTime(daysFrom(-days), hours, minutes);

/** The first date on or after `from` that falls on `weekday` (1 is Monday). */
export function nextOn(from: string, weekday: number): string {
  let date = from;
  while (dayOfWeek(date) !== weekday) date = addDays(date, 1);
  return date;
}

/** The next `month`-`day` that's today or later (this year's, or next year's). */
function nextDate(month: number, day: number): string {
  const year = Number(today().slice(0, 4));
  const thisYear = `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
  return thisYear >= today() ? thisYear : `${year + 1}${thisYear.slice(4)}`;
}

/** A UK clock time, "HH:mm:ss". */
export const clock = (hours: number, minutes = 0) => `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}:00`;

/** The API's starting roles (`StandardRoles`): each one's name, description and permissions. */
const ROLE_SPECS: [keyof typeof ROLES, string, string, readonly Permission[]][] = [
  [
    'admin',
    'Admin',
    'Runs MedApp: accounts, roles, setup, privacy and oversight, reading every team’s records; no clinical changes. Built in, with fixed permissions.',
    ADMIN_PERMISSIONS,
  ],
  ['clinician', 'Clinician', 'Clinicians who triage referrals and lead pathways.', STAFF.permissions],
  [
    'seniorClinician',
    'Senior clinician',
    'Senior clinicians: as Clinician, and they unlock stages and finish one without its visit.',
    STAFF.permissions,
  ],
  [
    'nurse',
    'Nurse',
    'Nurses and healthcare assistants who work on pathways.',
    ['UseApp', 'ViewPatients', 'EditPatients', 'RegisterReferrals', 'CreatePathways', 'WorkPathways', 'RecordConsent', 'BookAppointments', 'AttachFiles', 'RecordSpecimens', 'WriteLetters', 'SendLetters'],
  ],
  [
    'booking',
    'Booking',
    'The booking team: patients, referrals, appointments and calendars.',
    ['UseApp', 'ViewPatients', 'EditPatients', 'DeletePatients', 'RegisterReferrals', 'BookAppointments', 'ManageAnyCalendar', 'ManageClinics', 'AttachFiles', 'SendLetters'],
  ],
  ['lab', 'Lab', 'Laboratory staff who work on their pathway stages.', ['UseApp', 'ViewPatients', 'WorkPathways', 'AttachFiles', 'RecordSpecimens', 'ProcessSpecimens']],
  ['pathologist', 'Pathologist', 'Pathologists: as Lab, and they enter lab cases’ results and authorise their reports.', PATHOLOGIST_ROLE],
  [
    'manager',
    'Manager',
    'Service managers: assigning work, meetings, Insights and monitoring.',
    ['UseApp', 'ViewPatients', 'AssignPathways', 'ArrangeMeetings', 'ManageClinics', 'ViewInsights', 'ManageMonitoring'],
  ],
];

function seedPractices(state: BaseState, created: string): SetupSlice['practices'] {
  // Some patients' GP practices.
  const practiceOf: [string, string][] = [
    [PATIENTS.ellisVarden, PRACTICES.exampleLane],
    [PATIENTS.priyaHolloway, PRACTICES.exampleLane],
    [PATIENTS.harrietQuill, PRACTICES.exampleLane],
    [PATIENTS.margaretFenwick, PRACTICES.harbourView],
    [PATIENTS.oliverMaddox, PRACTICES.harbourView],
  ];
  for (const [patientId, practiceId] of practiceOf) {
    const patient = state.patients.find((p) => p.id === patientId);
    if (patient) patient.practiceId = practiceId;
  }
  const practice = (
    practiceId: string,
    name: string,
    odsCode: string | null,
    addressLines: string[],
    postcode: string,
    phone: string | null,
    isArchived = false,
  ) => ({
    id: practiceId,
    name,
    odsCode,
    addressLines,
    postcode,
    phone,
    secureEmail: null,
    isArchived,
    version: isArchived ? 2 : 1,
    createdAt: created,
    lastUpdated: isArchived ? ago(40) : created,
  });
  return [
    practice(PRACTICES.exampleLane, 'Example Lane Surgery (synthetic)', 'DEMO01', ['1 Example Lane', 'Exampleton'], 'ZZ1 1AA', '01632 960001'),
    practice(PRACTICES.harbourView, 'Harbour View Medical Practice (synthetic)', 'DEMO02', ['22 Quay Street', 'Exampleton'], 'ZZ2 2BB', '01632 960002'),
    practice(PRACTICES.oldMill, 'Old Mill Health Centre (synthetic)', null, ['Mill Road', 'Sampleford'], 'ZZ3 3CC', null, true),
  ];
}

function seedClinics(): Pick<SetupSlice, 'clinicPatterns' | 'clinicSessions'> {
  const thisMonday = weekStart(today());
  const pattern = (patternId: string, staffUserId: string, roomId: string, teamId: string, starts: string, ends: string, slotMinutes: number) => ({
    id: patternId,
    staffUserId,
    roomId,
    teamId,
    dayOfWeek: 'Tuesday',
    starts,
    ends,
    slotMinutes,
    everyWeeks: 1,
    fromDate: addDays(thisMonday, -27),
    untilDate: null,
    lastUpdated: ago(30),
  });
  const clinicPatterns: DemoClinicPattern[] = [
    pattern(CLINIC_PATTERNS.hallworthSkin, USERS.hallworth, ROOMS.clinicRoom1, TEAMS.dermatology, clock(9), clock(12), 20),
    pattern(CLINIC_PATTERNS.adminDiabetes, CARROW.id, MORE_ROOMS.roomA, TEAMS.diabetes, clock(13, 30), clock(15, 30), 15),
  ];
  // The sessions the patterns made for this week and the next two, and a one-off session.
  const session = (
    n: number,
    patternId: string | null,
    staffUserId: string,
    roomId: string,
    teamId: string,
    date: string,
    from: [number, number],
    to: [number, number],
    slotMinutes: number,
  ): DemoClinicSession => ({
    id: id(0xf3, n),
    staffUserId,
    roomId,
    teamId,
    patternId,
    date,
    startUtc: ukTime(date, ...from),
    endUtc: ukTime(date, ...to),
    slotMinutes,
    status: 'Planned',
    cancelReason: null,
    cancelledAtUtc: null,
    lastUpdated: ago(30),
    cancelledAppointmentIds: [],
  });
  const clinicSessions: DemoClinicSession[] = [];
  for (const week of [0, 1, 2]) {
    const tuesday = addDays(thisMonday, week * 7 + 1);
    clinicSessions.push(
      session(101 + week, CLINIC_PATTERNS.hallworthSkin, USERS.hallworth, ROOMS.clinicRoom1, TEAMS.dermatology, tuesday, [9, 0], [12, 0], 20),
      session(201 + week, CLINIC_PATTERNS.adminDiabetes, CARROW.id, MORE_ROOMS.roomA, TEAMS.diabetes, tuesday, [13, 30], [15, 30], 15),
    );
  }
  clinicSessions.push(
    session(301, null, USERS.hallworth, ROOMS.procedureRoom, TEAMS.dermatology, addDays(thisMonday, 10), [13, 30], [16, 30], 30),
  );
  return {clinicPatterns, clinicSessions};
}

function seedActivityLog(): DemoActivityEntry[] {
  // Who signed in, and who viewed, listed or changed which patient's record (field names only, never what was seen).
  let n = 0;
  const log: DemoActivityEntry[] = [];
  const add = (atUtc: string, action: string, userId: string, more: Partial<DemoActivityEntry> = {}) =>
    log.push({
      id: id(0xf8, ++n),
      atUtc,
      action,
      userId,
      username: PEOPLE.find((p) => p.id === userId)?.username ?? null,
      ipAddress: '192.0.2.10',
      patientId: null,
      pathwayId: null,
      targetUsername: null,
      details: null,
      ...more,
    });
  add(ago(3, 8, 55), 'SignedIn', STAFF.id);
  add(ago(3, 9, 2), 'PatientViewed', STAFF.id, {patientId: PATIENTS.oliverMaddox});
  add(ago(3, 9, 4), 'ReferralViewed', STAFF.id, {patientId: PATIENTS.oliverMaddox, details: JSON.stringify({referralId: REFERRALS.dermatologyToTriage})});
  add(ago(3, 14, 10), 'PathwayViewed', USERS.hallworth, {patientId: PATIENTS.samuelOkafor, pathwayId: PATHWAYS.onHold});
  add(ago(2, 8, 40), 'SignedIn', ADMIN.id);
  add(ago(2, 8, 45), 'PatientListed', ADMIN.id, {patientId: PATIENTS.margaretFenwick});
  add(ago(2, 8, 46), 'PatientViewed', ADMIN.id, {patientId: PATIENTS.margaretFenwick});
  add(ago(2, 11, 0), 'PatientUpdated', USERS.marsh, {patientId: PATIENTS.ellisVarden, details: JSON.stringify({fields: ['mobilePhone']})});
  add(ago(2, 16, 30), 'PatientViewed', ADMIN.id, {patientId: PATIENTS.graceAbernethy});
  add(ago(1, 9, 5), 'SignInFailed', USERS.whitlock, {details: JSON.stringify({reason: 'InvalidCredentials'})});
  add(ago(1, 9, 6), 'SignedIn', USERS.whitlock);
  add(ago(1, 10, 15), 'TeamMembershipChanged', ADMIN.id, {targetUsername: 't.hallworth', details: JSON.stringify({from: [], to: ['Dermatology']})});
  add(ago(1, 15, 20), 'PatientViewed', STAFF.id, {patientId: PATIENTS.harrietQuill});
  add(ago(1, 15, 22), 'PathwayViewed', STAFF.id, {patientId: PATIENTS.harrietQuill, pathwayId: PATHWAYS.consentToRecord});
  add(ago(0, 8, 30), 'SignedIn', STAFF.id);
  add(ago(0, 8, 35), 'PatientListed', STAFF.id, {patientId: PATIENTS.priyaHolloway});
  add(ago(0, 8, 36), 'PathwayViewed', STAFF.id, {patientId: PATIENTS.priyaHolloway, pathwayId: PATHWAYS.histologyToFinish});
  return log;
}

function seedJobs(): Schemas['ScheduledJobResponse'][] {
  // The last runs are made up: the purge early this morning, reminders at the last quarter hour (switched off).
  const purgeRun = ukTime(today(), 4, 30);
  const remindersRun = new Date(Math.floor(Date.now() / 900_000) * 900_000).toISOString();
  return [
    {
      name: 'operational-purge',
      title: 'Operational data purge',
      description:
        'Dry run (Retention:Operational:DryRun): counts what it would delete, and deletes nothing. Deletes sessions after 12 months, sign-in and security events after 12 months, notifications after 6 months, notepads unchanged for 30 days, and expired sign-in codes. Never patient records.',
      schedule: 'Daily at 04:30 (UK time)',
      enabled: true,
      isRunning: false,
      isOverdue: false,
      nextRunUtc: ukTime(daysFrom(1), 4, 30),
      lastStartedAtUtc: purgeRun,
      lastFinishedAtUtc: new Date(Date.parse(purgeRun) + 2300).toISOString(),
      lastOutcome: 'Succeeded',
      lastCount: 14,
      consecutiveFailures: 0,
      lastErrorType: null,
    },
    {
      name: 'patient-reminders',
      title: 'Patient reminders',
      description: 'Off (PatientMessages:Mode): makes and sends no texts or emails to patients.',
      schedule: 'Every 15 minutes',
      enabled: true,
      isRunning: false,
      isOverdue: false,
      nextRunUtc: new Date(Date.parse(remindersRun) + 900_000).toISOString(),
      lastStartedAtUtc: remindersRun,
      lastFinishedAtUtc: new Date(Date.parse(remindersRun) + 180).toISOString(),
      lastOutcome: 'Succeeded',
      lastCount: 0,
      consecutiveFailures: 0,
      lastErrorType: null,
    },
  ];
}

/** Seeds this slice, after the base seed (`state`: read it, or add to it, as the clinic's other records). */
export function seedSetup(state: BaseState): SetupSlice {
  const created = ago(200, 9, 0);
  const weekdays = (days: string[], opens: string, closes: string) => days.map((dayOfWeek) => ({dayOfWeek, opens, closes}));
  return {
    practices: seedPractices(state, created),
    sites: [
      {
        id: SITE.id,
        name: SITE.name,
        addressLines: ['1 Example Way', 'Exampleton'],
        postcode: 'ZZ1 4DD',
        phone: '01632 960100',
        isArchived: false,
        createdAt: created,
        lastUpdated: created,
        hours: null,
        hoursSince: null,
      },
      {
        id: MORE_SITES.northfield,
        name: 'Northfield Health Centre (synthetic)',
        addressLines: ['5 Sample Street', 'Northfield'],
        postcode: 'ZZ5 5EE',
        phone: '01632 960200',
        isArchived: false,
        createdAt: created,
        lastUpdated: created,
        hours: weekdays(['Monday', 'Tuesday', 'Wednesday', 'Thursday'], clock(8, 30), clock(17)),
        hoursSince: daysFrom(-60),
      },
    ],
    rooms: [
      ...ROOM_LIST.map((room) => ({id: room.id, siteId: SITES.centre, name: room.name, isArchived: false})),
      {id: MORE_ROOMS.roomA, siteId: MORE_SITES.northfield, name: 'Room A', isArchived: false},
      {id: MORE_ROOMS.roomB, siteId: MORE_SITES.northfield, name: 'Room B', isArchived: false},
    ],
    activityTypes: ACTIVITY_TYPE_LIST.map((type) => ({...type})),
    closedDays: [
      {id: CLOSED_DAYS.christmasDay, date: nextDate(12, 25), name: 'Christmas Day', siteId: null},
      {id: CLOSED_DAYS.boxingDay, date: nextDate(12, 26), name: 'Boxing Day', siteId: null},
      {id: CLOSED_DAYS.newYearsDay, date: nextDate(1, 1), name: "New Year's Day", siteId: null},
      {id: CLOSED_DAYS.northfieldTraining, date: nextOn(daysFrom(21), 3), name: 'Staff training day', siteId: MORE_SITES.northfield},
    ],
    // Tom Hallworth works Tuesday to Thursday at the centre; everyone else the default.
    staffHours: {
      [USERS.hallworth]: {
        since: daysFrom(-90),
        days: ['Tuesday', 'Wednesday', 'Thursday'].map((dayOfWeek) => ({dayOfWeek, starts: clock(9), ends: clock(17), siteId: SITES.centre})),
      },
    },
    dayTemplates: [
      {
        id: DAY_TEMPLATES.adminEarlyTriage,
        ownerUserId: ADMIN.id,
        name: 'Early triage',
        isArchived: false,
        blocks: [{id: id(0xf4, 101), activityTypeId: ACTIVITY_TYPES.triage, startTime: clock(8, 15), endTime: clock(9), note: 'Referral triage'}],
      },
      {
        id: DAY_TEMPLATES.thornburyClinicDay,
        ownerUserId: STAFF.id,
        name: 'Skin clinic day',
        isArchived: false,
        blocks: [
          {id: id(0xf4, 201), activityTypeId: ACTIVITY_TYPES.clinic, startTime: clock(9), endTime: clock(12, 30), note: 'Skin clinic'},
          {id: id(0xf4, 202), activityTypeId: ACTIVITY_TYPES.lunch, startTime: clock(12, 30), endTime: clock(13, 15), note: null},
          {id: id(0xf4, 203), activityTypeId: ACTIVITY_TYPES.admin, startTime: clock(16), endTime: clock(17), note: 'Letters'},
        ],
      },
    ],
    weekTemplates: [
      {
        id: WEEK_TEMPLATES.adminUsualWeek,
        ownerUserId: ADMIN.id,
        name: 'Usual week',
        isArchived: false,
        days: {
          monday: null,
          tuesday: DAY_TEMPLATES.adminEarlyTriage,
          wednesday: DAY_TEMPLATES.adminEarlyTriage,
          thursday: null,
          friday: DAY_TEMPLATES.adminEarlyTriage,
        },
      },
    ],
    defaultWeeks: {[ADMIN.id]: {weekTemplateId: WEEK_TEMPLATES.adminUsualWeek, since: daysFrom(-30)}},
    dayOverrides: {},
    templateExceptions: [],
    usedTemplateIds: [DAY_TEMPLATES.adminEarlyTriage, WEEK_TEMPLATES.adminUsualWeek],
    ...seedClinics(),
    memberships: Object.fromEntries(PEOPLE.map((person) => [person.id, person.teams.map((t) => ({...t}))])),
    roles: ROLE_SPECS.map(([key, name, description, permissions]) => ({
      id: ROLES[key],
      name,
      description,
      isBuiltIn: key === 'admin',
      isArchived: false,
      permissions: [...permissions],
      version: 1,
      createdAt: created,
      lastUpdated: created,
    })),
    monitoringRules: [
      {
        id: MONITORING_RULES.urgentHistology,
        ownerUserId: ADMIN.id,
        subjectUserId: null,
        event: 'StageFinished',
        minimumPriority: 'High',
        templateId: TEMPLATES.skinLesion,
        stageDefinitionId: STAGES.histology,
        isEnabled: true,
        createdAtUtc: ago(20),
        updatedAtUtc: ago(20),
      },
      {
        id: MONITORING_RULES.hallworthOnHold,
        ownerUserId: ADMIN.id,
        subjectUserId: USERS.hallworth,
        event: 'PathwayBlocked',
        minimumPriority: null,
        templateId: null,
        stageDefinitionId: null,
        isEnabled: false,
        createdAtUtc: ago(15),
        updatedAtUtc: ago(4),
      },
      {
        id: MONITORING_RULES.dermatologyTriage,
        ownerUserId: ADMIN.id,
        subjectUserId: null,
        event: 'ReferralNotTriaged',
        minimumPriority: null,
        templateId: null,
        stageDefinitionId: null,
        thresholdDays: 2,
        teamId: TEAMS.dermatology,
        isEnabled: true,
        createdAtUtc: ago(10),
        updatedAtUtc: ago(10),
      },
      {
        id: MONITORING_RULES.labCaseOverdue,
        ownerUserId: ADMIN.id,
        subjectUserId: null,
        event: 'LabCaseOverdue',
        minimumPriority: null,
        templateId: null,
        stageDefinitionId: null,
        thresholdDays: 0,
        teamId: null,
        isEnabled: true,
        createdAtUtc: ago(10),
        updatedAtUtc: ago(10),
      },
    ],
    activityLog: seedActivityLog(),
    jobs: seedJobs(),
    securitySettings: {requireTwoFactorForAdmins: false, updatedAtUtc: null, updatedBy: null},
    // Grace Abernethy's care is made out to have ended over eight years ago, so the review has a record to show.
    retention: [
      {
        patientId: PATIENTS.graceAbernethy,
        conclusionOfTreatment: addDays(today(), -(8 * 365 + 95)),
        retentionClass: 'Adult',
        keptUntil: null,
        keptReason: null,
      },
    ],
  };
}
