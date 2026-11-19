import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {List, ListItem} from '@astryxdesign/core/List';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {NumberInput} from '@astryxdesign/core/NumberInput';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {fieldStatus} from '../../api/errors';
import {labTestKindLabel, labTestsQuery, labTestTimesWords, useLabTestActions, useSaveLabTest, type LabTest, type LabTestInput} from '../../api/labTests';
import {specimenTypesQuery, useSaveSpecimenType, useSpecimenTypeActions, type SpecimenType} from '../../api/specimens';
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
import {LabTestDialog} from './LabTestDialog';

/** The page's queries: every type and lab test, archived ones too. Its route's loader preloads the same. */
export const specimenSetupPageQueries = [specimenTypesQuery(true), labTestsQuery(true)] as const;

/** The days a type's turnaround can be (the API's limits). */
export const MIN_TURNAROUND_DAYS = 1;
export const MAX_TURNAROUND_DAYS = 90;

type Dialog =
  | {kind: 'type'; type: SpecimenType | null}
  | {kind: 'test'; test: LabTest | null}
  | {kind: 'delete-type'; type: SpecimenType}
  | {kind: 'delete-test'; test: LabTest}
  | null;

/** A type's expected turnaround in words: "Expected in 5 days". */
export const turnaroundWords = (days: number | undefined) => `Expected in ${days ?? 0} ${days === 1 ? 'day' : 'days'}`;

/** Open ones first, then archived, each by name, as the API sorts them. */
const activeFirst = <T extends {isArchived?: boolean}>(items: readonly T[]) => [
  ...items.filter((item) => !item.isArchived),
  ...items.filter((item) => item.isArchived),
];

/**
 * Specimen Types and Lab Tests (`/admin/specimen-setup`, holders of ManageSetup; decision 82, MC13): the types of
 * specimen staff record (a name, the days the lab is expected to take, 1 to 90, from which a specimen is Overdue at
 * lab, and an optional SNOMED CT specimen code) and the lab's test catalogue (`LabTestDialog`). The external labs list
 * was dropped in MC13: the centre's own lab does every test. Add, edit, archive one no longer offered (what has it keeps
 * it), restore, and delete one nothing used (`canBeDeleted`). The API's refusals show in its words.
 */
export function SpecimenSetupPage() {
  const isNarrow = useIsNarrow();
  const announce = useAnnounce();
  const types = useQuery(specimenSetupPageQueries[0]);
  const tests = useQuery(specimenSetupPageQueries[1]);
  const saveType = useSaveSpecimenType();
  const saveTest = useSaveLabTest();
  const typeActions = useSpecimenTypeActions();
  const testActions = useLabTestActions();
  const [dialog, setDialog] = useState<Dialog>(null);
  const [name, setName] = useState('');
  const [days, setDays] = useState<number | null>(7);
  const [snomedCode, setSnomedCode] = useState('');
  // A new key each time the test dialog opens, so its fields start from the test opened.
  const [testDialogKey, setTestDialogKey] = useState(0);
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);
  const [error, setError] = useState<string | null>(null);
  const [open, setOpen] = useState({types: true, tests: true});
  const close = () => setDialog(null);

  const editType = (type: SpecimenType | null) => {
    setName(type?.name ?? '');
    setDays(type?.expectedTurnaroundDays ?? 7);
    setSnomedCode(type?.snomedCode ?? '');
    setProblem(NO_PROBLEM);
    setError(null);
    setDialog({kind: 'type', type});
  };
  const editTest = (test: LabTest | null) => {
    setTestDialogKey((k) => k + 1);
    setProblem(NO_PROBLEM);
    setError(null);
    setDialog({kind: 'test', test});
  };

  const editingType = dialog?.kind === 'type' ? dialog.type : null;
  const editingTest = dialog?.kind === 'test' ? dialog.test : null;
  const deletingType = dialog?.kind === 'delete-type' ? dialog.type : null;
  const deletingTest = dialog?.kind === 'delete-test' ? dialog.test : null;

  const failed = (action: 'archive' | 'restore' | 'delete', subject: 'specimen type' | 'lab test') => (e: unknown) => {
    setError(saveProblem(e, action, subject).message);
    close();
  };

  const submitType = () =>
    saveType.mutate(
      {id: editingType?.id, name: name.trim(), expectedTurnaroundDays: days ?? 0, snomedCode: snomedCode.trim() || null},
      {
        onSuccess: () => {
          announce(editingType ? 'Specimen type saved' : 'Specimen type added');
          close();
        },
        onError: (e) => setProblem(saveProblem(e, editingType ? 'save' : 'add', 'specimen type')),
      },
    );

  const submitTest = (input: LabTestInput) =>
    saveTest.mutate(input, {
      onSuccess: () => {
        announce(editingTest ? 'Lab test saved' : 'Lab test added');
        close();
      },
      onError: (e) => setProblem(saveProblem(e, editingTest ? 'save' : 'add', 'lab test')),
    });

  const setArchived = <T extends {id?: string; name?: string | null}>(
    actions: typeof typeActions | typeof testActions,
    item: T,
    archive: boolean,
    subject: 'specimen type' | 'lab test',
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
        secondary: [turnaroundWords(type.expectedTurnaroundDays), type.snomedCode ? `SNOMED CT ${type.snomedCode}` : null].filter(Boolean).join(' · '),
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

  const testList = (
    <SetupList
      label="Lab tests"
      isLoading={tests.isPending}
      isError={tests.isError}
      emptyText="No lab tests yet. Add the tests the lab does, such as skin histology."
      items={activeFirst(tests.data ?? []).map((test) => ({
        id: test.id ?? '',
        name: test.name ?? '',
        secondary: `${labTestKindLabel(test.kind)} · ${labTestTimesWords(test)} · ${test.steps?.length ?? 0} ${test.steps?.length === 1 ? 'step' : 'steps'}`,
        isArchived: test.isArchived ?? false,
        canBeDeleted: test.canBeDeleted ?? false,
        onEdit: () => editTest(test),
        onArchive: (archive: boolean) => setArchived(testActions, test, archive, 'lab test'),
        onDelete: () => {
          setError(null);
          setDialog({kind: 'delete-test', test});
        },
      }))}
    />
  );

  const addType = <Button label="Add Specimen Type" variant="secondary" size="sm" icon={<Icon icon={AppIcon.Add} />} onClick={() => editType(null)} />;
  const addTest = <Button label="Add Lab Test" variant="secondary" size="sm" icon={<Icon icon={AppIcon.Add} />} onClick={() => editTest(null)} />;

  return (
    <Page
      title="Specimen Types and Lab Tests"
      subtitle={
        types.data && tests.data
          ? `${types.data.filter((t) => !t.isArchived).length} active types, ${tests.data.filter((t) => !t.isArchived).length} active lab tests`
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
              {problem.validation && <ValidationAlert errors={problem.validation} fields={['name', 'expectedTurnaroundDays', 'snomedCode']} />}
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
              <TextInput
                label="SNOMED CT code"
                description="The specimen's concept ID (FHIR's Specimen.type), optional."
                value={snomedCode}
                onChange={setSnomedCode}
                autoComplete="off"
                status={fieldStatus(problem.validation, 'snomedCode')}
              />
            </VStack>
          </ConfirmDialog>

          <LabTestDialog
            key={testDialogKey}
            isOpen={dialog?.kind === 'test'}
            test={editingTest}
            isProcessing={saveTest.isPending}
            problem={problem}
            onSave={submitTest}
            onCancel={close}
          />

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
            isOpen={dialog?.kind === 'delete-test'}
            title="Confirm Delete"
            message="No lab case has used this test. This action cannot be undone."
            isDanger
            confirmText="Delete Lab Test"
            isProcessing={testActions.remove.isPending}
            onConfirm={() =>
              deletingTest &&
              testActions.remove.mutate(deletingTest.id ?? '', {
                onSuccess: () => {
                  announce('Lab test deleted');
                  close();
                },
                onError: failed('delete', 'lab test'),
              })
            }
            onCancel={close}>
            {deletingTest && (
              <MetadataList>
                <MetadataListItem label="Lab test">{deletingTest.name}</MetadataListItem>
              </MetadataList>
            )}
          </ConfirmDialog>
        </>
      }>
      {isNarrow ? (
        <FoldingCard
          sections={[
            {key: 'types', title: 'Specimen Types', isOpen: open.types, onToggle: () => setOpen((o) => ({...o, types: !o.types})), children: typeList},
            {key: 'tests', title: 'Lab Tests', isOpen: open.tests, onToggle: () => setOpen((o) => ({...o, tests: !o.tests})), children: testList},
          ]}
          footerActions={
            <>
              {addType}
              {addTest}
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
            <PanelCard title="Lab Tests" isBodyPadded={false} footerActions={addTest}>
              {testList}
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
