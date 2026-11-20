import {accountTypeOfPerson, DEMO_ACCOUNTS, personByUsername, type DemoPerson} from '../accounts';
import {on, problem} from '../api/http';
import {db} from '../store';
import {nowUtc} from '../time';
import {logActivity} from './adminLogs';
import {me, membershipsOf, signedIn} from './common';

/**
 * Signing in, the session and the account's own settings. There's no real sign-in: the login page picks one of the
 * three demo accounts (any password works), and the session never runs out.
 */

/** Someone's teams with their kind and their role in each, as the API answers them (by name). */
export function teamsOfPerson(person: DemoPerson) {
  const mine = membershipsOf(person);
  return db()
    .teams.filter((t) => mine.some((m) => m.teamId === t.id))
    .map((t) => ({
      id: t.id,
      name: t.name,
      isArchived: t.isArchived,
      kind: t.kind ?? 'Clinic',
      role: mine.find((m) => m.teamId === t.id)?.role ?? 'Member',
    }))
    .sort((a, b) => (a.name ?? '').localeCompare(b.name ?? ''));
}
export function registerAccount() {
  on('get', '/api/auth/whoami', () => {
    const person = signedIn();
    if (!person) return {isAuthenticated: false, roles: [], permissions: []};
    return {
      isAuthenticated: true,
      name: person.username,
      roles: person.roles,
      permissions: person.permissions,
      mustChangePassword: false,
      mustSetUpTwoFactor: false,
      accountType: accountTypeOfPerson(person, membershipsOf(person)),
      teams: teamsOfPerson(person),
    };
  });

  on('post', '/api/auth/login', ({body}) => {
    const person = personByUsername(body.username);
    if (!person || !DEMO_ACCOUNTS.some((account) => account.id === person.id))
      return problem(401, 'In the light demo, choose one of the four accounts on the login page.');
    db().user = person.username;
    logActivity('SignedIn', {userId: person.id});
    return {sessionId: person.id, expiresAtUtc: new Date(Date.now() + 8 * 3600_000).toISOString()};
  });

  on('post', '/api/auth/logout', () => {
    const person = signedIn();
    if (person) logActivity('SignedOut', {userId: person.id});
    db().user = null;
    return undefined;
  });

  // Eight hours left, always: the light demo has no session to run out.
  on('get', '/api/auth/session', () => {
    me();
    return {expiresAtUtc: new Date(Date.now() + 8 * 3600_000).toISOString(), expiresInSeconds: 8 * 3600, canExtend: true};
  });
  on('post', '/api/auth/session/extend', () => ({
    expiresAtUtc: new Date(Date.now() + 8 * 3600_000).toISOString(),
    expiresInSeconds: 8 * 3600,
    canExtend: true,
  }));

  // No email: "Forgot your password?" and email codes don't show.
  on('get', '/api/auth/features', () => ({canSendEmail: false}));

  on('get', '/api/auth/me/settings', () => {
    const person = me();
    return {settings: db().settings[person.id] ?? null, updatedAtUtc: null};
  });
  on('put', '/api/auth/me/settings', ({body}) => {
    const person = me();
    db().settings[person.id] = body.settings ?? '{}';
    return undefined;
  });

  on('get', '/api/auth/me/details', () => {
    const person = me();
    return {fullName: person.fullName, jobTitle: person.jobTitle};
  });
  on('put', '/api/auth/me/details', () => problem(409, 'The demo accounts’ names are fixed in the light demo.'));

  on('get', '/api/auth/me/teams', () => teamsOfPerson(me()));

  on('get', '/api/auth/me/email', () => {
    const person = me();
    return {email: `${person.username}@centre.example`, pendingEmail: null};
  });
  on('get', '/api/auth/me/two-factor', () => ({
    enabled: false,
    recoveryCodesLeft: 0,
    required: false,
    method: null,
    canUseEmailCodes: false,
    emailCodesUnavailableReason: 'The light demo sends no email.',
  }));

  on('get', '/api/notifications', ({query}) => {
    const person = me();
    const take = Number(query.get('take')) || 20;
    const mine = db()
      .notifications.filter((n) => n.userId === person.id)
      .sort((a, b) => (b.createdAtUtc ?? '').localeCompare(a.createdAtUtc ?? ''));
    // Answered without whose it is (the API's response has no owner).
    const withoutOwner = ({userId, ...notification}: (typeof mine)[number]) => {
      void userId;
      return notification;
    };
    return {items: mine.slice(0, take).map(withoutOwner), unreadCount: mine.filter((n) => !n.isRead).length};
  });
  on('post', '/api/notifications/read', () => {
    const person = me();
    for (const n of db().notifications) if (n.userId === person.id) n.isRead = true;
    return undefined;
  });
  on('post', '/api/notifications/{id}/read', ({params}) => {
    const notification = db().notifications.find((n) => n.id === params.id);
    if (notification) notification.isRead = true;
    return undefined;
  });
  on('get', '/api/notifications/preferences', () => ({pathwayAssigned: true}));
  on('put', '/api/notifications/preferences', ({body}) => ({pathwayAssigned: body.pathwayAssigned ?? true}));

  // The notepad, kept per account (in this browser only: see docs/agents/notepad.md).
  on('get', '/api/me/notepad', () => {
    const person = me();
    return db().notepads[person.id] ?? {text: '', lastUpdatedUtc: null, version: 0, keepDays: 30};
  });
  on('put', '/api/me/notepad', ({body}) => {
    const person = me();
    const current = db().notepads[person.id];
    const saved = {text: body.text ?? '', lastUpdatedUtc: nowUtc(), version: (current?.version ?? 0) + 1, keepDays: 30};
    db().notepads[person.id] = saved;
    return saved;
  });

  // No demographics service, and no texts or emails to patients.
  on('get', '/api/demographics/status', () => ({isAvailable: false, source: null}));
  on('get', '/api/appointments/patient-messages/status', () => ({
    mode: 'Off',
    isOn: false,
    sendsMissedAppointmentMessages: false,
    reminderDaysBefore: 0,
  }));
}
