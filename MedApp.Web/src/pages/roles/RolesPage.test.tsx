import {screen, waitFor, within} from '@testing-library/react';
import {
  adminUser,
  calls,
  expectAnnounced,
  findAlert,
  isDisabled,
  json,
  mockApi,
  openDialog,
  renderWithApp,
  withAnnouncements,
} from '../../test/render';
import {roleResponse} from '../../test/roles';
import type {SetupListSearch} from '../listSearch';
import {RolesPage, rolesPage} from './RolesPage';

const roles = [
  roleResponse('Admin', {holderCount: 2, canDelete: false}),
  roleResponse('Nurse', {holderCount: 4, canDelete: false, description: 'Nurses who work on pathways.'}),
  roleResponse('Booking', {holderCount: 0}),
  roleResponse('Old team', {isArchived: true, holderCount: 1, canDelete: false, permissions: ['ViewPatients']}),
];

function renderPage(search: SetupListSearch = {}, extra: Parameters<typeof mockApi>[0] = {}) {
  const fetch = mockApi({'GET /api/roles': () => json(roles), ...extra});
  const onSearchChange = vi.fn();
  return {
    fetch,
    onSearchChange,
    ...renderWithApp(<RolesPage search={search} onSearchChange={onSearchChange} />, {user: adminUser}),
  };
}

const rowOf = async (name: string) => (await screen.findByText(name)).closest('tr')!;
const sent = (fetch: ReturnType<typeof mockApi>, method: string) =>
  fetch.mock.calls.map(([r]) => r as Request).filter((r) => r.method === method);

describe('rolesPage', () => {
  it('keeps the built-in Admin role first, then the rest by name, and pages them', () => {
    expect(rolesPage(roles, {}, 10).rows.map((r) => r.name)).toEqual(['Admin', 'Booking', 'Nurse']);
    expect(rolesPage(roles, {status: 'any'}, 2)).toMatchObject({page: 1, total: 4});
    expect(rolesPage(roles, {status: 'any', page: 9}, 2).rows.map((r) => r.name)).toEqual(['Nurse', 'Old team']);
    expect(rolesPage(roles, {name: 'nur'}, 10).rows.map((r) => r.name)).toEqual(['Nurse']);
  });
});

describe('RolesPage', () => {
  it('lists each role with what it is for, who holds it and how many permissions it grants, Admin first and built in', async () => {
    const {fetch} = renderPage();

    const nurse = await rowOf('Nurse');
    expect(within(nurse).getByText('Nurses who work on pathways.')).toBeInTheDocument();
    expect(within(nurse).getByText('4')).toBeInTheDocument();
    expect(within(nurse).getByText(String(roles[1].permissions!.length))).toBeInTheDocument();
    expect(within(nurse).getByText('Active')).toBeInTheDocument();
    const admin = await rowOf('Admin');
    expect(within(admin).getByText('Built in')).toBeInTheDocument();
    // Admin can be looked at, but has no archive or delete.
    expect(within(admin).getByRole('link', {name: 'View Admin'})).toHaveAttribute('href', '/admin/roles/role-admin');
    expect(within(admin).queryByRole('button', {name: /Archive|Delete/})).toBeNull();
    expect(within(nurse).getByRole('link', {name: 'Open Nurse'})).toHaveAttribute('href', '/admin/roles/role-nurse');
    expect(screen.queryByText('Old team')).not.toBeInTheDocument();
    expect(screen.getByText('3 active roles')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Add Role'})).toHaveAttribute('href', '/admin/roles/new');
    expect(calls(fetch)).toContain('GET /api/roles');
  });

  it('archives a role from the version shown, after saying its holders keep it', async () => {
    withAnnouncements();
    const {user, fetch} = renderPage({}, {'POST /api/roles/role-nurse/archive': () => json({...roles[1], isArchived: true})});

    await user.click(within(await rowOf('Nurse')).getByRole('button', {name: 'Archive Nurse'}));
    const dialog = within(openDialog()!);
    expect(dialog.getByText(/The people who hold it keep it/)).toBeInTheDocument();
    expect(dialog.getByText('4 people')).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Archive'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(await sent(fetch, 'POST')[0].json()).toEqual({expectedVersion: 'v1'});
    await expectAnnounced('Nurse archived');
  });

  it('restores an archived one at once', async () => {
    const {user, fetch} = renderPage(
      {status: 'archived'},
      {'POST /api/roles/role-old-team/restore': () => json({...roles[3], isArchived: false})},
    );

    await user.click(within(await rowOf('Old team')).getByRole('button', {name: 'Restore Old team'}));

    await waitFor(() => expect(sent(fetch, 'POST')).toHaveLength(1));
    expect(calls(fetch)).toContain('POST /api/roles/role-old-team/restore');
  });

  it('deletes only a role nobody holds, after asking', async () => {
    const {user, fetch} = renderPage({}, {'DELETE /api/roles/role-booking': () => new Response(null, {status: 204})});

    expect(isDisabled(within(await rowOf('Nurse')).getByRole('button', {name: /Nurse: people hold it/}))).toBe(true);
    await user.click(within(await rowOf('Booking')).getByRole('button', {name: 'Delete Booking'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Delete Role'}));

    await waitFor(() => expect(calls(fetch)).toContain('DELETE /api/roles/role-booking'));
  });

  it("shows the API's words when another admin changed the role meanwhile", async () => {
    const {user} = renderPage(
      {},
      {
        'POST /api/roles/role-nurse/archive': () =>
          json(
            {
              title: 'Another admin changed this role while you had it open. Reload to see their change, then try again.',
              status: 409,
            },
            409,
          ),
      },
    );

    await user.click(within(await rowOf('Nurse')).getByRole('button', {name: 'Archive Nurse'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Archive'}));

    expect(await findAlert(/Another admin changed this role/)).toBeInTheDocument();
  });

  it('says when the roles could not be loaded', async () => {
    renderPage({}, {'GET /api/roles': () => json({title: 'Boom'}, 500)});

    expect(await findAlert('The roles could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('offers to show archived ones when nothing matches', async () => {
    const {user, onSearchChange} = renderPage({name: 'zzz'});

    await user.click(await screen.findByRole('button', {name: 'Show Archived'}));
    expect(onSearchChange).toHaveBeenCalledWith({name: 'zzz', page: undefined, status: 'any'});
  });
});
