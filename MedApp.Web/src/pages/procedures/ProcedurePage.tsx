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
import {procedureAppointmentsQuery, useAppointmentActions, type Appointment} from '../../api/appointments';
import {ApiError, problemMessage} from '../../api/errors';
import {
  canDeleteProcedure,
  procedureQuery,
  useDeleteProcedure,
  useProcedureActions,
  type Procedure,
  type ProcedureStage,
} from '../../api/procedures';
import {userSearchQuery} from '../../api/search';
import {useAnnounce} from '../../app/announcements';
import {useCurrentUser} from '../../auth/auth';
import {BookAppointmentDialog} from '../../components/calendar/BookAppointmentDialog';
import {Attachments} from '../../components/attachments/Attachments';
import {AttachmentsFold} from '../../components/attachments/AttachmentsFold';
import {useAttachmentsState} from '../../components/attachments/useAttachmentsState';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {SelectionDialog} from '../../components/modals/SelectionDialog';
import {PatientBanner} from '../../components/patients/PatientBanner';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {TwoColumns} from '../../components/pages/TwoColumns';
import {AppointmentList} from '../../components/procedures/AppointmentList';
import {AssigneeList} from '../../components/procedures/AssigneeList';
import {DueDateCell, PriorityToken, StatusToken, statusLabel} from '../../components/procedures/ProcedureCells';
import {ProcedureSummary} from '../../components/procedures/ProcedureSummary';
import {StageDetails, StageList, StageTitle, type InstructionsFolding} from '../../components/procedures/StageList';
import {appointmentWhen, finishesWithoutVisit, involvesVisit, StageVisit, StageVisitTokens, stageVisit} from '../../components/procedures/StageVisit';
import {StageWork} from '../../components/procedures/StageWork';
import {StatusChangeDialog, statusAction} from '../../components/procedures/StatusChangeDialog';
import {PatientFlagBanners} from '../../components/patients/PatientFlagBanners';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {FoldingCard, type FoldingSection} from '../../components/views/FoldingCard';
import {PanelCard} from '../../components/views/PanelCard';
import {ViewSection} from '../../components/views/ViewSection';
import {formatDateTime} from '../../utils/dates';
import {RichText} from '../../components/shared/RichText';

/** The dialog the page has open, if any. */
type Dialog =
  | {kind: 'status'; to: string}
  | {kind: 'finish'; stage: ProcedureStage; notes: string}
  | {kind: 'finishWithoutVisit'; stage: ProcedureStage; notes: string}
  | {kind: 'unlock'; stage: ProcedureStage}
  | {kind: 'assign'}
  | {kind: 'unassign'; userId: string; username: string}
  | {kind: 'delete'}
  | null;

/** What a screen reader says when a stage is finished: which, and what's next. */
export function finishedMessage(stage: ProcedureStage, procedure: Procedure): string {
  const next = procedure.stages?.find((s) => s.id === procedure.currentStageId);
  return `${stage.name} finished. ${next ? `Next stage: ${next.name}` : 'Every stage is finished'}`;
}

/** The wording of decision 18 in docs/procedures-ui-plan.md. */
export const NOT_ASSIGNED_MESSAGE = 'This procedure is not assigned to you, are you sure you want to make these changes?';

/** Why staff can't finish a stage that requires a visit until one is marked Attended (an admin can, with a reason). */
export const NEEDS_VISIT_MESSAGE = 'Needs an attended visit first';

/** Procedures that are closed for good: nothing more is booked for them. */
const CLOSED_FOR_BOOKING = ['Cancelled', 'Archived'];

/** What booking from the page is for: a stage's visit (its Requested or Booked appointment, if any), or the procedure. */
interface Booking {
  stage?: ProcedureStage;
  appointment?: Appointment;
}

/**
 * One procedure, and the place it's worked on: its details, who is assigned and the current stage on the left,
 * beside all its stages. Like Home, the two cards fill the page and each scrolls on its own, so the page itself
 * never does; a narrow window has one card instead (see ProcedureView).
 *
 * Staff save the current stage's notes, finish it, and change the status (block, cancel, complete, archive);
 * admins also assign and unassign staff and unlock finished stages. Anyone can work on any procedure, but if
 * it isn't assigned to you, the first change of the visit asks you to confirm.
 */
export function ProcedurePage({id}: {id: string}) {
  const isNarrow = useIsNarrow();
  return <ProcedureView id={id} isNarrow={isNarrow} />;
}

export interface ProcedureViewProps {
  id: string;
  /**
   * A narrow window: one card of three folding sections (Details, Current stage, Stages) instead of two cards
   * (Details and Current stage beside Stages).
   */
  isNarrow: boolean;
}

/** The procedure page's content, laid out for the window (see ProcedurePage). */
export function ProcedureView({id, isNarrow}: ProcedureViewProps) {
  const user = useCurrentUser();
  const {data: procedure, error, isPending} = useQuery(procedureQuery(id));
  const actions = useProcedureActions(id);
  const remove = useDeleteProcedure(id);
  const appointmentsQuery = useQuery(procedureAppointmentsQuery(id));
  const appointments = appointmentsQuery.data?.items ?? [];
  const appointmentActions = useAppointmentActions();
  const [booking, setBooking] = useState<Booking | null>(null);
  const [withoutVisitReason, setWithoutVisitReason] = useState('');
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

  const staff = useQuery({...userSearchQuery({PageSize: 100, Sort: 'username'}), enabled: user.isAdmin && dialog?.kind === 'assign'});

  const isMissing = error instanceof ApiError && error.status === 404;
  const currentStage = procedure?.stages?.find((s) => s.id === procedure.currentStageId);
  const isMine = procedure?.assignments?.some((a) => a.username === user.username) ?? false;
  const isBusy = Object.values(actions).some((action) => action.isPending) || remove.isPending || appointmentActions.recordOutcome.isPending;

  /**
   * Runs a change to the procedure, first asking (once per visit) if it isn't assigned to you. Admins aren't asked:
   * looking after any procedure is their job.
   */
  const checked = (action: () => void) => {
    setActionError(null);
    if (isMine || user.isAdmin || isCheckAnswered) action();
    else setWaitingOnCheck(() => action);
  };

  const failed = (fallback: string) => (e: unknown) => {
    setActionError(problemMessage(e, fallback));
    setDialog(null);
  };
  const close = () => setDialog(null);

  const startStage = (stage: ProcedureStage) =>
    checked(() => actions.startStage.mutate(stage.id ?? '', {onError: failed('The stage could not be started.')}));
  const saveNotes = (stage: ProcedureStage, notes: string) =>
    checked(() =>
      actions.saveNotes.mutate(
        {stageId: stage.id ?? '', notes},
        {onSuccess: () => announce('Notes saved'), onError: failed('The notes could not be saved.')},
      ),
    );
  /**
   * Finishing asks first: as it always has, or, for a stage that involves a visit with none marked Attended, whether
   * to finish it without one (an admin giving a reason for a required one; staff can't, as Finish says).
   */
  const finishStage = (stage: ProcedureStage, notes: string) =>
    checked(() => {
      setWithoutVisitReason('');
      setDialog({kind: finishesWithoutVisit(stage, stageVisit(stage.id, appointments)) ? 'finishWithoutVisit' : 'finish', stage, notes});
    });

  /** Saves notes typed but not saved, then finishes the stage (without its visit, when that's what was confirmed). */
  const confirmFinish = async (stage: ProcedureStage, notes: string, withoutVisit = false) => {
    try {
      if (notes !== (stage.notes ?? '')) await actions.saveNotes.mutateAsync({stageId: stage.id ?? '', notes});
      const updated = await actions.finishStage.mutateAsync(
        withoutVisit
          ? {stageId: stage.id ?? '', withoutVisit, ...(stage.visitRequirement === 'Required' ? {reason: withoutVisitReason.trim()} : {})}
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
  const workFor = (stage: ProcedureStage, isCurrentSection = false) => (
    <StageWork
      notesHeight={isCurrentSection ? notesHeight : undefined}
      onFitNotes={isCurrentSection ? setNotesHeight : undefined}
      key={`${stage.id}:${stage.notes}`}
      stage={stage}
      isAdmin={user.isAdmin}
      isBusy={isBusy}
      onStart={() => startStage(stage)}
      onSaveNotes={(notes) => saveNotes(stage, notes)}
      onFinish={(notes) => finishStage(stage, notes)}
      onUnlock={() => {
        setActionError(null);
        setDialog({kind: 'unlock', stage});
      }}
      visit={involvesVisit(stage) && visitFor(stage)}
      finishDisabledReason={
        !user.isAdmin && stage.visitRequirement === 'Required' && finishesWithoutVisit(stage, stageVisit(stage.id, appointments))
          ? NEEDS_VISIT_MESSAGE
          : undefined
      }
    />
  );

  /** A stage's Visit part: where its visit stands, and booking it, moving it or recording whether the patient came. */
  const visitFor = (stage: ProcedureStage) =>
    appointmentsQuery.isError ? (
      <AlertMessage message="The stage's visit could not be loaded. Please try again." />
    ) : (
      <StageVisit
        stage={stage}
        appointments={appointments}
        isBusy={isBusy || appointmentsQuery.isPending}
        onBook={(requested) => setBooking({stage, appointment: requested})}
        onReschedule={(booked) => setBooking({stage, appointment: booked})}
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

  /** The procedure's only assignee, whom booking suggests (decision 30); none when there are several, or nobody. */
  const onlyAssignee = procedure?.assignments?.length === 1 ? (procedure.assignments[0].userId ?? undefined) : undefined;
  const withoutVisit = dialog?.kind === 'finishWithoutVisit' ? dialog.stage : null;
  const withoutVisitState = withoutVisit ? stageVisit(withoutVisit.id, appointments) : null;

  /** A stage's files, folded under its notes: they can change while its notes can. */
  const filesFor = (stage: ProcedureStage) => (
    <AttachmentsFold
      target={{stageId: stage.id ?? ''}}
      canChange={stage.canEdit ?? false}
      isLocked={Boolean(procedure?.readOnlyReason)}
      state={attachments}
      isOpen={stageFilesOpen[stage.id ?? ''] ?? false}
      onOpenChange={(isOpen) => setStageFilesOpen((current) => ({...current, [stage.id ?? '']: isOpen}))}
    />
  );

  const assignedIds = new Set(procedure?.assignments?.map((a) => a.userId));
  const assignable = (staff.data?.items ?? [])
    .filter((u) => !assignedIds.has(u.userId))
    .map((u) => ({value: u.userId ?? '', label: u.username ?? ''}));

  return (
    <Page
      title={procedure?.title ?? 'Procedure'}
      backHref="/procedures"
      backLabel="Back to procedures"
      isLoading={isPending}
      loadingMessage="Loading procedure..."
      fill={procedure !== undefined}
      banner={procedure?.patient && <PatientBanner patient={procedure.patient} />}
      message={
        isMissing ? (
          <AlertMessage message="Procedure not found. It may have been deleted." />
        ) : error ? (
          <AlertMessage message="The procedure could not be loaded. Please try again." />
        ) : actionError ? (
          <AlertMessage message={actionError} />
        ) : procedure?.patientFlags ? (
          // The patient's banners: restricted (when only blocking and cancelling are offered), disputed, anonymised.
          <PatientFlagBanners patient={procedure.patientFlags} />
        ) : (
          // The patient's record is restricted or anonymised, so nothing here can change (and nothing is offered).
          procedure?.readOnlyReason && <AlertMessage status="warning" message={procedure.readOnlyReason} />
        )
      }
      actions={
        procedure && (
          <>
            {procedure.allowedStatusChanges?.map((to) => {
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
            {canDeleteProcedure(procedure, user.isAdmin) && (
              <Button
                label="Delete Procedure"
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
            message="The stage will be locked and the procedure moves on to the next one. An admin can unlock it if it needs correcting."
            icon={AppIcon.Check}
            confirmText="Finish Stage"
            isProcessing={actions.saveNotes.isPending || actions.finishStage.isPending}
            onConfirm={() => dialog?.kind === 'finish' && void confirmFinish(dialog.stage, dialog.notes)}
            onCancel={close}
          />
          <ConfirmDialog
            isOpen={withoutVisit !== null}
            title="Finish without a visit?"
            message={
              withoutVisit?.visitRequirement === 'Required'
                ? `${withoutVisit.name} requires a visit, and none is marked Attended. As an admin, you can finish it without one: say why. The reason is kept in the audit log.`
                : `${withoutVisit?.name ?? 'This stage'} suggests a visit, and none is marked Attended. Finish it anyway?`
            }
            icon={AppIcon.Warning}
            intent="warning"
            confirmText="Finish Without Visit"
            confirmDisabled={withoutVisit?.visitRequirement === 'Required' && !withoutVisitReason.trim()}
            isProcessing={actions.saveNotes.isPending || actions.finishStage.isPending}
            onConfirm={() => dialog?.kind === 'finishWithoutVisit' && void confirmFinish(dialog.stage, dialog.notes, true)}
            onCancel={close}>
            <VStack gap={3}>
              {withoutVisitState?.status === 'Booked' && withoutVisitState.appointment && (
                <Text type="body">
                  {`Its visit, ${appointmentWhen(withoutVisitState.appointment)}, stays booked: cancel it on the calendar if it's no longer needed.`}
                </Text>
              )}
              {withoutVisitState?.status === 'Requested' && <Text type="body">Its visit comes off Needs booking.</Text>}
              {withoutVisit?.visitRequirement === 'Required' && (
                <TextArea
                  label="Reason"
                  value={withoutVisitReason}
                  onChange={setWithoutVisitReason}
                  placeholder="e.g. Seen at another clinic"
                  isRequired
                />
              )}
            </VStack>
          </ConfirmDialog>
          {procedure && (
            <BookAppointmentDialog
              isOpen={booking !== null}
              onClose={() => setBooking(null)}
              patient={{...procedure.patient, ...procedure.patientFlags, id: procedure.patientId ?? ''}}
              procedure={{id: procedure.id ?? '', title: procedure.title ?? ''}}
              stage={booking?.stage && {id: booking.stage.id ?? '', name: booking.stage.name ?? ''}}
              appointment={booking?.appointment}
              suggestedStaffUserId={onlyAssignee}
            />
          )}
          <ConfirmDialog
            isOpen={dialog?.kind === 'unlock'}
            title={dialog?.kind === 'unlock' ? `Unlock ${dialog.stage.name}?` : 'Unlock Stage?'}
            message="Staff can then edit its notes and finish it again. A procedure in review or completed goes back to in progress."
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
          <SelectionDialog
            isOpen={dialog?.kind === 'assign'}
            title="Assign Staff"
            message="The procedure will show in their task lists."
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
              dialog?.kind === 'unassign' ? `${dialog.username} will no longer see this procedure in their task list.` : undefined
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
            message="The procedure and its stages will be deleted. This action cannot be undone; its audit log is kept."
            isDanger
            confirmText="Delete Procedure"
            isProcessing={remove.isPending}
            onConfirm={() =>
              remove.mutate(undefined, {
                onSuccess: () => {
                  announce('Procedure deleted');
                  void navigate({to: '/patients/$id/procedures', params: {id: procedure?.patientId ?? ''}, replace: true});
                },
                onError: failed('The procedure could not be deleted.'),
              })
            }
            onCancel={close}>
            {dialog?.kind === 'delete' && procedure && (
              <ProcedureSummary title={procedure.title} patientName={procedure.patientName} templateName={procedure.templateName} />
            )}
          </ConfirmDialog>
        </>
      }>
      {procedure && (isNarrow ? narrowLayout(procedure) : wideLayout(procedure))}
    </Page>
  );

  /** Status and progress, then two parts that fold: Patient info (the details and notes) and Assigned staff. */
  function details(procedure: Procedure) {
    return (
      <>
        <Section variant="transparent" padding={6}>
          <VStack gap={4}>
            <HStack gap={2} vAlign="center">
            <StatusToken status={procedure.status ?? ''} />
            <PriorityToken priority={procedure.priority ?? ''} />
            {procedure.status === 'Blocked' && procedure.blockedFromStatus && (
              <Text type="supporting" color="secondary">
                Was {statusLabel(procedure.blockedFromStatus).toLowerCase()} before it was blocked
              </Text>
            )}
          </HStack>
          <ProgressBar
            label="Stages finished"
            value={procedure.completedStageCount ?? 0}
            max={Math.max(procedure.stageCount ?? 0, 1)}
            hasValueLabel
            formatValueLabel={(value) => `${value} of ${procedure.stageCount ?? 0}`}
          />
          </VStack>
        </Section>
        <DetailPart title="Patient info" isOpen={open.patientInfo} onToggle={() => toggle('patientInfo')}>
          <ViewSection>
            <MetadataListItem label="Patient">
              <Link href={`/patients/${procedure.patientId}`}>{procedure.patientName}</Link>
            </MetadataListItem>
            <MetadataListItem label="Template">{procedure.templateName || '—'}</MetadataListItem>
            <MetadataListItem label="Due">
              <DueDateCell dueDate={procedure.dueDate ?? ''} isOverdue={procedure.isOverdue ?? false} />
            </MetadataListItem>
            <MetadataListItem label="Created">{formatDateTime(procedure.createdAt ?? '')}</MetadataListItem>
            <MetadataListItem label="Last updated">{formatDateTime(procedure.lastUpdated ?? '')}</MetadataListItem>
          </ViewSection>
          {procedure.notes?.trim() && (
            <ViewSection useGrid={false}>
              <MetadataListItem label="Procedure notes">
                <RichText>{procedure.notes}</RichText>
              </MetadataListItem>
            </ViewSection>
          )}
        </DetailPart>
        <DetailPart title="Appointments" isOpen={open.appointments} onToggle={() => toggle('appointments')}>
          {/* Booking sits with the appointments, not in the footer, which has no room for another button. */}
          <VStack gap={3}>
            {appointmentsList(procedure)}
            {!CLOSED_FOR_BOOKING.includes(procedure.status ?? '') && (
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
            assignments={procedure.assignments ?? []}
            canManage={canAssign(procedure)}
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
   * Every appointment for the procedure, each opening on the Calendar. Booked ones of a cancelled procedure are never
   * cancelled by the app (someone may need to tell the patient), so they're pointed out.
   */
  function appointmentsList(procedure: Procedure) {
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
        {procedure.status === 'Cancelled' && stillBooked > 0 && (
          <Text type="body">
            {`The procedure is cancelled, but ${stillBooked === 1 ? 'an appointment is' : `${stillBooked} appointments are`} still booked. Tell the patient, then cancel ${stillBooked === 1 ? 'it' : 'them'} on the calendar.`}
          </Text>
        )}
        <AppointmentList appointments={appointments} label="Appointments" />
      </VStack>
    );
  }

  function canAssign(procedure: Procedure) {
    return user.isAdmin && (procedure.canChangeAssignments ?? false);
  }

  /** Edit Details, View Patient, and for admins Assign Staff and the procedure's Audit Log. Book Appointment is under Appointments. */
  function footer(procedure: Procedure) {
    return (
      <>
        {procedure.canEditDetails && (
          <Button
            label="Edit Details"
            variant="secondary"
            icon={<Icon icon={AppIcon.Edit} />}
            href={`/procedures/${procedure.id}/edit`}
            isDisabled={isBusy}
          />
        )}
        {canAssign(procedure) && (
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
        {user.isAdmin && (
          <Button
            label="Audit Log"
            tooltip="View this procedure's audit log"
            variant="secondary"
            icon={<Icon icon={AppIcon.AuditLog} />}
            href={`/admin/audit?procedureId=${procedure.id}`}
          />
        )}
        <Button
          label="View Patient"
          variant="secondary"
          icon={<Icon icon={AppIcon.View} />}
          href={`/patients/${procedure.patientId}`}
        />
      </>
    );
  }

  /** The stage being worked on, with its notes box and actions. */
  function currentStageBody(procedure: Procedure) {
    return (
      <Section variant="transparent" padding={6}>
        <CurrentStage
          procedure={procedure}
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
   * Every stage, each opening when clicked. Finished stages can be unlocked (admins) and then edited here. The
   * current stage is worked on in the Current stage section while that's open; when it's folded, here.
   */
  function stageList(procedure: Procedure) {
    return (
      <StageList
        stages={procedure.stages ?? []}
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

  function detailsSection(procedure: Procedure): FoldingSection {
    return {key: 'details', title: 'Details', isOpen: open.details, onToggle: () => toggle('details'), children: details(procedure)};
  }

  function currentStageSection(procedure: Procedure): FoldingSection {
    return {
      key: 'currentStage',
      title: 'Current stage',
      isOpen: open.currentStage,
      onToggle: () => toggle('currentStage'),
      children: currentStageBody(procedure),
    };
  }

  /**
   * The procedure's own files (not its stages'). They can change unless it's cancelled or archived, as its details
   * can; admins can delete any.
   */
  function filesSection(procedure: Procedure): FoldingSection {
    return {
      key: 'files',
      title: 'Files',
      isOpen: open.files,
      onToggle: () => toggle('files'),
      children: (
        <Section variant="transparent" padding={6}>
          <Attachments
            target={{procedureId: procedure.id ?? ''}}
            canChange={procedure.canEditDetails ?? false}
            isLocked={Boolean(procedure.readOnlyReason)}
            state={attachments}
          />
        </Section>
      ),
    };
  }

  /**
   * Two cards side by side: Details, the Current stage and Files, each folding and sharing the left card, and all
   * the stages on the right.
   */
  function wideLayout(procedure: Procedure) {
    return (
      <TwoColumns
        start={
          <FoldingCard
            sections={[detailsSection(procedure), currentStageSection(procedure), filesSection(procedure)]}
            footerActions={footer(procedure)}
          />
        }
        end={
          <PanelCard title="Stages" isBodyPadded={false}>
            {stageList(procedure)}
          </PanelCard>
        }
      />
    );
  }

  /**
   * One card of four folding sections: Details, Current stage, Files and Stages, scrolling together; the page never
   * does. View Patient stays in the footer.
   */
  function narrowLayout(procedure: Procedure) {
    return (
      <FoldingCard
        footerActions={footer(procedure)}
        sections={[
          detailsSection(procedure),
          currentStageSection(procedure),
          filesSection(procedure),
          {key: 'stages', title: 'Stages', isOpen: open.stages, onToggle: () => toggle('stages'), children: stageList(procedure)},
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
 * finished. Says why when the procedure's status stops work on it.
 */
interface CurrentStageProps {
  procedure: Procedure;
  stage: ProcedureStage | undefined;
  work: ReactNode;
  /** The stage's files, under its notes. */
  files: ReactNode;
  instructions: InstructionsFolding;
  /** The stage's visit, after its title. */
  visit?: ReactNode;
}

function CurrentStage({procedure, stage, work, files, instructions, visit}: CurrentStageProps) {
  return (
    <VStack gap={4}>
      {stage ? (
        <VStack gap={3}>
          <StageTitle stage={stage} extra={visit} />
          {!procedure.canWorkOnStages && (
            <Text type="body" color="secondary">
              {`Stages can't be worked on while the procedure is ${statusLabel(procedure.status ?? '').toLowerCase()}.`}
            </Text>
          )}
          <StageDetails stage={stage} work={work} files={files} instructions={instructions} />
        </VStack>
      ) : (
        <Text type="body" color="secondary">
          Every stage is finished.
        </Text>
      )}
    </VStack>
  );
}
