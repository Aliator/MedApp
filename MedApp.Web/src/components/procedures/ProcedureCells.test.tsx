import {screen} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {DueDateCell, PriorityToken, ProcedureCell, StageProgressCell, StatusToken, statusLabel} from './ProcedureCells';

describe('ProcedureCells', () => {
  it('puts statuses into words', () => {
    expect(statusLabel('InReview')).toBe('In review');
    expect(statusLabel('Blocked')).toBe('Blocked');
  });

  it('shows the title with a link to the patient', async () => {
    renderWithApp(<ProcedureCell title="Colonoscopy" patientId="p-1" patientName="Test Patient" />);

    expect(await screen.findByText('Colonoscopy')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Test Patient'})).toHaveAttribute('href', '/patients/p-1');
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
