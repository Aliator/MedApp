import {RadioList, RadioListItem} from '@astryxdesign/core/RadioList';
import {TextArea} from '@astryxdesign/core/TextArea';
import {fieldStatus, type ValidationErrors} from '../../api/errors';
import {MAX_DELETION_DETAIL_LENGTH, needsDetail, NOTE_DELETION_REASONS, type NoteDeletionReason} from '../../api/noteDeletion';

export interface NoteDeletionReasonFieldsProps {
  reason: NoteDeletionReason | '';
  onReasonChange: (reason: NoteDeletionReason) => void;
  detail: string;
  onDetailChange: (detail: string) => void;
  /** The API's 400, shown on its fields (`reason`, `detail`). */
  errors?: ValidationErrors | null;
  isDisabled?: boolean;
  /** The reason's label: "Reason" (asking) or "Reason for deleting" (an admin deciding). */
  label?: string;
}

/** Whether the fields can be sent: a reason chosen, and a detail that's short enough, and given for Other. */
export const reasonFieldsComplete = (reason: string, detail: string) =>
  reason !== '' && detail.length <= MAX_DELETION_DETAIL_LENGTH && (!needsDetail(reason) || detail.trim() !== '');

/**
 * Why a note should be deleted: a required choice of the API's reasons (Wrong patient, Written in error, Duplicate,
 * Other) and an optional detail (500 characters, required for Other). A fixed choice keeps patient details out of the
 * reason, and lets the patient's export flag a note deleted as "Wrong patient" for the reviewing clinician. Used by
 * every way of deleting a note, or asking to: `PathwayNotes`' Delete and Request Deletion, and
 * `NoteDeletionRequestDialog`.
 */
export function NoteDeletionReasonFields({
  reason,
  onReasonChange,
  detail,
  onDetailChange,
  errors,
  isDisabled,
  label = 'Reason',
}: NoteDeletionReasonFieldsProps) {
  const isTooLong = detail.length > MAX_DELETION_DETAIL_LENGTH;
  return (
    <>
      <RadioList
        label={label}
        value={reason}
        onChange={(value) => onReasonChange(value as NoteDeletionReason)}
        isRequired
        isDisabled={isDisabled}
        status={fieldStatus(errors ?? null, 'reason')}>
        {NOTE_DELETION_REASONS.map((r) => (
          <RadioListItem key={r.value} value={r.value} label={r.label} />
        ))}
      </RadioList>
      <TextArea
        label={needsDetail(reason) ? 'Detail' : 'Detail (optional)'}
        description="Say more if it helps. Don't write patient details here."
        value={detail}
        onChange={onDetailChange}
        rows={2}
        isRequired={needsDetail(reason)}
        isDisabled={isDisabled}
        status={
          fieldStatus(errors ?? null, 'detail') ??
          (isTooLong ? {type: 'error', message: `At most ${MAX_DELETION_DETAIL_LENGTH} characters.`} : undefined)
        }
      />
    </>
  );
}
