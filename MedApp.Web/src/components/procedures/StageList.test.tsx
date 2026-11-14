import {screen} from '@testing-library/react';
import type {ProcedureStage} from '../../api/procedures';
import {renderWithApp} from '../../test/render';
import {StageList} from './StageList';

const stage = (overrides: Partial<ProcedureStage>): ProcedureStage => ({
  id: 'stage-1',
  stageDefinitionId: 'def-1',
  stageOrder: 1,
  name: 'Consultation',
  description: 'The patient has been seen.',
  instructions: 'Review the history.',
  notes: '',
  state: 'NotStarted',
  isCurrent: false,
  isLocked: false,
  canEdit: false,
  canUnlock: false,
  ...overrides,
});

/** A stage's header button, by the start of its text ("1. Consultation"). */
const header = (text: string) => ({name: new RegExp(`^${text.replace('.', '\\.')}`)});

describe('StageList', () => {
  const stages = [
    stage({id: 's3', stageOrder: 3, name: 'Consultation'}),
    stage({
      id: 's1',
      stageOrder: 1,
      state: 'Completed',
      isLocked: true,
      notes: 'Patient keen to go ahead.\nLeaflet given.',
      startedAtUtc: '2026-11-03T13:05:00Z',
      startedByUsername: 'a.user',
      completedAtUtc: '2026-11-03T14:30:00Z',
      completedByUsername: 'b.user',
    }),
    stage({
      id: 's2',
      stageOrder: 2,
      name: 'Procedure',
      instructions: 'Complete the safety checklist.',
      state: 'InProgress',
      isCurrent: true,
      startedAtUtc: '2026-11-04T09:00:00Z',
      startedByUsername: 'a.user',
    }),
  ];

  it('lists the stages in order, with their state', async () => {
    renderWithApp(<StageList stages={stages} />);

    await screen.findAllByRole('button');
    expect(screen.getAllByRole('button').map((b) => b.textContent)).toEqual([
      expect.stringMatching(/^1\. Consultation.*Finished/),
      expect.stringMatching(/^2\. Procedure.*In progress.*Current stage/),
      expect.stringMatching(/^3\. Consultation.*Not started/),
    ]);
  });

  it('opens only the current stage, with its instructions showing', async () => {
    renderWithApp(<StageList stages={stages} />);

    expect(await screen.findByRole('button', header('2. Procedure'))).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('button', header('1. Consultation'))).toHaveAttribute('aria-expanded', 'false');
    expect(screen.getByRole('button', header('3. Consultation'))).toHaveAttribute('aria-expanded', 'false');
    expect(screen.getByText('Complete the safety checklist.')).toBeVisible();
    expect(screen.getByText('Started by a.user, 4 Nov 2026, 09:00')).toBeVisible();
  });

  it('opens a closed stage to show who did what, and its notes', async () => {
    const {user} = renderWithApp(<StageList stages={stages} />);

    const finished = await screen.findByRole('button', header('1. Consultation'));
    await user.click(finished);

    expect(finished).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByText('Started by a.user, 3 Nov 2026, 13:05')).toBeVisible();
    expect(screen.getByText('Finished by b.user, 3 Nov 2026, 14:30')).toBeVisible();
    expect(screen.getByText('Patient keen to go ahead.')).toBeVisible();
    expect(screen.getByText('Leaflet given.')).toBeVisible();
  });

  it('starts with every stage closed when the current one is shown elsewhere', async () => {
    renderWithApp(<StageList stages={stages} isCurrentOpen={false} />);

    await screen.findAllByRole('button');
    expect(screen.getAllByRole('button').map((b) => b.getAttribute('aria-expanded'))).toEqual(['false', 'false', 'false']);
  });

  it('closes the current stage when its header is clicked', async () => {
    const {user} = renderWithApp(<StageList stages={stages} />);

    const current = await screen.findByRole('button', header('2. Procedure'));
    await user.click(current);

    expect(current).toHaveAttribute('aria-expanded', 'false');
  });

  it('marks a finished stage an admin has unlocked', async () => {
    renderWithApp(<StageList stages={[stage({state: 'Completed', isLocked: false})]} />);

    expect(await screen.findByText('Unlocked for editing')).toBeInTheDocument();
  });

  it('says when a stage has no notes', async () => {
    renderWithApp(<StageList stages={[stage({isCurrent: true})]} />);

    expect(await screen.findByText('No notes yet.')).toBeVisible();
  });
});
