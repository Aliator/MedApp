import {renderHook, waitFor} from '@testing-library/react';
import {StrictMode} from 'react';
import {linkFragment, parseEmailLink, PENDING_CONFIRMATION_KEY, pendingEmailConfirmation, useEmailLink} from './emailLink';

const USER = '0000000e-0000-0000-0000-000000000001';

describe('parseEmailLink', () => {
  it("reads the account and token from a link's fragment, decoding the token", () => {
    expect(parseEmailLink(`#user=${USER}&token=Q2Zk%2B%2Fx%3D%3D`)).toEqual({userId: USER, token: 'Q2Zk+/x=='});
  });

  it('keeps a "+" a mail client left unencoded', () => {
    expect(parseEmailLink(`#user=${USER}&token=a+b`)).toEqual({userId: USER, token: 'a+b'});
  });

  it.each(['', '#', `#user=${USER}`, '#token=abc', `#user=%20&token=abc`, `#user=${USER}&token=`])('is null for %j', (hash) => {
    expect(parseEmailLink(hash)).toBeNull();
  });
});

describe('useEmailLink', () => {
  afterEach(() => window.history.replaceState(null, '', '/'));

  it("reads the link once mounted, then takes it off the address, keeping the page's query", async () => {
    window.history.replaceState(null, '', `/reset-password?from=email#user=${USER}&token=abc`);

    const {result} = renderHook(() => useEmailLink());

    await waitFor(() => expect(result.current).toEqual({status: 'found', userId: USER, token: 'abc'}));
    expect(window.location.hash).toBe('');
    expect(window.location.pathname + window.location.search).toBe('/reset-password?from=email');
  });

  it('says when the address holds no link, leaving it alone', async () => {
    const clear = vi.spyOn(linkFragment, 'clear');
    window.history.replaceState(null, '', '/reset-password');

    const {result} = renderHook(() => useEmailLink());

    await waitFor(() => expect(result.current).toEqual({status: 'missing'}));
    expect(clear).not.toHaveBeenCalled();
  });

  describe('with a kept link', () => {
    afterEach(() => sessionStorage.clear());

    it('uses the kept link when the address holds none', async () => {
      window.history.replaceState(null, '', '/confirm-email');
      pendingEmailConfirmation.save({userId: USER, token: 'kept'});

      const {result} = renderHook(() => useEmailLink(pendingEmailConfirmation.read));

      await waitFor(() => expect(result.current).toEqual({status: 'found', userId: USER, token: 'kept'}));
    });

    it("prefers the address's link to a kept one", async () => {
      window.history.replaceState(null, '', `/confirm-email#user=${USER}&token=fresh`);
      pendingEmailConfirmation.save({userId: USER, token: 'kept'});

      const {result} = renderHook(() => useEmailLink(pendingEmailConfirmation.read));

      await waitFor(() => expect(result.current).toEqual({status: 'found', userId: USER, token: 'fresh'}));
    });

    it('says it is missing when neither has one', async () => {
      window.history.replaceState(null, '', '/confirm-email');

      const {result} = renderHook(() => useEmailLink(pendingEmailConfirmation.read));

      await waitFor(() => expect(result.current).toEqual({status: 'missing'}));
    });
  });

  describe("under StrictMode, which runs the app's effects twice in development", () => {
    afterEach(() => sessionStorage.clear());

    it('keeps the link it read, though the second run finds the address without it', async () => {
      window.history.replaceState(null, '', `/confirm-email#user=${USER}&token=abc`);

      const {result} = renderHook(() => useEmailLink(pendingEmailConfirmation.read), {wrapper: StrictMode});

      await waitFor(() => expect(result.current.status).not.toBe('reading'));
      expect(result.current).toEqual({status: 'found', userId: USER, token: 'abc'});
      expect(window.location.hash).toBe('');
    });

    it("keeps the address's link, not a kept one the second run would find", async () => {
      window.history.replaceState(null, '', `/confirm-email#user=${USER}&token=fresh`);
      pendingEmailConfirmation.save({userId: USER, token: 'kept'});

      const {result} = renderHook(() => useEmailLink(pendingEmailConfirmation.read), {wrapper: StrictMode});

      await waitFor(() => expect(result.current.status).not.toBe('reading'));
      expect(result.current).toEqual({status: 'found', userId: USER, token: 'fresh'});
    });

    it('still says when there is no link', async () => {
      window.history.replaceState(null, '', '/reset-password');

      const {result} = renderHook(() => useEmailLink(), {wrapper: StrictMode});

      await waitFor(() => expect(result.current).toEqual({status: 'missing'}));
    });
  });

  it('says a partial link is missing, and still clears it', async () => {
    window.history.replaceState(null, '', `/confirm-email#user=${USER}`);

    const {result} = renderHook(() => useEmailLink());

    await waitFor(() => expect(result.current).toEqual({status: 'missing'}));
    expect(window.location.hash).toBe('');
  });
});

describe('pendingEmailConfirmation', () => {
  afterEach(() => {
    sessionStorage.clear();
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it('keeps a link in this tab only, under a medapp. key, until cleared', () => {
    pendingEmailConfirmation.save({userId: USER, token: 'abc'});

    expect(PENDING_CONFIRMATION_KEY.startsWith('medapp.')).toBe(true);
    expect(JSON.parse(sessionStorage.getItem(PENDING_CONFIRMATION_KEY)!)).toEqual({userId: USER, token: 'abc'});
    expect(localStorage.length).toBe(0);
    expect(pendingEmailConfirmation.read()).toEqual({userId: USER, token: 'abc'});
    pendingEmailConfirmation.clear();
    expect(pendingEmailConfirmation.read()).toBeNull();
  });

  it.each([
    'not json',
    'null',
    '"text"',
    JSON.stringify({userId: USER}),
    JSON.stringify({userId: '', token: 'abc'}),
    JSON.stringify({userId: USER, token: 1}),
  ])('reads nothing from %s', (stored) => {
    sessionStorage.setItem(PENDING_CONFIRMATION_KEY, stored);

    expect(pendingEmailConfirmation.read()).toBeNull();
  });

  it('keeps nothing, without failing, when storage is blocked', () => {
    const blocked = () => {
      throw new Error('blocked');
    };
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(blocked);
    vi.spyOn(Storage.prototype, 'removeItem').mockImplementation(blocked);

    expect(() => pendingEmailConfirmation.save({userId: USER, token: 'abc'})).not.toThrow();
    expect(() => pendingEmailConfirmation.clear()).not.toThrow();
    expect(pendingEmailConfirmation.read()).toBeNull();
  });
});
