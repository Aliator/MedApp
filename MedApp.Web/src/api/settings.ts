import {queryOptions} from '@tanstack/react-query';
import {api, unwrap} from './client';

/**
 * The signed-in user's app settings, as saved with their account (`GET api/auth/me/settings`): the settings by key,
 * or null when they've never saved any (or what's saved isn't readable).
 */
export const settingsQuery = queryOptions({
  queryKey: ['settings'],
  queryFn: async (): Promise<Record<string, unknown> | null> => {
    const response = await unwrap(api.GET('/api/auth/me/settings'));
    if (!response?.settings) return null;
    try {
      const parsed: unknown = JSON.parse(response.settings);
      return parsed && typeof parsed === 'object' && !Array.isArray(parsed) ? (parsed as Record<string, unknown>) : null;
    } catch {
      return null;
    }
  },
  // Read once a visit: after that, this browser's are the ones in use, and it saves them as they change.
  staleTime: Infinity,
  retry: false,
});

/** Saves the signed-in user's app settings with their account (`PUT api/auth/me/settings`), replacing any saved. */
export async function saveSettings(settings: Record<string, string>) {
  await unwrap(api.PUT('/api/auth/me/settings', {body: {settings: JSON.stringify(settings)}}));
}
