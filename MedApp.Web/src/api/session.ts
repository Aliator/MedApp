import {queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';
import {ApiError} from './errors';

/** When the signed-in session ends, by this device's clock, and whether it can be extended. */
export interface SessionExpiry {
  /** When it ends, in milliseconds (`Date.now()`'s clock). */
  endsAt: number;
  /** False once it has been extended as far as a session can last (24 hours from sign-in). */
  canExtend: boolean;
}

/**
 * The API's answer, on this device's clock: the seconds the server says are left, counted from now. So a device
 * whose clock is wrong still warns at the right moment.
 */
export function toExpiry(session: Schemas['SessionResponse'], now = Date.now()): SessionExpiry {
  return {endsAt: now + (session.expiresInSeconds ?? 0) * 1000, canExtend: session.canExtend ?? false};
}

const sessionKey = ['session'];

/** A 401: the session has ended (or there's none). */
export const isUnauthorized = (error: unknown) => error instanceof ApiError && error.status === 401;

/** While there's no answer (a failed check), it asks again this often. */
export const SESSION_RETRY_MS = 60_000;

/**
 * The signed-in session's expiry, from `GET api/auth/session` (reading it doesn't extend it). Fetched when asked
 * (`SessionTimeout` asks as the warning is due, and again as the session ends). A failure other than a 401 (a session
 * that has ended) is retried, and while the last check failed it asks again every minute, so a blip as the app opens
 * doesn't leave it with no warning at all.
 */
export const sessionQuery = queryOptions({
  queryKey: sessionKey,
  queryFn: async () => toExpiry(await unwrap(api.GET('/api/auth/session'))),
  staleTime: Infinity,
  retry: (failures, error) => !isUnauthorized(error) && failures < 3,
  refetchInterval: (query) => (query.state.status === 'error' && !isUnauthorized(query.state.error) ? SESSION_RETRY_MS : false),
});

/** "Stay signed in": extends the session (`POST api/auth/session/extend`) and keeps its new expiry. */
export function useExtendSession() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => toExpiry(await unwrap(api.POST('/api/auth/session/extend'))),
    onSuccess: (expiry) => queryClient.setQueryData(sessionKey, expiry),
  });
}
