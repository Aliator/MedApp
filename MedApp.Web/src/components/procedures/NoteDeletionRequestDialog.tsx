import {Button} from '@astryxdesign/core/Button';
import {Card} from '@astryxdesign/core/Card';
import {Icon} from '@astryxdesign/core/Icon';
import {Spinner} from '@astryxdesign/core/Spinner';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextArea} from '@astryxdesign/core/TextArea';
import {useQuery, useQueryClient} from '@tanstack/react-query';
import {useState, type ReactNode} from 'react';
import {ApiError, ApiValidationError, fieldStatus, problemMessage, type ValidationErrors} from '../../api/errors';
import {noteDeletionRequestKey, noteDeletionRequestQuery, useDecideNoteDeletion, type NoteDeletionRequest} from '../../api/procedures';
import {useAnnounce} from '../../app/announcements';
import {formatDateTime} from '../../utils/dates';
import {DialogFrame} from '../modals/DialogFrame';
import {PatientBanner} from '../patients/PatientBanner';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import {RichText} from '../shared/RichText';

/** The most a reason can hold, as the API allows. */
export const MAX_DELETION_REASON_LENGTH = 500;

export interface NoteDeletionRequestDialogProps {
  /** The request to show; null when closed. */
  requestId: string | null;
  onClose: () => void;
}

/**
 * A member of staff's request to delete a note, for an admin to decide (opened from its "Note deletion requested"
 * notification, which links to the procedure page with `?deletionRequest=<id>`, or from a note's "Deletion requested").
 * Deleting the wrong note, or one that's clinically needed, is a clinical hazard, so it shows who the patient is as the
 * patient banner does (name, date of birth, NHS number), the procedure's title, the note itself (with its author and
 * time), and who asked, when and why. **Delete note** deletes it (soft: kept, hidden, with the reason, which starts as
 * the requester's and can be changed); **Keep note** declines the request. Either way the requester is told.
 *
 * A request that can't be decided (another admin decided it, the record is restricted, or the note's procedure was
 * deleted) shows why, and its outcome, instead of the buttons. A 409 (decided meanwhile) shows the API's words and
 * reads the request again. Reading it is recorded in the activity log as a view of the record.
 */
export function NoteDeletionRequestDialog({requestId, onClose}: NoteDeletionRequestDialogProps) {
  return requestId ? <RequestDialog key={requestId} requestId={requestId} onClose={onClose} /> : null;
}

function RequestDialog({requestId, onClose}: {requestId: string; onClose: () => void}) {
  const query = useQuery(noteDeletionRequestQuery(requestId));
  const request = query.data;
  const decide = useDecideNoteDeletion(requestId);
  const queryClient = useQueryClient();
  const announce = useAnnounce();
  // The reason recorded with the deletion: the requester's, until the admin changes it.
  const [reason, setReason] = useState<string | null>(null);
  const reasonShown = reason ?? request?.reason ?? '';
  const [problem, setProblem] = useState<string | null>(null);
  const [errors, setErrors] = useState<ValidationErrors | null>(null);
  const isTooLong = reasonShown.length > MAX_DELETION_REASON_LENGTH;

  const send = (deleteNote: boolean) => {
    setProblem(null);
    setErrors(null);
    decide.mutate(
      deleteNote ? {deleteNote, reason: reasonShown.trim()} : {deleteNote},
      {
        onSuccess: () => {
          announce(deleteNote ? 'Note deleted' : 'Note kept');
          onClose();
        },
        onError: (error) => {
          // The reason's own messages show on its field; anything else above the buttons.
          if (error instanceof ApiValidationError && error.errors.fields.reason?.length) setErrors(error.errors);
          else setProblem(problemMessage(error, 'The request could not be decided. Please try again.'));
          // Decided by someone else meanwhile, or the record changed: show it as it is now.
          if (error instanceof ApiError && error.status === 409)
            void queryClient.invalidateQueries({queryKey: noteDeletionRequestKey(requestId)});
        },
      },
    );
  };

  const canDecide = request?.canDecide ?? false;
  const isBusy = decide.isPending;

  return (
    <DialogFrame
      isOpen
      title="Note Deletion Request"
      icon={<Icon icon={AppIcon.Delete} size="lg" color="warning" />}
      onClose={onClose}
      purpose={isBusy ? 'required' : 'form'}
      width={560}
      footer={
        canDecide ? (
          <>
            <Button label="Keep note" variant="secondary" isDisabled={isBusy} isLoading={isBusy && decide.variables?.deleteNote === false} onClick={() => send(false)} />
            <Button
              label="Delete note"
              variant="destructive"
              icon={<Icon icon={AppIcon.Delete} />}
              isDisabled={isBusy || !reasonShown.trim() || isTooLong}
              isLoading={isBusy && decide.variables?.deleteNote === true}
              onClick={() => send(true)}
            />
          </>
        ) : (
          <Button label="Close" variant="secondary" onClick={onClose} />
        )
      }>
      {query.isPending ? (
        <HStack gap={2} vAlign="center" hAlign="center" padding={4}>
          <Spinner size="sm" label="Loading the request" />
        </HStack>
      ) : query.isError || !request ? (
        <AlertMessage message={problemMessage(query.error, 'The request could not be loaded.')} />
      ) : (
        <VStack gap={4} hAlign="stretch">
          {canDecide && (
            <Text type="body" color="secondary">
              Delete this note? It will be hidden from the procedure for everyone, but kept in the record&apos;s history and export,
              with your reason. Check it is the right patient and the right note.
            </Text>
          )}
          {request.patient && <PatientBanner patient={request.patient} isCompact />}
          <Line label="Procedure">{request.procedureTitle ?? 'Deleted procedure'}</Line>
          <NoteShown request={request} />
          <Line label="Asked by">{`${request.requestedByUsername ?? ''}, ${formatDateTime(request.requestedAtUtc ?? '')}`}</Line>
          <Line label="Their reason">{request.reason ?? ''}</Line>
          {!canDecide && <AlertMessage message={outcome(request)} status={request.status === 'Pending' ? 'warning' : 'info'} />}
          {canDecide && (
            <TextArea
              label="Reason for deleting"
              description="Recorded with the deletion. It starts as theirs; change it if you need to."
              value={reasonShown}
              onChange={setReason}
              rows={3}
              isRequired
              isDisabled={isBusy}
              status={
                fieldStatus(errors, 'reason') ??
                (isTooLong ? {type: 'error', message: `At most ${MAX_DELETION_REASON_LENGTH} characters.`} : undefined)
              }
            />
          )}
          {problem && <AlertMessage message={problem} />}
        </VStack>
      )}
    </DialogFrame>
  );
}

/** A labelled line: "Procedure: Knee review". */
function Line({label, children}: {label: string; children: ReactNode}) {
  return (
    <Text type="body">
      <Text type="body" weight="semibold">
        {`${label}:`}
      </Text>{' '}
      {children}
    </Text>
  );
}

/** The note itself, with who wrote it and when; or, once its procedure is gone, that it's gone. */
function NoteShown({request}: {request: NoteDeletionRequest}) {
  const note = request.note;
  if (!note) return <AlertMessage message="This note's procedure has been deleted, and the note with it." status="info" />;
  return (
    <VStack gap={1} hAlign="stretch">
      <Text type="body" weight="semibold">
        {`The note, by ${note.authorUsername ?? ''}, ${formatDateTime(note.createdAtUtc ?? '')}`}
      </Text>
      <Card variant="muted" padding={3}>
        <RichText>{note.text ?? ''}</RichText>
      </Card>
    </VStack>
  );
}

/** Why a request can't be decided, or how it was. */
export function outcome(request: NoteDeletionRequest): string {
  const by = request.decidedByUsername ?? 'An admin';
  const when = request.decidedAtUtc ? `, ${formatDateTime(request.decidedAtUtc)}` : '';
  switch (request.status) {
    case 'Approved': {
      const why = request.note?.deletionReason?.trim();
      return `${by} deleted this note${when}.${why ? ` Reason: ${why}` : ''}`;
    }
    case 'Declined':
      return `${by} kept this note${when}.`;
    case 'Closed':
      return `This request was closed without a decision${when}: the note's procedure was deleted, or the patient's record anonymised.`;
    default:
      return request.whyCantDecide ?? (request.note ? "This request can't be decided now." : "This request can't be decided: the note is gone.");
  }
}
