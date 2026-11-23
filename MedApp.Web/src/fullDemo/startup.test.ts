import {answer, fakeBackend, FULL_DEMO_VERSION, resetFullDemoForTests} from '../test/fullDemo';
import type {BackendProgress} from './backend';
import {rememberedUser} from './session';
import {isDownloaded, startFullDemo, startupState, subscribeStartup, whenReady} from './startup';

describe('the full demo’s start', () => {
  beforeEach(() => {
    resetFullDemoForTests();
    vi.stubGlobal('fetch', vi.fn(async () => new Response('', {status: 404})));
  });
  afterEach(() => resetFullDemoForTests());

  it('reports the download, then starting, then ready with the version it started on', async () => {
    vi.useFakeTimers();
    let report: (p: BackendProgress) => void = () => {};
    let finish: () => void = () => {};
    const backend = fakeBackend();
    backend.startBackend.mockImplementation(async ({onProgress}) => {
      report = onProgress as (p: BackendProgress) => void;
      await new Promise<void>((resolve) => (finish = resolve));
      return {version: FULL_DEMO_VERSION, source: 'downloaded', timings: {}};
    });
    const phases: string[] = [];
    subscribeStartup(() => phases.push(startupState().phase));

    void startFullDemo();
    await vi.waitFor(() => expect(backend.startBackend).toHaveBeenCalled());
    report({backend: {loaded: 10, total: 242}, database: {loaded: 0, total: 300}});
    await vi.advanceTimersByTimeAsync(150);
    expect(startupState()).toMatchObject({phase: 'downloading', progress: {backend: {loaded: 10}}});
    report({backend: {loaded: 242, total: 242}, database: {loaded: 300, total: 300}});
    await vi.advanceTimersByTimeAsync(150);
    expect(startupState().phase).toBe('starting');
    finish();
    await whenReady();
    expect(startupState()).toMatchObject({phase: 'ready', version: FULL_DEMO_VERSION});
    expect(phases).toContain('downloading');
  });

  it('signs in again as whoever was signed in before a reload', async () => {
    sessionStorage.setItem('medappFullDemo.user', 'r.aldous');
    const backend = fakeBackend(() => answer(200, {sessionId: 's'}));
    await startFullDemo();
    expect(backend.fetchApi).toHaveBeenCalledWith('POST', '/api/auth/login', {'Content-Type': 'application/json'}, JSON.stringify({username: 'r.aldous', password: 'full-demo'}));
    expect(rememberedUser()).toBe('r.aldous');
  });

  it('forgets them when signing in again is refused', async () => {
    sessionStorage.setItem('medappFullDemo.user', 'gone');
    fakeBackend(() => answer(401));
    await startFullDemo();
    expect(rememberedUser()).toBeNull();
  });

  it('says what went wrong when it can’t start', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    const backend = fakeBackend();
    backend.startBackend.mockRejectedValue(new Error('The demo database could not be downloaded (404).'));
    await startFullDemo();
    expect(startupState()).toMatchObject({phase: 'failed', error: 'The demo database could not be downloaded (404).'});
    await expect(whenReady()).rejects.toThrow('could not be downloaded');
  });

  it('knows when everything has downloaded', () => {
    expect(isDownloaded(null)).toBe(false);
    expect(isDownloaded({backend: {loaded: 242, total: 242}, database: {loaded: 0, total: 0}})).toBe(true);
    expect(isDownloaded({backend: {loaded: 242, total: 242}, database: {loaded: 1, total: 300}})).toBe(false);
  });
});
