import {renderHook, waitFor} from '@testing-library/react';
import {linkFragment, parseEmailLink, useEmailLink} from './emailLink';

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

  it('says a partial link is missing, and still clears it', async () => {
    window.history.replaceState(null, '', `/confirm-email#user=${USER}`);

    const {result} = renderHook(() => useEmailLink());

    await waitFor(() => expect(result.current).toEqual({status: 'missing'}));
    expect(window.location.hash).toBe('');
  });
});
