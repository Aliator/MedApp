import {screen} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {
  AssigneesCell,
  DueDateCell,
  PriorityToken,
  PathwayActions,
  PathwayCell,
  StageProgressCell,
  StatusToken,
  statusLabel,
} from './PathwayCells';

describe('PathwayCells', () => {
  it('puts statuses into words', () => {
    expect(statusLabel('InReview')).toBe('Awaiting review');
    expect(statusLabel('Blocked')).toBe('On hold');
  });

  it('links the title to the pathway', async () => {
    renderWithApp(<PathwayCell id="proc-1" title="Colonoscopy" />);

    expect(await screen.findByRole('link', {name: 'Colonoscopy'})).toHaveAttribute('href', '/pathways/proc-1');
  });

  it('offers View pathway, and View patient when given the patient', async () => {
    const {unmount} = renderWithApp(<PathwayActions id="proc-1" patientId="p-1" />);

    expect(await screen.findByRole('link', {name: 'View pathway'})).toHaveAttribute('href', '/pathways/proc-1');
    expect(screen.getByRole('link', {name: 'View patient'})).toHaveAttribute('href', '/patients/p-1');
    unmount();

    renderWithApp(<PathwayActions id="proc-1" />);
    await screen.findByRole('link', {name: 'View pathway'});
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
    expect(screen.queryByText('Awaiting review')).not.toBeInTheDocument();
  });

  it('says when a pathway is on hold or awaiting review, for a list without a status column', async () => {
    renderWithApp(
      <>
        <StageProgressCell completed={2} total={7} currentStageName="Consultation" status="Blocked" />
        <StageProgressCell completed={4} total={4} currentStageName={null} status="InReview" />
        <StageProgressCell completed={1} total={7} currentStageName="Consent" status="InProgress" />
      </>,
    );

    expect(await screen.findByText('On hold')).toBeInTheDocument();
    expect(screen.getByText('Awaiting review')).toBeInTheDocument();
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

  it('shows statuses by colour and name alone by default', async () => {
    renderWithApp(<StatusToken status="Blocked" />);
    await screen.findByText('On hold');
    expect(document.querySelector('.astryx-token svg')).toBeNull();
  });

  it('adds icons to statuses, priorities and Overdue when "Icons on statuses" is on', async () => {
    localStorage.setItem('medapp.accessibility', JSON.stringify({statusIcons: true}));
    renderWithApp(
      <>
        <StatusToken status="Blocked" />
        <PriorityToken priority="Urgent" />
        <DueDateCell dueDate="2026-01-01" isOverdue />
      </>,
    );
    await screen.findByText('Overdue');
    expect(document.querySelectorAll('.astryx-token svg')).toHaveLength(3);
  });

  it("shows a status or priority it doesn't know in words, without an icon", async () => {
    localStorage.setItem('medapp.accessibility', JSON.stringify({statusIcons: true}));
    renderWithApp(
      <>
        <StatusToken status="OnHold" />
        <PriorityToken priority="Routine" />
      </>,
    );

    expect(await screen.findByText('On hold')).toBeInTheDocument();
    expect(screen.getByText('Routine')).toBeInTheDocument();
    expect(document.querySelectorAll('.astryx-token svg')).toHaveLength(0);
  });
});
