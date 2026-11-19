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
import {
  hospitalsQuery,
  MAX_DEADLINE_DAYS,
  MIN_DEADLINE_DAYS,
  useHospitalActions,
  useSaveHospital,
  type Hospital,
} from '../../api/hospitals';
import {DISPATCH_METHODS, dispatchMethodWords} from '../../api/letters';
import {AnchoredSelector} from '../../components/forms/AnchoredSelector';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page} from '../../components/pages/Page';
import {SetupNameCell, SetupStatusToken} from '../../components/pathways/SetupCells';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {TableActions} from '../../components/tables/TableActions';
import {PanelCard} from '../../components/views/PanelCard';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../saveProblem';

/** The page's query: every hospital, archived ones too. Its route's loader preloads the same. */
export const hospitalsPageQuery = hospitalsQuery(true);

const FIELDS = ['name', 'odsCode', 'contact', 'reportDeliveryMethod', 'defaultDeadlineDays'];

/** A hospital's details in a line: "ODS ZZR01 · Reports by Post · 10 days when no date is given". */
export const hospitalDetailWords = (h: Hospital) =>
  [
    h.odsCode ? `ODS ${h.odsCode}` : null,
    `Reports by ${dispatchMethodWords(h.reportDeliveryMethod).toLowerCase()}`,
    `${h.defaultDeadlineDays ?? 0} ${h.defaultDeadlineDays === 1 ? 'day' : 'days'} when no date is given`,
  ]
    .filter(Boolean)
    .join(' · ');

type Dialog = {kind: 'edit'; hospital: Hospital | null} | {kind: 'delete'; hospital: Hospital} | null;

/**
 * Hospitals (`/admin/hospitals`, holders of ManageSetup; MC18): the hospitals that send the lab samples, each with its
 * name, ODS code, a contact, how its reports go, and the days its report is due in when a request names no date (the
 * lab's clock, MC19, uses it). Add, edit, archive one no longer sending (its requests keep it), restore, and delete one
 * that never sent a request (`canBeDeleted`). The API's refusals show in its words.
 */
export function HospitalsPage() {
  const announce = useAnnounce();
  const hospitals = useQuery(hospitalsPageQuery);
  const save = useSaveHospital();
  const actions = useHospitalActions();
  const [dialog, setDialog] = useState<Dialog>(null);
  const [name, setName] = useState('');
  const [odsCode, setOdsCode] = useState('');
  const [contact, setContact] = useState('');
  const [method, setMethod] = useState('Post');
  const [days, setDays] = useState<number | null>(10);
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);
  const [error, setError] = useState<{label: string; message: string | null} | null>(null);
  const close = () => setDialog(null);

  const edit = (hospital: Hospital | null) => {
    setName(hospital?.name ?? '');
    setOdsCode(hospital?.odsCode ?? '');
    setContact(hospital?.contact ?? '');
    setMethod(hospital?.reportDeliveryMethod ?? 'Post');
    setDays(hospital?.defaultDeadlineDays ?? 10);
    setProblem(NO_PROBLEM);
    setError(null);
    setDialog({kind: 'edit', hospital});
  };
  const editing = dialog?.kind === 'edit' ? dialog.hospital : null;
  const deleting = dialog?.kind === 'delete' ? dialog.hospital : null;

  const submit = () =>
    save.mutate(
      {id: editing?.id, name: name.trim(), odsCode: odsCode.trim(), contact: contact.trim() || null, reportDeliveryMethod: method, defaultDeadlineDays: days ?? 0},
      {
        onSuccess: () => {
          announce(editing ? 'Hospital saved' : 'Hospital added');
          close();
        },
        onError: (e) => setProblem(saveProblem(e, editing ? 'save' : 'add', 'hospital')),
      },
    );

  const setArchived = (hospital: Hospital, archive: boolean) => {
    setError(null);
    (archive ? actions.archive : actions.restore).mutate(hospital.id ?? '', {
      onSuccess: () => announce(`${hospital.name ?? 'It'} ${archive ? 'archived' : 'restored'}`),
      onError: (e) =>
        setError({
          label: archive ? 'Hospital not archived' : 'Hospital not restored',
          message: saveProblem(e, archive ? 'archive' : 'restore', 'hospital').message,
        }),
    });
  };

  const items = [...(hospitals.data ?? []).filter((h) => !h.isArchived), ...(hospitals.data ?? []).filter((h) => h.isArchived)];
  const active = items.filter((h) => !h.isArchived).length;

  return (
    <Page
      title="Hospitals"
      subtitle={hospitals.data ? `${active} active ${active === 1 ? 'hospital' : 'hospitals'}` : 'Loading...'}
      fill
      message={error && <AlertMessage label={error.label} message={error.message ?? undefined} />}
      dialogs={
        <>
          <ConfirmDialog
            isOpen={dialog?.kind === 'edit'}
            title={editing ? 'Edit Hospital' : 'Add Hospital'}
            message={
              editing
                ? 'Its requests show the change. A new default applies to requests registered from now on.'
                : 'A hospital that sends the lab samples, with how its reports go back.'
            }
            icon={editing ? AppIcon.Edit : AppIcon.Add}
            confirmText={editing ? 'Save' : 'Add Hospital'}
            confirmDisabled={!name.trim() || !odsCode.trim() || days === null}
            isProcessing={save.isPending}
            hasSaveShortcut
            onConfirm={submit}
            onCancel={close}>
            <VStack gap={2}>
              {problem.validation && <ValidationAlert errors={problem.validation} fields={FIELDS} />}
              {problem.message && <AlertMessage label={editing ? 'Hospital not saved' : 'Hospital not added'} message={problem.message} />}
              <TextInput
                label="Name"
                value={name}
                onChange={setName}
                placeholder="e.g. Riverside General"
                autoComplete="off"
                isRequired
                hasAutoFocus
                status={fieldStatus(problem.validation, 'name')}
              />
              <TextInput
                label="ODS code"
                description="The hospital's code: 3 to 10 letters and digits."
                value={odsCode}
                onChange={setOdsCode}
                autoComplete="off"
                isRequired
                status={fieldStatus(problem.validation, 'odsCode')}
              />
              <TextInput
                label="Contact"
                description="Who at the hospital to ask about a request: a desk or department, never a patient."
                value={contact}
                onChange={setContact}
                autoComplete="off"
                isOptional
                status={fieldStatus(problem.validation, 'contact')}
              />
              <AnchoredSelector
                label="Reports go by"
                options={DISPATCH_METHODS.map((m) => ({value: m.value, label: m.label}))}
                value={method}
                onChange={setMethod}
                isRequired
                status={fieldStatus(problem.validation, 'reportDeliveryMethod')}
              />
              <NumberInput
                label="Report due in, when no date is given"
                description={`Calendar days from receipt, ${MIN_DEADLINE_DAYS} to ${MAX_DEADLINE_DAYS}. A date on the request wins.`}
                value={days}
                onChange={setDays}
                min={MIN_DEADLINE_DAYS}
                max={MAX_DEADLINE_DAYS}
                isRequired
                status={fieldStatus(problem.validation, 'defaultDeadlineDays')}
              />
            </VStack>
          </ConfirmDialog>

          <ConfirmDialog
            isOpen={dialog?.kind === 'delete'}
            title="Confirm Delete"
            message="This hospital never sent a request. This action cannot be undone."
            isDanger
            confirmText="Delete Hospital"
            isProcessing={actions.remove.isPending}
            onConfirm={() =>
              deleting &&
              actions.remove.mutate(deleting.id ?? '', {
                onSuccess: () => {
                  announce('Hospital deleted');
                  close();
                },
                onError: (e) => {
                  setError({label: 'Hospital not deleted', message: saveProblem(e, 'delete', 'hospital').message});
                  close();
                },
              })
            }
            onCancel={close}>
            {deleting && (
              <MetadataList>
                <MetadataListItem label="Hospital">{deleting.name}</MetadataListItem>
              </MetadataList>
            )}
          </ConfirmDialog>
        </>
      }>
      <PanelCard
        title="Hospitals"
        isBodyPadded={false}
        footerActions={<Button label="Add Hospital" variant="secondary" size="sm" icon={<Icon icon={AppIcon.Add} />} onClick={() => edit(null)} />}>
        {hospitals.isError ? (
          <VStack padding={6}>
            <AlertMessage label="Hospitals not loaded" message="The hospitals could not be loaded. Please try again." />
          </VStack>
        ) : hospitals.isPending || items.length === 0 ? (
          <VStack padding={6}>
            <Text type="body" color="secondary">
              {hospitals.isPending ? 'Loading hospitals...' : 'No hospitals yet. Add the hospitals that send the lab samples.'}
            </Text>
          </VStack>
        ) : (
          <List edge="bleed" hasDividers aria-label="Hospitals">
            {items.map((hospital) => (
              <ListItem
                key={hospital.id}
                label={<SetupNameCell name={hospital.name ?? ''} secondary={hospitalDetailWords(hospital)} />}
                endContent={
                  <HStack gap={2} vAlign="center">
                    <SetupStatusToken isArchived={hospital.isArchived ?? false} />
                    <TableActions
                      actions={[
                        {label: `Edit ${hospital.name}`, icon: AppIcon.Edit, onClick: () => edit(hospital)},
                        hospital.isArchived
                          ? {label: `Restore ${hospital.name}`, icon: AppIcon.Restore, onClick: () => setArchived(hospital, false)}
                          : {label: `Archive ${hospital.name}`, icon: AppIcon.Archive, onClick: () => setArchived(hospital, true)},
                        {
                          label: hospital.canBeDeleted ? `Delete ${hospital.name}` : `${hospital.name} has sent requests, so it can be archived but not deleted`,
                          icon: AppIcon.Delete,
                          isDestructive: true,
                          isDisabled: !hospital.canBeDeleted,
                          onClick: () => {
                            setError(null);
                            setDialog({kind: 'delete', hospital});
                          },
                        },
                      ]}
                    />
                  </HStack>
                }
              />
            ))}
          </List>
        )}
      </PanelCard>
    </Page>
  );
}
