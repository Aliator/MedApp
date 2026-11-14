import {screen} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {
  AssigneesCell,
  DueDateCell,
  PriorityToken,
  ProcedureActions,
  ProcedureCell,
  StageProgressCell,
  StatusToken,
  statusLabel,
} from './ProcedureCells';

describe('ProcedureCells', () => {
  it('puts statuses into words', () => {
    expect(statusLabel('InReview')).toBe('In review');
    expect(statusLabel('Blocked')).toBe('Blocked');
  });

  it('links the title to the procedure', async () => {
    renderWithApp(<ProcedureCell id="proc-1" title="Colonoscopy" />);

    expect(await screen.findByRole('link', {name: 'Colonoscopy'})).toHaveAttribute('href', '/procedures/proc-1');
  });

  it('offers View procedure, and View patient when given the patient', async () => {
    const {unmount} = renderWithApp(<ProcedureActions id="proc-1" patientId="p-1" />);

    expect(await screen.findByRole('link', {name: 'View procedure'})).toHaveAttribute('href', '/procedures/proc-1');
    expect(screen.getByRole('link', {name: 'View patient'})).toHaveAttribute('href', '/patients/p-1');
    unmount();

    renderWithApp(<ProcedureActions id="proc-1" />);
    await screen.findByRole('link', {name: 'View procedure'});
    expect(screen.queryByRole('link', {name: 'View patient'})).not.toBeInTheDocument();
  });

  it('lists the assignees, or says nobody is assigned', async () => {
    renderWithApp(
      <>
        <AssigneesCell assignees={['a.user', 'b.user']} />
        <AssigneesCell assignees={[]} />
      </>,
    );

    expect(await screen.findByText('a.user, b.user')).toBeInTheDocument();
    expect(screen.getByText('Nobody')).toBeInTheDocument();
  });

  it('flags an overdue date', async () => {
    renderWithApp(<DueDateCell dueDate="2026-01-05" isOverdue />);

    expect(await screen.findByText('5 Jan 2026')).toBeInTheDocument();
    expect(screen.getByText('Overdue')).toBeInTheDocument();
  });

  it('does not flag a date that is not overdue', async () => {
    renderWithApp(<DueDateCell dueDate="2026-01-05" isOverdue={false} />);

    expect(await screen.findByText('5 Jan 2026')).toBeInTheDocument();
    expect(screen.queryByText('Overdue')).not.toBeInTheDocument();
  });

  it('shows the current stage and progress', async () => {
    renderWithApp(<StageProgressCell completed={2} total={7} currentStageName="Consultation" />);

    expect(await screen.findByText('Consultation')).toBeInTheDocument();
    expect(screen.getByText('2 of 7 stages done')).toBeInTheDocument();
  });

  it('says Completed when every stage is finished, on one line', async () => {
    renderWithApp(<StageProgressCell completed={4} total={4} currentStageName={null} />);

    expect(await screen.findByText('Completed')).toBeInTheDocument();
    expect(screen.queryByText('In review')).not.toBeInTheDocument();
  });

  it('says when a procedure is blocked or in review, for a list without a status column', async () => {
    renderWithApp(
      <>
        <StageProgressCell completed={2} total={7} currentStageName="Consultation" status="Blocked" />
        <StageProgressCell completed={4} total={4} currentStageName={null} status="InReview" />
        <StageProgressCell completed={1} total={7} currentStageName="Consent" status="InProgress" />
      </>,
    );

    expect(await screen.findByText('Blocked')).toBeInTheDocument();
    expect(screen.getByText('In review')).toBeInTheDocument();
    expect(screen.queryByText('In progress')).not.toBeInTheDocument();
  });

  it('labels priority and status tokens', async () => {
    renderWithApp(
      <>
        <PriorityToken priority="Urgent" />
        <StatusToken status="InProgress" />
      </>,
    );

    expect(await screen.findByText('Urgent')).toBeInTheDocument();
    expect(screen.getByText('In progress')).toBeInTheDocument();
  });
});
