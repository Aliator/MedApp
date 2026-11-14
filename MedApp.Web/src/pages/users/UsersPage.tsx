import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextInput} from '@astryxdesign/core/TextInput';
import {Token} from '@astryxdesign/core/Token';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {ApiValidationError, allMessages} from '../../api/errors';
import {PAGE_SIZE, userSearchQuery, type UserPage} from '../../api/search';
import {
  DEFAULT_NEW_USER_PASSWORD,
  rolesQuery,
  useCreateUser,
  useDeleteUser,
  useResetPassword,
  useSetRoles,
} from '../../api/users';
import {useCurrentUser} from '../../auth/auth';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {SelectionDialog} from '../../components/modals/SelectionDialog';
import {Page} from '../../components/pages/Page';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {AvatarCell} from '../../components/tables/AvatarCell';
import {ListFilterBar} from '../../components/tables/ListFilterBar';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {TableActions} from '../../components/tables/TableActions';
import {hasUserFilters, type UsersListSearch, choiceValues} from '../listSearch';

type User = NonNullable<UserPage['items']>[number];

/** The same roles, in any order. */
const sameRoles = (a: readonly string[], b: readonly string[]) => a.length === b.length && a.every((r) => b.includes(r));
type UserRow = User & Record<string, unknown>;

type DialogState =
  | {kind: 'create'}
  | {kind: 'roles' | 'reset' | 'delete'; username: string; roles: string[]}
  | null;

/** The role to ask the API for: the one chosen, or none when the choice covers every role (or none at all). */
function oneRole(role: string | undefined, roles: readonly string[]) {
  const chosen = choiceValues(role, roles);
  return chosen?.length === 1 ? chosen[0] : undefined;
}

export interface UsersPageProps {
  search: UsersListSearch;
  onSearchChange: (search: UsersListSearch) => void;
}

/**
 * Admin user management (replaces Blazor's UsersTable): one page of users with their roles, a search, and
 * dialogs to create users, edit their roles (every role listed, theirs ticked), reset passwords and delete users
 * (confirmed with the admin's own password). Admins can't change
 * their own account here, as in Blazor.
 */
export function UsersPage({search, onSearchChange}: UsersPageProps) {
  const me = useCurrentUser();
  const page = search.page ?? 1;
  const ascending = search.asc ?? true;
  const isFiltered = hasUserFilters(search);
  const {data: allRoles = []} = useQuery(rolesQuery);

  const {data, isPending, isError} = useQuery(
    // The API takes one role: any of them all is no filter at all.
    userSearchQuery({Page: page, Sort: 'username', Ascending: ascending, Username: search.username, Role: oneRole(search.role, allRoles)}),
  );
  const total = data?.totalCount ?? 0;
  const rows = (data?.items ?? []) as UserRow[];

  const create = useCreateUser();
  const setRoles = useSetRoles();
  const reset = useResetPassword();
  const remove = useDeleteUser();

  const [dialog, setDialog] = useState<DialogState>(null);
  const [selectedRoles, setSelectedRoles] = useState<string[]>([]);
  const [newUsername, setNewUsername] = useState('');
  const [newPassword, setNewPassword] = useState<string | null>(null);
  // Deleting a user: the admin's own password, and why it was refused.
  const [deletePassword, setDeletePassword] = useState('');
  const [deleteProblem, setDeleteProblem] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const open = (next: DialogState) => {
    // Edit roles starts from the user's current roles, ticked.
    setSelectedRoles(next && next.kind !== 'create' ? next.roles : []);
    setNewUsername('');
    setNewPassword(null);
    setDeletePassword('');
    setDeleteProblem(null);
    create.reset();
    setError(null);
    setDialog(next);
  };
  const close = () => setDialog(null);
  const failed = (message: string) => () => {
    setError(message);
    close();
  };

  /** Keeps the order; clears the filters. */
  const clearSearch = () => onSearchChange({asc: search.asc});

  const columns: DataTableColumn<UserRow>[] = [
    {key: 'User', header: 'User', sortable: true, renderCell: (u) => <AvatarCell primaryText={u.username ?? ''} />},
    {
      key: 'roles',
      header: 'Roles',
      renderCell: (u) =>
        u.roles?.length ? (
          <HStack gap={1} wrap="wrap">
            {u.roles.map((role) => (
              <Token key={role} label={role} size="sm" color={role === 'Admin' ? 'blue' : 'default'} />
            ))}
          </HStack>
        ) : (
          <Text type="supporting" color="secondary">
            No roles
          </Text>
        ),
    },
    {
      key: 'actions',
      header: 'Actions',
      align: 'end',
      renderCell: (u) => {
        const username = u.username ?? '';
        const roles = [...(u.roles ?? [])];
        const isMe = username.toLowerCase() === me.username.toLowerCase();
        const target = {username, roles};
        return (
          <TableActions
            actions={[
              {label: 'Edit roles', icon: AppIcon.Assign, isDisabled: isMe, onClick: () => open({kind: 'roles', ...target})},
              {label: 'Reset password', icon: AppIcon.Lock, isDisabled: isMe, onClick: () => open({kind: 'reset', ...target})},
              {label: 'Delete user', icon: AppIcon.Delete, isDisabled: isMe, isDestructive: true, onClick: () => open({kind: 'delete', ...target})},
            ]}
          />
        );
      },
    },
  ];

  const subtitle = isPending ? 'Loading users...' : `${total} ${total === 1 ? 'user' : 'users'} ${isFiltered ? 'found' : 'in the system'}`;
  const target = dialog && dialog.kind !== 'create' ? dialog : null;
  const createErrors = create.error instanceof ApiValidationError ? create.error.errors : null;

  return (
    <Page
      title="User Management"
      subtitle={subtitle}
      fill
      message={(isError || error) && <AlertMessage message={error ?? 'The users could not be loaded. Please try again.'} />}
      actions={
        <Button label="Add User" variant="primary" icon={<Icon icon={AppIcon.Add} />} onClick={() => open({kind: 'create'})} />
      }
      dialogs={
        <>
          <ConfirmDialog
            isOpen={dialog?.kind === 'create'}
            title="Create New User"
            message="Enter a username for the new user."
            icon={AppIcon.Add}
            confirmText="Create User"
            isProcessing={create.isPending}
            confirmDisabled={!newUsername.trim()}
            onConfirm={() => create.mutate(newUsername.trim(), {onSuccess: close})}
            onCancel={close}>
            <TextInput
              label="Username"
              value={newUsername}
              onChange={setNewUsername}
              placeholder="Enter username"
              // A new account's name: never a saved login.
              autoComplete="off"
              description={`Default password will be: ${DEFAULT_NEW_USER_PASSWORD}`}
              isRequired
              hasAutoFocus
              status={
                createErrors
                  ? {type: 'error', message: allMessages(createErrors).join(' ')}
                  : create.isError
                    ? {type: 'error', message: 'The user could not be created. Please try again.'}
                    : undefined
              }
            />
          </ConfirmDialog>

          <SelectionDialog
            isOpen={dialog?.kind === 'roles'}
            title="Edit Roles"
            message={`Tick the roles ${target?.username} should have.`}
            icon={AppIcon.Assign}
            listLabel="Roles"
            items={allRoles.map((r) => ({value: r, label: r}))}
            selected={selectedRoles}
            onSelectedChange={setSelectedRoles}
            emptyTitle="There are no roles."
            confirmText="Save Roles"
            canConfirmNone
            confirmDisabled={sameRoles(selectedRoles, target?.roles ?? [])}
            isProcessing={setRoles.isPending}
            onConfirm={() =>
              target && setRoles.mutate({username: target.username, roles: selectedRoles}, {onSuccess: close, onError: failed('The roles could not be saved.')})
            }
            onCancel={close}
          />

          <ConfirmDialog
            isOpen={dialog?.kind === 'reset'}
            title="Reset Password"
            message={newPassword ? `Password reset for ${target?.username}.` : `Reset password for ${target?.username}?`}
            icon={AppIcon.Lock}
            intent="warning"
            confirmText={newPassword ? 'Done' : 'Reset Password'}
            isProcessing={reset.isPending}
            onConfirm={() =>
              newPassword
                ? close()
                : target && reset.mutate(target.username, {onSuccess: setNewPassword, onError: failed('The password could not be reset.')})
            }
            onCancel={close}>
            {newPassword && (
              <VStack gap={2} hAlign="center">
                <Text type="supporting" color="secondary">
                  New password
                </Text>
                <Text type="code" size="lg">
                  {newPassword}
                </Text>
                <Text type="supporting" color="secondary" justify="center">
                  Please save this password and share it securely with the user.
                </Text>
              </VStack>
            )}
          </ConfirmDialog>

          <ConfirmDialog
            isOpen={dialog?.kind === 'delete'}
            title="Confirm Delete"
            message="This action cannot be undone. Enter your password to confirm."
            isDanger
            confirmText="Delete User"
            confirmDisabled={!deletePassword}
            isProcessing={remove.isPending}
            onConfirm={() =>
              target &&
              remove.mutate(
                {username: target.username, password: deletePassword},
                {
                  onSuccess: close,
                  // A wrong password keeps the dialog open, saying so on the field; anything else closes it.
                  onError: (e) =>
                    e instanceof ApiValidationError ? setDeleteProblem(allMessages(e.errors).join(' ')) : failed('The user could not be deleted.')(),
                },
              )
            }
            onCancel={close}>
            <VStack gap={4}>
              <MetadataList>
                <MetadataListItem label="User">{target?.username}</MetadataListItem>
              </MetadataList>
              {/* Only while it's open: a password box in the page (closed dialogs stay in it) makes browsers take the
                  page for a login and fill the list's search with a saved username. Beside it, the signed-in
                  user's name, hidden, so a password manager pairs the password with that, not with another box. */}
              {dialog?.kind === 'delete' && (
                <>
                  <input type="text" name="username" autoComplete="username" value={me.username} readOnly hidden />
                  <TextInput
                    label="Your password"
                    type="password"
                    value={deletePassword}
                    onChange={(value) => {
                      setDeletePassword(value);
                      setDeleteProblem(null);
                    }}
                    autoComplete="current-password"
                    isRequired
                    hasAutoFocus
                    status={deleteProblem ? {type: 'error', message: deleteProblem} : undefined}
                  />
                </>
              )}
            </VStack>
          </ConfirmDialog>
        </>
      }>
      {!isPending && !isError && !isFiltered && total === 0 ? (
        <EmptyState
          title="No users yet"
          description="Get started by adding your first user to the system."
          icon={<Icon icon={AppIcon.Users} size="lg" />}
          actions={<Button label="Add Your First User" variant="primary" onClick={() => open({kind: 'create'})} />}
        />
      ) : (
        <DataTable
          data={rows}
          columns={columns}
          idKey="userId"
          isLoading={isPending}
          search={
            <ListFilterBar
              fields={[
                // Not "Username": browsers take a box named that for a login, and fill in a saved one.
                {kind: 'text', key: 'username', label: 'User'},
                {kind: 'choice', key: 'role', label: 'Role', options: allRoles.map((r) => ({value: r, label: r})), isMultiple: true},
              ]}
              values={search}
              onChange={(values) => onSearchChange({...search, ...values, page: undefined})}
              label="Search users"
              placeholder="Search users by name, or filter by role..."
              textKey="username"
              favourites={[
                {label: 'Admins', values: {role: 'Admin'}},
                {label: 'Users', values: {role: 'User'}},
              ]}
            />
          }
          skeletonRows={PAGE_SIZE}
          sort={{column: 'User', ascending}}
          onSortChange={(sort) => onSearchChange({...search, page: undefined, sort: 'User', asc: sort.ascending})}
          pagination={{
            page,
            pageSize: PAGE_SIZE,
            totalItems: total,
            onPageChange: (next) => onSearchChange({...search, page: next > 1 ? next : undefined}),
          }}
          emptyState={
            <EmptyState
              title="No users match your search"
              description="Try a different username or role, or clear the search."
              isCompact
              actions={<Button label="Clear Search" variant="secondary" onClick={clearSearch} />}
            />
          }
        />
      )}
    </Page>
  );
}

