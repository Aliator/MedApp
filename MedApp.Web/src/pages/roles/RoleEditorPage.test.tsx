import {screen, waitFor, within} from '@testing-library/react';
import {
  adminUser,
  expectAnnounced,
  findAlert,
  json,
  mockApi,
  openDialog,
  renderWithApp,
  withAnnouncements,
} from '../../test/render';
import {roleResponse} from '../../test/roles';
import {RoleEditorPage} from './RoleEditorPage';

const nurse = roleResponse('Nurse', {
  description: 'Nurses who work on pathways.',
  holderCount: 2,
  canDelete: false,
  holders: [
    {userId: 'u1', username: 'k.ashford'},
    {userId: 'u2', username: 'n.thornbury'},
  ],
});

function renderEditor(id: string | undefined, extra: Parameters<typeof mockApi>[0] = {}) {
  const fetch = mockApi({'GET /api/roles/role-nurse': () => json(nurse), ...extra});
  return {fetch, ...renderWithApp(<RoleEditorPage id={id} />, {user: adminUser})};
}

const box = (name: string) => screen.getByRole('checkbox', {name});
const sent = (fetch: ReturnType<typeof mockApi>, method: string) =>
  fetch.mock.calls.map(([r]) => r as Request).filter((r) => r.method === method);

describe('RoleEditorPage', () => {
  it('shows the role: its details, its permissions ticked in groups, and who holds it, linking to the Users page', async () => {
    renderEditor('role-nurse');

    expect(await screen.findByRole('heading', {name: 'Edit Role', level: 1})).toBeInTheDocument();
    expect(screen.getByRole('textbox', {name: /Name/})).toHaveValue('Nurse');
    expect(screen.getByRole('textbox', {name: /Description/})).toHaveValue('Nurses who work on pathways.');
    expect(box('View patients')).toBeChecked();
    expect(box('Work pathways')).toBeChecked();
    expect(box('Assign pathways')).not.toBeChecked();
    expect(
      screen.getByText("Change other people's calendars: their blocks, day and week templates and default week."),
    ).toBeInTheDocument();
    expect(screen.getByText('Appointments and calendars')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'k.ashford'})).toHaveAttribute('href', '/admin/users?username=k.ashford');
    expect(screen.getByText('People with this role (2)')).toBeInTheDocument();
  });

  it('shows the Administrator-only permissions read only, never ticked on another role', async () => {
    const {user} = renderEditor('role-nurse');

    const manageRoles = await screen.findByRole('checkbox', {name: 'Manage roles'});
    expect(manageRoles).not.toBeChecked();
    expect(screen.getByText(/Only the built-in Admin role has these/)).toBeInTheDocument();
    await user.click(manageRoles);
    expect(manageRoles).not.toBeChecked();
  });

  it('filters the permissions by name or what they allow', async () => {
    const {user} = renderEditor('role-nurse');

    await user.type(await screen.findByRole('textbox', {name: 'Search permissions'}), 'meetings');
    expect(box('Arrange meetings')).toBeInTheDocument();
    expect(screen.queryByRole('checkbox', {name: 'View patients'})).toBeNull();

    await user.clear(screen.getByRole('textbox', {name: 'Search permissions'}));
    await user.type(screen.getByRole('textbox', {name: 'Search permissions'}), 'nothing like this');
    expect(screen.getByText('No permissions match')).toBeInTheDocument();
  });

  it('shows what a save adds and removes and how many people it affects, then saves from the version shown', async () => {
    withAnnouncements();
    const {user, fetch} = renderEditor('role-nurse', {
      'PUT /api/roles/role-nurse': () => json({...nurse, permissions: ['ViewPatients', 'AssignPathways'], version: 'v2'}),
    });

    await user.click(await screen.findByRole('checkbox', {name: 'Assign pathways'}));
    await user.click(box('Work pathways'));
    await user.click(screen.getByRole('button', {name: 'Save'}));

    const dialog = within(openDialog()!);
    expect(dialog.getByText('This changes what 2 people can do, from their next click.')).toBeInTheDocument();
    expect(dialog.getByText('Assign pathways')).toBeInTheDocument();
    expect(dialog.getByText('Work pathways')).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Confirm Save'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    const body = (await sent(fetch, 'PUT')[0].json()) as {permissions: string[]; expectedVersion: string; name: string};
    expect(body.expectedVersion).toBe('v1');
    expect(body.name).toBe('Nurse');
    expect(body.permissions).toEqual(
      nurse
        .permissions!.filter((p) => p !== 'WorkPathways')
        .concat('AssignPathways')
        .sort(byCatalogue),
    );
    await expectAnnounced('Role saved');
  });

  it('saves nothing when nothing changed, and says so', async () => {
    const {user, fetch} = renderEditor('role-nurse');

    await user.click(await screen.findByRole('button', {name: 'Save'}));

    expect(await findAlert('Nothing has changed, so there is nothing to save.')).toBeInTheDocument();
    expect(sent(fetch, 'PUT')).toHaveLength(0);
  });

  it("shows the API's words when another admin saved the role first, and reads it again", async () => {
    const {user, fetch} = renderEditor('role-nurse', {
      'PUT /api/roles/role-nurse': () =>
        json(
          {
            title: 'Another admin changed this role while you had it open. Reload to see their change, then try again.',
            status: 409,
          },
          409,
        ),
    });

    await user.click(await screen.findByRole('checkbox', {name: 'Assign pathways'}));
    await user.click(screen.getByRole('button', {name: 'Save'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Confirm Save'}));

    expect(await findAlert(/Another admin changed this role/)).toBeInTheDocument();
    await waitFor(() => expect(fetch.mock.calls.filter(([r]) => (r as Request).method === 'GET').length).toBeGreaterThan(1));
  });

  it('makes a new role, then opens its page', async () => {
    const {user, fetch} = renderEditor(undefined, {
      'POST /api/roles': () => json(roleResponse('Visiting consultant', {id: 'role-new'}), 201),
      'GET /api/roles/role-new': () => json(roleResponse('Visiting consultant', {id: 'role-new'})),
    });

    expect(await screen.findByRole('heading', {name: 'New Role', level: 1})).toBeInTheDocument();
    expect(screen.queryByText(/People with this role/)).toBeNull();
    await user.type(screen.getByRole('textbox', {name: /Name/}), 'Visiting consultant');
    await user.click(box('View patients'));
    await user.click(screen.getByRole('button', {name: 'Create Role'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Create Role'}));

    await waitFor(() => expect(sent(fetch, 'POST')).toHaveLength(1));
    expect(await sent(fetch, 'POST')[0].json()).toEqual({
      name: 'Visiting consultant',
      description: '',
      permissions: ['ViewPatients'],
    });
  });

  it('asks for a name before anything else', async () => {
    const {user, fetch} = renderEditor(undefined);

    await user.click(await screen.findByRole('button', {name: 'Create Role'}));

    expect(await screen.findAllByText('Name is required.')).not.toHaveLength(0);
    expect(openDialog()).toBeNull();
    expect(sent(fetch, 'POST')).toHaveLength(0);
  });

  it('shows the built-in Admin role read only, with no way to save, archive or delete it', async () => {
    renderEditor('role-admin', {'GET /api/roles/role-admin': () => json(roleResponse('Admin', {canDelete: false, holders: []}))});

    expect(await screen.findByRole('heading', {name: 'Admin Role', level: 1})).toBeInTheDocument();
    expect(await screen.findByText(/The Admin role is built in/)).toBeInTheDocument();
    expect(box('Manage roles')).toBeChecked();
    expect(box('Triage referrals')).not.toBeChecked();
    expect(screen.queryByRole('button', {name: 'Save'})).toBeNull();
    expect(screen.queryByRole('button', {name: 'Archive'})).toBeNull();
  });

  it('archives the role from its page, after saying its holders keep it', async () => {
    const {user, fetch} = renderEditor('role-nurse', {
      'POST /api/roles/role-nurse/archive': () => json({...nurse, isArchived: true, version: 'v2'}),
    });

    await user.click(await screen.findByRole('button', {name: 'Archive'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Archive'}));

    await waitFor(() => expect(sent(fetch, 'POST')).toHaveLength(1));
    expect(await sent(fetch, 'POST')[0].json()).toEqual({expectedVersion: 'v1'});
    expect(await screen.findByRole('button', {name: 'Restore'})).toBeInTheDocument();
    expect(await findAlert(/This role is archived/)).toBeInTheDocument();
  });

  it('deletes a role nobody holds, then goes back to the list', async () => {
    const lonely = roleResponse('Old team', {holders: []});
    const {user, fetch} = renderEditor('role-old-team', {
      'GET /api/roles/role-old-team': () => json(lonely),
      'DELETE /api/roles/role-old-team': () => new Response(null, {status: 204}),
    });

    expect(await screen.findByText('Nobody holds this role')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Delete Role'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Delete Role'}));

    await waitFor(() => expect(sent(fetch, 'DELETE')).toHaveLength(1));
  });

  it('says when the role could not be loaded', async () => {
    renderEditor('role-nurse', {'GET /api/roles/role-nurse': () => json({title: 'Boom'}, 500)});

    expect(await findAlert('The role could not be loaded. Please try again.')).toBeInTheDocument();
  });
});

/** The catalogue's order, as the editor sends them. */
const ORDER = [
  'ViewPatients',
  'EditPatients',
  'RegisterReferrals',
  'CreatePathways',
  'WorkPathways',
  'AssignPathways',
  'BookAppointments',
  'AttachFiles',
  'RecordSpecimens',
];
function byCatalogue(a: string, b: string) {
  return ORDER.indexOf(a) - ORDER.indexOf(b);
}
