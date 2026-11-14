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
  useAssignRoles,
  useCreateUser,
  useDeleteUser,
  useResetPassword,
  useRevokeRoles,
} from '../../api/users';
import {useCurrentUser} from '../../auth/auth';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {SearchDialog, type SearchValues} from '../../components/modals/SearchDialog';
import {SelectionDialog} from '../../components/modals/SelectionDialog';
import {Page} from '../../components/pages/Page';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {AvatarCell} from '../../components/tables/AvatarCell';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {TableActions} from '../../components/tables/TableActions';
import {hasUserFilters, type UsersListSearch} from '../listSearch';

type User = NonNullable<UserPage['items']>[number];
type UserRow = User & Record<string, unknown>;

type DialogState =
  | {kind: 'create'}
  | {kind: 'assign' | 'revoke' | 'reset' | 'delete'; username: string; roles: string[]}
  | null;

export interface UsersPageProps {
  search: UsersListSearch;
  onSearchChange: (search: UsersListSearch) => void;
}

/**
 * Admin user management (replaces Blazor's UsersTable): one page of users with their roles, a search, and
 * dialogs to create users, assign and revoke roles, reset passwords and delete users. Admins can't change
 * their own account here, as in Blazor.
 */
export function UsersPage({search, onSearchChange}: UsersPageProps) {
  const me = useCurrentUser();
  const page = search.page ?? 1;
  const ascending = search.asc ?? true;
  const isFiltered = hasUserFilters(search);

  const {data, isPending, isError} = useQuery(
    userSearchQuery({Page: page, Sort: 'username', Ascending: ascending, Username: search.username, Role: search.role}),
  );
  const {data: allRoles = []} = useQuery(rolesQuery);
  const total = data?.totalCount ?? 0;
  const rows = (data?.items ?? []) as UserRow[];

  const create = useCreateUser();
  const assign = useAssignRoles();
  const revoke = useRevokeRoles();
  const reset = useResetPassword();
  const remove = useDeleteUser();

  const [dialog, setDialog] = useState<DialogState>(null);
  const [selectedRoles, setSelectedRoles] = useState<string[]>([]);
  const [newUsername, setNewUsername] = useState('');
  const [newPassword, setNewPassword] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSearchOpen, setIsSearchOpen] = useState(false);
  const [draft, setDraft] = useState<SearchValues>({});

  const open = (next: DialogState) => {
    setSelectedRoles([]);
    setNewUsername('');
    setNewPassword(null);
    create.reset();
    setError(null);
    setDialog(next);
  };
  const close = () => setDialog(null);
  const failed = (message: string) => () => {
    setError(message);
    close();
  };

  const applySearch = () => {
    const text = (key: string) => {
      const value = draft[key];
      return typeof value === 'string' && value.trim() ? value.trim() : undefined;
    };
    onSearchChange({asc: search.asc, username: text('username'), role: text('role')});
    setIsSearchOpen(false);
  };
  const clearSearch = () => {
    onSearchChange({asc: search.asc});
    setIsSearchOpen(false);
  };

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
              {label: 'Assign roles', icon: AppIcon.Assign, isDisabled: isMe, onClick: () => open({kind: 'assign', ...target})},
              {label: 'Revoke roles', icon: AppIcon.Revoke, isDisabled: isMe, onClick: () => open({kind: 'revoke', ...target})},
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
      maxWidth={1400}
      actions={
        <>
          {isFiltered && <Button label="Clear Search" variant="ghost" icon={<Icon icon={AppIcon.Cancel} />} onClick={clearSearch} />}
          <Button
            label="Search"
            variant="secondary"
            icon={<Icon icon={AppIcon.Search} />}
            onClick={() => {
              setDraft({username: search.username ?? '', role: search.role ?? ''});
              setIsSearchOpen(true);
            }}
          />
          <Button label="Add User" variant="primary" icon={<Icon icon={AppIcon.Add} />} onClick={() => open({kind: 'create'})} />
        </>
      }
      dialogs={
        <>
          <SearchDialog
            isOpen={isSearchOpen}
            title="Search Users"
            fields={[
              {type: 'text', key: 'username', label: 'Username', placeholder: 'Search by username'},
              {type: 'select', key: 'role', label: 'Role', placeholder: 'Any role', options: allRoles.map((r) => ({value: r, label: r}))},
            ]}
            values={draft}
            onValuesChange={setDraft}
            onSearch={applySearch}
            onClear={clearSearch}
            onClose={() => setIsSearchOpen(false)}
          />

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
            isOpen={dialog?.kind === 'assign'}
            title="Assign Roles"
            message={`Select roles to assign to ${target?.username}.`}
            icon={AppIcon.Assign}
            listLabel="Roles to assign"
            items={allRoles.filter((r) => !target?.roles.includes(r)).map((r) => ({value: r, label: r}))}
            selected={selectedRoles}
            onSelectedChange={setSelectedRoles}
            emptyTitle="This user already has every role."
            confirmText="Assign Roles"
            isProcessing={assign.isPending}
            onConfirm={() =>
              target && assign.mutate({username: target.username, roles: selectedRoles}, {onSuccess: close, onError: failed('The roles could not be assigned.')})
            }
            onCancel={close}
          />

          <SelectionDialog
            isOpen={dialog?.kind === 'revoke'}
            title="Revoke Roles"
            message={`Select roles to revoke from ${target?.username}.`}
            icon={AppIcon.Revoke}
            intent="warning"
            listLabel="Roles to revoke"
            items={(target?.roles ?? []).map((r) => ({value: r, label: r}))}
            selected={selectedRoles}
            onSelectedChange={setSelectedRoles}
            emptyTitle="This user has no roles to revoke."
            confirmText="Revoke Roles"
            isProcessing={revoke.isPending}
            onConfirm={() =>
              target && revoke.mutate({username: target.username, roles: selectedRoles}, {onSuccess: close, onError: failed('The roles could not be revoked.')})
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
            message="This action cannot be undone."
            isDanger
            confirmText="Delete User"
            isProcessing={remove.isPending}
            onConfirm={() => target && remove.mutate(target.username, {onSuccess: close, onError: failed('The user could not be deleted.')})}
            onCancel={close}>
            <MetadataList>
              <MetadataListItem label="User">{target?.username}</MetadataListItem>
            </MetadataList>
          </ConfirmDialog>
        </>
      }>
      {(isError || error) && <AlertMessage message={error ?? 'The users could not be loaded. Please try again.'} />}
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

