import {screen} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {AssigneesCell, DueDateCell, PriorityToken, ProcedureCell, StageProgressCell, StatusToken, statusLabel} from './ProcedureCells';

describe('ProcedureCells', () => {
  it('puts statuses into words', () => {
    expect(statusLabel('InReview')).toBe('In review');
    expect(statusLabel('Blocked')).toBe('Blocked');
  });

  it('links the title to the procedure and the patient to theirs', async () => {
    renderWithApp(<ProcedureCell id="proc-1" title="Colonoscopy" patientId="p-1" patientName="Test Patient" />);

    expect(await screen.findByRole('link', {name: 'Colonoscopy'})).toHaveAttribute('href', '/procedures/proc-1');
    expect(screen.getByRole('link', {name: 'Test Patient'})).toHaveAttribute('href', '/patients/p-1');
  });

  it('shows secondary text instead of the patient when given no patient', async () => {
    renderWithApp(<ProcedureCell id="proc-1" title="Colonoscopy" secondary="Diagnostic procedure" />);

    expect(await screen.findByText('Diagnostic procedure')).toBeInTheDocument();
    expect(screen.getAllByRole('link')).toHaveLength(1);
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

  it('shows the current stage and progress, and says when a procedure is blocked', async () => {
    renderWithApp(<StageProgressCell completed={2} total={7} currentStageName="Consultation" status="Blocked" />);

    expect(await screen.findByText('Consultation')).toBeInTheDocument();
    expect(screen.getByText('2 of 7 stages done')).toBeInTheDocument();
    expect(screen.getByText('Blocked')).toBeInTheDocument();
  });

  it('says when every stage is finished', async () => {
    renderWithApp(<StageProgressCell completed={4} total={4} currentStageName={null} status="InReview" />);

    expect(await screen.findByText('All stages finished')).toBeInTheDocument();
    expect(screen.getByText('In review')).toBeInTheDocument();
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
