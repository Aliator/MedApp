import {useEffect, useState} from 'react';
import type {EmailLinkToken} from '../api/account';

/**
 * What an emailed link's fragment (`#user=<id>&token=<token>`) holds: `null` when it isn't a whole link (missing a
 * part, or not one at all). The API puts these in the fragment, which browsers never send to a server, so the token
 * stays out of logs and Referer headers; only this page reads it.
 */
export function parseEmailLink(hash: string): EmailLinkToken | null {
  const params = new URLSearchParams(hash.replace(/^#/, ''));
  const userId = params.get('user')?.trim();
  // Encoded by the API, so a "+" in the token arrives as %2B. One that wasn't (a mail client that decoded the link)
  // reads as a space, which a token never has.
  const token = params.get('token')?.replace(/ /g, '+');
  return userId && token ? {userId, token} : null;
}

/** An emailed link as the page sees it: `reading` until it has looked (on the server, and the first render). */
export type EmailLinkState = {status: 'reading'} | {status: 'missing'} | ({status: 'found'} & EmailLinkToken);

/** Leaves the address without its fragment, so the token isn't left in the address bar. Its own object, for tests. */
export const linkFragment = {
  clear: () => window.history.replaceState(window.history.state, '', window.location.pathname + window.location.search),
};

/**
 * The emailed link this page was opened with (`/reset-password`, `/confirm-email`), read from the address's fragment
 * after the page has mounted: the server never sees the fragment, so the server's render (and the first in the
 * browser, which must match it) shows `reading`. Once read, the fragment is taken off the address, so the token
 * doesn't stay on screen or in the browser's history; the page keeps it.
 */
export function useEmailLink(): EmailLinkState {
  const [state, setState] = useState<EmailLinkState>({status: 'reading'});
  useEffect(() => {
    const link = parseEmailLink(window.location.hash);
    // Read from the browser's address once mounted: the server can't know it.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setState(link ? {status: 'found', ...link} : {status: 'missing'});
    if (window.location.hash) linkFragment.clear();
  }, []);
  return state;
}
