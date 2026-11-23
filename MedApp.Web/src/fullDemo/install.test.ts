import {answer, fakeBackend, fullDemoReady, resetFullDemoForTests} from '../test/fullDemo';
import {answerApi, answerBody, installFullDemo} from './install';
import {rememberedUser} from './session';
import {setStartupStateForTests} from './startup';

describe('the full demo’s requests', () => {
  const originalFetch = window.fetch;
  const originalXhr = window.XMLHttpRequest;
  beforeEach(() => resetFullDemoForTests());
  afterEach(() => {
    window.fetch = originalFetch;
    window.XMLHttpRequest = originalXhr;
    resetFullDemoForTests();
  });

  it('answers whoami as signed out at once while the backend starts, and holds anything else until it’s ready', async () => {
    const backend = fakeBackend(() => answer(200, {items: []}));
    const whoami = await answerApi('GET', '/api/auth/whoami', {});
    expect(JSON.parse(whoami.body!).isAuthenticated).toBe(false);
    expect(backend.fetchApi).not.toHaveBeenCalled();

    let settled = false;
    const search = answerApi('GET', '/api/patients/search', {}).then((a) => ((settled = true), a));
    await Promise.resolve();
    expect(settled).toBe(false);
    fullDemoReady();
    expect((await search).status).toBe(200);
    expect(backend.fetchApi).toHaveBeenCalledWith('GET', '/api/patients/search', {}, undefined);
  });

  it('says so when the backend couldn’t start', async () => {
    fakeBackend();
    const pending = answerApi('GET', '/api/patients/search', {});
    setStartupStateForTests({phase: 'failed', progress: null, version: null, size: null, error: 'no WebAssembly'});
    const refused = await pending;
    expect(refused.status).toBe(503);
  });

  it('remembers who signed in, and forgets them on signing out', async () => {
    fakeBackend((_method, url) => (url === '/api/auth/login' ? answer(200, {sessionId: 's'}) : answer(204)));
    fullDemoReady();
    await answerApi('POST', '/api/auth/login', {}, new TextEncoder().encode(JSON.stringify({username: 'n.thornbury', password: 'x'})));
    expect(rememberedUser()).toBe('n.thornbury');
    await answerApi('POST', '/api/auth/logout', {});
    expect(rememberedUser()).toBeNull();
  });

  it('sends fetch’s requests for /api to the backend, with their bodies, and everything else to the network', async () => {
    const network = vi.fn(async () => new Response('network'));
    window.fetch = network as typeof window.fetch;
    const backend = fakeBackend((_method, url) => (url.startsWith('/api/patients') ? answer(201, {id: 'p1'}) : answer(200, {isAuthenticated: false})));
    await installFullDemo();
    fullDemoReady();

    const created = await fetch('/api/patients?x=1', {method: 'POST', headers: {'Content-Type': 'application/json'}, body: '{"firstName":"Synthetic"}'});
    expect(created.status).toBe(201);
    expect(await created.json()).toEqual({id: 'p1'});
    const [method, url, headers, body] = backend.fetchApi.mock.calls.at(-1)!;
    expect([method, url]).toEqual(['POST', '/api/patients?x=1']);
    expect(headers).toMatchObject({'content-type': 'application/json', Host: window.location.host});
    expect(new TextDecoder().decode(body as Uint8Array)).toBe('{"firstName":"Synthetic"}');

    expect(await (await fetch('/MedApp/full/db-version.json')).text()).toBe('network');
    expect(network).toHaveBeenCalled();
    expect(window.XMLHttpRequest).not.toBe(originalXhr);
  });

  it('encodes an XMLHttpRequest’s body, with its content type unless one was set', async () => {
    const backend = fakeBackend();
    fullDemoReady();
    await answerBody('POST', '/api/attachments?patientId=p1', {}, new Blob(['abc'], {type: 'text/plain'}));
    expect(backend.fetchApi.mock.calls[0][2]).toMatchObject({'Content-Type': 'text/plain'});
    await answerBody('POST', '/api/attachments', {'Content-Type': 'application/octet-stream'}, new Blob(['abc'], {type: 'text/plain'}));
    expect(backend.fetchApi.mock.calls[1][2]).toMatchObject({'Content-Type': 'application/octet-stream'});
    expect(backend.fetchApi.mock.calls[1][2]).not.toHaveProperty('content-type');
  });
});
