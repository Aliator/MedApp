import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';
import {Token} from '@astryxdesign/core/Token';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {useListSettings} from '../../app/listSettings';
import {people, rolesQuery, useRoleActions, type Role} from '../../api/roles';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page} from '../../components/pages/Page';
import {SetupNameCell, SetupStatusToken} from '../../components/pathways/SetupCells';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {ListFilterBar} from '../../components/tables/ListFilterBar';
import {TableActions} from '../../components/tables/TableActions';
import type {SetupListSearch} from '../listSearch';
import {saveProblem} from '../saveProblem';
import {fromSetupBar, SETUP_FAVOURITES, SETUP_FILTER_FIELDS, setupBarValues, setupPage, setupSubtitle} from '../setup/setupList';

type RoleRow = Role & Record<string, unknown>;

type DialogState = {kind: 'archive' | 'delete'; role: Role} | null;

/** The address of a role's page. */
export const roleHref = (id: string) => `/admin/roles/${id}`;

/**
 * One page of roles: as a setup list (active ones unless asked for archived ones, by name), but with the built-in Admin
 * role always first, as the API sends it.
 */
export function rolesPage(roles: readonly Role[], search: SetupListSearch, pageSize: number) {
  const builtIn = roles.filter((r) => r.isBuiltIn);
  const others = setupPage(
    roles.filter((r) => !r.isBuiltIn),
    search,
    Number.MAX_SAFE_INTEGER,
  ).rows;
  const shown = [...setupPage(builtIn, search, Number.MAX_SAFE_INTEGER).rows, ...others];
  const pages = Math.max(1, Math.ceil(shown.length / pageSize));
  const page = Math.min(search.page ?? 1, pages);
  return {
    rows: shown.slice((page - 1) * pageSize, page * pageSize),
    page,
    total: shown.length,
  };
}

export interface RolesPageProps {
  search: SetupListSearch;
  onSearchChange: (search: SetupListSearch) => void;
}

/**
 * Roles and Permissions (admin, decision 62): every role with what it's for, how many people hold it, how many
 * permissions it grants, and whether it's archived; Admin first, marked built in. A role's page (its row's Open, or Add
 * Role) chooses its permissions. From here a role can be archived (nobody new can be given it; those who hold it keep
 * it), restored, or deleted when nobody holds it. Archiving and restoring send the version shown, so another admin's
 * change made meanwhile is refused (409) with the API's words, and the list is read again.
 */
export function RolesPage({search, onSearchChange}: RolesPageProps) {
  const {data = [], isPending, isError} = useQuery(rolesQuery);
  const actions = useRoleActions();
  const announce = useAnnounce();
  const [dialog, setDialog] = useState<DialogState>(null);
  const [error, setError] = useState<{label: string; message: string | null} | null>(null);

  const {pageSize} = useListSettings();
  const {rows, page, total} = rolesPage(data, search, pageSize);
  const close = () => setDialog(null);
  const versionOf = (role: Role) => ({
    id: role.id ?? '',
    expectedVersion: role.version ?? '',
  });
  const failed = (action: 'archive' | 'restore' | 'delete') => (e: unknown) => {
    setError({label: `Role not ${action}d`, message: saveProblem(e, action, 'role').message});
    close();
  };
  const target = dialog?.role ?? null;

  const columns: DataTableColumn<RoleRow>[] = [
    {
      key: 'name',
      header: 'Role',
      width: proportional(3, {minWidth: 160}),
      renderCell: (r) => (
        <SetupNameCell
          name={r.name ?? ''}
          secondary={r.description}
          token={r.isBuiltIn ? <Token size="sm" label="Built in" color="blue" /> : undefined}
        />
      ),
    },
    {
      key: 'people',
      header: 'People',
      width: pixel(90),
      align: 'end',
      renderCell: (r) => <Text type="body">{r.holderCount ?? 0}</Text>,
    },
    {
      key: 'permissions',
      header: 'Permissions',
      width: pixel(120),
      align: 'end',
      renderCell: (r) => <Text type="body">{r.permissions?.length ?? 0}</Text>,
    },
    {
      key: 'status',
      header: 'Status',
      width: pixel(100),
      renderCell: (r) => <SetupStatusToken isArchived={r.isArchived ?? false} />,
    },
    {
      key: 'actions',
      header: 'Actions',
      align: 'end',
      width: pixel(124),
      renderCell: (r) => (
        <TableActions
          actions={[
            {
              label: r.canEdit ? `Open ${r.name}` : `View ${r.name}`,
              icon: r.canEdit ? AppIcon.Edit : AppIcon.View,
              href: roleHref(r.id ?? ''),
            },
            ...(!r.canEdit
              ? []
              : [
                  r.isArchived
                    ? {
                        label: `Restore ${r.name}`,
                        icon: AppIcon.Restore,
                        onClick: () => {
                          setError(null);
                          actions.restore.mutate(versionOf(r), {
                            onSuccess: () => announce(`${r.name} restored`),
                            onError: failed('restore'),
                          });
                        },
                      }
                    : {
                        label: `Archive ${r.name}`,
                        icon: AppIcon.Archive,
                        onClick: () => {
                          setError(null);
                          setDialog({kind: 'archive', role: r});
                        },
                      },
                  {
                    label: r.canDelete ? `Delete ${r.name}` : `${r.name}: people hold it, so it can be archived but not deleted`,
                    icon: AppIcon.Delete,
                    isDestructive: true,
                    isDisabled: !r.canDelete,
                    onClick: () => {
                      setError(null);
                      setDialog({kind: 'delete', role: r});
                    },
                  },
                ]),
          ]}
        />
      ),
    },
  ];

  const summary = target && (
    <MetadataList>
      <MetadataListItem label="Role">{target.name}</MetadataListItem>
      <MetadataListItem label="Held by">{people(target.holderCount ?? 0)}</MetadataListItem>
    </MetadataList>
  );

  return (
    <Page
      title="Roles and Permissions"
      subtitle={isPending ? 'Loading roles...' : setupSubtitle(total, search, 'role', 'roles')}
      fill
      message={
        error ? (
          <AlertMessage label={error.label} message={error.message ?? undefined} />
        ) : (
          isError && <AlertMessage label="Roles not loaded" message="The roles could not be loaded. Please try again." />
        )
      }
      actions={<Button label="Add Role" variant="primary" icon={<Icon icon={AppIcon.Add} />} href="/admin/roles/new" />}
      dialogs={
        <>
          <ConfirmDialog
            isOpen={dialog?.kind === 'archive'}
            title="Archive Role"
            message="Nobody new can be given it. The people who hold it keep it, and what it lets them do, until it's taken from them. It can be restored."
            icon={AppIcon.Archive}
            intent="warning"
            confirmText="Archive"
            isProcessing={actions.archive.isPending}
            onConfirm={() =>
              target &&
              actions.archive.mutate(versionOf(target), {
                onSuccess: () => {
                  announce(`${target.name} archived`);
                  close();
                },
                onError: failed('archive'),
              })
            }
            onCancel={close}>
            {summary}
          </ConfirmDialog>

          <ConfirmDialog
            isOpen={dialog?.kind === 'delete'}
            title="Confirm Delete"
            message="Nobody holds this role. This action cannot be undone."
            isDanger
            confirmText="Delete Role"
            isProcessing={actions.remove.isPending}
            onConfirm={() =>
              target &&
              actions.remove.mutate(target.id ?? '', {
                onSuccess: () => {
                  announce(`${target.name} deleted`);
                  close();
                },
                onError: failed('delete'),
              })
            }
            onCancel={close}>
            {summary}
          </ConfirmDialog>
        </>
      }>
      <DataTable
        data={rows as RoleRow[]}
        columns={columns}
        idKey="id"
        isLoading={isPending}
        search={
          <ListFilterBar
            fields={SETUP_FILTER_FIELDS}
            values={setupBarValues(search)}
            onChange={(values) => onSearchChange({...fromSetupBar(values), page: undefined})}
            list="roles"
            isUnfiltered={!search.name && search.status === undefined}
            label="Search roles"
            placeholder="Search roles by name, or filter by status..."
            textKey="name"
            favourites={SETUP_FAVOURITES}
          />
        }
        pagination={{
          page,
          pageSize,
          totalItems: total,
          onPageChange: (next) => onSearchChange({...search, page: next > 1 ? next : undefined}),
        }}
        emptyState={
          <EmptyState
            title="No roles match your search"
            description="Search for archived ones too, to see and restore them."
            isCompact
            actions={
              <Button
                label="Show Archived"
                variant="secondary"
                onClick={() => onSearchChange({...search, page: undefined, status: 'any'})}
              />
            }
          />
        }
      />
    </Page>
  );
}
