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
/** Every stage's header button (not the Instructions buttons inside the stages). */
const stageHeaders = () => screen.getAllByRole('button', {name: /^\d+\./});

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
    expect(stageHeaders().map((b) => b.textContent)).toEqual([
      expect.stringMatching(/^1\. Consultation.*Finished/),
      expect.stringMatching(/^2\. Procedure.*In progress.*Current stage/),
      expect.stringMatching(/^3\. Consultation.*Not started/),
    ]);
  });

  it('opens only the current stage', async () => {
    renderWithApp(<StageList stages={stages} />);

    expect(await screen.findByRole('button', header('2. Procedure'))).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('button', header('1. Consultation'))).toHaveAttribute('aria-expanded', 'false');
    expect(screen.getByRole('button', header('3. Consultation'))).toHaveAttribute('aria-expanded', 'false');
    expect(screen.getByText('Started by a.user, 4 Nov 2026, 09:00')).toBeVisible();
  });

  it('keeps the instructions folded under their heading until it is clicked', async () => {
    const {user} = renderWithApp(<StageList stages={stages} />);

    // The current stage's: the second stage, and the one open.
    const toggle = (await screen.findAllByRole('button', {name: 'Instructions'}))[1];
    expect(toggle).toHaveAttribute('aria-expanded', 'false');

    await user.click(toggle);

    expect(toggle).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByText('Complete the safety checklist.')).toBeVisible();
  });

  it('opens a closed stage to show who did what, and its notes', async () => {
    const {user} = renderWithApp(<StageList stages={stages} />);

    const finished = await screen.findByRole('button', header('1. Consultation'));
    await user.click(finished);

    expect(finished).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByText('Started by a.user, 3 Nov 2026, 13:05')).toBeVisible();
    expect(screen.getByText('Finished by b.user, 3 Nov 2026, 14:30')).toBeVisible();
    // Formatted (RichText), its two lines kept apart as typed.
    const notes = screen.getByText(/Patient keen to go ahead\./);
    expect(notes).toBeVisible();
    expect(notes).toHaveTextContent('Patient keen to go ahead.Leaflet given.');
    expect(notes.querySelector('br')).not.toBeNull();
  });

  it('starts with every stage closed when the current one is shown elsewhere', async () => {
    renderWithApp(<StageList stages={stages} isCurrentOpen={false} />);

    await screen.findAllByRole('button');
    expect(stageHeaders().map((b) => b.getAttribute('aria-expanded'))).toEqual(['false', 'false', 'false']);
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

  it("shows a stage's SNOMED CT concept, copied from its definition", async () => {
    // A real SNOMED CT concept ID, used only as a code: 80146002 is Appendectomy.
    renderWithApp(<StageList stages={[stage({isCurrent: true, snomedCode: '80146002', snomedTerm: 'Appendectomy'})]} />);

    expect(await screen.findByText('SNOMED CT: 80146002 | Appendectomy')).toBeVisible();
  });

  it('says when a stage has no notes', async () => {
    renderWithApp(<StageList stages={[stage({isCurrent: true})]} />);

    expect(await screen.findByText('No notes yet.')).toBeVisible();
  });
});

describe('StageList, held by the page', () => {
  const held = [
    stage({id: 's1', stageOrder: 1, name: 'Consultation', isCurrent: true}),
    stage({id: 's2', stageOrder: 2, name: 'Procedure', instructions: 'Complete the safety checklist.'}),
  ];

  it('opens the stages the page says, and tells it when one is opened or closed', async () => {
    const onStageOpenChange = vi.fn();
    const {user} = renderWithApp(<StageList stages={held} openStageIds={['s2']} onStageOpenChange={onStageOpenChange} />);

    // The page's choice, not the current stage.
    const procedure = await screen.findByRole('button', header('2. Procedure'));
    expect(procedure).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('button', header('1. Consultation'))).toHaveAttribute('aria-expanded', 'false');

    await user.click(screen.getByRole('button', header('1. Consultation')));
    expect(onStageOpenChange).toHaveBeenLastCalledWith('s1', true);
    await user.click(procedure);
    expect(onStageOpenChange).toHaveBeenLastCalledWith('s2', false);
  });

  it("holds each stage's instructions open or folded as the page says", async () => {
    const instructions = {isOpen: vi.fn(() => true), onOpenChange: vi.fn()};
    const {user} = renderWithApp(<StageList stages={held} openStageIds={['s2']} instructions={instructions} />);

    // The second stage's (the open one).
    const toggle = (await screen.findAllByRole('button', {name: 'Instructions'}))[1];
    expect(toggle).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByText('Complete the safety checklist.')).toBeVisible();

    await user.click(toggle);
    expect(instructions.onOpenChange).toHaveBeenCalledWith(held[1], false);
  });

  it("adds the stage's visit to its title", async () => {
    renderWithApp(<StageList stages={held} renderVisit={(s) => <span>Visit for {s.name}</span>} />);

    expect(await screen.findByRole('button', {name: /^1\. Consultation.*Visit for Consultation/})).toBeInTheDocument();
  });

  it("shows the stage's own work in place of its notes", async () => {
    renderWithApp(<StageList stages={held} renderWork={(s) => <p>Work for {s.name}</p>} />);

    expect(await screen.findByText('Work for Consultation')).toBeVisible();
    expect(screen.queryByText('No notes yet.')).not.toBeInTheDocument();
  });

  it('says who started or finished a stage, when the API no longer knows them', async () => {
    renderWithApp(
      <StageList
        stages={[
          stage({
            isCurrent: true,
            state: 'Completed',
            isLocked: true,
            startedAtUtc: '2026-11-03T13:05:00Z',
            completedAtUtc: '2026-11-03T14:30:00Z',
          }),
        ]}
      />,
    );

    expect(await screen.findByText('Started by unknown, 3 Nov 2026, 13:05')).toBeVisible();
    expect(screen.getByText('Finished by unknown, 3 Nov 2026, 14:30')).toBeVisible();
  });
});
