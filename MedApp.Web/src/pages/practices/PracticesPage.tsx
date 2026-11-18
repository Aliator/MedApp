import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {VStack} from '@astryxdesign/core/Stack';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useQuery, useQueryClient} from '@tanstack/react-query';
import {useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {useListSettings} from '../../app/listSettings';
import {fieldStatus} from '../../api/errors';
import {
  EMPTY_PRACTICE,
  practiceAddress,
  practiceInput,
  practicesQuery,
  usePracticeActions,
  useSavePractice,
  type Practice,
  type PracticeInput,
} from '../../api/practices';
import {useCan} from '../../auth/auth';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page} from '../../components/pages/Page';
import {SetupNameCell, SetupStatusToken} from '../../components/pathways/SetupCells';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {ListFilterBar} from '../../components/tables/ListFilterBar';
import {TableActions} from '../../components/tables/TableActions';
import type {SetupListSearch} from '../listSearch';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../saveProblem';
import {fromSetupBar, SETUP_FAVOURITES, setupBarValues, setupPage, setupSubtitle} from '../setup/setupList';
import type {ListFilterField} from '../../components/tables/ListFilterBar';

/** The page's query: every practice, archived ones too (the list filters them). Its route's loader preloads the same. */
export const practicesPageQuery = practicesQuery(true);

type PracticeRow = Practice & Record<string, unknown>;

type DialogState = {kind: 'edit'; practice: Practice | null} | {kind: 'archive' | 'delete'; practice: Practice} | null;

/** The fields a practice's 400 can name, whose errors show on them. */
const PRACTICE_FIELDS = ['name', 'odsCode', 'addressLines', 'postcode', 'phone', 'secureEmail'];

/** The search: a name or ODS code, and Active or Archived (the setup lists' status filter, kept in the URL). */
const FILTER_FIELDS: ListFilterField[] = [
  {kind: 'text', key: 'name', label: 'Name or ODS code'},
  {
    kind: 'choice',
    key: 'status',
    label: 'Status',
    options: [
      {value: 'active', label: 'Active'},
      {value: 'archived', label: 'Archived'},
    ],
  },
];

/**
 * One page of the directory: the practices whose name or ODS code contains the text typed, then the setup lists'
 * status filter, by name, paged.
 */
export function practicesPage(practices: readonly Practice[], search: SetupListSearch, pageSize: number) {
  const text = search.name?.trim().toLowerCase();
  const matching = text
    ? practices.filter((p) => (p.name ?? '').toLowerCase().includes(text) || (p.odsCode ?? '').toLowerCase().includes(text))
    : practices;
  return setupPage(matching, {...search, name: undefined}, pageSize);
}

/** How much uses a practice, in words: "3 patients, 1 referral", or "Not used yet". */
export function practiceUse(practice: Pick<Practice, 'patientCount' | 'referralCount'>): string {
  const count = (n: number, one: string, many: string) => (n > 0 ? `${n} ${n === 1 ? one : many}` : '');
  const words = [count(practice.patientCount ?? 0, 'patient', 'patients'), count(practice.referralCount ?? 0, 'referral', 'referrals')].filter(Boolean);
  return words.length ? words.join(', ') : 'Not used yet';
}

export interface PracticesPageProps {
  search: SetupListSearch;
  onSearchChange: (search: SetupListSearch) => void;
}

/**
 * The GP practice directory (`/practices`, for anyone who can view patients; docs/centre-phases-3-5-plan.md,
 * decision 79): each practice's name and address, ODS code, postcode, phone, how many patients and referrals have it,
 * and whether it's archived, searched by name or ODS code. Holders of EditPatients add and edit practices (name, ODS
 * code, three address lines, postcode, phone and a secure email for letters), archive one no longer used (patients and
 * referrals keep it, but it isn't offered for anything new), restore it, and delete one nothing has used. Each change
 * sends the version shown, so one made meanwhile is refused (409) in the API's words, and the list is read again.
 */
export function PracticesPage({search, onSearchChange}: PracticesPageProps) {
  const {data = [], isPending, isError} = useQuery(practicesPageQuery);
  const canEdit = useCan('EditPatients');
  const queryClient = useQueryClient();
  const save = useSavePractice();
  const actions = usePracticeActions();
  const announce = useAnnounce();
  const [dialog, setDialog] = useState<DialogState>(null);
  const [form, setForm] = useState<PracticeInput>(EMPTY_PRACTICE);
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);
  const [error, setError] = useState<string | null>(null);

  const {pageSize} = useListSettings();
  const {rows, page, total} = practicesPage(data, search, pageSize);
  const close = () => setDialog(null);

  const openEditor = (practice: Practice | null) => {
    setForm(practiceInput(practice));
    setProblem(NO_PROBLEM);
    setError(null);
    setDialog({kind: 'edit', practice});
  };
  const open = (kind: 'archive' | 'delete', practice: Practice) => {
    setError(null);
    setDialog({kind, practice});
  };
  const failed = (action: 'archive' | 'restore' | 'delete') => (e: unknown) => {
    setError(saveProblem(e, action, 'practice').message);
    close();
  };
  const versionOf = (practice: Practice) => ({id: practice.id ?? '', expectedVersion: practice.version ?? 0});

  const editing = dialog?.kind === 'edit' ? dialog.practice : null;
  const target = dialog && dialog.kind !== 'edit' ? dialog.practice : null;

  const submit = () =>
    save.mutate(
      {id: editing?.id, expectedVersion: editing?.version ?? undefined, input: form},
      {
        onSuccess: () => {
          announce(editing ? 'Practice saved' : 'Practice added');
          close();
        },
        // A 409 (the ODS code is taken, or someone changed it meanwhile) keeps the dialog open with the API's words. The
        // list has been read again by then (`useSavePractice`), so saving again, having been told, starts from the
        // version that's there now.
        onError: (e) => {
          setProblem(saveProblem(e, editing ? 'save' : 'add', 'practice'));
          const fresh = editing && queryClient.getQueryData(practicesPageQuery.queryKey)?.find((p) => p.id === editing.id);
          if (fresh) setDialog({kind: 'edit', practice: fresh});
        },
      },
    );

  const setLine = (index: number, value: string) =>
    setForm((current) => {
      const addressLines: PracticeInput['addressLines'] = [...current.addressLines];
      addressLines[index] = value;
      return {...current, addressLines};
    });

  const columns: DataTableColumn<PracticeRow>[] = [
    {
      key: 'name',
      header: 'Practice',
      width: proportional(3, {minWidth: 200}),
      renderCell: (p) => <SetupNameCell name={p.name ?? ''} secondary={(p.addressLines ?? []).join(', ') || null} />,
    },
    {key: 'odsCode', header: 'ODS code', width: pixel(110), renderCell: (p) => <Text type="body">{p.odsCode}</Text>},
    {key: 'postcode', header: 'Postcode', width: pixel(110), renderCell: (p) => <Text type="body">{p.postcode}</Text>},
    {key: 'phone', header: 'Phone', width: pixel(150), renderCell: (p) => <Text type="body">{p.phone}</Text>},
    {key: 'patients', header: 'Patients', width: pixel(90), align: 'end', renderCell: (p) => <Text type="body">{p.patientCount ?? 0}</Text>},
    {key: 'referrals', header: 'Referrals', width: pixel(90), align: 'end', renderCell: (p) => <Text type="body">{p.referralCount ?? 0}</Text>},
    {key: 'status', header: 'Status', width: pixel(100), renderCell: (p) => <SetupStatusToken isArchived={p.isArchived ?? false} />},
    ...(canEdit
      ? [
          {
            key: 'actions',
            header: 'Actions',
            align: 'end',
            width: pixel(124),
            renderCell: (p) => (
              <TableActions
                actions={[
                  {label: `Edit ${p.name}`, icon: AppIcon.Edit, onClick: () => openEditor(p)},
                  p.isArchived
                    ? {
                        label: `Restore ${p.name}`,
                        icon: AppIcon.Restore,
                        onClick: () => {
                          setError(null);
                          actions.restore.mutate(versionOf(p), {onSuccess: () => announce('Practice restored'), onError: failed('restore')});
                        },
                      }
                    : {label: `Archive ${p.name}`, icon: AppIcon.Archive, onClick: () => open('archive', p)},
                  {
                    label: p.canBeDeleted ? `Delete ${p.name}` : `${p.name} has patients or referrals, so it can be archived but not deleted`,
                    icon: AppIcon.Delete,
                    isDestructive: true,
                    isDisabled: !p.canBeDeleted,
                    onClick: () => open('delete', p),
                  },
                ]}
              />
            ),
          } satisfies DataTableColumn<PracticeRow>,
        ]
      : []),
  ];

  const subtitle = isPending ? 'Loading practices...' : setupSubtitle(total, search, 'practice', 'practices');

  const summary = target && (
    <MetadataList>
      <MetadataListItem label="Practice">{target.name}</MetadataListItem>
      {target.odsCode && <MetadataListItem label="ODS code">{target.odsCode}</MetadataListItem>}
      {practiceAddress(target) && <MetadataListItem label="Address">{practiceAddress(target)}</MetadataListItem>}
      <MetadataListItem label="Used by">{practiceUse(target)}</MetadataListItem>
    </MetadataList>
  );

  return (
    <Page
      title="GP Practices"
      subtitle={subtitle}
      fill
      message={
        error ? <AlertMessage message={error} /> : isError && <AlertMessage message="The practices could not be loaded. Please try again." />
      }
      actions={canEdit && <Button label="Add Practice" variant="primary" icon={<Icon icon={AppIcon.Add} />} onClick={() => openEditor(null)} />}
      dialogs={
        canEdit && (
          <>
            <ConfirmDialog
              isOpen={dialog?.kind === 'edit'}
              title={editing ? 'Edit Practice' : 'Add Practice'}
              message={
                editing
                  ? 'Patients and referrals with this practice show the change.'
                  : 'A GP practice patients are registered with, or referrals come from.'
              }
              icon={editing ? AppIcon.Edit : AppIcon.Add}
              confirmText={editing ? 'Save' : 'Add Practice'}
              isProcessing={save.isPending}
              confirmDisabled={!form.name.trim()}
              hasSaveShortcut
              onConfirm={submit}
              onCancel={close}>
              <VStack gap={2}>
                {problem.validation && <ValidationAlert errors={problem.validation} fields={PRACTICE_FIELDS} />}
                {problem.message && <AlertMessage message={problem.message} />}
                <TextInput
                  label="Name"
                  value={form.name}
                  onChange={(name) => setForm((current) => ({...current, name}))}
                  placeholder="e.g. Testbrook Surgery"
                  autoComplete="off"
                  isRequired
                  hasAutoFocus
                  status={fieldStatus(problem.validation, 'name')}
                />
                <TextInput
                  label="ODS code"
                  description="The practice's code, if known: 3 to 10 letters and digits."
                  value={form.odsCode}
                  onChange={(odsCode) => setForm((current) => ({...current, odsCode}))}
                  autoComplete="off"
                  hasAutoCorrect={false}
                  hasSpellCheck={false}
                  isOptional
                  status={fieldStatus(problem.validation, 'odsCode')}
                />
                {([0, 1, 2] as const).map((index) => (
                  <TextInput
                    key={index}
                    label={`Address line ${index + 1}`}
                    value={form.addressLines[index]}
                    onChange={(value) => setLine(index, value)}
                    autoComplete="off"
                    isOptional
                    status={index === 0 ? fieldStatus(problem.validation, 'addressLines') : undefined}
                  />
                ))}
                <TextInput
                  label="Postcode"
                  value={form.postcode}
                  onChange={(postcode) => setForm((current) => ({...current, postcode}))}
                  autoComplete="off"
                  isOptional
                  status={fieldStatus(problem.validation, 'postcode')}
                />
                <TextInput
                  label="Phone"
                  type="tel"
                  value={form.phone}
                  onChange={(phone) => setForm((current) => ({...current, phone}))}
                  autoComplete="off"
                  isOptional
                  status={fieldStatus(problem.validation, 'phone')}
                />
                <TextInput
                  label="Secure email"
                  type="email"
                  description="For letters: an address at an allowed secure domain, such as nhs.net."
                  value={form.secureEmail}
                  onChange={(secureEmail) => setForm((current) => ({...current, secureEmail}))}
                  autoComplete="off"
                  isOptional
                  status={fieldStatus(problem.validation, 'secureEmail')}
                />
              </VStack>
            </ConfirmDialog>

            <ConfirmDialog
              isOpen={dialog?.kind === 'archive'}
              title="Archive Practice"
              message="It won't be offered for new patients or referrals. Those that have it keep it, and it can be restored."
              icon={AppIcon.Archive}
              intent="warning"
              confirmText="Archive"
              isProcessing={actions.archive.isPending}
              onConfirm={() =>
                target &&
                actions.archive.mutate(versionOf(target), {
                  onSuccess: () => {
                    announce('Practice archived');
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
              message="No patient or referral has this practice. This action cannot be undone."
              isDanger
              confirmText="Delete Practice"
              isProcessing={actions.remove.isPending}
              onConfirm={() =>
                target &&
                actions.remove.mutate(target.id ?? '', {
                  onSuccess: () => {
                    announce('Practice deleted');
                    close();
                  },
                  onError: failed('delete'),
                })
              }
              onCancel={close}>
              {summary}
            </ConfirmDialog>
          </>
        )
      }>
      <DataTable
        data={rows as PracticeRow[]}
        columns={columns}
        idKey="id"
        isLoading={isPending}
        search={
          <ListFilterBar
            fields={FILTER_FIELDS}
            values={setupBarValues(search)}
            onChange={(values) => onSearchChange({...fromSetupBar(values), page: undefined})}
            list="practices"
            isUnfiltered={!search.name && search.status === undefined}
            label="Search practices"
            placeholder="Search practices by name or ODS code, or filter by status..."
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
            title={data.length === 0 && !isPending ? 'No practices yet' : 'No practices match your search'}
            description={
              data.length === 0 && !isPending
                ? canEdit
                  ? 'Add the GP practices your patients are registered with.'
                  : 'Someone who can edit patients adds them.'
                : 'Search for archived ones too, to see and restore them.'
            }
            icon={<Icon icon={AppIcon.Practices} size="lg" />}
            isCompact
            actions={
              data.length > 0 && (
                <Button label="Show Archived" variant="secondary" onClick={() => onSearchChange({...search, page: undefined, status: 'any'})} />
              )
            }
          />
        }
      />
    </Page>
  );
}
