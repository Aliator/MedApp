import {answer, fakeBackend, resetFullDemoForTests} from '../test/fullDemo';
import {fullDemoNavigation, noteAnswer, refreshFullDemo, rememberedUser, resetFullDemo, switchAccount} from './session';

describe('the full demo’s session', () => {
  beforeEach(() => resetFullDemoForTests());
  afterEach(() => resetFullDemoForTests());

  it('notes sign-ins and sign-outs, and a whoami that says nobody is signed in', () => {
    noteAnswer('POST', '/api/auth/login', 200, JSON.stringify({username: 's.varma'}), '{}');
    expect(rememberedUser()).toBe('s.varma');
    noteAnswer('POST', '/api/auth/login', 401, JSON.stringify({username: 'admin'}), '{}');
    expect(rememberedUser()).toBe('s.varma');
    noteAnswer('GET', '/api/auth/whoami', 200, null, JSON.stringify({isAuthenticated: true}));
    expect(rememberedUser()).toBe('s.varma');
    noteAnswer('GET', '/api/auth/whoami', 200, null, JSON.stringify({isAuthenticated: false}));
    expect(rememberedUser()).toBeNull();
    noteAnswer('POST', '/api/auth/login', 200, JSON.stringify({username: 'admin'}), '{}');
    noteAnswer('POST', '/api/auth/logout', 204, null, '');
    expect(rememberedUser()).toBeNull();
  });

  it('switches account through the API, saves, and opens the step’s page as them with a full load', async () => {
    const backend = fakeBackend((_method, url) => answer(url === '/api/auth/login' ? 200 : 204));
    const assign = vi.spyOn(fullDemoNavigation, 'assign').mockImplementation(() => {});
    sessionStorage.setItem('medapp.halfView', 'x');
    await switchAccount('r.aldous', '/patients/light-id', async (path) => path.replace('light-id', 'full-id'));
    expect(backend.fetchApi.mock.calls.map(([method, url]) => `${method} ${url}`)).toEqual(['POST /api/auth/logout', 'POST /api/auth/login']);
    expect(backend.flush).toHaveBeenCalled();
    expect(rememberedUser()).toBe('r.aldous');
    expect(sessionStorage.getItem('medapp.halfView')).toBeNull();
    expect(assign).toHaveBeenCalledWith('/patients/full-id');
  });

  it('resets: forgets only the saved database, keeping settings, then reloads', async () => {
    const backend = fakeBackend();
    const assign = vi.spyOn(fullDemoNavigation, 'assign').mockImplementation(() => {});
    localStorage.setItem('medapp.pins', '{}');
    await resetFullDemo('/login');
    expect(backend.forgetSavedDatabase).toHaveBeenCalled();
    expect(localStorage.getItem('medapp.pins')).toBe('{}');
    expect(assign).toHaveBeenCalledWith('/login');
  });

  it('refreshes: forgets the database and all this browser keeps, then loads the page past the cache', async () => {
    const backend = fakeBackend();
    const assign = vi.spyOn(fullDemoNavigation, 'assign').mockImplementation(() => {});
    const fetch = vi.fn().mockResolvedValue(new Response(''));
    vi.stubGlobal('fetch', fetch);
    localStorage.setItem('medapp.pins', '{}');
    sessionStorage.setItem('anything', '1');
    await refreshFullDemo('/login');
    expect(backend.forgetSavedDatabase).toHaveBeenCalled();
    expect(localStorage.getItem('medapp.pins')).toBeNull();
    expect(sessionStorage.length).toBe(0);
    expect(fetch).toHaveBeenCalledWith('/login', {cache: 'reload'});
    expect(assign).toHaveBeenCalledWith('/login');
    vi.unstubAllGlobals();
  });
});
