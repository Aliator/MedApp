import {act, screen} from '@testing-library/react';
import {fullDemoReady, resetFullDemoForTests} from '../test/fullDemo';
import {showStartScreen} from './startScreen';
import {setStartupStateForTests} from './startup';

describe('the full demo’s start screen', () => {
  beforeEach(() => resetFullDemoForTests());
  afterEach(() => resetFullDemoForTests());

  it('shows the progress until the backend is ready, then makes way for the app', async () => {
    const root = document.createElement('div');
    document.body.append(root);
    let done = false;
    let shown: Promise<void> = Promise.resolve();
    await act(async () => {
      shown = showStartScreen(root).then(() => void (done = true));
    });
    expect(await screen.findByText(/it’s starting again, and signing you back in/)).toBeInTheDocument();
    expect(screen.getByRole('progressbar', {name: 'Getting MedApp ready…'})).toBeInTheDocument();
    expect(done).toBe(false);

    await act(async () => {
      fullDemoReady();
      await shown;
    });
    expect(done).toBe(true);
    expect(root.childElementCount).toBe(0);
    root.remove();
  });

  it('stays, saying so, when the start fails', async () => {
    const root = document.createElement('div');
    document.body.append(root);
    let done = false;
    await act(async () => {
      void showStartScreen(root).then(() => void (done = true));
    });
    await act(async () => setStartupStateForTests({phase: 'failed', progress: null, version: null, size: null, error: 'x'}));
    expect(await screen.findByText('MedApp couldn’t be downloaded')).toBeInTheDocument();
    expect(done).toBe(false);
    root.remove();
  });
});
