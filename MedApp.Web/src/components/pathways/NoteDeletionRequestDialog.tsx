import {Button} from '@astryxdesign/core/Button';
import {Card} from '@astryxdesign/core/Card';
import {Icon} from '@astryxdesign/core/Icon';
import {Spinner} from '@astryxdesign/core/Spinner';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery, useQueryClient} from '@tanstack/react-query';
import {useEffect, useState, type ReactNode} from 'react';
import {ApiError, ApiValidationError, problemMessage, type ValidationErrors} from '../../api/errors';
import {describeNoteDeletionReason, isNoteDeletionReason, type NoteDeletionReason} from '../../api/noteDeletion';
import {noteDeletionRequestKey, noteDeletionRequestQuery, useDecideNoteDeletion, type NoteDeletionRequest} from '../../api/pathways';
import {useAnnounce} from '../../app/announcements';
import {formatDateTime, parseUtc} from '../../utils/dates';
import {DialogFrame} from '../modals/DialogFrame';
import {PatientBanner} from '../patients/PatientBanner';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import {RichText} from '../shared/RichText';
import {NoteDeletionReasonFields, reasonFieldsComplete} from './NoteDeletionReasonFields';

/** Said when the note was edited after the request was made, so the requester may have meant other words. */
export const EDITED_SINCE_REQUEST = 'This note was edited after the request was made, so the reason may be about other words.';

export interface NoteDeletionRequestDialogProps {
  /** The request to show; null when closed. */
  requestId: string | null;
  /**
   * The pathway whose page it opens over. A request about another pathway's note (an edited link) isn't shown
   * here: `onWrongPathway` is called with its own pathway, to go there instead.
   */
  pathwayId: string;
  onClose: () => void;
  /** Goes to the request's own pathway page (with the request); without it, the dialog just closes. */
  onWrongPathway?: (pathwayId: string) => void;
}

/**
 * A member of staff's request to delete a note, for an admin to decide (opened from its "Note deletion requested"
 * notification, which links to the pathway page with `?deletionRequest=<id>`, or from a note's Review Request).
 * Deleting the wrong note, or one that's clinically needed, is a clinical hazard (H-38), so it shows who the patient is
 * as the patient banner does (name, date of birth, NHS number), the pathway's title, the note itself (with its author,
 * time and, if it was, when it was edited, with a warning when that was after the request), and who asked, when and why.
 * **Delete note** deletes it from the version shown (`expectedVersion`: one edited since is refused), with a reason (a
 * choice and a detail, starting as the requester's, which the admin may change); **Keep note** declines the request.
 * Either way the requester is told.
 *
 * It only opens over its own pathway's page: a request about another pathway's note goes to that page instead.
 * A request that can't be decided (another admin decided it, the record is restricted, or the note's pathway was
 * deleted) shows why, and its outcome, instead of the buttons. A 409 (decided or edited meanwhile) shows the API's
 * words and reads the request again. Reading it is recorded in the activity log as a view of the record.
 */
export function NoteDeletionRequestDialog({requestId, pathwayId, onClose, onWrongPathway}: NoteDeletionRequestDialogProps) {
  return requestId ? (
    <RequestDialog key={requestId} requestId={requestId} pathwayId={pathwayId} onClose={onClose} onWrongPathway={onWrongPathway} />
  ) : null;
}

function RequestDialog({requestId, pathwayId, onClose, onWrongPathway}: Omit<NoteDeletionRequestDialogProps, 'requestId'> & {requestId: string}) {
  const query = useQuery(noteDeletionRequestQuery(requestId));
  const request = query.data;
  const isElsewhere = !!request?.pathwayId && request.pathwayId.toLowerCase() !== pathwayId.toLowerCase();
  const decide = useDecideNoteDeletion(requestId);
  const queryClient = useQueryClient();
  const announce = useAnnounce();
  // The reason recorded with the deletion: the requester's, until the admin changes it.
  const [reason, setReason] = useState<NoteDeletionReason | '' | null>(null);
  const [detail, setDetail] = useState<string | null>(null);
  const reasonShown: NoteDeletionReason | '' = reason ?? (isNoteDeletionReason(request?.reason) ? request.reason : '');
  const detailShown = detail ?? request?.detail ?? '';
  const [problem, setProblem] = useState<string | null>(null);
  const [errors, setErrors] = useState<ValidationErrors | null>(null);

  // Never shown over another pathway's page: go to its own, or close.
  useEffect(() => {
    if (!isElsewhere || !request?.pathwayId) return;
    if (onWrongPathway) onWrongPathway(request.pathwayId);
    else onClose();
  }, [isElsewhere, request?.pathwayId, onWrongPathway, onClose]);

  const send = (deleteNote: boolean) => {
    setProblem(null);
    setErrors(null);
    if (deleteNote && !reasonShown) return;
    decide.mutate(
      deleteNote && reasonShown
        ? {deleteNote: true, expectedVersion: request?.note?.version ?? 0, reason: reasonShown, detail: detailShown}
        : {deleteNote: false},
      {
        onSuccess: () => {
          announce(deleteNote ? 'Note deleted' : 'Note kept');
          onClose();
        },
        onError: (error) => {
          // The reason's and detail's own messages show on their fields; anything else above the buttons.
          if (error instanceof ApiValidationError && (error.errors.fields.reason?.length || error.errors.fields.detail?.length))
            setErrors(error.errors);
          else setProblem(problemMessage(error, 'The request could not be decided. Please try again.'));
          // Decided by someone else meanwhile, the note edited, or the record changed: show it as it is now.
          if (error instanceof ApiError && error.status === 409)
            void queryClient.invalidateQueries({queryKey: noteDeletionRequestKey(requestId)});
        },
      },
    );
  };

  if (isElsewhere) return null;

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
              isDisabled={isBusy || !reasonFieldsComplete(reasonShown, detailShown)}
              isLoading={isBusy && decide.variables?.deleteNote === true}
              onClick={() => send(true)}
            />
          </>
        ) : (
          <Button label="Close" variant="secondary" onClick={onClose} />
        )
      }>
      {query.isPending ? (
        // A silent spinner, with its words beside it: a Spinner that announces leaves its words in a hidden status
        // region after the request has loaded. The dialog's own title is announced as it opens.
        <HStack gap={2} vAlign="center" hAlign="center" padding={4}>
          <Spinner size="sm" aria-hidden />
          <Text type="body" color="secondary">
            Loading the request...
          </Text>
        </HStack>
      ) : query.isError || !request ? (
        <AlertMessage message={problemMessage(query.error, 'The request could not be loaded.')} />
      ) : (
        <VStack gap={4} hAlign="stretch">
          {canDecide && (
            <Text type="body" color="secondary">
              Delete this note? It will be hidden from the pathway for everyone, but kept in the record&apos;s history and export,
              with your reason. Check it is the right patient and the right note. Its author is told.
            </Text>
          )}
          {request.patient && <PatientBanner patient={request.patient} isCompact />}
          <Line label="Pathway">{request.pathwayTitle ?? 'Deleted pathway'}</Line>
          <NoteShown request={request} />
          <Line label="Asked by">{`${request.requestedByUsername ?? ''}, ${formatDateTime(request.requestedAtUtc ?? '')}`}</Line>
          <Line label="Their reason">{describeNoteDeletionReason(request.reason, request.detail)}</Line>
          {!canDecide && <AlertMessage message={outcome(request)} status={request.status === 'Pending' ? 'warning' : 'info'} />}
          {canDecide && (
            <NoteDeletionReasonFields
              label="Reason for deleting"
              reason={reasonShown}
              onReasonChange={setReason}
              detail={detailShown}
              onDetailChange={setDetail}
              errors={errors}
              isDisabled={isBusy}
            />
          )}
          {problem && <AlertMessage message={problem} />}
        </VStack>
      )}
    </DialogFrame>
  );
}

/** A labelled line: "Pathway: Knee review". */
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

/** Whether the note was edited after the request was made, so the requester may have meant other words. */
export const editedSinceRequest = (request: NoteDeletionRequest) =>
  !!request.note?.lastEditedAtUtc &&
  !!request.requestedAtUtc &&
  parseUtc(request.note.lastEditedAtUtc).getTime() > parseUtc(request.requestedAtUtc).getTime();

/** The note itself, with who wrote it, when, and when it was edited; or, once its pathway is gone, that it's gone. */
function NoteShown({request}: {request: NoteDeletionRequest}) {
  const note = request.note;
  if (!note) return <AlertMessage message="This note's pathway has been deleted, and the note with it." status="info" />;
  return (
    <VStack gap={1} hAlign="stretch">
      <Text type="body" weight="semibold">
        {`The note, by ${note.authorUsername ?? ''}, ${formatDateTime(note.createdAtUtc ?? '')}`}
        {note.lastEditedAtUtc ? `. Edited ${formatDateTime(note.lastEditedAtUtc)}` : ''}
      </Text>
      {editedSinceRequest(request) && <AlertMessage message={EDITED_SINCE_REQUEST} status="warning" />}
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
      const why = describeNoteDeletionReason(request.note?.deletionReason, request.note?.deletionDetail);
      return `${by} deleted this note${when}.${why ? ` Reason: ${why}` : ''}`;
    }
    case 'Declined':
      return `${by} kept this note${when}.`;
    case 'Closed':
      return `This request was closed without a decision${when}: the note's pathway was deleted, or the patient's record anonymised.`;
    default:
      return request.whyCantDecide ?? (request.note ? "This request can't be decided now." : "This request can't be decided: the note is gone.");
  }
}
