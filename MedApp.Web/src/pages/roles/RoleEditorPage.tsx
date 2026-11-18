import {CheckboxList, CheckboxListItem} from '@astryxdesign/core/CheckboxList';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {List, ListItem} from '@astryxdesign/core/List';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {Button} from '@astryxdesign/core/Button';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextArea} from '@astryxdesign/core/TextArea';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useQuery, useQueryClient} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {useEffect, useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {ApiError, fieldStatus} from '../../api/errors';
import {people, roleQuery, useRoleActions, useSaveRole, type Role} from '../../api/roles';
import {PERMISSION_CATALOGUE, type Permission} from '../../auth/permissions';
import {FormCard} from '../../components/forms/FormCard';
import {FormSection} from '../../components/forms/FormSection';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {TwoColumns} from '../../components/pages/TwoColumns';
import {PanelCard} from '../../components/views/PanelCard';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../saveProblem';
import {inCatalogueOrder, permissionChanges, permissionGroups, permissionLabel} from './rolePermissions';
import {roleHref} from './RolesPage';

const BACK = '/admin/roles';

/** The most a role's description may be (the API refuses longer: 400; a name's 50 is the API's to say). */
const MAX_DESCRIPTION = 200;

/** The Users page, searched for one person. */
const userHref = (username: string) => `/admin/users?username=${encodeURIComponent(username)}`;

/**
 * A new role (no `id`), or one to edit. This part loads the role; an unknown one goes back to the list. The editor
 * mounts once it's here, so it starts from what's saved.
 */
export function RoleEditorPage({id}: {id?: string}) {
  const navigate = useNavigate();
  const role = useQuery({...roleQuery(id ?? ''), enabled: id !== undefined});
  const isMissing = role.error instanceof ApiError && role.error.status === 404;

  useEffect(() => {
    if (isMissing) void navigate({to: BACK, replace: true});
  }, [isMissing, navigate]);

  if (id === undefined || role.data) return <RoleEditor key={role.data?.id ?? 'new'} role={role.data ?? null} />;
  return (
    <Page
      title="Role"
      backHref={BACK}
      backLabel="Back to roles and permissions"
      isLoading={role.isPending || isMissing}
      loadingMessage="Loading role..."
      message={role.isError && !isMissing && <AlertMessage message="The role could not be loaded. Please try again." />}
    />
  );
}

type Confirming = 'save' | 'archive' | 'delete' | null;

/**
 * A role's page (decision 62): its name and description, its permissions as ticks in groups with a search box that
 * filters them by name or description, the Administrator-only group read only (only the built-in Admin role has them;
 * the API refuses them on any other), and who holds it, each linking to the Users page. Saving first shows what's added
 * and removed and how many people it affects. Archive, Restore and Delete (only while nobody holds it) are in the
 * footer. The built-in Admin role is shown read only. Every change sends the version this page started from, so
 * another admin's change made meanwhile is refused (409) with the API's words, and the page reads the role again.
 */
function RoleEditor({role}: {role: Role | null}) {
  const navigate = useNavigate();
  const isNarrow = useIsNarrow();
  const queryClient = useQueryClient();
  const announce = useAnnounce();
  const save = useSaveRole();
  const actions = useRoleActions();
  const isBuiltIn = role?.isBuiltIn ?? false;
  const saved = role?.permissions ?? [];
  const [name, setName] = useState(role?.name ?? '');
  const [description, setDescription] = useState(role?.description ?? '');
  const [ticked, setTicked] = useState<string[]>(saved);
  const [filter, setFilter] = useState('');
  const [confirming, setConfirming] = useState<Confirming>(null);
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);

  const holders = role?.holders ?? [];
  const holderCount = role?.holderCount ?? holders.length;
  const {added, removed} = permissionChanges(saved, ticked);
  const renamed = role !== null && name.trim() !== role.name;
  const describedAgain = role !== null && description.trim() !== (role.description ?? '');
  const hasChanges = role === null || renamed || describedAgain || added.length > 0 || removed.length > 0;
  const isBusy = save.isPending || actions.archive.isPending || actions.restore.isPending || actions.remove.isPending;
  const groups = permissionGroups(filter);
  const backToList = () => void navigate({to: BACK});

  /** A refused change: the API's words, and the role read again (a 409 means someone changed it meanwhile). */
  const failed = (action: 'save' | 'add' | 'archive' | 'restore' | 'delete') => (e: unknown) => {
    setProblem(saveProblem(e, action, 'role'));
    setConfirming(null);
    if (role?.id && e instanceof ApiError && e.status === 409)
      void queryClient.invalidateQueries({
        queryKey: roleQuery(role.id).queryKey,
      });
  };

  const submit = () =>
    save.mutate(
      {
        id: role?.id ?? undefined,
        name: name.trim(),
        description: description.trim(),
        permissions: inCatalogueOrder(ticked),
        expectedVersion: role?.version ?? undefined,
      },
      {
        onSuccess: (result) => {
          announce(role ? 'Role saved' : 'Role created');
          setConfirming(null);
          if (role) queryClient.setQueryData(roleQuery(role.id ?? '').queryKey, result);
          else void navigate({to: roleHref(result?.id ?? ''), replace: true});
        },
        onError: failed(role ? 'save' : 'add'),
      },
    );

  const version = () => ({
    id: role?.id ?? '',
    expectedVersion: role?.version ?? '',
  });
  const archiveOrRestore = (archive: boolean) =>
    (archive ? actions.archive : actions.restore).mutate(version(), {
      onSuccess: (result) => {
        announce(archive ? `${role?.name} archived` : `${role?.name} restored`);
        setConfirming(null);
        queryClient.setQueryData(roleQuery(role?.id ?? '').queryKey, result);
      },
      onError: failed(archive ? 'archive' : 'restore'),
    });

  const onGroupChange = (shown: readonly Permission[]) => (values: string[]) =>
    setTicked((current) => [...current.filter((p) => !shown.includes(p as Permission)), ...values]);

  const permissionLists =
    groups.length === 0 ? (
      <EmptyState
        title="No permissions match"
        description="Search by a word in a permission's name or what it allows."
        isCompact
      />
    ) : (
      groups.map(({group, title, permissions}) => {
        const adminOnly = group === 'AdministratorOnly';
        return (
          <CheckboxList
            key={group}
            label={title}
            description={adminOnly ? 'Only the built-in Admin role has these: no other role can be given them.' : undefined}
            value={adminOnly && !isBuiltIn ? [] : ticked.filter((p) => permissions.includes(p as Permission))}
            onChange={onGroupChange(permissions)}
            isReadOnly={isBuiltIn || adminOnly}
            isDisabled={isBusy}
            hasDividers>
            {permissions.map((p) => (
              <CheckboxListItem key={p} value={p} label={permissionLabel(p)} description={PERMISSION_CATALOGUE[p].description} />
            ))}
          </CheckboxList>
        );
      })
    );

  const holderList =
    holders.length === 0 ? (
      <EmptyState title="Nobody holds this role" description="Give it to people on the Users page." isCompact />
    ) : (
      <List hasDividers aria-label="People with this role">
        {holders.map((h) => (
          <ListItem key={h.userId} label={<Link href={userHref(h.username ?? '')}>{h.username}</Link>} />
        ))}
      </List>
    );

  const title = isBuiltIn ? 'Admin Role' : role ? 'Edit Role' : 'New Role';
  const changeLines = [renamed && `Renamed from ${role?.name}`, describedAgain && 'Description changed'].filter(
    Boolean,
  ) as string[];

  const holdersTitle = `People with this role (${holderCount})`;
  const form = (
    <FormCard
      fill
      showFooter={!isBuiltIn}
      saveText={role ? 'Save' : 'Create Role'}
      onSave={
        isBuiltIn
          ? undefined
          : () => {
              setProblem(NO_PROBLEM);
              if (!name.trim()) {
                setProblem({
                  validation: {title: 'Name is required.', fields: {name: ['Name is required.']}, general: []},
                  message: null,
                });
                return;
              }
              if (!hasChanges) {
                setProblem({
                  validation: null,
                  message: 'Nothing has changed, so there is nothing to save.',
                });
                return;
              }
              setConfirming('save');
            }
      }
      onCancel={backToList}
      onDelete={role?.canDelete ? () => setConfirming('delete') : undefined}
      deleteText="Delete Role"
      footerActions={
        role?.canEdit &&
        (role.isArchived ? (
          <Button
            label="Restore"
            variant="secondary"
            icon={<Icon icon={AppIcon.Restore} />}
            isDisabled={isBusy}
            onClick={() => archiveOrRestore(false)}
          />
        ) : (
          <Button
            label="Archive"
            variant="secondary"
            icon={<Icon icon={AppIcon.Archive} />}
            isDisabled={isBusy}
            onClick={() => setConfirming('archive')}
          />
        ))
      }
      isProcessing={isBusy}>
      <FormSection title="Details" useGrid={false}>
        <TextInput
          label="Name"
          value={name}
          onChange={setName}
          placeholder="e.g. Visiting consultant"
          autoComplete="off"
          isRequired
          isDisabled={isBusy}
          isReadOnly={isBuiltIn}
          status={fieldStatus(problem.validation, 'name')}
        />
        <TextArea
          label="Description"
          description="What the role is for, as the Users page shows it when giving roles. Not for patient details."
          value={description}
          onChange={setDescription}
          rows={2}
          isDisabled={isBusy}
          isReadOnly={isBuiltIn}
          maxLength={MAX_DESCRIPTION}
          status={fieldStatus(problem.validation, 'description')}
        />
      </FormSection>
      <FormSection title={`Permissions (${ticked.length})`} useGrid={false}>
        <VStack gap={4}>
          <Text type="supporting" color="secondary">
            Every role also lets its holders use the app: {PERMISSION_CATALOGUE.UseApp.description.replace(/^Use the app: /, '')}
          </Text>
          <TextInput
            label="Search permissions"
            value={filter}
            onChange={setFilter}
            placeholder="e.g. calendar"
            autoComplete="off"
          />
          {permissionLists}
        </VStack>
      </FormSection>
      {role && isNarrow && (
        <FormSection title={holdersTitle} useGrid={false}>
          {holderList}
        </FormSection>
      )}
    </FormCard>
  );

  return (
    <Page
      title={title}
      subtitle={role?.name ?? undefined}
      onBack={backToList}
      backLabel="Back to roles and permissions"
      fill
      message={
        problem.validation ? (
          <ValidationAlert errors={problem.validation} fields={['name', 'description']} />
        ) : problem.message ? (
          <AlertMessage message={problem.message} />
        ) : isBuiltIn ? (
          <AlertMessage
            status="info"
            message="The Admin role is built in: what it allows is fixed, and it can't be renamed, archived or deleted."
          />
        ) : (
          role?.isArchived && (
            <AlertMessage
              status="warning"
              message="This role is archived: nobody new can be given it. The people who hold it keep it."
            />
          )
        )
      }
      dialogs={
        <>
          <ConfirmDialog
            isOpen={confirming === 'save'}
            title={role ? 'Confirm Changes' : 'Confirm New Role'}
            message={
              role
                ? holderCount > 0 && (added.length > 0 || removed.length > 0)
                  ? `This changes what ${people(holderCount)} can do, from their next click.`
                  : holderCount > 0
                    ? `${people(holderCount)} ${holderCount === 1 ? 'holds' : 'hold'} this role. What they can do doesn't change.`
                    : 'Nobody holds this role yet.'
                : 'Nobody holds it yet: give it to people on the Users page.'
            }
            icon={role ? AppIcon.Save : AppIcon.Add}
            confirmText={role ? 'Confirm Save' : 'Create Role'}
            isProcessing={save.isPending}
            onConfirm={submit}
            onCancel={() => setConfirming(null)}>
            <MetadataList>
              <MetadataListItem label="Role">{name.trim() || 'No name'}</MetadataListItem>
              {changeLines.length > 0 && <MetadataListItem label="Details">{changeLines.join('. ')}</MetadataListItem>}
              <MetadataListItem label={role ? 'Added' : 'Permissions'}>
                {added.length ? added.map(permissionLabel).join(', ') : 'None'}
              </MetadataListItem>
              {role && (
                <MetadataListItem label="Removed">
                  {removed.length ? removed.map(permissionLabel).join(', ') : 'None'}
                </MetadataListItem>
              )}
            </MetadataList>
          </ConfirmDialog>

          <ConfirmDialog
            isOpen={confirming === 'archive'}
            title="Archive Role"
            message="Nobody new can be given it. The people who hold it keep it, and what it lets them do, until it's taken from them. It can be restored."
            icon={AppIcon.Archive}
            intent="warning"
            confirmText="Archive"
            isProcessing={actions.archive.isPending}
            onConfirm={() => archiveOrRestore(true)}
            onCancel={() => setConfirming(null)}>
            <MetadataList>
              <MetadataListItem label="Role">{role?.name}</MetadataListItem>
              <MetadataListItem label="Held by">{people(holderCount)}</MetadataListItem>
            </MetadataList>
          </ConfirmDialog>

          <ConfirmDialog
            isOpen={confirming === 'delete'}
            title="Confirm Delete"
            message="Nobody holds this role. This action cannot be undone."
            isDanger
            confirmText="Delete Role"
            isProcessing={actions.remove.isPending}
            onConfirm={() =>
              actions.remove.mutate(role?.id ?? '', {
                onSuccess: () => {
                  announce(`${role?.name} deleted`);
                  backToList();
                },
                onError: failed('delete'),
              })
            }
            onCancel={() => setConfirming(null)}>
            <MetadataList>
              <MetadataListItem label="Role">{role?.name}</MetadataListItem>
            </MetadataList>
          </ConfirmDialog>
        </>
      }>
      {role && !isNarrow ? <TwoColumns start={form} end={<PanelCard title={holdersTitle}>{holderList}</PanelCard>} /> : form}
    </Page>
  );
}
