import {mockFetch, json} from '../test/render';
import {SSR_KEY_HEADER, setSsrRequestContextProvider} from '../ssr/requestContext';
import {api, onPermissionRefused, pageNavigation, PASSWORD_CHANGE_REQUIRED, TWO_FACTOR_SETUP_REQUIRED, unwrap} from './client';
import {ApiError, ApiValidationError} from './errors';

describe('api', () => {
  afterEach(() => setSsrRequestContextProvider(() => undefined));

  it("calls this app's origin in the browser, with nothing added", async () => {
    const fetch = mockFetch(() => json({items: []}));

    await api.GET('/api/patients/search');

    const request = fetch.mock.calls[0][0] as Request;
    expect(new URL(request.url).origin).toBe(window.location.origin);
    expect(request.headers.get('Cookie')).toBeNull();
    expect(request.headers.get(SSR_KEY_HEADER)).toBeNull();
  });

  it("rendering on the server, calls the API itself with the page request's cookie and the server's key", async () => {
    setSsrRequestContextProvider(() => ({apiUrl: 'https://api.example.org', cookie: 'session=synthetic', ssrKey: 'synthetic-key'}));
    const fetch = mockFetch(() => json({}));

    await api.PUT('/api/auth/me/settings', {body: {settings: '{}'}});

    const request = fetch.mock.calls[0][0] as Request;
    expect(request.url).toBe('https://api.example.org/api/auth/me/settings');
    expect(request.method).toBe('PUT');
    expect(request.headers.get('Cookie')).toBe('session=synthetic');
    expect(request.headers.get(SSR_KEY_HEADER)).toBe('synthetic-key');
    expect(await request.json()).toEqual({settings: '{}'});
  });

  it('rendering on the server for someone signed out, sends no cookie or key', async () => {
    setSsrRequestContextProvider(() => ({apiUrl: 'https://api.example.org'}));
    const fetch = mockFetch(() => json({items: []}));

    await api.GET('/api/patients/search', {params: {query: {Page: 2}}});

    const request = fetch.mock.calls[0][0] as Request;
    expect(request.url).toBe('https://api.example.org/api/patients/search?Page=2');
    expect(request.headers.get('Cookie')).toBeNull();
    expect(request.headers.get(SSR_KEY_HEADER)).toBeNull();
  });
});

describe('a forced password change', () => {
  const forced = () => json({title: 'Password change required', status: 403, code: PASSWORD_CHANGE_REQUIRED}, 403);

  afterEach(() => {
    vi.restoreAllMocks();
    window.history.replaceState(null, '', '/');
  });

  it('sends the page to change the password, with a full load, when the API says one is required', async () => {
    const assign = vi.spyOn(pageNavigation, 'assign').mockImplementation(() => {});
    window.history.replaceState(null, '', '/patients');
    mockFetch(forced);

    const error = await unwrap(api.GET('/api/patients/search')).catch((e: unknown) => e);

    expect(assign).toHaveBeenCalledExactlyOnceWith('/change-password');
    // The caller still gets its error, for the moment before the page leaves.
    expect(error).toMatchObject({status: 403, title: 'Password change required'});
  });

  it('stays on the change-password page itself', async () => {
    const assign = vi.spyOn(pageNavigation, 'assign').mockImplementation(() => {});
    window.history.replaceState(null, '', '/change-password');
    mockFetch(forced);

    await api.GET('/api/patients/search');

    expect(assign).not.toHaveBeenCalled();
  });

  it.each([
    ['another 403', () => json({title: 'Forbidden', status: 403}, 403)],
    ['a 403 without a body', () => new Response(null, {status: 403})],
    ['a 403 whose body is a list', () => json([PASSWORD_CHANGE_REQUIRED], 403)],
    ['another status with the code', () => json({code: PASSWORD_CHANGE_REQUIRED}, 401)],
    ['a 403 with a code that names no page', () => json({code: 'toString'}, 403)],
  ])('leaves %s alone', async (_, respond) => {
    const assign = vi.spyOn(pageNavigation, 'assign').mockImplementation(() => {});
    window.history.replaceState(null, '', '/patients');
    mockFetch(respond);

    await api.GET('/api/patients/search');

    expect(assign).not.toHaveBeenCalled();
  });

  it('leaves the page by assigning the address in the browser', () => {
    // jsdom can't navigate, but the call reaches its location.
    expect(() => pageNavigation.assign('/change-password')).not.toThrow();
  });
});

describe('a required two-factor set-up', () => {
  const held = () => json({title: 'Two-factor authentication required', status: 403, code: TWO_FACTOR_SETUP_REQUIRED}, 403);

  afterEach(() => {
    vi.restoreAllMocks();
    window.history.replaceState(null, '', '/');
  });

  it('sends the page to set it up, with a full load, when the API says the account must', async () => {
    const assign = vi.spyOn(pageNavigation, 'assign').mockImplementation(() => {});
    window.history.replaceState(null, '', '/patients');
    mockFetch(held);

    const error = await unwrap(api.GET('/api/patients/search')).catch((e: unknown) => e);

    expect(assign).toHaveBeenCalledExactlyOnceWith('/set-up-two-factor');
    expect(error).toMatchObject({status: 403, code: TWO_FACTOR_SETUP_REQUIRED});
  });

  it('stays on the set-up page itself', async () => {
    const assign = vi.spyOn(pageNavigation, 'assign').mockImplementation(() => {});
    window.history.replaceState(null, '', '/set-up-two-factor');
    mockFetch(held);

    await api.GET('/api/patients/search');

    expect(assign).not.toHaveBeenCalled();
  });
});

describe('unwrap', () => {
  it('returns the body of a successful response', async () => {
    mockFetch(() => json({settings: '{}'}));

    expect(await unwrap(api.GET('/api/auth/me/settings'))).toEqual({settings: '{}'});
  });

  it('throws the validation errors of a 400', async () => {
    mockFetch(() => json({errors: {Name: ['Name is required.']}}, 400));

    const error = await unwrap(api.GET('/api/auth/me/settings')).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiValidationError);
    expect((error as ApiValidationError).errors.fields).toEqual({name: ['Name is required.']});
  });

  it('throws the status and title of any other failure', async () => {
    mockFetch(() => json({title: 'Someone else changed it.'}, 409));

    const error = await unwrap(api.GET('/api/auth/me/settings')).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({status: 409, title: 'Someone else changed it.'});
  });
});

describe('a permission the roles do not grant', () => {
  const refusal = {
    title: 'Permission needed',
    detail: "Your roles don't allow this. It needs the permission to: Assign staff to pathways, and unassign them.",
    code: 'permission_missing',
    permission: 'AssignPathways',
  };

  it("throws an ApiError naming the permission, with the API's words as its title", async () => {
    mockFetch(() => json(refusal, 403));

    const error = await unwrap(api.GET('/api/patients/search')).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({status: 403, code: 'permission_missing', permission: 'AssignPathways', title: refusal.detail});
  });

  it('tells every listener, once each, and stops when asked', async () => {
    mockFetch(() => json(refusal, 403));
    const listener = vi.fn();
    const stop = onPermissionRefused(listener);

    await api.GET('/api/patients/search');
    stop();
    await api.GET('/api/patients/search');

    expect(listener).toHaveBeenCalledTimes(1);
    expect(listener).toHaveBeenCalledWith({permission: 'AssignPathways', message: refusal.detail});
  });

  it('says nothing of any other 403', async () => {
    mockFetch(() => json({title: 'Forbidden'}, 403));
    const listener = vi.fn();
    const stop = onPermissionRefused(listener);

    await api.GET('/api/patients/search');
    stop();

    expect(listener).not.toHaveBeenCalled();
  });
});
