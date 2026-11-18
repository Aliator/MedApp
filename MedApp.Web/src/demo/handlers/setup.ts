import type {Schemas} from '../../api/client';
import {PEOPLE} from '../accounts';
import {DemoReply, invalid, notFound, on, problem, refuse} from '../api/http';
import {newId} from '../ids';
import type {DemoRoom, DemoSite} from '../slices/setup';
import {db} from '../store';
import {nowUtc, today} from '../time';
import {registerAdminAccess} from './adminAccess';
import {registerAdminLogs} from './adminLogs';
import {me, requirePermission} from './common';
import {DAY_NAMES, DEFAULT_END, DEFAULT_START, registerSetupCalendar} from './setupCalendar';
import {registerSetupPrivacy} from './setupPrivacy';

/**
 * Setup and admin pages: the GP practice directory, sites and rooms with their opening hours, and specialties (with
 * `setupCalendar.ts`'s calendar setup and clinics, `adminAccess.ts`'s users and roles, `adminLogs.ts`'s logs,
 * monitoring and jobs, and `setupPrivacy.ts`'s patient privacy). Lists change as the API's do: a change names the
 * version it saw where the API takes one (a 409 when stale), and what's in use is archived, never deleted.
 */

const STALE = 'Someone else changed this meanwhile. Look at it again before changing it.';

// GP practices.

function practiceResponse(practice: SetupPractice): Schemas['PracticeResponse'] {
  const state = db();
  const patientCount = state.patients.filter((p) => p.practiceId === practice.id).length;
  // Referrals name their practice in words in the light demo; one with a directory link would count too.
  const referralCount = state.referrals.filter((r) => (r as {practiceId?: string | null}).practiceId === practice.id).length;
  return {...practice, patientCount, referralCount, canBeDeleted: patientCount === 0 && referralCount === 0};
}
type SetupPractice = ReturnType<typeof db>['practices'][number];

/** A practice summary, as a patient carries theirs. */
export function practiceSummary(practiceId: string | null | undefined): Schemas['PracticeSummaryResponse'] | undefined {
  const practice = db().practices.find((p) => p.id === practiceId);
  if (!practice) return undefined;
  const {id, name, odsCode, addressLines, postcode, phone, isArchived} = practice;
  return {id, name, odsCode, addressLines, postcode, phone, isArchived};
}

function findPractice(practiceId: string): SetupPractice {
  const practice = db().practices.find((p) => p.id === practiceId);
  if (!practice) refuse(notFound('The practice'));
  return practice;
}

function practiceInput(body: Partial<Schemas['CreatePracticeRequest']>, exceptId?: string) {
  const name = body.name?.trim() ?? '';
  const odsCode = body.odsCode?.replace(/\s/g, '').toUpperCase() || null;
  const errors: Record<string, string[]> = {};
  if (!name || name.length > 200) errors.name = ['Give the practice a name (up to 200 characters).'];
  if (odsCode && !/^[A-Z0-9]{3,10}$/.test(odsCode)) errors.odsCode = ['An ODS code is 3 to 10 letters and digits.'];
  if ((body.addressLines ?? []).length > 3) errors.addressLines = ['Up to three lines of address.'];
  if (body.secureEmail && !/@nhs\.net$/i.test(body.secureEmail.trim())) errors.secureEmail = ['A secure email address ends @nhs.net.'];
  if (Object.keys(errors).length > 0) refuse(invalid(errors));
  if (odsCode && db().practices.some((p) => p.id !== exceptId && p.odsCode === odsCode))
    refuse(problem(409, `Another practice has the ODS code ${odsCode}.`));
  return {
    name,
    odsCode,
    addressLines: (body.addressLines ?? []).map((l) => l.trim()).filter(Boolean),
    postcode: body.postcode?.trim().toUpperCase() || null,
    phone: body.phone?.trim() || null,
    secureEmail: body.secureEmail?.trim() || null,
  };
}

function checkVersion(current: number | undefined, expected: number | undefined) {
  if (expected !== undefined && expected !== current) refuse(problem(409, STALE));
}

// Sites and rooms.

function roomInUse(room: DemoRoom): boolean {
  const state = db();
  return (
    state.clinicSessions.some((s) => s.roomId === room.id) ||
    state.clinicPatterns.some((p) => p.roomId === room.id) ||
    state.appointments.some((a) => a.roomId === room.id)
  );
}

function roomResponse(room: DemoRoom): Schemas['RoomResponse'] {
  const site = db().sites.find((s) => s.id === room.siteId);
  return {...room, siteName: site?.name ?? null, canBeDeleted: !roomInUse(room)};
}

function siteResponse(site: DemoSite, includeArchived = true): Schemas['SiteResponse'] {
  const state = db();
  const rooms = state.rooms.filter((r) => r.siteId === site.id);
  return {
    id: site.id,
    name: site.name,
    addressLines: site.addressLines,
    postcode: site.postcode,
    phone: site.phone,
    isArchived: site.isArchived,
    canBeDeleted: rooms.length === 0 && !state.closedDays.some((c) => c.siteId === site.id),
    rooms: rooms
      .filter((r) => includeArchived || !r.isArchived)
      .sort((a, b) => a.name.localeCompare(b.name))
      .map(roomResponse),
    createdAt: site.createdAt,
    lastUpdated: site.lastUpdated,
  };
}

function findSite(siteId: string): DemoSite {
  const site = db().sites.find((s) => s.id === siteId);
  if (!site) refuse(notFound('The site'));
  return site;
}

function findRoom(roomId: string): DemoRoom {
  const room = db().rooms.find((r) => r.id === roomId);
  if (!room) refuse(notFound('The room'));
  return room;
}

function siteInput(body: Partial<Schemas['SiteRequest']>, exceptId?: string) {
  const name = body.name?.trim() ?? '';
  if (!name || name.length > 100) refuse(invalid({name: ['Give the site a name (up to 100 characters).']}));
  if (db().sites.some((s) => s.id !== exceptId && s.name.toLowerCase() === name.toLowerCase()))
    refuse(problem(409, `There's already a site called '${name}'.`));
  return {
    name,
    addressLines: (body.addressLines ?? []).map((l) => l.trim()).filter(Boolean),
    postcode: body.postcode?.trim().toUpperCase() || null,
    phone: body.phone?.trim() || null,
  };
}

function roomName(name: string | null | undefined, siteId: string, exceptId?: string) {
  const trimmed = name?.trim() ?? '';
  if (!trimmed || trimmed.length > 100) refuse(invalid({name: ['Give the room a name (up to 100 characters).']}));
  if (db().rooms.some((r) => r.id !== exceptId && r.siteId === siteId && r.name.toLowerCase() === trimmed.toLowerCase()))
    refuse(problem(409, `There's already a room called '${trimmed}' at this site.`));
  return trimmed;
}

function siteHoursResponse(site: DemoSite): Schemas['SiteHoursResponse'] {
  return {
    siteId: site.id,
    hasOwnHours: site.hours !== null,
    since: site.hoursSince,
    days: site.hours ?? DAY_NAMES.slice(0, 5).map((dayOfWeek) => ({dayOfWeek, opens: DEFAULT_START, closes: DEFAULT_END})),
  };
}

// Specialties.

type Specialty = Schemas['SpecialtyResponse'];

/** A specialty with its counts as they are now (`SpecialtyUsage`): what has it, and who can triage its referrals. */
export function specialtyResponse(specialty: Specialty): Specialty {
  const state = db();
  const members = PEOPLE.filter((p) => (state.memberships[p.id] ?? []).includes(specialty.id ?? ''));
  const counts = {
    templateCount: state.templates.filter((t) => t.specialtyId === specialty.id).length,
    pathwayCount: state.pathways.filter((p) => p.specialtyId === specialty.id).length,
    referralCount: state.referrals.filter((r) => r.specialtyId === specialty.id).length,
    memberCount: members.length,
    triagerCount: members.filter((p) => p.permissions.includes('TriageReferrals')).length,
  };
  const inUse = Object.values(counts).some((n) => n > 0) || (specialty.letterTemplateCount ?? 0) > 0;
  return {...specialty, ...counts, canBeDeleted: !inUse};
}

function findSpecialty(specialtyId: string): Specialty {
  const specialty = db().specialties.find((s) => s.id === specialtyId);
  if (!specialty) refuse(notFound('The specialty'));
  return specialty;
}

function specialtyInput(body: Partial<Schemas['UpdateSpecialtyRequest']>, current?: Specialty) {
  const name = body.name?.trim() ?? '';
  const errors: Record<string, string[]> = {};
  if (!name || name.length > 100) errors.name = ['Give the specialty a name (up to 100 characters).'];
  const targets = {
    targetDaysUrgent: body.targetDaysUrgent ?? current?.targetDaysUrgent ?? 14,
    targetDaysHigh: body.targetDaysHigh ?? current?.targetDaysHigh ?? 28,
    targetDaysMedium: body.targetDaysMedium ?? current?.targetDaysMedium ?? 126,
    targetDaysLow: body.targetDaysLow ?? current?.targetDaysLow ?? 126,
  };
  for (const [field, days] of Object.entries(targets)) if (days < 1 || days > 365) errors[field] = ['A target is 1 to 365 days.'];
  if (Object.keys(errors).length > 0) refuse(invalid(errors));
  if (db().specialties.some((s) => s.id !== current?.id && (s.name ?? '').toLowerCase() === name.toLowerCase()))
    refuse(problem(409, `There's already a specialty called '${name}'.`));
  return {name, ...targets};
}

export function registerSetup() {
  registerSetupCalendar();
  registerAdminAccess();
  registerAdminLogs();
  registerSetupPrivacy();

  // The GP practice directory: organisations, so reading it records nothing.
  on('get', '/api/practices', ({query}) => {
    requirePermission('ViewPatients');
    return db()
      .practices.filter((p) => query.get('includeArchived') === 'true' || !p.isArchived)
      .sort((a, b) => (a.name ?? '').localeCompare(b.name ?? ''))
      .map(practiceResponse);
  });
  on('get', '/api/practices/{id}', ({params}) => {
    requirePermission('ViewPatients');
    return practiceResponse(findPractice(params.id));
  });
  on('post', '/api/practices', ({body}) => {
    requirePermission('EditPatients');
    const now = nowUtc();
    const practice: SetupPractice = {id: newId(), ...practiceInput(body), isArchived: false, version: 1, createdAt: now, lastUpdated: now};
    db().practices.push(practice);
    return new DemoReply(201, practiceResponse(practice));
  });
  on('put', '/api/practices/{id}', ({params, body}) => {
    requirePermission('EditPatients');
    const practice = findPractice(params.id);
    checkVersion(practice.version, body.expectedVersion);
    Object.assign(practice, practiceInput(body, practice.id), {version: (practice.version ?? 1) + 1, lastUpdated: nowUtc()});
    return practiceResponse(practice);
  });
  for (const [path, archived] of [
    ['/api/practices/{id}/archive', true],
    ['/api/practices/{id}/restore', false],
  ] as const)
    on('post', path, ({params, body}) => {
      requirePermission('EditPatients');
      const practice = findPractice(params.id);
      checkVersion(practice.version, body.expectedVersion);
      Object.assign(practice, {isArchived: archived, version: (practice.version ?? 1) + 1, lastUpdated: nowUtc()});
      return practiceResponse(practice);
    });
  on('delete', '/api/practices/{id}', ({params}) => {
    requirePermission('EditPatients');
    const practice = practiceResponse(findPractice(params.id));
    if (!practice.canBeDeleted) return problem(409, `Patients or referrals have '${practice.name}', so it can't be deleted: archive it instead.`);
    db().practices = db().practices.filter((p) => p.id !== practice.id);
    return undefined;
  });

  // Sites and rooms.
  on('get', '/api/sites', ({query}) => {
    me();
    const includeArchived = query.get('includeArchived') === 'true';
    return db()
      .sites.filter((s) => includeArchived || !s.isArchived)
      .sort((a, b) => a.name.localeCompare(b.name))
      .map((s) => siteResponse(s, includeArchived));
  });
  on('get', '/api/sites/{id}', ({params}) => {
    me();
    return siteResponse(findSite(params.id));
  });
  on('post', '/api/sites', ({body}) => {
    requirePermission('ManageSetup');
    const now = nowUtc();
    const site: DemoSite = {id: newId(), ...siteInput(body), isArchived: false, createdAt: now, lastUpdated: now, hours: null, hoursSince: null};
    db().sites.push(site);
    return new DemoReply(201, siteResponse(site));
  });
  on('put', '/api/sites/{id}', ({params, body}) => {
    requirePermission('ManageSetup');
    const site = findSite(params.id);
    Object.assign(site, siteInput(body, site.id), {lastUpdated: nowUtc()});
    return siteResponse(site);
  });
  on('post', '/api/sites/{id}/archive', ({params}) => {
    requirePermission('ManageSetup');
    const site = findSite(params.id);
    if (!db().sites.some((s) => s.id !== site.id && !s.isArchived)) return problem(409, 'There must always be at least one open site.');
    Object.assign(site, {isArchived: true, lastUpdated: nowUtc()});
    return siteResponse(site);
  });
  on('post', '/api/sites/{id}/restore', ({params}) => {
    requirePermission('ManageSetup');
    const site = findSite(params.id);
    Object.assign(site, {isArchived: false, lastUpdated: nowUtc()});
    return siteResponse(site);
  });
  on('delete', '/api/sites/{id}', ({params}) => {
    requirePermission('ManageSetup');
    const site = findSite(params.id);
    if (!siteResponse(site).canBeDeleted) return problem(409, `'${site.name}' has rooms or closed days, so it can't be deleted: archive it instead.`);
    if (!db().sites.some((s) => s.id !== site.id && !s.isArchived)) return problem(409, 'There must always be at least one open site.');
    db().sites = db().sites.filter((s) => s.id !== site.id);
    return undefined;
  });
  on('get', '/api/sites/{id}/hours', ({params}) => {
    me();
    return siteHoursResponse(findSite(params.id));
  });
  on('put', '/api/sites/{id}/hours', ({params, body}) => {
    requirePermission('ManageSetup');
    const site = findSite(params.id);
    const days: NonNullable<DemoSite['hours']> = [];
    for (const d of body.days ?? []) {
      const opens = /^\d{2}:\d{2}/.test(d.opens ?? '') ? `${d.opens!.slice(0, 5)}:00` : '';
      const closes = /^\d{2}:\d{2}/.test(d.closes ?? '') ? `${d.closes!.slice(0, 5)}:00` : '';
      if (!d.dayOfWeek || !DAY_NAMES.includes(d.dayOfWeek) || !opens || !closes || closes <= opens || opens < '06:00' || closes > '23:00:00')
        return invalid({days: ['Each day opens and closes between 06:00 and 23:00, closing after it opens.']});
      if (days.some((x) => x.dayOfWeek === d.dayOfWeek)) return invalid({days: ['Each day of the week once.']});
      days.push({dayOfWeek: d.dayOfWeek, opens, closes});
    }
    // Hours that would leave someone working there while it's closed are refused (`StaffHoursRules`).
    for (const [userId, hours] of Object.entries(db().staffHours))
      for (const row of hours.days.filter((r) => r.siteId === site.id)) {
        const day = days.length === 0 ? (DAY_NAMES.indexOf(row.dayOfWeek) < 5 ? {opens: DEFAULT_START, closes: DEFAULT_END} : undefined) : days.find((x) => x.dayOfWeek === row.dayOfWeek);
        if (!day || row.starts < day.opens || row.ends > day.closes)
          return problem(409, `${PEOPLE.find((p) => p.id === userId)?.username ?? 'Someone'} works at ${site.name} on ${row.dayOfWeek}s outside those hours: change their hours first.`);
      }
    site.hours = days.length > 0 ? days : null;
    site.hoursSince = days.length > 0 ? today() : null;
    return siteHoursResponse(site);
  });

  on('get', '/api/rooms', ({query}) => {
    me();
    const siteId = query.get('siteId');
    const includeArchived = query.get('includeArchived') === 'true';
    const openSites = db().sites.filter((s) => includeArchived || !s.isArchived).map((s) => s.id);
    return db()
      .rooms.filter((r) => (!siteId || r.siteId === siteId) && openSites.includes(r.siteId) && (includeArchived || !r.isArchived))
      .map(roomResponse)
      .sort((a, b) => (a.siteName ?? '').localeCompare(b.siteName ?? '') || (a.name ?? '').localeCompare(b.name ?? ''));
  });
  on('post', '/api/rooms', ({body}) => {
    requirePermission('ManageSetup');
    const site = findSite(body.siteId ?? '');
    if (site.isArchived) return problem(409, `${site.name} is archived: restore it before adding rooms.`);
    const room: DemoRoom = {id: newId(), siteId: site.id, name: roomName(body.name, site.id), isArchived: false};
    db().rooms.push(room);
    return new DemoReply(201, roomResponse(room));
  });
  on('put', '/api/rooms/{id}', ({params, body}) => {
    requirePermission('ManageSetup');
    const room = findRoom(params.id);
    room.name = roomName(body.name, room.siteId, room.id);
    return roomResponse(room);
  });
  for (const [path, archived] of [
    ['/api/rooms/{id}/archive', true],
    ['/api/rooms/{id}/restore', false],
  ] as const)
    on('post', path, ({params}) => {
      requirePermission('ManageSetup');
      const room = findRoom(params.id);
      room.isArchived = archived;
      return roomResponse(room);
    });
  on('delete', '/api/rooms/{id}', ({params}) => {
    requirePermission('ManageSetup');
    const room = findRoom(params.id);
    if (roomInUse(room)) return problem(409, `${room.name} is used by clinics or appointments, so it can't be deleted: archive it instead.`);
    db().rooms = db().rooms.filter((r) => r.id !== room.id);
    return undefined;
  });

  // Specialties: labels that decide nothing clinical. Every change names the version it saw.
  on('get', '/api/specialties', ({query}) => {
    me();
    return db()
      .specialties.filter((s) => query.get('includeArchived') === 'true' || !s.isArchived)
      .map(specialtyResponse)
      .sort((a, b) => (a.name ?? '').localeCompare(b.name ?? ''));
  });
  on('get', '/api/specialties/{id}', ({params}) => {
    me();
    return specialtyResponse(findSpecialty(params.id));
  });
  on('post', '/api/specialties', ({body}) => {
    requirePermission('ManageSetup');
    const now = nowUtc();
    const specialty: Specialty = {id: newId(), ...specialtyInput(body), isArchived: false, version: 1, letterTemplateCount: 0, createdAt: now, lastUpdated: now};
    db().specialties.push(specialty);
    return new DemoReply(201, specialtyResponse(specialty));
  });
  on('put', '/api/specialties/{id}', ({params, body}) => {
    requirePermission('ManageSetup');
    const specialty = findSpecialty(params.id);
    checkVersion(specialty.version, body.expectedVersion);
    Object.assign(specialty, specialtyInput(body, specialty), {version: (specialty.version ?? 1) + 1, lastUpdated: nowUtc()});
    // Templates carry their specialty's name.
    for (const t of db().templates) if (t.specialtyId === specialty.id) t.specialtyName = specialty.name;
    return specialtyResponse(specialty);
  });
  on('post', '/api/specialties/{id}/archive', ({params, body}) => {
    requirePermission('ManageSetup');
    const specialty = findSpecialty(params.id);
    checkVersion(specialty.version, body.expectedVersion);
    const awaiting = db().referrals.filter((r) => r.specialtyId === specialty.id && r.status === 'AwaitingTriage').length;
    if (awaiting > 0)
      return problem(409, `${awaiting} ${awaiting === 1 ? 'referral is' : 'referrals are'} awaiting triage in '${specialty.name}': triage or redirect them before archiving it.`);
    Object.assign(specialty, {isArchived: true, version: (specialty.version ?? 1) + 1, lastUpdated: nowUtc()});
    return specialtyResponse(specialty);
  });
  on('post', '/api/specialties/{id}/restore', ({params, body}) => {
    requirePermission('ManageSetup');
    const specialty = findSpecialty(params.id);
    checkVersion(specialty.version, body.expectedVersion);
    Object.assign(specialty, {isArchived: false, version: (specialty.version ?? 1) + 1, lastUpdated: nowUtc()});
    return specialtyResponse(specialty);
  });
  on('delete', '/api/specialties/{id}', ({params}) => {
    requirePermission('ManageSetup');
    const specialty = specialtyResponse(findSpecialty(params.id));
    if (!specialty.canBeDeleted)
      return problem(409, `Templates, pathways, referrals, letter templates or staff have '${specialty.name}', so it can't be deleted: archive it instead.`);
    db().specialties = db().specialties.filter((s) => s.id !== specialty.id);
    return undefined;
  });
}
