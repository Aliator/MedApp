import {useQuery} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {useEffect, useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {useCurrentUser} from '../../auth/auth';
import {ApiError} from '../../api/errors';
import {procedureQuery, useUpdateProcedure, type Procedure, type ProcedureDetailsInput} from '../../api/procedures';
import {FormCard} from '../../components/forms/FormCard';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {PatientBanner} from '../../components/patients/PatientBanner';
import {Page} from '../../components/pages/Page';
import {statusLabel} from '../../components/procedures/ProcedureCells';
import {ProcedureSummary} from '../../components/procedures/ProcedureSummary';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../saveProblem';
import {PROCEDURE_DETAILS_FIELDS, ProcedureDetailsFields} from './ProcedureDetailsFields';
import {NOT_ASSIGNED_MESSAGE} from './ProcedurePage';

const toInput = (p: Procedure): ProcedureDetailsInput => ({
  title: p.title ?? '',
  notes: p.notes ?? '',
  dueDate: p.dueDate ?? '',
  priority: p.priority ?? 'Medium',
});

const pageProps = {
  title: 'Edit Procedure',
  backLabel: 'Back to procedure',
} as const;

/**
 * Edit a procedure's details: title, due date, priority and notes (not its stages, which come from its template).
 * This part loads the procedure; an unknown one goes back to the list. The form mounts once it's here, so it
 * starts from the procedure's details.
 */
export function EditProcedurePage({id}: {id: string}) {
  const navigate = useNavigate();
  const {data: procedure, error, isPending} = useQuery(procedureQuery(id));
  const isMissing = error instanceof ApiError && error.status === 404;

  useEffect(() => {
    if (isMissing) void navigate({to: '/procedures', replace: true});
  }, [isMissing, navigate]);

  if (procedure) return <EditProcedureForm id={id} procedure={procedure} />;

  return (
    <Page {...pageProps} backHref={`/procedures/${id}`} isLoading={isPending || isMissing} loadingMessage="Loading procedure..."
      message={error && !isMissing && <AlertMessage message="The procedure could not be loaded. Please try again." />}
    />
  );
}

function EditProcedureForm({id, procedure}: {id: string; procedure: Procedure}) {
  const navigate = useNavigate();
  const user = useCurrentUser();
  const update = useUpdateProcedure(id);
  const announce = useAnnounce();
  const [form, setForm] = useState<ProcedureDetailsInput>(() => toInput(procedure));
  const [isConfirming, setIsConfirming] = useState(false);
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);
  const backToProcedure = () => void navigate({to: '/procedures/$id', params: {id}});
  // Decision 18: saving changes to a procedure that isn't yours asks first. The save confirmation asks it.
  // Admins aren't warned: looking after any procedure is their job.
  const isMine = user.isAdmin || (procedure.assignments?.some((a) => a.username === user.username) ?? false);

  const save = () =>
    update.mutate(form, {
      onSuccess: () => {
        announce('Procedure saved');
        backToProcedure();
      },
      onError: (e) => {
        setProblem(saveProblem(e, 'save', 'procedure'));
        setIsConfirming(false);
      },
    });

  const banner = procedure.patient && <PatientBanner patient={procedure.patient} />;

  if (!procedure.canEditDetails) {
    return (
      <Page {...pageProps} onBack={backToProcedure} banner={banner}>
        <AlertMessage
          message={`This procedure is ${statusLabel(procedure.status ?? '').toLowerCase()}, so its details can't be edited.`}
        />
      </Page>
    );
  }

  return (
    <Page
      {...pageProps}
      subtitle={procedure.patientName ? `For ${procedure.patientName}` : undefined}
      onBack={backToProcedure}
      banner={banner}
      message={
        problem.validation ? (
          <ValidationAlert errors={problem.validation} fields={PROCEDURE_DETAILS_FIELDS} />
        ) : (
          problem.message && <AlertMessage message={problem.message} />
        )
      }
      dialogs={
        <ConfirmDialog
          isOpen={isConfirming}
          title={isMine ? 'Confirm Changes' : 'Not Assigned to You'}
          message={isMine ? 'Are you sure you want to save these changes?' : NOT_ASSIGNED_MESSAGE}
          intent={isMine ? undefined : 'warning'}
          icon={AppIcon.Save}
          confirmText="Confirm Save"
          isProcessing={update.isPending}
          onConfirm={save}
          onCancel={() => setIsConfirming(false)}>
          <ProcedureSummary title={form.title} dueDate={form.dueDate} priority={form.priority} />
        </ConfirmDialog>
      }>
      <FormCard
        onSave={() => {
          setProblem(NO_PROBLEM);
          setIsConfirming(true);
        }}
        onCancel={backToProcedure}
        isProcessing={update.isPending}>
        <ProcedureDetailsFields value={form} onChange={setForm} errors={problem.validation} isDisabled={update.isPending} />
      </FormCard>
    </Page>
  );
}
