import {fireEvent, screen, waitFor} from '@testing-library/react';
import type {PathwayStage} from '../../api/pathways';
import {isDisabled, renderWithApp} from '../../test/render';
import {SaveShortcutProvider} from '../forms/SaveShortcut';
import {StageWork} from './StageWork';
import {richText, typeRichText} from '../../test/richText';

const stage = (overrides: Partial<PathwayStage> = {}): PathwayStage => ({
  id: 's1',
  stageDefinitionId: 'd1',
  stageOrder: 1,
  name: 'Consultation',
  description: '',
  instructions: '',
  notes: 'Seen in clinic.',
  state: 'InProgress',
  isCurrent: true,
  isLocked: false,
  canEdit: true,
  canUnlock: false,
  ...overrides,
});

function renderWork(overrides: Partial<PathwayStage> = {}, canUnlock = false) {
  const handlers = {onStart: vi.fn(), onSaveNotes: vi.fn(), onFinish: vi.fn(), onUnlock: vi.fn()};
  const result = renderWithApp(<StageWork stage={stage(overrides)} canUnlock={canUnlock} isBusy={false} {...handlers} />);
  return {...result, ...handlers};
}

describe('StageWork', () => {
  it('has a five-line notes box, or the size the page fitted it to', async () => {
    // The editor sets its writing area's least height through this inline variable.
    const minHeight = (box: HTMLElement) => box.style.getPropertyValue('--x-minHeight');
    const {unmount} = renderWork();
    expect(minHeight(await screen.findByRole('textbox', {name: 'Notes'}))).toBe('7.5rem');
    unmount();

    const handlers = {onStart: vi.fn(), onSaveNotes: vi.fn(), onFinish: vi.fn(), onUnlock: vi.fn()};
    const onFitNotes = vi.fn();
    renderWithApp(<StageWork stage={stage()} canUnlock={false} isBusy={false} notesHeight={240} onFitNotes={onFitNotes} {...handlers} />);
    expect(minHeight(await screen.findByRole('textbox', {name: 'Notes'}))).toBe('240px');
    // Already fitted: it doesn't measure again.
    await new Promise((resolve) => requestAnimationFrame(resolve));
    expect(onFitNotes).not.toHaveBeenCalled();
  });

  it('edits the notes, saving only once they change', async () => {
    const {user, onSaveNotes} = renderWork();

    const box = await screen.findByRole('textbox', {name: 'Notes'});
    expect(richText(box)).toBe('Seen in clinic.');
    expect(isDisabled(screen.getByRole('button', {name: 'Save Notes'}))).toBe(true);

    await typeRichText(box, ' Plan agreed.');
    expect(screen.getByText('Unsaved changes')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Save Notes'}));

    expect(onSaveNotes).toHaveBeenCalledWith('Seen in clinic. Plan agreed.');
  });

  it('saves changed notes on Ctrl+S in the box', async () => {
    const handlers = {onStart: vi.fn(), onSaveNotes: vi.fn(), onFinish: vi.fn(), onUnlock: vi.fn()};
    renderWithApp(
      <SaveShortcutProvider>
        <StageWork stage={stage()} canUnlock={false} isBusy={false} {...handlers} />
      </SaveShortcutProvider>,
    );
    const box = await screen.findByRole('textbox', {name: 'Notes'});

    // Nothing to save yet.
    fireEvent.keyDown(box, {key: 's', ctrlKey: true});
    expect(handlers.onSaveNotes).not.toHaveBeenCalled();

    await typeRichText(box, ' Plan agreed.');
    expect(fireEvent.keyDown(box, {key: 's', ctrlKey: true})).toBe(false);
    expect(handlers.onSaveNotes).toHaveBeenCalledWith('Seen in clinic. Plan agreed.');
  });

  it('saves nothing on Ctrl+S while a save is under way', async () => {
    const handlers = {onStart: vi.fn(), onSaveNotes: vi.fn(), onFinish: vi.fn(), onUnlock: vi.fn()};
    renderWithApp(
      <SaveShortcutProvider>
        <StageWork stage={stage()} canUnlock={false} isBusy {...handlers} />
      </SaveShortcutProvider>,
    );

    fireEvent.keyDown(await screen.findByRole('textbox', {name: 'Notes'}), {key: 's', ctrlKey: true});

    expect(handlers.onSaveNotes).not.toHaveBeenCalled();
  });

  it('finishes the stage with the notes as typed', async () => {
    const {user, onFinish} = renderWork();

    await typeRichText(await screen.findByRole('textbox', {name: 'Notes'}), ' Done.');
    await user.click(screen.getByRole('button', {name: 'Finish Stage'}));

    expect(onFinish).toHaveBeenCalledWith('Seen in clinic. Done.');
  });

  it('offers to start a stage nobody has started yet', async () => {
    const {user, onStart} = renderWork({state: 'NotStarted', notes: ''});

    await user.click(await screen.findByRole('button', {name: 'Start Stage'}));

    expect(onStart).toHaveBeenCalled();
  });

  it('shows the notes read-only when the stage cannot be edited', async () => {
    renderWork({canEdit: false, state: 'Completed', isLocked: true});

    expect(await screen.findByText('Seen in clinic.')).toBeInTheDocument();
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Unlock Stage'})).not.toBeInTheDocument();
  });

  it('lets a holder of UnlockStages unlock a finished, locked stage', async () => {
    const {user, onUnlock} = renderWork({canEdit: false, canUnlock: true, state: 'Completed', isLocked: true}, true);

    await user.click(await screen.findByRole('button', {name: 'Unlock Stage'}));

    expect(onUnlock).toHaveBeenCalled();
  });
});

describe('StageWork fitting its notes box', () => {
  const handlers = {onStart: vi.fn(), onSaveNotes: vi.fn(), onFinish: vi.fn(), onUnlock: vi.fn()};

  /**
   * jsdom has no layout: the area that scrolls round the stage is `space` tall, the stage's work in it `used` tall,
   * and the notes box's writing area 120 tall.
   */
  function stubLayout(space: number, used: number) {
    vi.spyOn(HTMLElement.prototype, 'offsetHeight', 'get').mockImplementation(function (this: HTMLElement) {
      if (this.hasAttribute('contenteditable')) return 120;
      return this.parentElement?.dataset.testid === 'scroller' ? used : 0;
    });
    vi.spyOn(HTMLElement.prototype, 'clientHeight', 'get').mockImplementation(function (this: HTMLElement) {
      return this.dataset.testid === 'scroller' ? space : 0;
    });
  }

  const renderInScroller = (onFitNotes: (height: number) => void) =>
    renderWithApp(
      <div data-testid="scroller" style={{overflowY: 'auto', padding: 0}}>
        <StageWork stage={stage()} canUnlock={false} isBusy={false} onFitNotes={onFitNotes} {...handlers} />
      </div>,
    );

  it('grows the box to fill the free space around it, exactly', async () => {
    stubLayout(600, 200);
    const onFitNotes = vi.fn();
    renderInScroller(onFitNotes);

    await waitFor(() => expect(onFitNotes).toHaveBeenCalledWith(120 + (600 - 200)));
    expect(onFitNotes).toHaveBeenCalledOnce();
  });

  it('keeps its five lines when there is no room to grow', async () => {
    stubLayout(150, 400);
    const onFitNotes = vi.fn();
    renderInScroller(onFitNotes);

    await waitFor(() => expect(onFitNotes).toHaveBeenCalledWith(120));
  });

  it('keeps its size when nothing is laid out to measure, or nothing round it scrolls', async () => {
    const onFitNotes = vi.fn();
    renderInScroller(onFitNotes);
    renderWithApp(<StageWork stage={stage()} canUnlock={false} isBusy={false} onFitNotes={onFitNotes} {...handlers} />);

    await waitFor(() => expect(screen.getAllByRole('textbox', {name: 'Notes'})).toHaveLength(2));
    // It tries for a few frames, then gives up.
    await new Promise((resolve) => setTimeout(resolve, 400));
    expect(onFitNotes).not.toHaveBeenCalled();
  });

  it('shows its visit above the notes, and says why it cannot be finished yet', async () => {
    const handlers = {onStart: vi.fn(), onSaveNotes: vi.fn(), onFinish: vi.fn(), onUnlock: vi.fn()};
    const {user} = renderWithApp(
      <StageWork
        stage={stage()}
        canUnlock={false}
        isBusy={false}
        visit={<p>The visit</p>}
        finishDisabledReason="Needs an attended visit first"
        {...handlers}
      />,
    );

    expect(await screen.findByText('The visit')).toBeInTheDocument();
    expect(screen.getAllByText('Needs an attended visit first').length).toBeGreaterThan(0);
    const finish = screen.getByRole('button', {name: 'Finish Stage'});
    expect(isDisabled(finish)).toBe(true);
    await user.click(finish);
    expect(handlers.onFinish).not.toHaveBeenCalled();
  });
});

describe('StageWork: skipping', () => {
  function renderSkippable(overrides: Partial<PathwayStage>) {
    const handlers = {onStart: vi.fn(), onSaveNotes: vi.fn(), onFinish: vi.fn(), onUnlock: vi.fn(), onSkip: vi.fn(), onReinstate: vi.fn()};
    const result = renderWithApp(<StageWork stage={stage(overrides)} canUnlock={false} isBusy={false} {...handlers} />);
    return {...result, ...handlers};
  }

  it('offers Skip Stage on an optional current stage, only when the API says it can be skipped', async () => {
    const {user, onSkip} = renderSkippable({isOptional: true, canSkip: true});

    await user.click(await screen.findByRole('button', {name: 'Skip Stage'}));
    expect(onSkip).toHaveBeenCalled();
  });

  it('offers no Skip Stage when the stage cannot be skipped', async () => {
    renderSkippable({isOptional: true, canSkip: false});

    await screen.findByRole('button', {name: 'Finish Stage'});
    expect(screen.queryByRole('button', {name: 'Skip Stage'})).not.toBeInTheDocument();
  });

  it('offers Reinstate Stage on a skipped stage, as the API allows', async () => {
    const {user, onReinstate, unmount} = renderSkippable({state: 'Skipped', isCurrent: false, canEdit: false, canReinstate: true});

    await user.click(await screen.findByRole('button', {name: 'Reinstate Stage'}));
    expect(onReinstate).toHaveBeenCalled();
    unmount();

    renderSkippable({state: 'Skipped', isCurrent: false, canEdit: false, canReinstate: false});
    await screen.findByText('Notes');
    expect(screen.queryByRole('button', {name: 'Reinstate Stage'})).not.toBeInTheDocument();
  });
});
