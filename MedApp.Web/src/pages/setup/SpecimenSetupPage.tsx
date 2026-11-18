import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {List, ListItem} from '@astryxdesign/core/List';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {NumberInput} from '@astryxdesign/core/NumberInput';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Switch} from '@astryxdesign/core/Switch';
import {Text} from '@astryxdesign/core/Text';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {fieldStatus} from '../../api/errors';
import {
  laboratoriesQuery,
  specimenTypesQuery,
  useLaboratoryActions,
  useSaveLaboratory,
  useSaveSpecimenType,
  useSpecimenTypeActions,
  type Laboratory,
  type SpecimenType,
} from '../../api/specimens';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {TwoColumns} from '../../components/pages/TwoColumns';
import {SetupNameCell, SetupStatusToken} from '../../components/pathways/SetupCells';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {TableActions} from '../../components/tables/TableActions';
import {FoldingCard} from '../../components/views/FoldingCard';
import {PanelCard} from '../../components/views/PanelCard';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../saveProblem';

/** The page's queries: every type and lab, archived ones too. Its route's loader preloads the same. */
export const specimenSetupPageQueries = [specimenTypesQuery(true), laboratoriesQuery(true)] as const;

/** The days a type's turnaround can be (the API's limits). */
export const MIN_TURNAROUND_DAYS = 1;
export const MAX_TURNAROUND_DAYS = 90;

type Dialog =
  | {kind: 'type'; type: SpecimenType | null}
  | {kind: 'lab'; lab: Laboratory | null}
  | {kind: 'delete-type'; type: SpecimenType}
  | {kind: 'delete-lab'; lab: Laboratory}
  | null;

/** A type's expected turnaround in words: "Expected in 5 days". */
export const turnaroundWords = (days: number | undefined) => `Expected in ${days ?? 0} ${days === 1 ? 'day' : 'days'}`;

/** Open ones first, then archived, each by name, as the API sorts them. */
const activeFirst = <T extends {isArchived?: boolean}>(items: readonly T[]) => [
  ...items.filter((item) => !item.isArchived),
  ...items.filter((item) => item.isArchived),
];

/**
 * Specimen Types and Labs (`/admin/specimen-setup`, holders of ManageSetup; decision 82): the types of specimen staff
 * record (a name, and the days the lab is expected to take, 1 to 90, from which a specimen is Overdue at lab) and the
 * labs they're sent to (a name, and whether the lab's own staff use MedApp). Add, edit, archive one no longer offered
 * (specimens that have it keep it), restore, and delete one no specimen has (`canBeDeleted`). The API's refusals show
 * in its words.
 */
export function SpecimenSetupPage() {
  const isNarrow = useIsNarrow();
  const announce = useAnnounce();
  const types = useQuery(specimenSetupPageQueries[0]);
  const labs = useQuery(specimenSetupPageQueries[1]);
  const saveType = useSaveSpecimenType();
  const saveLab = useSaveLaboratory();
  const typeActions = useSpecimenTypeActions();
  const labActions = useLaboratoryActions();
  const [dialog, setDialog] = useState<Dialog>(null);
  const [name, setName] = useState('');
  const [days, setDays] = useState<number | null>(7);
  const [usesMedApp, setUsesMedApp] = useState(false);
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);
  const [error, setError] = useState<string | null>(null);
  const [open, setOpen] = useState({types: true, labs: true});
  const close = () => setDialog(null);

  const editType = (type: SpecimenType | null) => {
    setName(type?.name ?? '');
    setDays(type?.expectedTurnaroundDays ?? 7);
    setProblem(NO_PROBLEM);
    setError(null);
    setDialog({kind: 'type', type});
  };
  const editLab = (lab: Laboratory | null) => {
    setName(lab?.name ?? '');
    setUsesMedApp(lab?.usesMedApp ?? false);
    setProblem(NO_PROBLEM);
    setError(null);
    setDialog({kind: 'lab', lab});
  };

  const editingType = dialog?.kind === 'type' ? dialog.type : null;
  const editingLab = dialog?.kind === 'lab' ? dialog.lab : null;
  const deletingType = dialog?.kind === 'delete-type' ? dialog.type : null;
  const deletingLab = dialog?.kind === 'delete-lab' ? dialog.lab : null;

  const failed = (action: 'archive' | 'restore' | 'delete', subject: 'specimen type' | 'lab') => (e: unknown) => {
    setError(saveProblem(e, action, subject).message);
    close();
  };

  const submitType = () =>
    saveType.mutate(
      {id: editingType?.id, name: name.trim(), expectedTurnaroundDays: days ?? 0},
      {
        onSuccess: () => {
          announce(editingType ? 'Specimen type saved' : 'Specimen type added');
          close();
        },
        onError: (e) => setProblem(saveProblem(e, editingType ? 'save' : 'add', 'specimen type')),
      },
    );

  const submitLab = () =>
    saveLab.mutate(
      {id: editingLab?.id, name: name.trim(), usesMedApp},
      {
        onSuccess: () => {
          announce(editingLab ? 'Lab saved' : 'Lab added');
          close();
        },
        onError: (e) => setProblem(saveProblem(e, editingLab ? 'save' : 'add', 'lab')),
      },
    );

  const setArchived = <T extends {id?: string; name?: string | null}>(
    actions: typeof typeActions | typeof labActions,
    item: T,
    archive: boolean,
    subject: 'specimen type' | 'lab',
  ) => {
    setError(null);
    (archive ? actions.archive : actions.restore).mutate(item.id ?? '', {
      onSuccess: () => announce(`${item.name ?? 'It'} ${archive ? 'archived' : 'restored'}`),
      onError: failed(archive ? 'archive' : 'restore', subject),
    });
  };

  const typeList = (
    <SetupList
      label="Specimen types"
      isLoading={types.isPending}
      isError={types.isError}
      emptyText="No specimen types yet. Add the ones the clinic takes, such as a skin biopsy."
      items={activeFirst(types.data ?? []).map((type) => ({
        id: type.id ?? '',
        name: type.name ?? '',
        secondary: turnaroundWords(type.expectedTurnaroundDays),
        isArchived: type.isArchived ?? false,
        canBeDeleted: type.canBeDeleted ?? false,
        onEdit: () => editType(type),
        onArchive: (archive: boolean) => setArchived(typeActions, type, archive, 'specimen type'),
        onDelete: () => {
          setError(null);
          setDialog({kind: 'delete-type', type});
        },
      }))}
    />
  );

  const labList = (
    <SetupList
      label="Labs"
      isLoading={labs.isPending}
      isError={labs.isError}
      emptyText="No labs yet. Add the labs specimens are sent to."
      items={activeFirst(labs.data ?? []).map((lab) => ({
        id: lab.id ?? '',
        name: lab.name ?? '',
        secondary: lab.usesMedApp ? 'Uses MedApp' : "Doesn't use MedApp",
        isArchived: lab.isArchived ?? false,
        canBeDeleted: lab.canBeDeleted ?? false,
        onEdit: () => editLab(lab),
        onArchive: (archive: boolean) => setArchived(labActions, lab, archive, 'lab'),
        onDelete: () => {
          setError(null);
          setDialog({kind: 'delete-lab', lab});
        },
      }))}
    />
  );

  const addType = <Button label="Add Specimen Type" variant="secondary" size="sm" icon={<Icon icon={AppIcon.Add} />} onClick={() => editType(null)} />;
  const addLab = <Button label="Add Lab" variant="secondary" size="sm" icon={<Icon icon={AppIcon.Add} />} onClick={() => editLab(null)} />;

  return (
    <Page
      title="Specimen Types and Labs"
      subtitle={
        types.data && labs.data
          ? `${types.data.filter((t) => !t.isArchived).length} active types, ${labs.data.filter((l) => !l.isArchived).length} active labs`
          : 'Loading...'
      }
      fill
      message={error && <AlertMessage message={error} />}
      dialogs={
        <>
          <ConfirmDialog
            isOpen={dialog?.kind === 'type'}
            title={editingType ? 'Edit Specimen Type' : 'Add Specimen Type'}
            message={
              editingType
                ? 'Specimens of this type show the change. A new expected time applies to every open one.'
                : 'A kind of specimen staff take, such as a skin biopsy, with how long its lab is expected to take.'
            }
            icon={editingType ? AppIcon.Edit : AppIcon.Add}
            confirmText={editingType ? 'Save' : 'Add Specimen Type'}
            confirmDisabled={!name.trim() || days === null}
            isProcessing={saveType.isPending}
            hasSaveShortcut
            onConfirm={submitType}
            onCancel={close}>
            <VStack gap={2}>
              {problem.validation && <ValidationAlert errors={problem.validation} fields={['name', 'expectedTurnaroundDays']} />}
              {problem.message && <AlertMessage message={problem.message} />}
              <TextInput
                label="Name"
                value={name}
                onChange={setName}
                placeholder="e.g. Skin biopsy (punch)"
                autoComplete="off"
                isRequired
                hasAutoFocus
                status={fieldStatus(problem.validation, 'name')}
              />
              <NumberInput
                label="Expected turnaround, in days"
                description={`From taken to reported, ${MIN_TURNAROUND_DAYS} to ${MAX_TURNAROUND_DAYS}. Later than this, it's Overdue at lab.`}
                value={days}
                onChange={setDays}
                min={MIN_TURNAROUND_DAYS}
                max={MAX_TURNAROUND_DAYS}
                isRequired
                status={fieldStatus(problem.validation, 'expectedTurnaroundDays')}
              />
            </VStack>
          </ConfirmDialog>

          <ConfirmDialog
            isOpen={dialog?.kind === 'lab'}
            title={editingLab ? 'Edit Lab' : 'Add Lab'}
            message={editingLab ? 'Specimens sent to it show the change.' : 'A lab specimens are sent to.'}
            icon={editingLab ? AppIcon.Edit : AppIcon.Add}
            confirmText={editingLab ? 'Save' : 'Add Lab'}
            confirmDisabled={!name.trim()}
            isProcessing={saveLab.isPending}
            hasSaveShortcut
            onConfirm={submitLab}
            onCancel={close}>
            <VStack gap={2}>
              {problem.validation && <ValidationAlert errors={problem.validation} fields={['name']} />}
              {problem.message && <AlertMessage message={problem.message} />}
              <TextInput
                label="Name"
                value={name}
                onChange={setName}
                placeholder="e.g. Northgate pathology"
                autoComplete="off"
                isRequired
                hasAutoFocus
                status={fieldStatus(problem.validation, 'name')}
              />
              <Switch
                label="Uses MedApp"
                description="The lab's own staff receive and report its specimens in MedApp."
                value={usesMedApp}
                onChange={setUsesMedApp}
              />
            </VStack>
          </ConfirmDialog>

          <ConfirmDialog
            isOpen={dialog?.kind === 'delete-type'}
            title="Confirm Delete"
            message="No specimen has this type. This action cannot be undone."
            isDanger
            confirmText="Delete Specimen Type"
            isProcessing={typeActions.remove.isPending}
            onConfirm={() =>
              deletingType &&
              typeActions.remove.mutate(deletingType.id ?? '', {
                onSuccess: () => {
                  announce('Specimen type deleted');
                  close();
                },
                onError: failed('delete', 'specimen type'),
              })
            }
            onCancel={close}>
            {deletingType && (
              <MetadataList>
                <MetadataListItem label="Specimen type">{deletingType.name}</MetadataListItem>
              </MetadataList>
            )}
          </ConfirmDialog>

          <ConfirmDialog
            isOpen={dialog?.kind === 'delete-lab'}
            title="Confirm Delete"
            message="No specimen was sent to this lab. This action cannot be undone."
            isDanger
            confirmText="Delete Lab"
            isProcessing={labActions.remove.isPending}
            onConfirm={() =>
              deletingLab &&
              labActions.remove.mutate(deletingLab.id ?? '', {
                onSuccess: () => {
                  announce('Lab deleted');
                  close();
                },
                onError: failed('delete', 'lab'),
              })
            }
            onCancel={close}>
            {deletingLab && (
              <MetadataList>
                <MetadataListItem label="Lab">{deletingLab.name}</MetadataListItem>
              </MetadataList>
            )}
          </ConfirmDialog>
        </>
      }>
      {isNarrow ? (
        <FoldingCard
          sections={[
            {key: 'types', title: 'Specimen Types', isOpen: open.types, onToggle: () => setOpen((o) => ({...o, types: !o.types})), children: typeList},
            {key: 'labs', title: 'Labs', isOpen: open.labs, onToggle: () => setOpen((o) => ({...o, labs: !o.labs})), children: labList},
          ]}
          footerActions={
            <>
              {addType}
              {addLab}
            </>
          }
        />
      ) : (
        <TwoColumns
          start={
            <PanelCard title="Specimen Types" isBodyPadded={false} footerActions={addType}>
              {typeList}
            </PanelCard>
          }
          end={
            <PanelCard title="Labs" isBodyPadded={false} footerActions={addLab}>
              {labList}
            </PanelCard>
          }
        />
      )}
    </Page>
  );
}

interface SetupListItem {
  id: string;
  name: string;
  secondary: string;
  isArchived: boolean;
  canBeDeleted: boolean;
  onEdit: () => void;
  onArchive: (archive: boolean) => void;
  onDelete: () => void;
}

/** One setup list: each item's name and detail, Active or Archived, and Edit, Archive or Restore, and Delete. */
function SetupList({label, items, isLoading, isError, emptyText}: {label: string; items: SetupListItem[]; isLoading: boolean; isError: boolean; emptyText: string}) {
  if (isError)
    return (
      <VStack padding={6}>
        <AlertMessage message={`The ${label.toLowerCase()} could not be loaded. Please try again.`} />
      </VStack>
    );
  if (isLoading || items.length === 0)
    return (
      <VStack padding={6}>
        <Text type="body" color="secondary">
          {isLoading ? `Loading ${label.toLowerCase()}...` : emptyText}
        </Text>
      </VStack>
    );
  return (
    <List edge="bleed" hasDividers aria-label={label}>
      {items.map((item) => (
        <ListItem
          key={item.id}
          label={<SetupNameCell name={item.name} secondary={item.secondary} />}
          endContent={
            <HStack gap={2} vAlign="center">
              <SetupStatusToken isArchived={item.isArchived} />
              <TableActions
                actions={[
                  {label: `Edit ${item.name}`, icon: AppIcon.Edit, onClick: item.onEdit},
                  item.isArchived
                    ? {label: `Restore ${item.name}`, icon: AppIcon.Restore, onClick: () => item.onArchive(false)}
                    : {label: `Archive ${item.name}`, icon: AppIcon.Archive, onClick: () => item.onArchive(true)},
                  {
                    label: item.canBeDeleted ? `Delete ${item.name}` : `${item.name} has been used, so it can be archived but not deleted`,
                    icon: AppIcon.Delete,
                    isDestructive: true,
                    isDisabled: !item.canBeDeleted,
                    onClick: item.onDelete,
                  },
                ]}
              />
            </HStack>
          }
        />
      ))}
    </List>
  );
}
