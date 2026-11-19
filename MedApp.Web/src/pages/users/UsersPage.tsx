import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import type {InputStatus} from '@astryxdesign/core/Field';
import {TextInput} from '@astryxdesign/core/TextInput';
import {pixel} from '@astryxdesign/core/Table';
import {Token} from '@astryxdesign/core/Token';
import {useQuery} from '@tanstack/react-query';
import {useMemo, useState} from 'react';
import {featuresQuery} from '../../api/account';
import {ApiValidationError, allMessages, fieldStatus, problemMessage, type ValidationErrors} from '../../api/errors';
import {userSearchQuery, type UserPage} from '../../api/search';
import {
  useCreateUser,
  useDeleteUser,
  useRemoveUserEmail,
  useResetPassword,
  useResetTwoFactor,
  useSetRoles,
  useSignOutEverywhere,
  type CreatedUser,
} from '../../api/users';
import {roleChoices, roleNames, rolesQuery} from '../../api/roles';
import {ACCOUNT_TYPE_LABELS, accountTypeOf, isAccountType} from '../../auth/accountTypes';
import {ADMIN_ROLE, sortRoles} from '../../auth/roles';
import {useCan, useCurrentUser} from '../../auth/auth';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {SelectionDialog} from '../../components/modals/SelectionDialog';
import {Page} from '../../components/pages/Page';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {OneTimePassword} from '../../components/shared/OneTimePassword';
import {AvatarCell} from '../../components/tables/AvatarCell';
import {ListFilterBar} from '../../components/tables/ListFilterBar';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {TableActions} from '../../components/tables/TableActions';
import {TextCell} from '../../components/tables/TextCell';
import {StaffDetailsDialog} from './StaffDetailsDialog';
import {UserTeamsDialog} from './UserTeamsDialog';
import {hasUserFilters, type UsersListSearch, choiceValues} from '../listSearch';
import {useListSettings} from '../../app/listSettings';

type User = NonNullable<UserPage['items']>[number];

/** The same roles, in any order. */
const sameRoles = (a: readonly string[], b: readonly string[]) => a.length === b.length && a.every((r) => b.includes(r));
type UserRow = User & Record<string, unknown>;

/**
 * How long "Done" ignores clicks once a generated password appears in its place: the second click of a double-click
 * on "Create User" or "Reset Password" would otherwise close the dialog before the password was seen.
 */
const PASSWORD_DONE_HOLD_MS = 1000;

type DialogState =
  | {kind: 'create'}
  | {
      kind: 'roles' | 'teams' | 'details' | 'reset' | 'resetTwoFactor' | 'signOut' | 'removeEmail' | 'delete';
      username: string;
      roles: string[];
      email: string | null;
      fullName: string | null;
      jobTitle: string | null;
    }
  | null;

/** The role to ask the API for: the one chosen, or none when the choice covers every role (or none at all). */
function oneRole(role: string | undefined, roles: readonly string[]) {
  const chosen = choiceValues(role, roles);
  return chosen?.length === 1 ? chosen[0] : undefined;
}

/**
 * The username field's messages from a failed create: its own, and those that belong to no field (Identity's, such as
 * a name already taken). The email field shows its own; anything else (or nothing at all) shows here too.
 */
function usernameProblems(errors: ValidationErrors): InputStatus | undefined {
  if (errors.fields.email) {
    const own = [...(errors.fields.username ?? []), ...errors.general];
    return own.length ? {type: 'error', message: own.join(' ')} : undefined;
  }
  return {type: 'error', message: allMessages(errors).join(' ') || errors.title};
}

/**
 * A user's email address, on one line (rows are a fixed height): the confirmed one, or one waiting for its link to be
 * opened, marked Unconfirmed. With both, the confirmed one, and the one it's changing to, unconfirmed.
 */
function EmailCell({email, pendingEmail}: {email?: string | null; pendingEmail?: string | null}) {
  if (!email && !pendingEmail) {
    return (
      <Text type="supporting" color="secondary">
        None
      </Text>
    );
  }
  return (
    <HStack gap={2} vAlign="center">
      <TextCell text={email ?? pendingEmail} />
      {pendingEmail && <Token label={email ? `Changing to ${pendingEmail} (unconfirmed)` : 'Unconfirmed'} size="sm" color="yellow" />}
    </HStack>
  );
}

/** A user's full name and job title on one line ("Dr Sam Test, Consultant dermatologist"); undefined for neither. */
export function staffDetailsLine(user: {fullName?: string | null; jobTitle?: string | null}): string | undefined {
  return [user.fullName, user.jobTitle].map((part) => part?.trim()).filter(Boolean).join(', ') || undefined;
}

export interface UsersPageProps {
  search: UsersListSearch;
  onSearchChange: (search: UsersListSearch) => void;
}

/**
 * Admin user management (replaces Blazor's UsersTable): one page of users with their roles, a search, and
 * dialogs to create users (the server generates the first password, shown once; an email address is optional, and
 * asked for only when the server can send email), edit their roles (every role that isn't archived, with its description, theirs ticked), reset passwords,
 * set the teams each works in (`UserTeamsDialog`) and the full name and job title that go on their letters
 * (`StaffDetailsDialog`; shown under each username), reset two-factor authentication (for someone who has lost their app), sign users out everywhere (every session they
 * have ends), remove a user's email address (so reset links can't go to an address that isn't theirs any more) and
 * delete users (confirmed with the admin's own password). Each user's email address shows, one waiting to be confirmed
 * marked so, and whether they use two-factor authentication. Admins can't change their own account here, as in Blazor:
 * their own email address is on Account Settings' Email Address page, which asks for their password.
 */
/** The list's page of users, for its search and page size, given every role (the route's loader fetches it too). */
export function usersListQuery(search: UsersListSearch, pageSize: number, allRoles: readonly string[]) {
  return userSearchQuery({
    Page: search.page ?? 1,
    PageSize: pageSize,
    Sort: 'username',
    Ascending: search.asc ?? true,
    Username: search.username,
    // The API takes one role: any of them all is no filter at all.
    Role: oneRole(search.role, allRoles),
  });
}

export function UsersPage({search, onSearchChange}: UsersPageProps) {
  const me = useCurrentUser();
  const canManageUsers = useCan('ManageUsers');
  const page = search.page ?? 1;
  const ascending = search.asc ?? true;
  const isFiltered = hasUserFilters(search);
  const {pageSize} = useListSettings();
  const {data: roles} = useQuery(rolesQuery);
  const allRoles = useMemo(() => roleNames(roles ?? []), [roles]);
  // Without a way to send email, an address couldn't be confirmed, so none is asked for.
  const canSendEmail = useQuery(featuresQuery).data?.canSendEmail === true;

  const {data, isPending, isError} = useQuery(usersListQuery(search, pageSize, allRoles));
  const total = data?.totalCount ?? 0;
  const rows = (data?.items ?? []) as UserRow[];

  const create = useCreateUser();
  const setRoles = useSetRoles();
  const reset = useResetPassword();
  const remove = useDeleteUser();
  const signOut = useSignOutEverywhere();
  const resetTwoFactor = useResetTwoFactor();
  const removeEmail = useRemoveUserEmail();

  const [dialog, setDialog] = useState<DialogState>(null);
  const [selectedRoles, setSelectedRoles] = useState<string[]>([]);
  const [newUsername, setNewUsername] = useState('');
  const [newEmail, setNewEmail] = useState('');
  const [newPassword, setNewPassword] = useState<string | null>(null);
  // The account just created, with its generated first password: shown once, then cleared when the dialog closes.
  const [created, setCreated] = useState<CreatedUser | null>(null);
  // Deleting a user: the admin's own password, and why it was refused.
  const [deletePassword, setDeletePassword] = useState('');
  const [deleteProblem, setDeleteProblem] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  // What the last action did, when there's nothing else to show for it (signing someone out everywhere).
  const [notice, setNotice] = useState<string | null>(null);

  const open = (next: DialogState) => {
    // Edit roles starts from the user's current roles, ticked.
    setSelectedRoles(next && next.kind !== 'create' ? next.roles : []);
    setNewUsername('');
    setNewEmail('');
    setNewPassword(null);
    setCreated(null);
    setDeletePassword('');
    setDeleteProblem(null);
    create.reset();
    setError(null);
    setNotice(null);
    setDialog(next);
  };
  const close = () => {
    setDialog(null);
    // However the dialog closed (Done, Cancel, Escape or its close button), the generated passwords go from the
    // page's state and from the mutations, so they don't stay in memory.
    setCreated(null);
    setNewPassword(null);
    create.reset();
    reset.reset();
  };
  const failed = (message: string) => () => {
    setError(message);
    close();
  };

  /** Keeps the order; clears the filters. */
  const clearSearch = () => onSearchChange({asc: search.asc});

  const columns: DataTableColumn<UserRow>[] = [
    {
      key: 'User',
      header: 'User',
      sortable: true,
      // Their full name and job title, as they go on letters, under the username.
      renderCell: (u) => <AvatarCell primaryText={u.username ?? ''} secondaryText={staffDetailsLine(u)} />,
    },
    {
      // Admin, clinic admin or staff: worked out by the API from their roles and teams (decision 182).
      key: 'type',
      header: 'Account type',
      renderCell: (u) => {
        const type = isAccountType(u.accountType) ? u.accountType : accountTypeOf(u.roles ?? [], []);
        return <Token label={ACCOUNT_TYPE_LABELS[type]} size="sm" color={type === 'Admin' ? 'blue' : type === 'ClinicAdmin' ? 'green' : 'default'} />;
      },
    },
    {
      key: 'roles',
      header: 'Roles',
      renderCell: (u) =>
        u.roles?.length ? (
          <HStack gap={1} wrap="wrap">
            {sortRoles(u.roles).map((role) => (
              <Token key={role} label={role} size="sm" color={role === ADMIN_ROLE ? 'blue' : 'default'} />
            ))}
          </HStack>
        ) : (
          <Text type="supporting" color="secondary">
            No roles
          </Text>
        ),
    },
    {
      key: 'email',
      header: 'Email',
      renderCell: (u) => <EmailCell email={u.email} pendingEmail={u.pendingEmail} />,
    },
    {
      key: 'twoFactor',
      header: 'Two-factor',
      renderCell: (u) => <Token label={u.twoFactorEnabled ? 'On' : 'Off'} size="sm" color={u.twoFactorEnabled ? 'green' : 'default'} />,
    },
    {
      key: 'actions',
      header: 'Actions',
      align: 'end',
      // Up to nine buttons: a column sized to fit them all, so none is covered by the cell beside it.
      width: pixel(330),
      renderCell: (u) => {
        const username = u.username ?? '';
        const roles = [...(u.roles ?? [])];
        const isMe = username.toLowerCase() === me.username.toLowerCase();
        const target = {username, roles, email: u.email ?? u.pendingEmail ?? null, fullName: u.fullName ?? null, jobTitle: u.jobTitle ?? null};
        return (
          <TableActions
            actions={[
              {label: 'Edit roles', icon: AppIcon.Assign, isDisabled: isMe, onClick: () => open({kind: 'roles', ...target})},
              // Unlike roles, admins may set their own (an admin is in no team, so the API refuses giving one).
              {label: 'Edit teams', icon: AppIcon.Teams, onClick: () => open({kind: 'teams', ...target})},
              // Their own too: a name and job title don't change what anyone can do.
              ...(canManageUsers ? [{label: 'Edit details', icon: AppIcon.StaffDetails, onClick: () => open({kind: 'details', ...target})}] : []),
              {label: 'Reset password', icon: AppIcon.Lock, isDisabled: isMe, onClick: () => open({kind: 'reset', ...target})},
              {
                label: 'Reset two-factor',
                icon: AppIcon.TwoFactor,
                isDisabled: isMe || !u.twoFactorEnabled,
                onClick: () => open({kind: 'resetTwoFactor', ...target}),
              },
              {label: 'Sign out everywhere', icon: AppIcon.Logout, isDisabled: isMe, onClick: () => open({kind: 'signOut', ...target})},
              {
                label: 'Remove email address',
                icon: AppIcon.Email,
                isDisabled: isMe || target.email === null,
                onClick: () => open({kind: 'removeEmail', ...target}),
              },
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
      message={
        isError || error ? (
          <AlertMessage message={error ?? 'The users could not be loaded. Please try again.'} />
        ) : (
          notice && <AlertMessage message={notice} status="success" />
        )
      }
      actions={
        <Button label="Add User" variant="primary" icon={<Icon icon={AppIcon.Add} />} onClick={() => open({kind: 'create'})} />
      }
      dialogs={
        <>
          <ConfirmDialog
            isOpen={dialog?.kind === 'create'}
            title="Create New User"
            message={
              created
                ? `Account created for ${created.username}.`
                : canSendEmail
                  ? 'Enter a username for the new user, and their email address if you have it.'
                  : 'Enter a username for the new user.'
            }
            icon={AppIcon.Add}
            confirmText={created ? 'Done' : 'Create User'}
            confirmHoldMs={created ? PASSWORD_DONE_HOLD_MS : undefined}
            isProcessing={create.isPending}
            confirmDisabled={!created && !newUsername.trim()}
            onConfirm={() =>
              created
                ? close()
                : create.mutate({username: newUsername.trim(), email: canSendEmail ? newEmail.trim() : undefined}, {onSuccess: setCreated})
            }
            onCancel={close}>
            {created ? (
              <OneTimePassword
                label="First password"
                password={created.password}
                username={created.username}
                announcement={`Account created for ${created.username}.`}
              />
            ) : (
              // Only while it's open, so its autofocus can't act in a closing dialog.
              dialog?.kind === 'create' && (
                <VStack gap={4} hAlign="stretch">
                  <TextInput
                    label="Username"
                    value={newUsername}
                    onChange={setNewUsername}
                    placeholder="Enter username"
                    // A new account's name: never a saved login.
                    autoComplete="off"
                    description="A password is generated for the account and shown to you once."
                    isRequired
                    hasAutoFocus
                    status={
                      createErrors
                        ? usernameProblems(createErrors)
                        : create.isError
                          ? {type: 'error', message: 'The user could not be created. Please try again.'}
                          : undefined
                    }
                  />
                  {canSendEmail && (
                    <TextInput
                      label="Email address"
                      type="email"
                      value={newEmail}
                      onChange={setNewEmail}
                      placeholder="name@example.org"
                      // Someone else's address: never one saved for this admin.
                      autoComplete="off"
                      description="We'll email a link to confirm it. The password is never emailed."
                      isOptional
                      status={fieldStatus(createErrors, 'email')}
                    />
                  )}
                </VStack>
              )
            )}
          </ConfirmDialog>

          <SelectionDialog
            isOpen={dialog?.kind === 'roles'}
            title="Edit Roles"
            message={`Tick the roles ${target?.username} should have.`}
            icon={AppIcon.Assign}
            listLabel="Roles"
            // Roles that aren't archived, with what each is for; one archived that they hold stays, to keep or take away.
            items={roleChoices(roles ?? [], target?.roles ?? [])}
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

          <StaffDetailsDialog target={dialog?.kind === 'details' ? dialog : null} onClose={close} onSaved={setNotice} />

          <UserTeamsDialog
            isOpen={dialog?.kind === 'teams'}
            username={target?.username ?? ''}
            onClose={close}
            onSaved={setNotice}
          />

          <ConfirmDialog
            isOpen={dialog?.kind === 'reset'}
            title="Reset Password"
            message={newPassword ? `Password reset for ${target?.username}.` : `Reset password for ${target?.username}?`}
            icon={AppIcon.Lock}
            intent="warning"
            confirmText={newPassword ? 'Done' : 'Reset Password'}
            confirmHoldMs={newPassword ? PASSWORD_DONE_HOLD_MS : undefined}
            isProcessing={reset.isPending}
            onConfirm={() =>
              newPassword
                ? close()
                : target && reset.mutate(target.username, {onSuccess: setNewPassword, onError: failed('The password could not be reset.')})
            }
            onCancel={close}>
            {newPassword && target && (
              <OneTimePassword
                label="New password"
                password={newPassword}
                username={target.username}
                announcement={`Password reset for ${target.username}.`}
              />
            )}
          </ConfirmDialog>

          <ConfirmDialog
            isOpen={dialog?.kind === 'resetTwoFactor'}
            title="Reset Two-Factor"
            message={`Reset two-factor authentication for ${target?.username}? It turns off their two-factor authentication (their authenticator app and recovery codes stop working) and signs them out everywhere. They sign in with their password alone until they set it up again, or are asked to set it up at once if it's required for them.`}
            icon={AppIcon.TwoFactor}
            intent="warning"
            confirmText="Reset Two-Factor"
            isProcessing={resetTwoFactor.isPending}
            onConfirm={() =>
              target &&
              resetTwoFactor.mutate(target.username, {
                onSuccess: () => {
                  close();
                  setNotice(`Two-factor authentication has been reset for ${target.username}, and they've been signed out everywhere.`);
                },
                onError: (e) => failed(problemMessage(e, `Two-factor authentication could not be reset for ${target.username}.`))(),
              })
            }
            onCancel={close}
          />

          <ConfirmDialog
            isOpen={dialog?.kind === 'signOut'}
            title="Sign Out Everywhere"
            message={`Sign ${target?.username} out everywhere? Every session they have ends, on every browser and device, and they must sign in again. Their password doesn't change.`}
            icon={AppIcon.Logout}
            intent="warning"
            confirmText="Sign Out Everywhere"
            isProcessing={signOut.isPending}
            onConfirm={() =>
              target &&
              signOut.mutate(target.username, {
                onSuccess: () => {
                  close();
                  setNotice(`${target.username} has been signed out everywhere.`);
                },
                onError: failed(`${target.username} could not be signed out.`),
              })
            }
            onCancel={close}
          />

          <ConfirmDialog
            isOpen={dialog?.kind === 'removeEmail'}
            title="Remove Email Address"
            message={`Remove ${target?.username}'s email address${target?.email ? ` (${target.email})` : ''}, and any waiting to be confirmed? Password reset links can't be sent to it any more, so use this when the address isn't theirs or may be in the wrong hands. A confirmed address is emailed to say so. They can add one again in Account Settings.`}
            icon={AppIcon.Email}
            isDanger
            confirmText="Remove Email Address"
            isProcessing={removeEmail.isPending}
            onConfirm={() =>
              target &&
              removeEmail.mutate(target.username, {
                onSuccess: () => {
                  close();
                  setNotice(`${target.username}'s email address has been removed.`);
                },
                onError: (e) => failed(problemMessage(e, `The email address could not be removed for ${target.username}.`))(),
              })
            }
            onCancel={close}
          />

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
              list="users"
              label="Search users"
              placeholder="Search users by name, or filter by role..."
              textKey="username"
              favourites={[
                {label: 'All', values: {}},
                {label: 'Admins', values: {role: 'Admin'}},
                {label: 'All staff (before roles)', values: {role: 'All staff (before roles)'}},
              ]}
            />
          }
          skeletonRows={pageSize}
          sort={{column: 'User', ascending}}
          onSortChange={(sort) => onSearchChange({...search, page: undefined, sort: 'User', asc: sort.ascending})}
          pagination={{
            page,
            pageSize,
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

