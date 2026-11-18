import {Button} from '@astryxdesign/core/Button';
import {Collapsible} from '@astryxdesign/core/Collapsible';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {MetadataListItem} from '@astryxdesign/core/MetadataList';
import {Section} from '@astryxdesign/core/Section';
import {ProgressBar} from '@astryxdesign/core/ProgressBar';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextArea} from '@astryxdesign/core/TextArea';
import {useQuery} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {useState, type ReactNode} from 'react';
import {pathwayAppointmentsQuery, useAppointmentActions, type Appointment} from '../../api/appointments';
import {staffWithPermissionQuery} from '../../api/calendar';
import {ApiError, problemMessage} from '../../api/errors';
import {
  canDeletePathway,
  pathwayQuery,
  useDeletePathway,
  usePathwayActions,
  type Pathway,
  type PathwayStage,
} from '../../api/pathways';
import {useAnnounce} from '../../app/announcements';
import {can, useCan, useCurrentUser} from '../../auth/auth';
import {BookAppointmentDialog} from '../../components/calendar/BookAppointmentDialog';
import {RecordPastAppointmentDialog} from '../../components/calendar/RecordPastAppointmentDialog';
import {Attachments} from '../../components/attachments/Attachments';
import {AttachmentsFold} from '../../components/attachments/AttachmentsFold';
import {useAttachmentsState} from '../../components/attachments/useAttachmentsState';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {SelectionDialog} from '../../components/modals/SelectionDialog';
import {PatientBanner} from '../../components/patients/PatientBanner';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {TwoColumns} from '../../components/pages/TwoColumns';
import {AppointmentList} from '../../components/pathways/AppointmentList';
import {AssigneeList} from '../../components/pathways/AssigneeList';
import {DueDateCell, PriorityToken, StatusToken, statusLabel} from '../../components/pathways/PathwayCells';
import {useOfferToNotepad} from '../../components/notepad/NotepadContext';
import {NoteDeletionRequestDialog} from '../../components/pathways/NoteDeletionRequestDialog';
import {PathwayNotes} from '../../components/pathways/PathwayNotes';
import {PathwaySummary} from '../../components/pathways/PathwaySummary';
import {StageDetails, StageList, StageTitle, type InstructionsFolding} from '../../components/pathways/StageList';
import {appointmentWhen, finishesWithoutVisit, involvesVisit, StageVisit, StageVisitTokens, stageVisit} from '../../components/pathways/StageVisit';
import {BranchChoice, ReinstateStageDialog, SkipStageDialog} from '../../components/pathways/StageSkipDialogs';
import {StageWork} from '../../components/pathways/StageWork';
import {StatusChangeDialog, statusAction} from '../../components/pathways/StatusChangeDialog';
import {PatientFlagBanners} from '../../components/patients/PatientFlagBanners';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {SummaryAlert} from '../../components/shared/SummaryAlert';
import {AppIcon} from '../../components/shared/icons';
import {FoldingCard, type FoldingSection} from '../../components/views/FoldingCard';
import {PanelCard} from '../../components/views/PanelCard';
import {ViewSection} from '../../components/views/ViewSection';
import {formatDate, formatDateTime} from '../../utils/dates';

/** The dialog the page has open, if any. */
type Dialog =
  | {kind: 'status'; to: string}
  | {kind: 'finish'; stage: PathwayStage; notes: string}
  | {kind: 'finishWithoutVisit'; stage: PathwayStage; notes: string}
  | {kind: 'unlock'; stage: PathwayStage}
  | {kind: 'skip'; stage: PathwayStage}
  | {kind: 'reinstate'; stage: PathwayStage}
  | {kind: 'assign'}
  | {kind: 'unassign'; userId: string; username: string}
  | {kind: 'delete'}
  | null;

/** What a screen reader says when a stage is finished (or skipped): which, and what's next. */
export function finishedMessage(stage: PathwayStage, pathway: Pathway, done = 'finished'): string {
  const next = pathway.stages?.find((s) => s.id === pathway.currentStageId);
  return `${stage.name} ${done}. ${next ? `Next stage: ${next.name}` : allDoneWords(pathway)}`;
}

/** That no stage is left to do: "Every stage is finished", or "...finished or skipped" when some were skipped. */
const allDoneWords = (pathway: Pathway) =>
  (pathway.skippedStageCount ?? 0) > 0 ? 'Every stage is finished or skipped' : 'Every stage is finished';

/** How far through its stages a pathway is: "3 of 5", with ", 1 skipped" when stages were skipped (they count as done). */
export function progressWords(pathway: Pick<Pathway, 'completedStageCount' | 'skippedStageCount' | 'stageCount'>): string {
  const skipped = pathway.skippedStageCount ?? 0;
  const done = (pathway.completedStageCount ?? 0) + skipped;
  return `${done} of ${pathway.stageCount ?? 0}${skipped > 0 ? `, ${skipped} skipped` : ''}`;
}

/** The wording of decision 18 in docs/pathways-ui-plan.md. */
export const NOT_ASSIGNED_MESSAGE = 'This pathway is not assigned to you, are you sure you want to make these changes?';

/**
 * Why staff can't finish a stage that requires a visit until one is marked Attended (a holder of OverrideStages can,
 * with a reason).
 */
export const NEEDS_VISIT_MESSAGE = 'Needs an attended visit first';

/** Pathways that are closed for good: nothing more is booked for them. */
const CLOSED_FOR_BOOKING = ['Cancelled', 'Archived'];

/** What booking from the page is for: a stage's visit (its Requested or Booked appointment, if any), or the pathway. */
interface Booking {
  stage?: PathwayStage;
  appointment?: Appointment;
}

/**
 * One pathway, and the place it's worked on: its details, who is assigned and the current stage on the left,
 * beside all its stages. Like Home, the two cards fill the page and each scrolls on its own, so the page itself
 * never does; a narrow window has one card instead (see PathwayView).
 *
 * Staff save the current stage's notes, finish it, and change the status (block, cancel, complete, archive), as
 * their roles allow (WorkPathways: the API's flags say so); holders of AssignPathways also assign and unassign
 * staff, and of OverrideStages unlock finished stages. Anyone who can work on pathways can work on any, but if it
 * isn't assigned to you, the first change of the visit asks you to confirm.
 *
 * Pathways that branch (docs/centre-phases-3-5-plan.md, decisions 113 to 117): an optional current stage can be
 * skipped with a reason (`SkipStageDialog`, showing the patient: hazard H-51); finishing a branch point asks which
 * branch follows (`BranchChoice`: in the template's order, none chosen, Finish disabled until the clinician chooses),
 * and says which stages that skips; skipped stages are struck through with why; and a holder of OverrideStages can
 * reinstate one (`ReinstateStageDialog`).
 *
 * A request to delete one of its notes in the address (`?deletionRequest=<id>`, from an admin's "Note deletion
 * requested" notification) opens in `NoteDeletionRequestDialog` over the page, for holders of CorrectRecords; closing it takes it out of
 * the address. A request about another pathway's note (an edited link) isn't shown over this page: it goes to its
 * own pathway's page (`onDeletionRequestElsewhere`).
 */
export function PathwayPage({id, deletionRequest, onCloseDeletionRequest, onDeletionRequestElsewhere}: PathwayPageProps) {
  const isNarrow = useIsNarrow();
  // Deciding note deletion requests is for holders of CorrectRecords.
  const canCorrectRecords = useCan('CorrectRecords');
  return (
    <>
      <PathwayView id={id} isNarrow={isNarrow} />
      <NoteDeletionRequestDialog
        requestId={canCorrectRecords ? (deletionRequest ?? null) : null}
        pathwayId={id}
        onClose={() => onCloseDeletionRequest?.()}
        onWrongPathway={
          onDeletionRequestElsewhere && deletionRequest ? (pathwayId) => onDeletionRequestElsewhere(pathwayId, deletionRequest) : undefined
        }
      />
    </>
  );
}

export interface PathwayPageProps {
  id: string;
  /** A request to delete one of its notes, to open (admins only): `?deletionRequest=<id>`. */
  deletionRequest?: string;
  /** Takes the request out of the address. */
  onCloseDeletionRequest?: () => void;
  /** Goes to another pathway's page with the request, when the request is about that pathway's note. */
  onDeletionRequestElsewhere?: (pathwayId: string, requestId: string) => void;
}

export interface PathwayViewProps {
  id: string;
  /**
   * A narrow window: one card of three folding sections (Details, Current stage, Stages) instead of two cards
   * (Details and Current stage beside Stages).
   */
  isNarrow: boolean;
}

/** The pathway page's content, laid out for the window (see PathwayPage). */
export function PathwayView({id, isNarrow}: PathwayViewProps) {
  const user = useCurrentUser();
  // What this person's roles allow here, beside what the pathway's own flags say (which the API also works out from
  // their permissions): assigning, unlocking and finishing without a visit, deleting, booking, and the audit log.
  const canAssignStaff = can(user, 'AssignPathways');
  const canOverrideStages = can(user, 'OverrideStages');
  const canBook = can(user, 'BookAppointments');
  const canViewAudit = can(user, 'ViewAudit');
  const canWork = can(user, 'WorkPathways');
  const {data: pathway, error, isPending} = useQuery(pathwayQuery(id));
  // While it's open, the notepad can add what's written to it as a note.
  useOfferToNotepad(pathway);
  const actions = usePathwayActions(id);
  const remove = useDeletePathway(id);
  const appointmentsQuery = useQuery(pathwayAppointmentsQuery(id));
  const appointments = appointmentsQuery.data?.items ?? [];
  const appointmentActions = useAppointmentActions();
  const [booking, setBooking] = useState<Booking | null>(null);
  // A stage whose visit has already happened (a walk-in), being recorded.
  const [recordingPast, setRecordingPast] = useState<PathwayStage | null>(null);
  const [withoutVisitReason, setWithoutVisitReason] = useState('');
  // The branch the clinician chooses as a branch point is finished: none until they choose (decision 114).
  const [chosenBranch, setChosenBranch] = useState('');
  const navigate = useNavigate();
  const announce = useAnnounce();
  // Files on their way, held here so they carry on when the window switches between one card and two.
  const attachments = useAttachmentsState();

  const [dialog, setDialog] = useState<Dialog>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  // The "not assigned to you" check: the action waiting on it, and whether it was answered this visit.
  const [waitingOnCheck, setWaitingOnCheck] = useState<(() => void) | null>(null);
  const [isCheckAnswered, setIsCheckAnswered] = useState(false);
  const [toAssign, setToAssign] = useState<string[]>([]);
  // What's open and folded is held here, so it carries over when the window switches between one card and two.
  // The defaults apply once, when the page loads: Details (with Patient info) and the Current stage open, Stages
  // folded, and in a narrow window Assigned staff and Files folded too. Stages in the list open only when clicked.
  const [open, setOpen] = useState(() => ({
    details: true,
    notes: true,
    currentStage: true,
    files: !isNarrow,
    stages: false,
    patientInfo: true,
    assignedStaff: !isNarrow,
    appointments: true,
  }));
  const toggle = (part: keyof typeof open) => setOpen((current) => ({...current, [part]: !current[part]}));
  const [openStageIds, setOpenStageIds] = useState<string[]>([]);
  // The current stage's notes box, in pixels: fitted to the free space once, as the page opens, then kept.
  const [notesHeight, setNotesHeight] = useState<number | undefined>(undefined);
  // Each stage's instructions start folded and open on their own (the same stage's in the Current stage section
  // and the list together).
  const [instructionsOpen, setInstructionsOpen] = useState<Record<string, boolean>>({});
  const instructions: InstructionsFolding = {
    isOpen: (stage) => instructionsOpen[stage.id ?? ''] ?? false,
    onOpenChange: (stage, isOpen) => setInstructionsOpen((current) => ({...current, [stage.id ?? '']: isOpen})),
  };
  // Each stage's files start folded (and unfetched), and open on their own, like its instructions.
  const [stageFilesOpen, setStageFilesOpen] = useState<Record<string, boolean>>({});

  // Who can be assigned: holders of WorkPathways (the API refuses anyone else).
  const staff = useQuery({...staffWithPermissionQuery('WorkPathways'), enabled: canAssignStaff && dialog?.kind === 'assign'});

  const isMissing = error instanceof ApiError && error.status === 404;
  const currentStage = pathway?.stages?.find((s) => s.id === pathway.currentStageId);
  const isMine = pathway?.assignments?.some((a) => a.username === user.username) ?? false;
  const isBusy = Object.values(actions).some((action) => action.isPending) || remove.isPending || appointmentActions.recordOutcome.isPending;

  /**
   * Runs a change to the pathway, first asking (once per visit) if it isn't assigned to you. Holders of AssignPathways
   * aren't asked: handing out and looking after any pathway is their job.
   */
  const checked = (action: () => void) => {
    setActionError(null);
    if (isMine || canAssignStaff || isCheckAnswered) action();
    else setWaitingOnCheck(() => action);
  };

  const failed = (fallback: string) => (e: unknown) => {
    setActionError(problemMessage(e, fallback));
    setDialog(null);
  };
  const close = () => setDialog(null);

  const startStage = (stage: PathwayStage) =>
    checked(() => actions.startStage.mutate(stage.id ?? '', {onError: failed('The stage could not be started.')}));
  const saveNotes = (stage: PathwayStage, notes: string) =>
    checked(() =>
      actions.saveNotes.mutate(
        {stageId: stage.id ?? '', notes},
        {onSuccess: () => announce('Notes saved'), onError: failed('The notes could not be saved.')},
      ),
    );
  /**
   * Finishing asks first: as it always has, or, for a stage that involves a visit with none marked Attended, whether
   * to finish it without one (a holder of OverrideStages giving a reason for a required one; anyone else can't, as Finish
   * says). A branch point finished for the first time asks either way which branch follows, none chosen to begin with.
   */
  const finishStage = (stage: PathwayStage, notes: string) =>
    checked(() => {
      setWithoutVisitReason('');
      setChosenBranch('');
      setDialog({kind: finishesWithoutVisit(stage, stageVisit(stage.id, appointments)) ? 'finishWithoutVisit' : 'finish', stage, notes});
    });

  /**
   * Saves notes typed but not saved, then finishes the stage (without its visit, when that's what was confirmed; with
   * the branch the clinician chose, at a branch point).
   */
  const confirmFinish = async (stage: PathwayStage, notes: string, withoutVisit = false) => {
    try {
      if (notes !== (stage.notes ?? '')) await actions.saveNotes.mutateAsync({stageId: stage.id ?? '', notes});
      const branch = stage.needsBranchChoice && chosenBranch ? {branch: chosenBranch} : undefined;
      const updated = await actions.finishStage.mutateAsync(
        withoutVisit
          ? {stageId: stage.id ?? '', withoutVisit, ...(stage.visitRequirement === 'Required' ? {reason: withoutVisitReason.trim()} : {}), ...branch}
          : branch
            ? {stageId: stage.id ?? '', ...branch}
            : (stage.id ?? ''),
      );
      announce(finishedMessage(stage, updated));
      close();
    } catch (e) {
      failed('The stage could not be finished.')(e);
    }
  };

  const confirmAssign = async () => {
    try {
      for (const userId of toAssign) await actions.assign.mutateAsync(userId);
      setToAssign([]);
      close();
    } catch (e) {
      failed('Staff could not be assigned.')(e);
    }
  };

  /**
   * The working part of a stage: its notes box and actions, or its notes read-only. In the Current stage section
   * (`isCurrentSection`), the notes box fills the section's free space as the page opens, then keeps that size.
   */
  const workFor = (stage: PathwayStage, isCurrentSection = false) => (
    <StageWork
      notesHeight={isCurrentSection ? notesHeight : undefined}
      onFitNotes={isCurrentSection ? setNotesHeight : undefined}
      key={`${stage.id}:${stage.notes}`}
      stage={stage}
      canUnlock={canOverrideStages}
      isBusy={isBusy}
      onStart={() => startStage(stage)}
      onSaveNotes={(notes) => saveNotes(stage, notes)}
      onFinish={(notes) => finishStage(stage, notes)}
      onUnlock={() => {
        setActionError(null);
        setDialog({kind: 'unlock', stage});
      }}
      onSkip={() => checked(() => setDialog({kind: 'skip', stage}))}
      onReinstate={() => {
        setActionError(null);
        setDialog({kind: 'reinstate', stage});
      }}
      visit={involvesVisit(stage) && visitFor(stage)}
      finishDisabledReason={
        !canOverrideStages && stage.visitRequirement === 'Required' && finishesWithoutVisit(stage, stageVisit(stage.id, appointments))
          ? NEEDS_VISIT_MESSAGE
          : undefined
      }
    />
  );

  /** A stage's Visit part: where its visit stands, and booking it, moving it or recording whether the patient came. */
  const visitFor = (stage: PathwayStage) =>
    appointmentsQuery.isError ? (
      <AlertMessage message="The stage's visit could not be loaded. Please try again." />
    ) : (
      <StageVisit
        stage={stage}
        appointments={appointments}
        isBusy={isBusy || appointmentsQuery.isPending}
        canBook={canBook}
        onBook={(requested) => setBooking({stage, appointment: requested})}
        onReschedule={(booked) => setBooking({stage, appointment: booked})}
        onRecordPast={() => {
          setActionError(null);
          setRecordingPast(stage);
        }}
        onOutcome={(appointment, outcome) => {
          setActionError(null);
          appointmentActions.recordOutcome.mutate(
            {id: appointment.id ?? '', outcome},
            {
              onSuccess: () => announce(outcome === 'Attended' ? 'Visit marked Attended' : 'Visit marked Did not attend'),
              onError: failed('The visit could not be updated.'),
            },
          );
        }}
      />
    );

  /** The pathway's only assignee, whom booking suggests (decision 30); none when there are several, or nobody. */
  const onlyAssignee = pathway?.assignments?.length === 1 ? (pathway.assignments[0].userId ?? undefined) : undefined;
  const withoutVisit = dialog?.kind === 'finishWithoutVisit' ? dialog.stage : null;
  const withoutVisitState = withoutVisit ? stageVisit(withoutVisit.id, appointments) : null;
  // The stage being finished, when it's a branch point finished for the first time: Finish waits for a branch.
  const finishing = dialog?.kind === 'finish' || dialog?.kind === 'finishWithoutVisit' ? dialog.stage : null;
  const choosesBranch = finishing?.needsBranchChoice ?? false;
  const isBranchMissing = choosesBranch && !chosenBranch;
  /** The branch choice, and who it's for (choosing one skips stages), in either finish dialog. */
  const branchChoice = finishing && choosesBranch && (
    <>
      {pathway?.patient && <PatientBanner patient={pathway.patient} isCompact />}
      <BranchChoice
        stage={finishing}
        branch={chosenBranch}
        onBranchChange={setChosenBranch}
        isDisabled={actions.saveNotes.isPending || actions.finishStage.isPending}
      />
    </>
  );
  // A stage's visit waiting in Requires Appointment is the one Record Past Visit records: its notes are kept, so it shows them.
  const recordingPastVisit = recordingPast ? stageVisit(recordingPast.id, appointments) : null;

  /** A stage's files, folded under its notes: they can change while its notes can. */
  const filesFor = (stage: PathwayStage) => (
    <AttachmentsFold
      target={{stageId: stage.id ?? ''}}
      canChange={stage.canEdit ?? false}
      isLocked={Boolean(pathway?.readOnlyReason)}
      state={attachments}
      isOpen={stageFilesOpen[stage.id ?? ''] ?? false}
      onOpenChange={(isOpen) => setStageFilesOpen((current) => ({...current, [stage.id ?? '']: isOpen}))}
    />
  );

  const assignedIds = new Set(pathway?.assignments?.map((a) => a.userId));
  const assignable = (staff.data ?? [])
    .filter((u) => !assignedIds.has(u.userId))
    .map((u) => ({value: u.userId ?? '', label: u.username ?? ''}));

  return (
    <Page
      title={pathway?.title ?? 'Pathway'}
      backHref="/pathways"
      backLabel="Back to pathways"
      isLoading={isPending}
      loadingMessage="Loading pathway..."
      fill={pathway !== undefined}
      banner={pathway?.patient && <PatientBanner patient={pathway.patient} />}
      message={
        isMissing ? (
          <AlertMessage message="Pathway not found. It may have been deleted." />
        ) : error ? (
          <AlertMessage message="The pathway could not be loaded. Please try again." />
        ) : actionError ? (
          <AlertMessage message={actionError} />
        ) : pathway?.patientFlags ? (
          // The patient's banners: restricted (when only blocking and cancelling are offered), disputed, anonymised.
          <PatientFlagBanners patient={pathway.patientFlags} />
        ) : (
          // The patient's record is restricted or anonymised, so nothing here can change (and nothing is offered).
          pathway?.readOnlyReason && <SummaryAlert status="warning" label="Read-only pathway" message={pathway.readOnlyReason} />
        )
      }
      actions={
        pathway && (
          <>
            {pathway.allowedStatusChanges?.map((to) => {
              const action = statusAction(to);
              return (
                <Button
                  key={to}
                  label={action.label}
                  variant={action.variant}
                  icon={<Icon icon={action.icon} />}
                  // Its own confirmation is the only question asked (not whether it's assigned to you).
                  onClick={() => {
                    setActionError(null);
                    setDialog({kind: 'status', to});
                  }}
                  isDisabled={isBusy}
                />
              );
            })}
            {canDeletePathway(pathway, can(user, 'DeletePathways')) && (
              <Button
                label="Delete Pathway"
                variant="destructive"
                icon={<Icon icon={AppIcon.Delete} />}
                onClick={() => {
                  setActionError(null);
                  setDialog({kind: 'delete'});
                }}
                isDisabled={isBusy}
              />
            )}
          </>
        )
      }
      dialogs={
        <>
          <ConfirmDialog
            isOpen={waitingOnCheck !== null}
            title="Not Assigned to You"
            message={NOT_ASSIGNED_MESSAGE}
            intent="warning"
            confirmText="Continue"
            onConfirm={() => {
              setIsCheckAnswered(true);
              const action = waitingOnCheck;
              setWaitingOnCheck(null);
              action?.();
            }}
            onCancel={() => setWaitingOnCheck(null)}
          />
          <StatusChangeDialog
            key={dialog?.kind === 'status' ? dialog.to : 'none'}
            to={dialog?.kind === 'status' ? dialog.to : null}
            isProcessing={actions.changeStatus.isPending}
            onConfirm={(reason) =>
              dialog?.kind === 'status' &&
              actions.changeStatus.mutate({status: dialog.to, reason}, {onSuccess: close, onError: failed('The status could not be changed.')})
            }
            onCancel={close}
          />
          <ConfirmDialog
            isOpen={dialog?.kind === 'finish'}
            title={dialog?.kind === 'finish' ? `Finish ${dialog.stage.name}?` : 'Finish Stage?'}
            message={
              choosesBranch
                ? 'The stage will be locked and the pathway moves on to the branch you choose. An admin can unlock it if it needs correcting.'
                : 'The stage will be locked and the pathway moves on to the next one. An admin can unlock it if it needs correcting.'
            }
            icon={AppIcon.Check}
            confirmText="Finish Stage"
            confirmDisabled={isBranchMissing}
            isProcessing={actions.saveNotes.isPending || actions.finishStage.isPending}
            onConfirm={() => dialog?.kind === 'finish' && void confirmFinish(dialog.stage, dialog.notes)}
            onCancel={close}>
            {dialog?.kind === 'finish' && branchChoice}
          </ConfirmDialog>
          <ConfirmDialog
            isOpen={withoutVisit !== null}
            title="Finish without a visit?"
            message={
              withoutVisit?.visitRequirement === 'Required'
                ? `${withoutVisit.name} requires a visit, and none is marked Attended. Your roles let you finish it without one: say why. The reason is kept in the audit log.`
                : `${withoutVisit?.name ?? 'This stage'} suggests a visit, and none is marked Attended. Finish it anyway?`
            }
            icon={AppIcon.Warning}
            intent="warning"
            confirmText="Finish Without Visit"
            confirmDisabled={(withoutVisit?.visitRequirement === 'Required' && !withoutVisitReason.trim()) || isBranchMissing}
            isProcessing={actions.saveNotes.isPending || actions.finishStage.isPending}
            onConfirm={() => dialog?.kind === 'finishWithoutVisit' && void confirmFinish(dialog.stage, dialog.notes, true)}
            onCancel={close}>
            <VStack gap={3}>
              {withoutVisitState?.status === 'Booked' && withoutVisitState.appointment && (
                <Text type="body">
                  {`Its visit, ${appointmentWhen(withoutVisitState.appointment)}, stays booked: cancel it on the calendar if it's no longer needed.`}
                </Text>
              )}
              {withoutVisitState?.status === 'Requested' && <Text type="body">Its visit comes off Requires Appointment.</Text>}
              {withoutVisit?.visitRequirement === 'Required' && (
                <TextArea
                  label="Reason"
                  value={withoutVisitReason}
                  onChange={setWithoutVisitReason}
                  placeholder="e.g. Seen at another clinic"
                  isRequired
                />
              )}
              {dialog?.kind === 'finishWithoutVisit' && branchChoice}
            </VStack>
          </ConfirmDialog>
          {pathway && (
            <BookAppointmentDialog
              isOpen={booking !== null}
              onClose={() => setBooking(null)}
              patient={{...pathway.patient, ...pathway.patientFlags, id: pathway.patientId ?? ''}}
              pathway={{id: pathway.id ?? '', title: pathway.title ?? ''}}
              stage={booking?.stage && {id: booking.stage.id ?? '', name: booking.stage.name ?? ''}}
              appointment={booking?.appointment}
              suggestedStaffUserId={onlyAssignee}
            />
          )}
          {pathway && (
            <RecordPastAppointmentDialog
              isOpen={recordingPast !== null}
              onClose={() => setRecordingPast(null)}
              patient={{...pathway.patient, ...pathway.patientFlags, id: pathway.patientId ?? ''}}
              pathway={{id: pathway.id ?? '', title: pathway.title ?? ''}}
              stage={recordingPast ? {id: recordingPast.id ?? '', name: recordingPast.name ?? ''} : undefined}
              waitingVisitNotes={recordingPastVisit?.status === 'Requested' ? recordingPastVisit.appointment?.notes : undefined}
              staffUserId={onlyAssignee}
            />
          )}
          <ConfirmDialog
            isOpen={dialog?.kind === 'unlock'}
            title={dialog?.kind === 'unlock' ? `Unlock ${dialog.stage.name}?` : 'Unlock Stage?'}
            message="Staff can then edit its notes and finish it again. A pathway awaiting review or discharged goes back to in progress."
            icon={AppIcon.Lock}
            intent="warning"
            confirmText="Unlock Stage"
            isProcessing={actions.unlockStage.isPending}
            onConfirm={() =>
              dialog?.kind === 'unlock' &&
              actions.unlockStage.mutate(dialog.stage.id ?? '', {onSuccess: close, onError: failed('The stage could not be unlocked.')})
            }
            onCancel={close}
          />
          {dialog?.kind === 'skip' && (
            <SkipStageDialog
              key={dialog.stage.id}
              stage={dialog.stage}
              patient={pathway?.patient}
              isProcessing={actions.skipStage.isPending}
              onCancel={close}
              onConfirm={(input, fail) =>
                actions.skipStage.mutate(
                  {stageId: dialog.stage.id ?? '', ...input},
                  {
                    onSuccess: (updated) => {
                      announce(finishedMessage(dialog.stage, updated, 'skipped'));
                      close();
                    },
                    onError: fail,
                  },
                )
              }
            />
          )}
          {dialog?.kind === 'reinstate' && (
            <ReinstateStageDialog
              key={dialog.stage.id}
              stage={dialog.stage}
              patient={pathway?.patient}
              isProcessing={actions.reinstateStage.isPending}
              onCancel={close}
              onConfirm={(reason, fail) =>
                actions.reinstateStage.mutate(
                  {stageId: dialog.stage.id ?? '', reason},
                  {
                    onSuccess: () => {
                      announce(`${dialog.stage.name} reinstated`);
                      close();
                    },
                    onError: fail,
                  },
                )
              }
            />
          )}
          <SelectionDialog
            isOpen={dialog?.kind === 'assign'}
            title="Assign Staff"
            message="The pathway will show in their task lists."
            icon={AppIcon.Assign}
            items={assignable}
            selected={toAssign}
            onSelectedChange={setToAssign}
            listLabel="Staff"
            emptyTitle={staff.isPending ? 'Loading staff...' : 'Everyone is already assigned.'}
            confirmText="Assign"
            isProcessing={actions.assign.isPending}
            onConfirm={() => void confirmAssign()}
            onCancel={() => {
              setToAssign([]);
              close();
            }}
          />
          <ConfirmDialog
            isOpen={dialog?.kind === 'unassign'}
            title="Unassign Staff"
            message={
              dialog?.kind === 'unassign' ? `${dialog.username} will no longer see this pathway in their task list.` : undefined
            }
            icon={AppIcon.Revoke}
            intent="warning"
            confirmText="Unassign"
            isProcessing={actions.unassign.isPending}
            onConfirm={() =>
              dialog?.kind === 'unassign' &&
              actions.unassign.mutate(dialog.userId, {onSuccess: close, onError: failed('Staff could not be unassigned.')})
            }
            onCancel={close}
          />
          <ConfirmDialog
            isOpen={dialog?.kind === 'delete'}
            title="Confirm Delete"
            message="The pathway and its stages will be deleted. This action cannot be undone; its audit log is kept."
            isDanger
            confirmText="Delete Pathway"
            isProcessing={remove.isPending}
            onConfirm={() =>
              remove.mutate(undefined, {
                onSuccess: () => {
                  announce('Pathway deleted');
                  void navigate({to: '/patients/$id/pathways', params: {id: pathway?.patientId ?? ''}, replace: true});
                },
                onError: failed('The pathway could not be deleted.'),
              })
            }
            onCancel={close}>
            {dialog?.kind === 'delete' && pathway && (
              <PathwaySummary title={pathway.title} patientName={pathway.patientName} templateName={pathway.templateName} />
            )}
          </ConfirmDialog>
        </>
      }>
      {pathway && (isNarrow ? narrowLayout(pathway) : wideLayout(pathway))}
    </Page>
  );

  /** Status and progress, then parts that fold: Patient info, Appointments and Assigned staff. */
  function details(pathway: Pathway) {
    return (
      <>
        <Section variant="transparent" padding={6}>
          <VStack gap={4}>
            <HStack gap={2} vAlign="center">
            <StatusToken status={pathway.status ?? ''} />
            <PriorityToken priority={pathway.priority ?? ''} />
            {pathway.status === 'Blocked' && pathway.blockedFromStatus && (
              <Text type="supporting" color="secondary">
                Was {statusLabel(pathway.blockedFromStatus).toLowerCase()} before it was put on hold
              </Text>
            )}
          </HStack>
          {/* Skipped stages count as done, and say how many were skipped. */}
          <ProgressBar
            label="Stages finished"
            value={(pathway.completedStageCount ?? 0) + (pathway.skippedStageCount ?? 0)}
            max={Math.max(pathway.stageCount ?? 0, 1)}
            hasValueLabel
            formatValueLabel={() => progressWords(pathway)}
          />
          {/* Accepted from a referral: a link back to it (only the pathway's own read says so). */}
          {pathway.referral?.id && (
            <Text type="body">
              <Link href={`/referrals/${pathway.referral.id}`}>
                {`From a referral received ${formatDate(pathway.referral.receivedOn ?? '')}, ${pathway.referral.specialtyName}`}
              </Link>
            </Text>
          )}
          </VStack>
        </Section>
        <DetailPart title="Patient info" isOpen={open.patientInfo} onToggle={() => toggle('patientInfo')}>
          <ViewSection>
            <MetadataListItem label="Patient">
              <Link href={`/patients/${pathway.patientId}`}>{pathway.patientName}</Link>
            </MetadataListItem>
            <MetadataListItem label="Template">{pathway.templateName || '—'}</MetadataListItem>
            <MetadataListItem label="Target date">
              <DueDateCell dueDate={pathway.dueDate ?? ''} isOverdue={pathway.isOverdue ?? false} />
            </MetadataListItem>
            <MetadataListItem label="Created">{formatDateTime(pathway.createdAt ?? '')}</MetadataListItem>
            <MetadataListItem label="Last updated">{formatDateTime(pathway.lastUpdated ?? '')}</MetadataListItem>
          </ViewSection>
        </DetailPart>
        <DetailPart title="Appointments" isOpen={open.appointments} onToggle={() => toggle('appointments')}>
          {/* Booking sits with the appointments, not in the footer, which has no room for another button. */}
          <VStack gap={3}>
            {appointmentsList(pathway)}
            {canBook && !CLOSED_FOR_BOOKING.includes(pathway.status ?? '') && (
              <HStack>
                <Button
                  label="Book Appointment"
                  variant="secondary"
                  icon={<Icon icon={AppIcon.Book} />}
                  onClick={() => {
                    setActionError(null);
                    setBooking({});
                  }}
                  isDisabled={isBusy}
                />
              </HStack>
            )}
          </VStack>
        </DetailPart>
        <DetailPart title="Assigned staff" isOpen={open.assignedStaff} onToggle={() => toggle('assignedStaff')}>
          <AssigneeList
            isTitled={false}
            assignments={pathway.assignments ?? []}
            canManage={canAssign(pathway)}
            isBusy={isBusy}
            onUnassign={(a) => {
              setActionError(null);
              setDialog({kind: 'unassign', userId: a.userId ?? '', username: a.username ?? ''});
            }}
          />
        </DetailPart>
      </>
    );
  }

  /**
   * Every appointment for the pathway, each opening on the Calendar. Booked ones of a cancelled pathway are never
   * cancelled by the app (someone may need to tell the patient), so they're pointed out.
   */
  function appointmentsList(pathway: Pathway) {
    if (appointmentsQuery.isError) return <AlertMessage message="The appointments could not be loaded. Please try again." />;
    if (appointmentsQuery.isPending)
      return (
        <Text type="body" color="secondary">
          Loading appointments...
        </Text>
      );
    if (appointments.length === 0)
      return (
        <Text type="body" color="secondary">
          No appointments yet.
        </Text>
      );
    const stillBooked = appointments.filter((a) => a.status === 'Booked').length;
    return (
      <VStack gap={2}>
        {pathway.status === 'Cancelled' && stillBooked > 0 && (
          <Text type="body">
            {`The pathway is cancelled, but ${stillBooked === 1 ? 'an appointment is' : `${stillBooked} appointments are`} still booked. Tell the patient, then cancel ${stillBooked === 1 ? 'it' : 'them'} on the calendar.`}
          </Text>
        )}
        <AppointmentList appointments={appointments} label="Appointments" />
      </VStack>
    );
  }

  function canAssign(pathway: Pathway) {
    return canAssignStaff && (pathway.canChangeAssignments ?? false);
  }

  /**
   * Edit Details, View Patient, Assign Staff (AssignPathways) and the pathway's Audit Log (ViewAudit). Book Appointment
   * is under Appointments.
   */
  function footer(pathway: Pathway) {
    return (
      <>
        {pathway.canEditDetails && (
          <Button
            label="Edit Details"
            variant="secondary"
            icon={<Icon icon={AppIcon.Edit} />}
            href={`/pathways/${pathway.id}/edit`}
            isDisabled={isBusy}
          />
        )}
        {canAssign(pathway) && (
          <Button
            label="Assign Staff"
            variant="secondary"
            icon={<Icon icon={AppIcon.Assign} />}
            onClick={() => {
              setActionError(null);
              setDialog({kind: 'assign'});
            }}
            isDisabled={isBusy}
          />
        )}
        {canViewAudit && (
          <Button
            label="Audit Log"
            tooltip="View this pathway's audit log"
            variant="secondary"
            icon={<Icon icon={AppIcon.AuditLog} />}
            href={`/admin/audit?pathwayId=${pathway.id}`}
          />
        )}
        <Button
          label="View Patient"
          variant="secondary"
          icon={<Icon icon={AppIcon.View} />}
          href={`/patients/${pathway.patientId}`}
        />
      </>
    );
  }

  /** The stage being worked on, with its notes box and actions. */
  function currentStageBody(pathway: Pathway) {
    return (
      <Section variant="transparent" padding={6}>
        <CurrentStage
          pathway={pathway}
          canWork={canWork}
          stage={currentStage}
          work={currentStage && workFor(currentStage, true)}
          files={currentStage && filesFor(currentStage)}
          instructions={instructions}
          // Its Visit part says where the visit stands while it's worked on; otherwise the title does.
          visit={currentStage && !currentStage.canEdit && <StageVisitTokens stage={currentStage} appointments={appointments} />}
        />
      </Section>
    );
  }

  /**
   * Every stage, each opening when clicked. Finished stages can be unlocked (OverrideStages) and then edited here. The
   * current stage is worked on in the Current stage section while that's open; when it's folded, here.
   */
  function stageList(pathway: Pathway) {
    return (
      <StageList
        stages={pathway.stages ?? []}
        openStageIds={openStageIds}
        onStageOpenChange={(stageId, isOpen) =>
          setOpenStageIds((ids) => (isOpen ? [...ids, stageId] : ids.filter((other) => other !== stageId)))
        }
        renderWork={(stage) => (stage.isCurrent && open.currentStage ? undefined : workFor(stage))}
        renderFiles={filesFor}
        instructions={instructions}
        renderVisit={(stage) => <StageVisitTokens stage={stage} appointments={appointments} />}
      />
    );
  }

  function detailsSection(pathway: Pathway): FoldingSection {
    return {key: 'details', title: 'Details', isOpen: open.details, onToggle: () => toggle('details'), children: details(pathway)};
  }

  /** The pathway's notes: Add Note, and every note, newest first (your own can be edited). */
  function notesSection(pathway: Pathway): FoldingSection {
    return {
      key: 'notes',
      title: 'Notes',
      isOpen: open.notes,
      onToggle: () => toggle('notes'),
      children: (
        <Section variant="transparent" padding={6}>
          <PathwayNotes pathway={pathway} />
        </Section>
      ),
    };
  }

  function currentStageSection(pathway: Pathway): FoldingSection {
    return {
      key: 'currentStage',
      title: 'Current stage',
      isOpen: open.currentStage,
      onToggle: () => toggle('currentStage'),
      children: currentStageBody(pathway),
    };
  }

  /**
   * The pathway's own files (not its stages'). They can change unless it's cancelled or archived, as its details
   * can; admins can delete any.
   */
  function filesSection(pathway: Pathway): FoldingSection {
    return {
      key: 'files',
      title: 'Files',
      isOpen: open.files,
      onToggle: () => toggle('files'),
      children: (
        <Section variant="transparent" padding={6}>
          <Attachments
            target={{pathwayId: pathway.id ?? ''}}
            canChange={pathway.canEditDetails ?? false}
            isLocked={Boolean(pathway.readOnlyReason)}
            state={attachments}
          />
        </Section>
      ),
    };
  }

  /**
   * Two cards side by side: Details, Notes, the Current stage and Files, each folding and sharing the left card, and
   * all the stages on the right.
   */
  function wideLayout(pathway: Pathway) {
    return (
      <TwoColumns
        start={
          <FoldingCard
            sections={[detailsSection(pathway), notesSection(pathway), currentStageSection(pathway), filesSection(pathway)]}
            footerActions={footer(pathway)}
          />
        }
        end={
          <PanelCard title="Stages" isBodyPadded={false}>
            {stageList(pathway)}
          </PanelCard>
        }
      />
    );
  }

  /**
   * One card of five folding sections: Details, Notes, Current stage, Files and Stages, scrolling together; the page
   * never does. View Patient stays in the footer.
   */
  function narrowLayout(pathway: Pathway) {
    return (
      <FoldingCard
        footerActions={footer(pathway)}
        sections={[
          detailsSection(pathway),
          notesSection(pathway),
          currentStageSection(pathway),
          filesSection(pathway),
          {key: 'stages', title: 'Stages', isOpen: open.stages, onToggle: () => toggle('stages'), children: stageList(pathway)},
        ]}
      />
    );
  }
}

/** A part of the details that folds, laid out like a row of the stage list: its title is the toggle. */
function DetailPart({title, isOpen, onToggle, children}: {title: string; isOpen: boolean; onToggle: () => void; children: ReactNode}) {
  return (
    <Section variant="transparent" paddingBlock={4} paddingInline={6} dividers={['top']}>
      <Collapsible
        isOpen={isOpen}
        onOpenChange={onToggle}
        chevronPosition="end"
        trigger={
          <Text type="body" size="lg" weight="bold">
            {title}
          </Text>
        }>
        {/* A little room between the title and what it opens. */}
        <VStack gap={4} paddingBlockStart={3}>
          {children}
        </VStack>
      </Collapsible>
    </Section>
  );
}

/**
 * The stage being worked on, in full, with its notes box and actions (`work`); or a note that every stage is
 * finished. Says why when the pathway's status, or the person's roles (`canWork`), stop work on it.
 */
interface CurrentStageProps {
  pathway: Pathway;
  /** The person's roles grant WorkPathways. */
  canWork: boolean;
  stage: PathwayStage | undefined;
  work: ReactNode;
  /** The stage's files, under its notes. */
  files: ReactNode;
  instructions: InstructionsFolding;
  /** The stage's visit, after its title. */
  visit?: ReactNode;
}

function CurrentStage({pathway, canWork, stage, work, files, instructions, visit}: CurrentStageProps) {
  return (
    <VStack gap={4}>
      {stage ? (
        <VStack gap={3}>
          <StageTitle stage={stage} extra={visit} />
          {!pathway.canWorkOnStages && (
            <Text type="body" color="secondary">
              {canWork
                ? `Stages can't be worked on while the pathway is ${statusLabel(pathway.status ?? '').toLowerCase()}.`
                : "Your roles don't allow working on pathways, so this is read only."}
            </Text>
          )}
          <StageDetails stage={stage} work={work} files={files} instructions={instructions} />
        </VStack>
      ) : (
        <Text type="body" color="secondary">
          {`${allDoneWords(pathway)}.`}
        </Text>
      )}
    </VStack>
  );
}
