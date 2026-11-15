import {mockFetch, json} from '../test/render';
import {SSR_KEY_HEADER, setSsrRequestContextProvider} from '../ssr/requestContext';
import {api, unwrap} from './client';
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
