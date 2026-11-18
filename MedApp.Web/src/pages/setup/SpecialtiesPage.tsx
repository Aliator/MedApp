import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {VStack} from '@astryxdesign/core/Stack';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';
import {TextInput} from '@astryxdesign/core/TextInput';
import {Token} from '@astryxdesign/core/Token';
import {useQuery, useQueryClient} from '@tanstack/react-query';
import {useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {useListSettings} from '../../app/listSettings';
import {fieldStatus} from '../../api/errors';
import {specialtiesQuery, useSaveSpecialty, useSpecialtyActions, type Specialty} from '../../api/specialties';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page} from '../../components/pages/Page';
import {SetupNameCell, SetupStatusToken} from '../../components/pathways/SetupCells';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {SummaryAlert} from '../../components/shared/SummaryAlert';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {ListFilterBar} from '../../components/tables/ListFilterBar';
import {TableActions} from '../../components/tables/TableActions';
import type {SetupListSearch} from '../listSearch';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../saveProblem';
import {fromSetupBar, SETUP_FAVOURITES, SETUP_FILTER_FIELDS, setupBarValues, setupPage, setupSubtitle} from './setupList';

type SpecialtyRow = Specialty & Record<string, unknown>;

type DialogState = {kind: 'edit'; specialty: Specialty | null} | {kind: 'archive' | 'delete'; specialty: Specialty} | null;

/** The whole list, archived ones too: the page filters it (`setupPage`). The route preloads it. */
export const specialtiesPageQuery = specialtiesQuery(true);

const count = (n: number | undefined, one: string, many: string) => `${n ?? 0} ${n === 1 ? one : many}`;

/** What uses a specialty, in words: "2 templates, 14 pathways, 5 referrals, 3 members of staff", or "Not used yet". */
export function specialtyUse(specialty: Specialty): string {
  const parts = [
    specialty.templateCount ? count(specialty.templateCount, 'template', 'templates') : '',
    specialty.pathwayCount ? count(specialty.pathwayCount, 'pathway', 'pathways') : '',
    specialty.referralCount ? count(specialty.referralCount, 'referral', 'referrals') : '',
    specialty.memberCount ? count(specialty.memberCount, 'member of staff', 'members of staff') : '',
  ].filter(Boolean);
  return parts.length ? parts.join(', ') : 'Not used yet';
}

/**
 * The specialties nobody can triage referrals to: not archived, and none of their members holds TriageReferrals
 * (`triagerCount` 0). Their referrals would wait with nobody able to accept, reject or redirect them (hazard H-40).
 */
export const untriageable = (specialties: readonly Specialty[]) => specialties.filter((s) => !s.isArchived && s.triagerCount === 0);

/** Names in a list: "Dermatology", "Dermatology and Urology", "Cardiology, Dermatology and Urology". */
const namesInWords = (names: readonly string[]) => (names.length < 2 ? (names[0] ?? '') : `${names.slice(0, -1).join(', ')} and ${names.at(-1)}`);

/** The page's warning about specialties nobody can triage, in words. */
export function untriageableWords(specialties: readonly Specialty[]): string {
  const names = untriageable(specialties).map((s) => s.name ?? '');
  const these = names.length === 1 ? 'this specialty' : 'these specialties';
  return `Nobody can triage referrals to ${namesInWords(names)}: no member of ${these} holds the Triage referrals permission. Give one a role that has it, or add a member who has.`;
}

export interface SpecialtiesPageProps {
  search: SetupListSearch;
  onSearchChange: (search: SetupListSearch) => void;
}

/**
 * Specialties (admin): what pathway templates belong to and staff work in, such as Dermatology or Urology. Admins add
 * and rename them, archive ones no longer wanted (templates, pathways and staff that have one keep it, but it isn't
 * offered for new ones), restore them, and delete ones nothing has used. Renaming, archiving and restoring send the
 * version shown, so a change someone made meanwhile is refused (409) with the API's words, and the list is read again.
 * Members of staff are given their specialties on the Users page.
 */
export function SpecialtiesPage({search, onSearchChange}: SpecialtiesPageProps) {
  const {data = [], isPending, isError} = useQuery(specialtiesPageQuery);
  const save = useSaveSpecialty();
  const actions = useSpecialtyActions();
  const announce = useAnnounce();
  const queryClient = useQueryClient();
  const [dialog, setDialog] = useState<DialogState>(null);
  const [name, setName] = useState('');
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);
  const [error, setError] = useState<string | null>(null);

  const {pageSize} = useListSettings();
  const {rows, page, total} = setupPage(data, search, pageSize);
  const close = () => setDialog(null);

  const openEditor = (specialty: Specialty | null) => {
    setName(specialty?.name ?? '');
    setProblem(NO_PROBLEM);
    setError(null);
    setDialog({kind: 'edit', specialty});
  };
  const open = (kind: 'archive' | 'delete', specialty: Specialty) => {
    setError(null);
    setDialog({kind, specialty});
  };
  const failed = (action: 'archive' | 'restore' | 'delete') => (e: unknown) => {
    setError(saveProblem(e, action, 'specialty').message);
    close();
  };
  const versionOf = (specialty: Specialty) => ({id: specialty.id ?? '', expectedVersion: specialty.version ?? 0});

  const editing = dialog?.kind === 'edit' ? dialog.specialty : null;
  const target = dialog && dialog.kind !== 'edit' ? dialog.specialty : null;

  const saveSpecialty = () =>
    save.mutate(
      editing ? {id: editing.id, name: name.trim(), expectedVersion: editing.version ?? 0} : {name: name.trim()},
      {
        onSuccess: () => {
          announce(editing ? 'Specialty renamed' : 'Specialty added');
          close();
        },
        // A 409 (the name is taken, or someone changed it meanwhile) keeps the dialog open with the API's words. The
        // list has been read again by then (`useSaveSpecialty`), so saving again, having been told, starts from the
        // version that's there now.
        onError: (e) => {
          setProblem(saveProblem(e, editing ? 'save' : 'add', 'specialty'));
          const fresh = editing && queryClient.getQueryData(specialtiesPageQuery.queryKey)?.find((s) => s.id === editing.id);
          if (fresh) setDialog({kind: 'edit', specialty: fresh});
        },
      },
    );

  const columns: DataTableColumn<SpecialtyRow>[] = [
    {
      key: 'name',
      header: 'Specialty',
      width: proportional(3, {minWidth: 140}),
      renderCell: (s) => (
        <SetupNameCell
          name={s.name ?? ''}
          secondary={specialtyUse(s)}
          token={!s.isArchived && s.triagerCount === 0 ? <Token size="sm" label="Nobody can triage" color="orange" icon={<Icon icon={AppIcon.Warning} />} /> : undefined}
        />
      ),
    },
    {key: 'templates', header: 'Templates', width: pixel(100), align: 'end', renderCell: (s) => <Text type="body">{s.templateCount ?? 0}</Text>},
    {key: 'pathways', header: 'Pathways', width: pixel(100), align: 'end', renderCell: (s) => <Text type="body">{s.pathwayCount ?? 0}</Text>},
    {key: 'members', header: 'Staff', width: pixel(80), align: 'end', renderCell: (s) => <Text type="body">{s.memberCount ?? 0}</Text>},
    {key: 'status', header: 'Status', width: pixel(100), renderCell: (s) => <SetupStatusToken isArchived={s.isArchived ?? false} />},
    {
      key: 'actions',
      header: 'Actions',
      align: 'end',
      width: pixel(124),
      renderCell: (s) => (
        <TableActions
          actions={[
            {label: 'Rename specialty', icon: AppIcon.Edit, onClick: () => openEditor(s)},
            s.isArchived
              ? {
                  label: 'Restore specialty',
                  icon: AppIcon.Restore,
                  onClick: () => {
                    setError(null);
                    actions.restore.mutate(versionOf(s), {onError: failed('restore')});
                  },
                }
              : {label: 'Archive specialty', icon: AppIcon.Archive, onClick: () => open('archive', s)},
            {
              label: s.canBeDeleted ? 'Delete specialty' : 'Used by templates, pathways or staff, so it can be archived but not deleted',
              icon: AppIcon.Delete,
              isDestructive: true,
              isDisabled: !s.canBeDeleted,
              onClick: () => open('delete', s),
            },
          ]}
        />
      ),
    },
  ];

  const subtitle = isPending ? 'Loading specialties...' : setupSubtitle(total, search, 'specialty', 'specialties');

  const summary = target && (
    <MetadataList>
      <MetadataListItem label="Specialty">{target.name}</MetadataListItem>
      <MetadataListItem label="Used by">{specialtyUse(target)}</MetadataListItem>
    </MetadataList>
  );

  return (
    <Page
      title="Specialties"
      subtitle={subtitle}
      fill
      message={
        error ? (
          <AlertMessage message={error} />
        ) : (
          isError ? (
            <AlertMessage message="The specialties could not be loaded. Please try again." />
          ) : (
            untriageable(data).length > 0 && (
              <SummaryAlert
                status="warning"
                label="Nobody can triage"
                summary={namesInWords(untriageable(data).map((s) => s.name ?? ''))}
                message={untriageableWords(data)}
              />
            )
          )
        )
      }
      actions={<Button label="Add Specialty" variant="primary" icon={<Icon icon={AppIcon.Add} />} onClick={() => openEditor(null)} />}
      dialogs={
        <>
          <ConfirmDialog
            isOpen={dialog?.kind === 'edit'}
            title={editing ? 'Rename Specialty' : 'Add Specialty'}
            message={
              editing
                ? 'Templates, pathways and staff show the new name everywhere they have it.'
                : 'Each pathway template belongs to a specialty, and staff can be given the ones they work in.'
            }
            icon={editing ? AppIcon.Edit : AppIcon.Add}
            confirmText={editing ? 'Save' : 'Add Specialty'}
            isProcessing={save.isPending}
            confirmDisabled={!name.trim()}
            hasSaveShortcut
            onConfirm={saveSpecialty}
            onCancel={close}>
            <VStack gap={2}>
              {problem.validation && <ValidationAlert errors={problem.validation} fields={['name']} />}
              {problem.message && <AlertMessage message={problem.message} />}
              {/* Only while it's open, so its autofocus can't act in a closing dialog. */}
              {dialog?.kind === 'edit' && (
                <TextInput
                  label="Name"
                  value={name}
                  onChange={setName}
                  placeholder="e.g. Dermatology"
                  autoComplete="off"
                  isRequired
                  hasAutoFocus
                  status={fieldStatus(problem.validation, 'name')}
                />
              )}
            </VStack>
          </ConfirmDialog>

          <ConfirmDialog
            isOpen={dialog?.kind === 'archive'}
            title="Archive Specialty"
            message="It won't be offered for new templates or given to more staff. Templates, pathways and staff that have it keep it, and it can be restored."
            icon={AppIcon.Archive}
            intent="warning"
            confirmText="Archive"
            isProcessing={actions.archive.isPending}
            onConfirm={() => target && actions.archive.mutate(versionOf(target), {onSuccess: close, onError: failed('archive')})}
            onCancel={close}>
            {summary}
          </ConfirmDialog>

          <ConfirmDialog
            isOpen={dialog?.kind === 'delete'}
            title="Confirm Delete"
            message="No template, pathway or member of staff has this specialty. This action cannot be undone."
            isDanger
            confirmText="Delete Specialty"
            isProcessing={actions.remove.isPending}
            onConfirm={() => target && actions.remove.mutate(target.id ?? '', {onSuccess: close, onError: failed('delete')})}
            onCancel={close}>
            {summary}
          </ConfirmDialog>
        </>
      }>
      {!isPending && !isError && data.length === 0 ? (
        <EmptyState
          title="No specialties yet"
          description="Each pathway template belongs to a specialty, such as Dermatology or Urology. A specialty in use can only be archived."
          icon={<Icon icon={AppIcon.Specialties} size="lg" />}
          actions={<Button label="Add Your First Specialty" variant="primary" onClick={() => openEditor(null)} />}
        />
      ) : (
        <DataTable
          data={rows as SpecialtyRow[]}
          columns={columns}
          idKey="id"
          isLoading={isPending}
          search={
            <ListFilterBar
              fields={SETUP_FILTER_FIELDS}
              values={setupBarValues(search)}
              onChange={(values) => onSearchChange({...fromSetupBar(values), page: undefined})}
              list="specialties"
              isUnfiltered={!search.name && search.status === undefined}
              label="Search specialties"
              placeholder="Search specialties by name, or filter by status..."
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
              title="No specialties match your search"
              description="Search for archived ones too, to see and restore them."
              isCompact
              actions={<Button label="Show Archived" variant="secondary" onClick={() => onSearchChange({...search, page: undefined, status: 'any'})} />}
            />
          }
        />
      )}
    </Page>
  );
}
