import type {Schemas} from '../../api/client';
import {PERMISSION_CATALOGUE, PERMISSIONS, type Permission} from '../../auth/permissions';
import {accountTypeOfPerson, PEOPLE, personByUsername, type DemoMembership, type DemoPerson} from '../accounts';
import {invalid, notFound, on, problem, refuse} from '../api/http';
import {newId} from '../ids';
import type {DemoRole} from '../slices/setup';
import {db} from '../store';
import {nowUtc} from '../time';
import {can, contains, me, membershipsOf, paged, requirePermission} from './common';
import {teamsOfPerson} from './account';
import {logActivity} from './adminLogs';

/**
 * Users, roles, permissions, team memberships and the security settings. The clinic's people are fixed
 * (`accounts.ts`'s `PEOPLE`): no account is made or deleted, and nobody's roles, name or password change, so their
 * permissions stay what the demo accounts were made with. Roles nobody holds can be made, changed, archived and
 * deleted; a role someone holds can be archived, not changed. Memberships change, and the activity log records it.
 */

/** Someone's email address in the demo (synthetic). */
const emailOf = (person: DemoPerson) => `${person.username}@centre.example`;

export function userResponse(person: DemoPerson): Schemas['UserResponse'] {
  return {
    accountType: accountTypeOfPerson(person, membershipsOf(person)),
    userId: person.id,
    username: person.username,
    roles: person.roles,
    email: emailOf(person),
    pendingEmail: null,
    twoFactorEnabled: false,
    fullName: person.fullName,
    jobTitle: person.jobTitle,
  };
}

function findPerson(username: string): DemoPerson {
  const person = personByUsername(username);
  if (!person) refuse(notFound('The user'));
  return person;
}

const ADMIN_ONLY = PERMISSIONS.filter((p) => PERMISSION_CATALOGUE[p].group === 'AdministratorOnly');

function roleResponse(role: DemoRole): Schemas['RoleResponse'] {
  const holders = PEOPLE.filter((p) => p.roles.includes(role.name));
  return {
    id: role.id,
    name: role.name,
    description: role.description,
    isBuiltIn: role.isBuiltIn,
    isArchived: role.isArchived,
    permissions: role.permissions,
    holderCount: holders.length,
    version: String(role.version),
    canEdit: !role.isBuiltIn,
    canDelete: !role.isBuiltIn && holders.length === 0,
    holders: holders.map((p) => ({userId: p.id, username: p.username})),
    createdAt: role.createdAt,
    lastUpdated: role.lastUpdated,
  };
}

/** Admin first, then by name (as the API sends them). */
const roleOrder = (a: DemoRole, b: DemoRole) => Number(b.isBuiltIn) - Number(a.isBuiltIn) || a.name.localeCompare(b.name);

function findRole(roleId: string): DemoRole {
  const role = db().roles.find((r) => r.id === roleId);
  if (!role) refuse(notFound('The role'));
  return role;
}

/** Refuses a change to a role from a stale copy (`expectedVersion`), or to the built-in Admin role. */
function checkChange(role: DemoRole, expectedVersion: string | null | undefined) {
  if (role.isBuiltIn) refuse(problem(409, 'The Admin role is built in: its permissions are fixed.'));
  if (expectedVersion && expectedVersion !== String(role.version))
    refuse(problem(409, 'Someone else changed this role meanwhile. Look at it again before changing it.'));
}

const isHeld = (role: DemoRole) => PEOPLE.some((p) => p.roles.includes(role.name));

/** A role's name, description and permissions, checked (`RoleRules`). */
function roleInput(body: {name?: string | null; description?: string | null; permissions?: string[] | null}, roleId?: string) {
  const name = body.name?.trim() ?? '';
  const permissions = [...new Set(body.permissions ?? [])];
  const errors: Record<string, string[]> = {};
  if (!name) errors.name = ['Give the role a name.'];
  else if (name.length > 50) errors.name = ['A role’s name is at most 50 characters.'];
  if (permissions.some((p) => !(PERMISSIONS as readonly string[]).includes(p))) errors.permissions = ['Choose permissions from the list.'];
  else if (permissions.some((p) => ADMIN_ONLY.includes(p as Permission)))
    errors.permissions = ['The Administrator-only permissions can’t be put on any role but Admin.'];
  if (Object.keys(errors).length > 0) refuse(invalid(errors));
  if (db().roles.some((r) => r.id !== roleId && r.name.toLowerCase() === name.toLowerCase()))
    refuse(problem(409, `There's already a role called '${name}'.`));
  return {name, description: body.description?.trim() ?? '', permissions: PERMISSIONS.filter((p) => permissions.includes(p))};
}

function securitySettingsResponse(): Schemas['SecuritySettingsResponse'] {
  const settings = db().securitySettings;
  // Nobody in the demo uses two-factor: every admin is without it.
  const admins = PEOPLE.filter((p) => p.roles.includes('Admin')).map((p) => p.username);
  return {...settings, adminsWithoutTwoFactor: admins.length, adminsWithoutTwoFactorUsernames: admins};
}

const FIXED_PEOPLE = 'The light demo’s clinic and its people are fixed: their roles, names and passwords can’t be changed here.';

export function registerAdminAccess() {
  // Everyone on staff (pickers use it too, so any signed-in person may search).
  on('get', '/api/auth/users/search', ({query}) => {
    me();
    const username = query.get('Username');
    const role = query.get('Role');
    const items = PEOPLE.filter((p) => contains(p.username, username) && (!role || p.roles.includes(role))).map(userResponse);
    const ordered = items.sort((x, y) => (x.username ?? '').localeCompare(y.username ?? ''));
    return paged(query.get('Ascending') === 'false' ? ordered.reverse() : ordered, query);
  });
  on('get', '/api/auth/users', () => {
    requirePermission('ManageUsers');
    return PEOPLE.map(userResponse);
  });
  on('get', '/api/auth/users/{username}', ({params}) => {
    requirePermission('ManageUsers');
    return userResponse(findPerson(params.username));
  });
  on('get', '/api/auth/users/{username}/roles', ({params}) => {
    requirePermission('ManageUsers');
    return findPerson(params.username).roles;
  });
  on('get', '/api/auth/roles', () => {
    requirePermission('ManageUsers');
    return db()
      .roles.filter((r) => !r.isArchived)
      .sort(roleOrder)
      .map((r) => r.name);
  });
  on('put', '/api/auth/users/{username}/roles', ({params}) => {
    requirePermission('ManageUsers');
    findPerson(params.username);
    return problem(409, FIXED_PEOPLE);
  });
  on('put', '/api/auth/users/{username}/details', ({params}) => {
    requirePermission('ManageUsers');
    findPerson(params.username);
    return problem(409, FIXED_PEOPLE);
  });
  on('post', '/api/auth/users/{username}/sign-out-everywhere', ({params}) => {
    requirePermission('ManageUsers');
    const person = findPerson(params.username);
    if (person.id === me().id) return problem(409, 'Sign yourself out from your own account’s Security page.');
    logActivity('SignedOutEverywhere', {targetUsername: person.username});
    return undefined;
  });

  on('get', '/api/auth/users/{username}/teams', ({params}) => {
    requirePermission('ManageUsers');
    return teamsOfPerson(findPerson(params.username));
  });
  on('put', '/api/auth/users/{username}/teams', ({params, body}) => {
    requirePermission('ManageUsers');
    const person = findPerson(params.username);
    const state = db();
    // Each team once, with its role (a clinic admin, or a member).
    const wanted: DemoMembership[] = [];
    for (const t of body.teams ?? []) {
      if (!t.teamId) return invalid({teams: ['Choose a team for each membership.']});
      if (t.role != null && t.role !== 'Member' && t.role !== 'ClinicAdmin') return invalid({teams: ['A role is Member or ClinicAdmin.']});
      if (!wanted.some((w) => w.teamId === t.teamId)) wanted.push({teamId: t.teamId, role: t.role === 'ClinicAdmin' ? 'ClinicAdmin' : 'Member'});
    }
    const before = membershipsOf(person);
    // An admin is in no team (decision 209, `TeamRules.AdminHasNoTeam`): someone who is both has two accounts.
    if (wanted.length > 0 && person.roles.includes('Admin'))
      return problem(409, `${person.username} is an admin, and an admin is in no team. Someone who does clinical work too has a staff account as well.`);
    for (const {teamId, role} of wanted) {
      const team = state.teams.find((s) => s.id === teamId);
      if (!team) return notFound('The team');
      // An archived one can't be newly given; one they already have stays.
      if (team.isArchived && !before.some((b) => b.teamId === teamId))
        return problem(409, `The '${team.name}' team is archived, so it can't be given to anyone new.`);
      if (role === 'ClinicAdmin' && team.kind === 'Lab') return problem(409, 'The lab has no clinic admin: choose Member for it.');
    }
    const same = wanted.length === before.length && wanted.every((w) => before.some((b) => b.teamId === w.teamId && b.role === w.role));
    if (!same) {
      const names = (list: DemoMembership[]) =>
        list.map((m) => `${state.teams.find((x) => x.id === m.teamId)?.name ?? ''}${m.role === 'ClinicAdmin' ? ' (clinic admin)' : ''}`).sort();
      logActivity('TeamMembershipChanged', {targetUsername: person.username, details: JSON.stringify({from: names(before), to: names(wanted)})});
      state.memberships[person.id] = wanted;
    }
    return teamsOfPerson(person);
  });

  on('get', '/api/permissions', () => {
    requirePermission('ManageRoles');
    return PERMISSIONS.map((name) => ({
      name,
      group: PERMISSION_CATALOGUE[name].group,
      description: PERMISSION_CATALOGUE[name].description,
      isAdminOnly: PERMISSION_CATALOGUE[name].group === 'AdministratorOnly',
    }));
  });
  on('get', '/api/roles', () => {
    if (!can('ManageRoles')) requirePermission('ManageUsers');
    return [...db().roles].sort(roleOrder).map(roleResponse);
  });
  on('get', '/api/roles/{id}', ({params}) => {
    requirePermission('ManageRoles');
    return roleResponse(findRole(params.id));
  });
  on('post', '/api/roles', ({body}) => {
    requirePermission('ManageRoles');
    const input = roleInput(body);
    const now = nowUtc();
    const role: DemoRole = {id: newId(), ...input, isBuiltIn: false, isArchived: false, version: 1, createdAt: now, lastUpdated: now};
    db().roles.push(role);
    logActivity('RoleCreated', {details: JSON.stringify({role: role.name, permissions: role.permissions})});
    return roleResponse(role);
  });
  on('put', '/api/roles/{id}', ({params, body}) => {
    requirePermission('ManageRoles');
    const role = findRole(params.id);
    checkChange(role, body.expectedVersion);
    if (isHeld(role))
      return problem(409, 'In the light demo, a role someone holds can’t be changed (their permissions are fixed). Change one nobody holds, or make a new one.');
    const input = roleInput(body, role.id);
    const added = input.permissions.filter((p) => !role.permissions.includes(p));
    const removed = role.permissions.filter((p) => !input.permissions.includes(p as Permission));
    logActivity('RoleChanged', {
      details: JSON.stringify({
        role: input.name,
        ...(input.name !== role.name ? {renamedFrom: role.name} : {}),
        ...(input.description !== role.description ? {descriptionChanged: true} : {}),
        added,
        removed,
      }),
    });
    Object.assign(role, input, {version: role.version + 1, lastUpdated: nowUtc()});
    return roleResponse(role);
  });
  for (const [path, archived] of [
    ['/api/roles/{id}/archive', true],
    ['/api/roles/{id}/restore', false],
  ] as const)
    on('post', path, ({params, body}) => {
      requirePermission('ManageRoles');
      const role = findRole(params.id);
      checkChange(role, body.expectedVersion);
      if (role.isArchived === archived) return problem(409, archived ? 'This role is already archived.' : 'This role isn’t archived.');
      Object.assign(role, {isArchived: archived, version: role.version + 1, lastUpdated: nowUtc()});
      logActivity('RoleChanged', {details: JSON.stringify({role: role.name, archived})});
      return roleResponse(role);
    });
  on('delete', '/api/roles/{id}', ({params}) => {
    requirePermission('ManageRoles');
    const role = findRole(params.id);
    if (role.isBuiltIn) return problem(409, 'The Admin role is built in: it can’t be deleted.');
    if (isHeld(role)) return problem(409, `Someone holds the '${role.name}' role, so it can't be deleted: archive it instead.`);
    db().roles = db().roles.filter((r) => r.id !== role.id);
    logActivity('RoleDeleted', {details: JSON.stringify({role: role.name, permissions: role.permissions})});
    return undefined;
  });

  on('get', '/api/auth/security-settings', () => {
    requirePermission('ManageSecuritySettings');
    return securitySettingsResponse();
  });
  on('put', '/api/auth/security-settings', ({body}) => {
    requirePermission('ManageSecuritySettings');
    if (body.requireTwoFactorForAdmins)
      return problem(409, 'Two-factor sign-in isn’t in the light demo, so it can’t be required for admins here.');
    const settings = db().securitySettings;
    if (settings.requireTwoFactorForAdmins) {
      Object.assign(settings, {requireTwoFactorForAdmins: false, updatedAtUtc: nowUtc(), updatedBy: me().username});
      logActivity('TwoFactorRequirementChanged', {details: JSON.stringify({requireTwoFactorForAdmins: false})});
    }
    return securitySettingsResponse();
  });
}
