import {Button} from '@astryxdesign/core/Button';
import {Collapsible} from '@astryxdesign/core/Collapsible';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {MetadataListItem} from '@astryxdesign/core/MetadataList';
import {Section} from '@astryxdesign/core/Section';
import {ProgressBar} from '@astryxdesign/core/ProgressBar';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {useMediaQuery} from '@astryxdesign/core/hooks';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useState, type ReactNode} from 'react';
import {ApiError, problemMessage} from '../../api/errors';
import {procedureQuery, useProcedureActions, type Procedure, type ProcedureStage} from '../../api/procedures';
import {userSearchQuery} from '../../api/search';
import {useCurrentUser} from '../../auth/auth';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {SelectionDialog} from '../../components/modals/SelectionDialog';
import {NARROW_WINDOW_QUERY, Page} from '../../components/pages/Page';
import {TwoColumns} from '../../components/pages/TwoColumns';
import {AssigneeList} from '../../components/procedures/AssigneeList';
import {DueDateCell, PriorityToken, StatusToken, statusLabel} from '../../components/procedures/ProcedureCells';
import {StageDetails, StageList, StageTitle, type InstructionsFolding} from '../../components/procedures/StageList';
import {StageWork} from '../../components/procedures/StageWork';
import {StatusChangeDialog, statusAction} from '../../components/procedures/StatusChangeDialog';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {FoldingCard, type FoldingSection} from '../../components/views/FoldingCard';
import {PanelCard} from '../../components/views/PanelCard';
import {ViewSection} from '../../components/views/ViewSection';
import {formatDateTime} from '../../utils/dates';

/** The dialog the page has open, if any. */
type Dialog =
  | {kind: 'status'; to: string}
  | {kind: 'finish'; stage: ProcedureStage; notes: string}
  | {kind: 'unlock'; stage: ProcedureStage}
  | {kind: 'assign'}
  | {kind: 'unassign'; userId: string; username: string}
  | null;

/** The wording of decision 18 in docs/procedures-ui-plan.md. */
export const NOT_ASSIGNED_MESSAGE = 'This procedure is not assigned to you, are you sure you want to make these changes?';

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
  const isNarrow = useMediaQuery(NARROW_WINDOW_QUERY, false);
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

  const [dialog, setDialog] = useState<Dialog>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  // The "not assigned to you" check: the action waiting on it, and whether it was answered this visit.
  const [waitingOnCheck, setWaitingOnCheck] = useState<(() => void) | null>(null);
  const [isCheckAnswered, setIsCheckAnswered] = useState(false);
  const [toAssign, setToAssign] = useState<string[]>([]);
  // What's open and folded is held here, so it carries over when the window switches between one card and two.
  // The defaults apply once, when the page loads: Details (with Patient info) and the Current stage open, Stages
  // folded, and in a narrow window Assigned staff folded too. Stages in the list open only when clicked.
  const [open, setOpen] = useState(() => ({
    details: true,
    currentStage: true,
    stages: false,
    patientInfo: true,
    assignedStaff: !isNarrow,
  }));
  const toggle = (part: keyof typeof open) => setOpen((current) => ({...current, [part]: !current[part]}));
  const [openStageIds, setOpenStageIds] = useState<string[]>([]);
  // Each stage's instructions start folded and open on their own (the same stage's in the Current stage section
  // and the list together).
  const [instructionsOpen, setInstructionsOpen] = useState<Record<string, boolean>>({});
  const instructions: InstructionsFolding = {
    isOpen: (stage) => instructionsOpen[stage.id ?? ''] ?? false,
    onOpenChange: (stage, isOpen) => setInstructionsOpen((current) => ({...current, [stage.id ?? '']: isOpen})),
  };

  const staff = useQuery({...userSearchQuery({PageSize: 100, Sort: 'username'}), enabled: user.isAdmin && dialog?.kind === 'assign'});

  const isMissing = error instanceof ApiError && error.status === 404;
  const currentStage = procedure?.stages?.find((s) => s.id === procedure.currentStageId);
  const isMine = procedure?.assignments?.some((a) => a.username === user.username) ?? false;
  const isBusy = Object.values(actions).some((action) => action.isPending);

  /** Runs a change to the procedure, first asking (once per visit) if it isn't assigned to you. */
  const checked = (action: () => void) => {
    setActionError(null);
    if (isMine || isCheckAnswered) action();
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
    checked(() => actions.saveNotes.mutate({stageId: stage.id ?? '', notes}, {onError: failed('The notes could not be saved.')}));
  const finishStage = (stage: ProcedureStage, notes: string) => checked(() => setDialog({kind: 'finish', stage, notes}));

  /** Saves notes typed but not saved, then finishes the stage. */
  const confirmFinish = async (stage: ProcedureStage, notes: string) => {
    try {
      if (notes !== (stage.notes ?? '')) await actions.saveNotes.mutateAsync({stageId: stage.id ?? '', notes});
      await actions.finishStage.mutateAsync(stage.id ?? '');
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
   * (`isNotesFilling`), the notes box fills the section's free space.
   */
  const workFor = (stage: ProcedureStage, isNotesFilling = false) => (
    <StageWork
      isNotesFilling={isNotesFilling}
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
      actions={procedure?.allowedStatusChanges?.map((to) => {
        const action = statusAction(to);
        return (
          <Button
            key={to}
            label={action.label}
            variant={action.variant}
            icon={<Icon icon={action.icon} />}
            onClick={() => checked(() => setDialog({kind: 'status', to}))}
            isDisabled={isBusy}
          />
        );
      })}
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
        </>
      }>
      {isMissing && <AlertMessage message="Procedure not found. It may have been deleted." />}
      {error && !isMissing && <AlertMessage message="The procedure could not be loaded. Please try again." />}
      {procedure && (isNarrow ? narrowLayout(procedure) : wideLayout(procedure))}
    </Page>
  );

  /** Status and progress, then two parts that fold: Patient info (the details and notes) and Assigned staff. */
  function details(procedure: Procedure) {
    return (
      <>
        <Section variant="transparent" padding={6}>
          <VStack gap={4}>
            {actionError && <AlertMessage message={actionError} />}
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
              <MetadataListItem label="Procedure notes">{procedure.notes}</MetadataListItem>
            </ViewSection>
          )}
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

  function canAssign(procedure: Procedure) {
    return user.isAdmin && (procedure.canChangeAssignments ?? false);
  }

  /** View Patient, and Assign Staff for admins. */
  function footer(procedure: Procedure) {
    return (
      <>
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
          instructions={instructions}
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
        instructions={instructions}
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
   * Two cards side by side: Details and the Current stage, each folding and sharing the left card, and all the
   * stages on the right.
   */
  function wideLayout(procedure: Procedure) {
    return (
      <TwoColumns
        start={<FoldingCard sections={[detailsSection(procedure), currentStageSection(procedure)]} footerActions={footer(procedure)} />}
        end={
          <PanelCard title="Stages" isBodyPadded={false}>
            {stageList(procedure)}
          </PanelCard>
        }
      />
    );
  }

  /**
   * One card of three folding sections: Details, Current stage and Stages. The open ones share the card equally,
   * and each scrolls on its own; the card and page never do. View Patient stays in the footer.
   */
  function narrowLayout(procedure: Procedure) {
    return (
      <FoldingCard
        footerActions={footer(procedure)}
        sections={[
          detailsSection(procedure),
          currentStageSection(procedure),
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
  instructions: InstructionsFolding;
}

function CurrentStage({procedure, stage, work, instructions}: CurrentStageProps) {
  return (
    <VStack gap={4}>
      {stage ? (
        <VStack gap={3}>
          <StageTitle stage={stage} />
          {!procedure.canWorkOnStages && (
            <Text type="body" color="secondary">
              {`Stages can't be worked on while the procedure is ${statusLabel(procedure.status ?? '').toLowerCase()}.`}
            </Text>
          )}
          <StageDetails stage={stage} work={work} instructions={instructions} />
        </VStack>
      ) : (
        <Text type="body" color="secondary">
          Every stage is finished.
        </Text>
      )}
    </VStack>
  );
}
