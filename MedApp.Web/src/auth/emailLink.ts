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

/** Where `/confirm-email` keeps its link while the person signs in: a `medapp.` key, so signing out clears it. */
export const PENDING_CONFIRMATION_KEY = 'medapp.pendingEmailConfirmation';

/**
 * A confirmation link kept while its account signs in (`/confirm-email` sends someone signed out to sign in, then
 * back). It's in this tab's sessionStorage, never localStorage, so it goes when the tab closes, and signing out clears
 * it too; the page clears it once the link is used or refused. Storage that can't be used keeps nothing.
 */
export const pendingEmailConfirmation = {
  save(link: EmailLinkToken) {
    try {
      sessionStorage.setItem(PENDING_CONFIRMATION_KEY, JSON.stringify({userId: link.userId, token: link.token}));
    } catch {
      // Not kept: they open the link from the email again after signing in.
    }
  },
  read(): EmailLinkToken | null {
    try {
      const value: unknown = JSON.parse(sessionStorage.getItem(PENDING_CONFIRMATION_KEY) ?? 'null');
      if (typeof value !== 'object' || value === null) return null;
      const {userId, token} = value as Record<string, unknown>;
      return typeof userId === 'string' && userId && typeof token === 'string' && token ? {userId, token} : null;
    } catch {
      return null;
    }
  },
  clear() {
    try {
      sessionStorage.removeItem(PENDING_CONFIRMATION_KEY);
    } catch {
      // Nothing could have been kept.
    }
  },
};

/**
 * The emailed link this page was opened with (`/reset-password`, `/confirm-email`), read from the address's fragment
 * after the page has mounted: the server never sees the fragment, so the server's render (and the first in the
 * browser, which must match it) shows `reading`. Once read, the fragment is taken off the address, so the token
 * doesn't stay on screen or in the browser's history; the page keeps it. Without one in the address, `fallback`
 * may give one kept from before (`pendingEmailConfirmation.read`, after signing in).
 */
export function useEmailLink(fallback?: () => EmailLinkToken | null): EmailLinkState {
  const [state, setState] = useState<EmailLinkState>({status: 'reading'});
  useEffect(() => {
    const link = parseEmailLink(window.location.hash) ?? fallback?.() ?? null;
    // Read from the browser's address once mounted: the server can't know it.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setState(link ? {status: 'found', ...link} : {status: 'missing'});
    if (window.location.hash) linkFragment.clear();
    // Once, as it mounts: the fallback is read then or not at all.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);
  return state;
}
