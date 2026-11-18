import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {VStack} from '@astryxdesign/core/Stack';
import {TextArea} from '@astryxdesign/core/TextArea';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {staffWithPermissionQuery} from '../../api/calendar';
import {ApiValidationError, fieldStatus, problemMessage, type ValidationErrors} from '../../api/errors';
import {
  laboratoriesQuery,
  MAX_REQUEST_NOTES,
  specimenTypesQuery,
  useRecordSpecimen,
  useSpecimenActions,
  type Specimen,
} from '../../api/specimens';
import {useCurrentUser} from '../../auth/auth';
import {ukDateTime, ukDateTimeToUtc, type UkDateTime} from '../../utils/specimenTimes';
import {AnchoredSelector} from '../forms/AnchoredSelector';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {PatientBanner, type BannerPatient} from '../patients/PatientBanner';
import {AlertMessage, ValidationAlert} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import {StepTimeFields} from './StepTimeFields';

/** The fields the API's 400 can name, each shown on its field; anything else goes above them. */
const FIELDS = ['specimenTypeId', 'laboratoryId', 'takenAtUtc', 'site', 'requestNotes', 'requestingUserId'];

/** Who can be a specimen's requesting clinician: holders of this permission (the API's `RequestingClinicianPermission`). */
export const REQUESTING_PERMISSION = 'WorkPathways' as const;

export interface SpecimenDetailsDialogProps {
  isOpen: boolean;
  onClose: () => void;
  /** The patient, shown as `PatientBanner` shows them (the dialog covers the page's), so it's recorded on the right one. */
  patient?: BannerPatient;
  /** Recording: the pathway and the stage that took it. Left out when correcting a specimen's details. */
  record?: {pathwayId: string; pathwayTitle: string; stageId: string; stageName: string};
  /** Correcting: the specimen as the page shows it, whose `version` is sent back. */
  specimen?: Specimen;
  /** After it's saved, with the specimen the API returned. */
  onSaved?: (specimen: Specimen) => void;
}

/** One choice of a setup list: its open ones, and the one already chosen even if it has since been archived. */
function choices(items: readonly {id?: string; name?: string | null; isArchived?: boolean}[] | undefined, chosen: string) {
  return (items ?? [])
    .filter((item) => !item.isArchived || item.id === chosen)
    .map((item) => ({value: item.id ?? '', label: item.isArchived ? `${item.name} (archived)` : (item.name ?? '')}));
}

/**
 * Recording a specimen taken at a pathway's stage (Record Specimen), or correcting one's details before it's sent
 * (Edit Specimen): its type and lab (from the setup lists, not archived), the requesting clinician (a holder of
 * WorkPathways, starting with you when you are one), when it was taken (now, or up to a week back), and optionally the
 * site and notes for the lab, which are free text in the clinical record. The patient shows as the banner does, and
 * the stage and pathway, so it's recorded on the right one (hazard H-47). A 400 shows on its fields; a 409 or 403 in
 * the API's words. Mount it afresh for each opening (a `key`), so it starts from the specimen or from now.
 */
export function SpecimenDetailsDialog({isOpen, onClose, patient, record, specimen, onSaved}: SpecimenDetailsDialogProps) {
  const isEditing = specimen !== undefined;
  const user = useCurrentUser();
  const announce = useAnnounce();
  // Archived types and labs come too when correcting, so one the specimen already has can stay.
  const types = useQuery({...specimenTypesQuery(isEditing), enabled: isOpen});
  const labs = useQuery({...laboratoriesQuery(isEditing), enabled: isOpen});
  const staff = useQuery({...staffWithPermissionQuery(REQUESTING_PERMISSION), enabled: isOpen});
  const recordSpecimen = useRecordSpecimen(record?.pathwayId ?? '');
  const actions = useSpecimenActions(specimen?.id ?? '');
  const saving = isEditing ? actions.edit : recordSpecimen;

  const [typeId, setTypeId] = useState(specimen?.specimenTypeId ?? '');
  const [labId, setLabId] = useState(specimen?.laboratoryId ?? '');
  const [requester, setRequester] = useState<string | null>(specimen?.requestingUserId ?? null);
  const [initialTaken] = useState<UkDateTime>(() => ukDateTime(specimen?.takenAtUtc ?? undefined));
  const [taken, setTaken] = useState<UkDateTime>(initialTaken);
  const [site, setSite] = useState(specimen?.site ?? '');
  const [requestNotes, setRequestNotes] = useState(specimen?.requestNotes ?? '');
  const [errors, setErrors] = useState<ValidationErrors | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  // Until someone is chosen, a new specimen's requester is you, when you can be one.
  const me = staff.data?.find((member) => member.username === user.username)?.userId;
  const requestingUserId = requester ?? me ?? '';
  const staffOptions = (staff.data ?? []).map((member) => ({value: member.userId ?? '', label: member.username ?? ''}));
  // A requester who has since lost the permission (or their account) stays shown on a specimen being corrected.
  if (specimen?.requestingUserId && !staffOptions.some((option) => option.value === specimen.requestingUserId))
    staffOptions.push({value: specimen.requestingUserId, label: specimen.requestingUsername ?? 'Deleted user'});

  const isUnchangedTime = taken.date === initialTaken.date && taken.time === initialTaken.time;
  const takenAtUtc = isUnchangedTime ? (specimen?.takenAtUtc ?? undefined) : (ukDateTimeToUtc(taken) ?? undefined);
  const isComplete = Boolean(typeId && labId && requestingUserId && taken.date && taken.time);

  const save = () => {
    setErrors(null);
    setMessage(null);
    const input = {specimenTypeId: typeId, laboratoryId: labId, takenAtUtc, site, requestNotes, requestingUserId};
    const callbacks = {
      onSuccess: (saved: Specimen) => {
        announce(isEditing ? 'Specimen details saved' : `Specimen ${saved.number ?? ''} recorded`);
        onSaved?.(saved);
        onClose();
      },
      onError: (e: unknown) => {
        if (e instanceof ApiValidationError) setErrors(e.errors);
        else setMessage(problemMessage(e, isEditing ? 'The specimen could not be saved. Please try again.' : 'The specimen could not be recorded. Please try again.'));
      },
    };
    if (isEditing)
      actions.edit.mutate({...input, takenAtUtc: takenAtUtc ?? specimen.takenAtUtc ?? '', expectedVersion: specimen.version ?? 0}, callbacks);
    else recordSpecimen.mutate({...input, stageId: record?.stageId ?? ''}, callbacks);
  };

  const pathwayTitle = record?.pathwayTitle ?? specimen?.pathwayTitle;
  const stageName = record?.stageName ?? specimen?.stageName;

  return (
    <ConfirmDialog
      isOpen={isOpen}
      title={isEditing ? `Edit Specimen ${specimen.number ?? ''}` : 'Record Specimen'}
      message={
        isEditing
          ? 'Correct its details before it goes to the lab. Once it has been sent they can no longer change.'
          : "A specimen taken from the patient at this stage. It's given its own number, for its label and the lab."
      }
      icon={isEditing ? AppIcon.Edit : AppIcon.Specimens}
      confirmText={isEditing ? 'Save' : 'Record Specimen'}
      confirmDisabled={!isComplete}
      isProcessing={saving.isPending}
      hasSaveShortcut
      onConfirm={save}
      onCancel={onClose}>
      <VStack gap={3} hAlign="stretch">
        {patient && <PatientBanner patient={patient} isCompact />}
        <MetadataList columns={2} label={{position: 'top'}}>
          <MetadataListItem label="Pathway">{pathwayTitle}</MetadataListItem>
          <MetadataListItem label="Stage">{stageName}</MetadataListItem>
        </MetadataList>
        {errors && <ValidationAlert errors={errors} fields={FIELDS} />}
        {message && <AlertMessage message={message} />}
        <AnchoredSelector
          label="Specimen type"
          placeholder={types.isPending ? 'Loading specimen types...' : 'Choose the type'}
          options={choices(types.data, specimen?.specimenTypeId ?? '')}
          value={typeId || undefined}
          onChange={(value: string) => setTypeId(value)}
          isRequired
          status={types.isError ? {type: 'error', message: 'The specimen types could not be loaded.'} : fieldStatus(errors, 'specimenTypeId')}
        />
        <AnchoredSelector
          label="Lab"
          placeholder={labs.isPending ? 'Loading labs...' : 'Choose the lab it goes to'}
          options={choices(labs.data, specimen?.laboratoryId ?? '')}
          value={labId || undefined}
          onChange={(value: string) => setLabId(value)}
          isRequired
          status={labs.isError ? {type: 'error', message: 'The labs could not be loaded.'} : fieldStatus(errors, 'laboratoryId')}
        />
        <AnchoredSelector
          label="Requesting clinician"
          description="Who asked for it. They're responsible for its result."
          placeholder={staff.isPending ? 'Loading staff...' : 'Choose who asked for it'}
          options={staffOptions}
          value={requestingUserId || undefined}
          onChange={(value: string) => setRequester(value)}
          isRequired
          status={staff.isError ? {type: 'error', message: 'The staff could not be loaded.'} : fieldStatus(errors, 'requestingUserId')}
        />
        <StepTimeFields step="Taken" value={taken} onChange={setTaken} status={fieldStatus(errors, 'takenAtUtc')} />
        <TextInput
          label="Site"
          description="Where on the patient it was taken from. Part of their clinical record."
          value={site}
          onChange={setSite}
          placeholder="e.g. Left forearm"
          isOptional
          autoComplete="off"
          status={fieldStatus(errors, 'site')}
        />
        <TextArea
          label="Notes for the lab"
          description="Printed on the request sheet. Part of the patient's clinical record."
          value={requestNotes}
          onChange={setRequestNotes}
          rows={3}
          maxLength={MAX_REQUEST_NOTES}
          isOptional
          status={fieldStatus(errors, 'requestNotes')}
        />
      </VStack>
    </ConfirmDialog>
  );
}
